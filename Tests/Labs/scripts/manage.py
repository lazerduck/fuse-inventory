#!/usr/bin/env python3
"""Manage isolated, reproducible Fuse integration lab instances on the local Docker engine."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import fcntl
import hashlib
import io
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import tarfile
import tempfile
import time
import urllib.request

HERE = Path(__file__).resolve().parents[1]
REPO = HERE.parent.parent
SQL = HERE / 'sql'
SQL_IMAGE = 'mcr.microsoft.com/mssql/server@sha256:4525e3efac9f3e241cb003bb2ff2a82818b502f37e12049d788b1a9fa7e13b7f'
ENGINE = ['docker', '--host', 'unix:///var/run/docker.sock']
MARKER = '.validation-state.json'
PASSWORD_KEYS = ('FUSE_ADMIN_PASSWORD', 'MSSQL_SA_PASSWORD', 'LAB_INSPECTOR_PASSWORD', 'LAB_MANAGER_PASSWORD', 'LAB_USER_PASSWORD')


class LabError(Exception):
    pass


def now():
    return datetime.now(timezone.utc).isoformat()


def valid_name(name):
    if not re.fullmatch(r'[a-z][a-z0-9-]{0,31}', name) or name.endswith('-'):
        raise LabError('Instance names must start with a lowercase letter, contain lowercase letters/digits/hyphens, and be at most 32 characters (no trailing hyphen).')
    return name


def private_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    fd, temporary = tempfile.mkstemp(prefix='.' + path.name, dir=path.parent)
    try:
        with os.fdopen(fd, 'w') as stream:
            json.dump(data, stream, indent=2)
            stream.write('\n')
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def read_json(path):
    try:
        return json.loads(path.read_text())
    except (OSError, ValueError) as error:
        raise LabError(f'Cannot read valid metadata: {path}') from error


def read_env(path):
    try:
        return dict(line.split('=', 1) for line in path.read_text().splitlines()
                    if line and not line.startswith('#'))
    except (OSError, ValueError) as error:
        raise LabError(f'Cannot read instance configuration: {path}') from error


def save_env(path, values):
    fd, temporary = tempfile.mkstemp(prefix='.env-', dir=path.parent)
    try:
        with os.fdopen(fd, 'w') as stream:
            stream.write('# Private local lab credentials. Never commit or publish.\n')
            for key, value in values.items():
                if '\n' in str(value) or '\r' in str(value):
                    raise LabError('Configuration values cannot contain newlines.')
                stream.write(f'{key}={value}\n')
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def clean_environment(values=None):
    result = {key: value for key, value in os.environ.items()
              if not key.startswith(('DOCKER_', 'COMPOSE_'))}
    if values:
        result.update(values)
    return result


def redact(text, values):
    for key in PASSWORD_KEYS:
        if values.get(key):
            text = text.replace(values[key], '[REDACTED]')
    text = re.sub(r'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '[REDACTED TOKEN]', text)
    return re.sub(r'(?i)(?:password|pwd)\s*=\s*[^;\r\n]+', 'Password=[REDACTED]', text)


def run(command, *, values=None, input=None, capture=True, check=True, timeout=None):
    try:
        result = subprocess.run(command, input=input, text=True, capture_output=capture,
                                env=clean_environment(values), timeout=timeout)
    except OSError as error:
        raise LabError(f'Cannot execute {command[0]}; check required tools and permissions.') from error
    if check and result.returncode:
        detail = redact((result.stderr or '')[-2000:], values or {}) if capture else ''
        raise LabError(f'{command[0]} failed (exit {result.returncode}). {detail}'.strip())
    return result


@contextmanager
def lock(path):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor = os.open(path, os.O_CREAT | os.O_RDWR, 0o600)
    with os.fdopen(descriptor, 'a') as stream:
        try:
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise LabError('Another lab command holds this lock; wait for it to finish.') from error
        try:
            yield
        finally:
            fcntl.flock(stream, fcntl.LOCK_UN)


class Manager:
    def __init__(self, root):
        self.root = Path(root).expanduser().resolve()
        if self.root == Path(self.root.anchor) or self.root == Path.home() or self.root == REPO or REPO in self.root.parents:
            raise LabError('Choose a dedicated runtime directory outside the Git checkout.')
        self.root_id = hashlib.sha256(str(self.root).encode()).hexdigest()[:8]

    def prepare(self):
        for name in ('instances', 'external', 'sources', 'reports', 'locks'):
            directory = self.root / name
            if directory.is_symlink():
                raise LabError(f'Refusing symlinked runtime directory: {directory}')
            directory.mkdir(parents=True, exist_ok=True, mode=0o700)

    def project(self, name):
        return f'fuse-lab-{self.root_id}-{valid_name(name)}'

    def directory(self, name):
        path = self.root / 'instances' / valid_name(name)
        if path.is_symlink() or path.resolve().parent != self.root / 'instances':
            raise LabError('Refusing an instance path outside its runtime directory.')
        return path

    def external_path(self, name):
        return self.root / 'external' / (valid_name(name) + '.json')

    def load(self, name):
        directory = self.directory(name)
        if self.external_path(name).exists():
            raise LabError('This is a read-only external reference; use its original management commands.')
        meta = read_json(directory / 'instance.json')
        env = read_env(directory / '.env')
        required = {'COMPOSE_PROJECT_NAME', 'LAB_INSTANCE', 'LAB_ROOT_ID', 'FUSE_SOURCE_DIR', 'FUSE_COMMIT', 'FUSE_IMAGE', 'FUSE_VERSION', 'FUSE_BUILD_DATE', 'SQL_IMAGE', 'FUSE_PORT', 'SQL_PORT', 'FUSE_ADMIN_USERNAME', *PASSWORD_KEYS}
        try:
            commit = meta['source_commit']
            expected = {
                'COMPOSE_PROJECT_NAME': self.project(name), 'LAB_INSTANCE': name, 'LAB_ROOT_ID': self.root_id,
                'FUSE_COMMIT': commit, 'FUSE_SOURCE_DIR': str(self.root / 'sources' / commit),
                'FUSE_PORT': str(meta['ports']['http']), 'SQL_PORT': str(meta['ports']['sql']),
                'FUSE_IMAGE': self.image(name, commit),
            }
            valid = (meta['schema_version'] == 1 and meta['name'] == name and meta['managed'] is True
                     and meta['scenario'] == 'sql' and meta['kind'] in ('reference', 'experiment', 'validation')
                     and meta['project_name'] == self.project(name) and re.fullmatch('[0-9a-f]{40,64}', commit)
                     and set(env) == required and all(env.get(key) == value for key, value in expected.items())
                     and all(1024 <= int(meta['ports'][key]) <= 65535 for key in ('http', 'sql'))
                     and meta['ports']['http'] != meta['ports']['sql'])
        except (KeyError, TypeError, ValueError):
            valid = False
        if not valid:
            raise LabError('Instance metadata/configuration is inconsistent; refusing to target Docker.')
        return directory, meta, env

    def image(self, name, commit):
        return f'fuse-inventory-lab-{self.root_id}-{name}:{commit[:12]}'

    def snapshot(self, ref):
        result = run(['git', '-C', str(REPO), 'rev-parse', '--verify', '--end-of-options', f'{ref}^{{commit}}'])
        commit = result.stdout.strip()
        if not re.fullmatch('[0-9a-f]{40,64}', commit):
            raise LabError('Git did not resolve an exact source commit.')
        target = self.root / 'sources' / commit
        if target.is_symlink():
            raise LabError('Refusing symlinked source snapshot.')
        if not target.exists():
            archive = subprocess.run(['git', '-C', str(REPO), 'archive', '--format=tar', commit], check=True, capture_output=True).stdout
            with tempfile.TemporaryDirectory(dir=target.parent, prefix='.snapshot-') as temporary:
                staging = Path(temporary) / 'source'
                staging.mkdir()
                with tarfile.open(fileobj=io.BytesIO(archive)) as contents:
                    contents.extractall(staging, filter='data')
                if not (staging / 'Dockerfile').is_file():
                    raise LabError('Source revision has no Dockerfile.')
                staging.rename(target)
        return commit, target

    def reserved_ports(self):
        reserved = set()
        for path in (self.root / 'instances').glob('*/instance.json'):
            data = read_json(path)
            try:
                reserved.update(int(value) for value in data['ports'].values())
            except (KeyError, TypeError, ValueError) as error:
                raise LabError('An instance has invalid port metadata; repair it before allocating ports.') from error
        for path in (self.root / 'external').glob('*.json'):
            data = read_json(path)
            reserved.update(int(value) for value in data['ports'].values())
        return reserved

    @staticmethod
    def port(requested, start, reserved):
        choices = [requested] if requested is not None else range(start, min(start + 1000, 65536))
        for port in choices:
            if not isinstance(port, int) or not 1024 <= port <= 65535:
                raise LabError('Ports must be integers between 1024 and 65535.')
            if port in reserved:
                if requested is not None:
                    raise LabError(f'Port {port} is reserved by another instance.')
                continue
            try:
                with socket.socket() as probe:
                    probe.bind(('127.0.0.1', port))
            except OSError:
                if requested is not None:
                    raise LabError(f'Port {port} is already in use.')
                continue
            reserved.add(port)
            return port
        raise LabError('No free lab port found; select an explicit free port.')

    def initialize(self, args):
        name = valid_name(args.name)
        self.prepare()
        with lock(self.root / '.registry.lock'), lock(self.root / 'locks' / (name + '.lock')):
            directory = self.directory(name)
            if directory.exists() or self.external_path(name).exists():
                raise LabError('This instance name already exists; configuration and credentials were preserved.')
            reserved = self.reserved_ports()
            ports = {'http': self.port(args.http_port, 5190, reserved), 'sql': self.port(args.sql_port, 14340, reserved)}
            commit, source = self.snapshot(args.ref)
            meta = {'schema_version': 1, 'name': name, 'managed': True, 'scenario': 'sql', 'kind': args.kind,
                    'project_name': self.project(name), 'ports': ports, 'source_commit': commit,
                    'purpose': args.purpose, 'task': args.task, 'created_at': now()}
            env = {'COMPOSE_PROJECT_NAME': meta['project_name'], 'LAB_INSTANCE': name, 'LAB_ROOT_ID': self.root_id,
                   'FUSE_SOURCE_DIR': str(source), 'FUSE_COMMIT': commit, 'FUSE_IMAGE': self.image(name, commit),
                   'FUSE_VERSION': f'lab.{commit[:12]}', 'FUSE_BUILD_DATE': now(), 'SQL_IMAGE': SQL_IMAGE,
                   'FUSE_PORT': str(ports['http']), 'SQL_PORT': str(ports['sql']), 'FUSE_ADMIN_USERNAME': 'LabAdmin'}
            env.update({key: 'Lab1!' + secrets.token_hex(24) for key in PASSWORD_KEYS})
            with tempfile.TemporaryDirectory(prefix='.' + name + '-', dir=directory.parent) as temporary:
                staging = Path(temporary) / 'instance'
                staging.mkdir(mode=0o700)
                private_json(staging / 'instance.json', meta)
                save_env(staging / '.env', env)
                staging.rename(directory)
            print(f'Initialized {name}: {commit[:12]}, HTTP {ports["http"]}, SQL {ports["sql"]}. Run up {name}.')

    def register(self, args):
        name = valid_name(args.name)
        self.prepare()
        with lock(self.root / '.registry.lock'):
            if self.directory(name).exists() or self.external_path(name).exists():
                raise LabError('This instance name already exists.')
            directory = args.directory.expanduser().resolve()
            values = read_env(directory / '.env')
            try:
                ports = {'http': int(values['FUSE_PORT']), 'sql': int(values['SQL_PORT'])}
                project = values['COMPOSE_PROJECT_NAME']
                commit = values['FUSE_COMMIT']
                if not re.fullmatch('[a-z0-9][a-z0-9_-]+', project) or not re.fullmatch('[0-9a-f]{40,64}', commit):
                    raise ValueError()
                if any(not 1024 <= value <= 65535 for value in ports.values()) or len(set(ports.values())) != 2:
                    raise ValueError()
                if self.reserved_ports().intersection(ports.values()):
                    raise LabError('External ports conflict with registered instances.')
                for other in (self.root / 'external').glob('*.json'):
                    if read_json(other)['project_name'] == project:
                        raise LabError('This external Docker project is already registered.')
            except (KeyError, ValueError) as error:
                raise LabError('External configuration lacks valid project, commit, or ports.') from error
            private_json(self.external_path(name), {'schema_version': 1, 'name': name, 'managed': False,
                'scenario': 'sql', 'kind': 'reference', 'project_name': project, 'ports': ports,
                'source_commit': commit, 'directory': str(directory), 'purpose': args.purpose,
                'task': args.task, 'created_at': now()})
            print(f'Registered {name} as an external read-only reference. Its data and configuration were not changed.')

    def compose(self, directory, env, *arguments, **kwargs):
        return run([*ENGINE, 'compose', '-p', env['COMPOSE_PROJECT_NAME'], '--project-directory', str(directory),
                    '--env-file', str(directory / '.env'), '-f', str(SQL / 'compose.yaml'), *arguments], values=env, **kwargs)

    def assert_owned(self, meta):
        project = meta['project_name']
        # Compose can adopt an existing volume by name even if its project label
        # is missing. Inspect both labelled resources and exact names it will use.
        for resource, listing, all_listing, expected, label_format in (
            ('container', ['ps', '-aq'], ['ps', '-a', '--format', '{{.Names}}'],
             {project + '-fuse-1', project + '-sql-1'}, '{{json .Config.Labels}}'),
            ('volume', ['volume', 'ls', '-q'], ['volume', 'ls', '--format', '{{.Name}}'],
             {project + '_fuse-data', project + '_sql-data'}, '{{json .Labels}}'),
            ('network', ['network', 'ls', '-q'], ['network', 'ls', '--format', '{{.Name}}'],
             {project + '_default'}, '{{json .Labels}}')):
            identifiers = set(run([*ENGINE, *listing, '--filter', 'label=com.docker.compose.project=' + project]).stdout.split())
            existing_names = set(run([*ENGINE, *all_listing]).stdout.split())
            identifiers.update(expected.intersection(existing_names))
            for identifier in sorted(identifiers):
                labels = json.loads(run([*ENGINE, resource, 'inspect', identifier, '--format', label_format]).stdout) or {}
                if (labels.get('io.fuse.lab.instance') != meta['name']
                        or labels.get('io.fuse.lab.root') != self.root_id
                        or labels.get('com.docker.compose.project') != project):
                    raise LabError('Docker project contains resources without matching ownership labels; refusing lifecycle action.')

    def observe(self, meta):
        info = {'state': 'unavailable', 'actual_commit': None, 'actual_image_id': None, 'sql_image_id': None}
        try:
            containers = run([*ENGINE, 'ps', '-a', '--filter', 'label=com.docker.compose.project=' + meta['project_name'],
                              '--format', '{{.ID}} {{.Label "com.docker.compose.service"}} {{.State}}'])
            records = [line.split() for line in containers.stdout.splitlines() if line.strip()]
            states = {row[1]: row[2] for row in records if len(row) == 3}
            info['state'] = 'running' if all(states.get(s) == 'running' for s in ('sql', 'fuse')) else ('stopped' if records else 'absent')
            for identifier, service, state in (row for row in records if len(row) == 3):
                if service not in ('sql', 'fuse'):
                    continue
                data = json.loads(run([*ENGINE, 'container', 'inspect', identifier, '--format', '{{json .}}']).stdout)
                if service == 'sql':
                    info['sql_image_id'] = data['Image']
                else:
                    image = json.loads(run([*ENGINE, 'image', 'inspect', data['Image'], '--format', '{{json .Config}}']).stdout)
                    image_env = dict(value.split('=', 1) for value in image.get('Env', []) if '=' in value)
                    info.update(actual_commit=(image.get('Labels') or {}).get('org.opencontainers.image.revision') or image_env.get('GIT_COMMIT_ID'),
                                actual_image_id=data['Image'], http_bindings=data['HostConfig'].get('PortBindings', {}).get('8080/tcp', []))
        except (LabError, ValueError, KeyError):
            info['state'] = 'unavailable'
        return info

    def inventory_entry(self, name):
        external = self.external_path(name)
        if external.exists():
            meta = read_json(external)
            directory = Path(meta['directory'])
        else:
            directory, meta, _ = self.load(name)
        result = dict(meta)
        result.update(self.observe(meta))
        result['last_result'] = None
        result['last_result_current'] = False
        marker = directory / MARKER
        if marker.exists():
            state = read_json(marker)
            report = Path(state.get('report', ''))
            result['last_result'] = {'status': state.get('status'), 'finished_at': state.get('finished_at'), 'report': str(report)}
            provenance = report / 'run.json'
            if provenance.is_file():
                previous = read_json(provenance)
                # The API validator finishes before browser/log checks. Its marker
                # cannot certify a combined run that was interrupted or later failed.
                result['last_result'].update(status=previous.get('status', 'unknown'),
                    finished_at=previous.get('finished_at'), api=previous.get('api'), browser=previous.get('browser'))
                result['last_result_current'] = (previous.get('status') in ('passed', 'failed')
                    and bool(previous.get('finished_at'))
                    and previous.get('application_commit') == result['actual_commit'] == meta['source_commit']
                    and previous.get('actual_image_id') == result.get('actual_image_id')
                    and bool(previous.get('sql_image_id'))
                    and previous.get('sql_image_id') == result.get('sql_image_id')
                    and previous.get('harness', {}).get('definition_sha256') == self.provenance()['definition_sha256'])
        return result

    def inventory(self):
        names = {p.parent.name for p in (self.root / 'instances').glob('*/instance.json')}
        names.update(p.stem for p in (self.root / 'external').glob('*.json'))
        result = []
        for name in sorted(names):
            try:
                result.append(self.inventory_entry(name))
            except (LabError, KeyError, TypeError, ValueError):
                result.append({'name': name, 'state': 'invalid', 'error': 'Invalid instance metadata; inspect configuration.'})
        return result

    def sql(self, directory, env, query, capture=True):
        return self.compose(directory, env, 'exec', '-T', 'sql', 'bash', '-lc',
            'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -l 10 -t 60 -h -1 -W',
            input=query, capture=capture)

    def fixture_state(self, directory, env):
        state = self.sql(directory, env, "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name IN ('FuseLab_App', 'FuseLab_Reporting') AND state=0;\nGO\n").stdout.strip()
        if state == '0':
            # Distinguish a genuinely empty server from offline/partial fixtures.
            count = self.sql(directory, env, "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name IN ('FuseLab_App', 'FuseLab_Reporting');\nGO\n").stdout.strip()
            return 'absent' if count == '0' else 'incomplete'
        if state != '2':
            return 'incomplete'
        complete = self.sql(directory, env, """SET NOCOUNT ON;
SELECT CASE WHEN
 (SELECT COUNT(*) FROM [FuseLab_App].sys.extended_properties WHERE class=0 AND name IN (N'FuseInventoryIntegrationLab',N'FuseInventorySeedComplete') AND CONVERT(nvarchar(128),value)=N'sql-lab-v1')=2
 AND (SELECT COUNT(*) FROM [FuseLab_Reporting].sys.extended_properties WHERE class=0 AND name IN (N'FuseInventoryIntegrationLab',N'FuseInventorySeedComplete') AND CONVERT(nvarchar(128),value)=N'sql-lab-v1')=2
 THEN 1 ELSE 0 END;
GO
""").stdout.strip()
        return 'complete' if complete == '1' else 'incomplete'

    def provenance(self):
        paths = [HERE / 'lab', *sorted((HERE / 'scripts').glob('*.py')), SQL / 'compose.yaml', SQL / 'package.json', SQL / 'package-lock.json', *sorted((SQL / 'scripts').glob('*.*')), *sorted((SQL / 'seed').glob('*.sql'))]
        digest = hashlib.sha256()
        for path in paths:
            digest.update(str(path.relative_to(HERE)).encode() + b'\0' + path.read_bytes() + b'\0')
        commit = run(['git', '-C', str(REPO), 'rev-parse', 'HEAD']).stdout.strip()
        dirty = bool(run(['git', '-C', str(REPO), 'status', '--porcelain', '--', str(HERE)]).stdout.strip())
        return {'commit': commit, 'dirty': dirty, 'definition_sha256': digest.hexdigest(),
                'fixture_sha256': hashlib.sha256((SQL / 'seed/seed.sql').read_bytes()).hexdigest()}

    def up(self, directory, meta, env):
        self.assert_owned(meta)
        self.compose(directory, env, 'up', '-d', '--build', '--wait', '--wait-timeout', '180', capture=False)
        state = self.fixture_state(directory, env)
        if state == 'absent':
            self.sql(directory, env, (SQL / 'seed/seed.sql').read_text(), capture=False)
            state = self.fixture_state(directory, env)
        if state != 'complete':
            raise LabError(f'Fixtures are incomplete; inspect the instance, then reset {meta["name"]} --yes explicitly.')
        deadline = time.monotonic() + 120
        url = f'http://127.0.0.1:{env["FUSE_PORT"]}'
        while time.monotonic() < deadline:
            try:
                with urllib.request.urlopen(url + '/api/security/state', timeout=5) as response:
                    if response.status == 200:
                        break
            except Exception:
                time.sleep(1)
        else:
            raise LabError('Fuse did not become ready; inspect logs for this instance.')
        observation = self.observe(meta)
        if observation['actual_commit'] != meta['source_commit']:
            raise LabError('Running image revision differs from requested revision.')
        private_json(directory / 'deployment.json', {'application_commit': meta['source_commit'], 'deployed_at': now(),
                     'harness': self.provenance(), 'sql_image': env['SQL_IMAGE'], **observation})
        print(f'{meta["name"]} ready at {url}; existing data retained. Run check {meta["name"]} for a fresh validation.')

    def check(self, directory, meta, env):
        if (directory / MARKER).exists():
            raise LabError(f'This instance was already checked. Run reset {meta["name"]} --yes before another full check.')
        self.assert_owned(meta)
        observation = self.observe(meta)
        expected_binding = {'HostIp': '127.0.0.1', 'HostPort': str(meta['ports']['http'])}
        if observation['state'] != 'running' or observation['actual_commit'] != meta['source_commit'] or expected_binding not in observation.get('http_bindings', []):
            raise LabError('The expected instance/revision is not running at its configured loopback port; run up first.')
        stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
        report = self.root / 'reports' / meta['name'] / stamp
        report.mkdir(parents=True, mode=0o700)
        record = {'schema_version': 1, 'instance': meta['name'], 'project_name': meta['project_name'],
                  'application_commit': meta['source_commit'], 'harness': self.provenance(),
                  'sql_image': env['SQL_IMAGE'], 'started_at': now(), 'status': 'running', **observation}
        private_json(report / 'run.json', record)
        api = subprocess.run([sys.executable, str(SQL / 'scripts/validate.py'), '--lab-dir', str(directory),
                              '--definition-dir', str(SQL), '--output', str(report)], env=clean_environment())
        ui_code = 1
        if (report / 'fixtures.json').exists():
            try:
                ui_code = subprocess.run(['node', str(SQL / 'scripts/ui-check.mjs'), '--lab-dir', str(directory),
                    '--definition-dir', str(SQL), '--output', str(report), '--fixtures', str(report / 'fixtures.json')], env=clean_environment()).returncode
            except OSError:
                private_json(report / 'ui-summary.json', {'status': 'failed', 'reason': 'Node/browser dependencies unavailable.'})
        else:
            private_json(report / 'ui-summary.json', {'status': 'skipped', 'reason': 'API fixture setup did not complete.'})
        failures = bool(api.returncode or ui_code)
        for service in ('fuse', 'sql'):
            result = self.compose(directory, env, 'logs', '--no-color', service, check=False)
            (report / (service + '.log')).write_text(redact(result.stdout + result.stderr, env))
            if result.returncode:
                failures = True
        if (report / 'results.json').exists():
            record['api'] = read_json(report / 'results.json').get('summary')
        if (report / 'ui-summary.json').exists():
            browser = read_json(report / 'ui-summary.json')
            record['browser'] = {'status': browser['status'], 'passed': sum(c['status'] == 'passed' for c in browser.get('checks', []))}
        record.update(status='failed' if failures else 'passed', finished_at=now())
        private_json(report / 'run.json', record)
        private_json(directory / MARKER, {'status': record['status'], 'report': str(report), 'finished_at': record['finished_at']})
        latest = report.parent / 'latest'
        if latest.is_symlink():
            latest.unlink()
        if not latest.exists():
            latest.symlink_to(report.name, target_is_directory=True)
        print(f'Results: {report}')
        return 1 if failures else 0

    def lifecycle(self, args):
        if args.command in ('reset', 'destroy') and not args.yes:
            raise LabError(f'{args.command} deletes this instance\'s data; specify --yes explicitly.')
        directory, meta, env = self.load(args.name)
        with lock(self.root / 'locks' / (args.name + '.lock')):
            # Reload after locking so another deployment cannot leave a stale configuration.
            directory, meta, env = self.load(args.name)
            if args.command == 'check':
                return self.check(directory, meta, env)
            if args.command == 'logs':
                result = self.compose(directory, env, 'logs', '--tail', '100', '--no-color')
                print(redact(result.stdout + result.stderr, env))
                return 0
            self.assert_owned(meta)
            if args.command == 'deploy':
                with lock(self.root / '.registry.lock'):
                    commit, source = self.snapshot(args.ref)
                    meta['source_commit'] = commit
                    env.update(FUSE_COMMIT=commit, FUSE_SOURCE_DIR=str(source), FUSE_IMAGE=self.image(args.name, commit), FUSE_VERSION=f'lab.{commit[:12]}', FUSE_BUILD_DATE=now())
                    save_env(directory / '.env', env)
                    private_json(directory / 'instance.json', meta)
                self.up(directory, meta, env)
            elif args.command == 'up':
                self.up(directory, meta, env)
            elif args.command == 'down':
                self.compose(directory, env, 'down', '--remove-orphans', capture=False)
                print('Stopped instance; data volumes retained.')
            elif args.command in ('reset', 'destroy'):
                with lock(self.root / '.registry.lock'):
                    self.compose(directory, env, 'down', '--volumes', '--remove-orphans', capture=False)
                    (directory / MARKER).unlink(missing_ok=True)
                    (directory / 'deployment.json').unlink(missing_ok=True)
                    if args.command == 'destroy':
                        shutil.rmtree(self.directory(args.name))
                        print('Removed instance and its data volumes; reports and shared source snapshots retained.')
                        return 0
                self.up(directory, meta, env)
            return 0


def parser():
    result = argparse.ArgumentParser(description=__doc__)
    default = os.environ.get('FUSE_LAB_HOME') or str(Path(os.environ.get('XDG_STATE_HOME', Path.home() / '.local/state')) / 'fuse-inventory/labs')
    result.add_argument('--root', type=Path, default=Path(default), help='Runtime root outside Git (default: FUSE_LAB_HOME or XDG state directory).')
    commands = result.add_subparsers(dest='command', required=True)
    init = commands.add_parser('init', help='Create a named instance without starting Docker.')
    init.add_argument('name')
    init.add_argument('--ref', default='HEAD')
    init.add_argument('--kind', choices=['reference', 'experiment', 'validation'], default='experiment')
    init.add_argument('--purpose', default='')
    init.add_argument('--task', default='')
    init.add_argument('--http-port', type=int)
    init.add_argument('--sql-port', type=int)
    listing = commands.add_parser('list', help='Show managed instances and external references.')
    listing.add_argument('--json', action='store_true')
    status = commands.add_parser('status')
    status.add_argument('name')
    status.add_argument('--json', action='store_true')
    register = commands.add_parser('register', help='Record an existing lab as a read-only external reference.')
    register.add_argument('name')
    register.add_argument('--directory', type=Path, required=True)
    register.add_argument('--purpose', default='')
    register.add_argument('--task', default='')
    for name in ('up', 'check', 'logs', 'down', 'reset', 'destroy', 'deploy'):
        command = commands.add_parser(name)
        command.add_argument('name')
        if name in ('reset', 'destroy'):
            command.add_argument('--yes', action='store_true')
        if name == 'deploy':
            command.add_argument('--ref', required=True)
    return result


def main():
    os.umask(0o077)
    args = parser().parse_args()
    manager = Manager(args.root)
    if args.command == 'init':
        manager.initialize(args)
    elif args.command == 'register':
        manager.register(args)
    elif args.command in ('list', 'status'):
        records = manager.inventory() if args.command == 'list' else [manager.inventory_entry(args.name)]
        if args.json:
            print(json.dumps(records if args.command == 'list' else records[0], indent=2))
        else:
            print('NAME | KIND | STATE | REQUESTED / ACTUAL | PORTS | LAST CHECK | PURPOSE / TASK')
            for entry in records:
                print(' | '.join([entry['name'], entry.get('kind', '?') + ('' if entry.get('managed') else ' (external)'), entry['state'],
                    str(entry.get('source_commit', '?'))[:12] + ' / ' + str(entry.get('actual_commit') or '?')[:12],
                    str(entry.get('ports', {})), str((entry.get('last_result') or {}).get('status', 'never'))
                    + (' (historical)' if entry.get('last_result') and not entry.get('last_result_current') else ''),
                    entry.get('purpose', '') + ' ' + entry.get('task', '')]))
    else:
        return manager.lifecycle(args)
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (LabError, subprocess.SubprocessError) as error:
        print(f'Lab error: {error}', file=sys.stderr)
        raise SystemExit(1)
