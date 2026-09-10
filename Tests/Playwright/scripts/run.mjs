import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
process.chdir(fileURLToPath(new URL('..', import.meta.url)));
const project = `fuse-e2e-${process.pid}-${Date.now()}`;
const compose = ['compose', '-p', project, '-f', 'docker-compose.test.yaml'];
const env = { ...process.env, FUSE_TEST_PORT: process.env.FUSE_TEST_PORT || '5099' };
env.PLAYWRIGHT_BASE_URL = `http://127.0.0.1:${env.FUSE_TEST_PORT}`;
let child;
let interrupted = false;
let cleaning = false;
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => {
    interrupted = true;
    if (!cleaning) child?.kill(signal);
  });
}
function run(command, args, capture = false) {
  return new Promise((resolve, reject) => {
    child = spawn(command, args, { env, stdio: capture ? ['ignore', 'pipe', 'pipe'] : 'inherit' });
    let output = '';
    if (capture) {
      child.stdout.on('data', chunk => { output += chunk; });
      child.stderr.on('data', chunk => { output += chunk; });
    }
    child.on('error', reject);
    child.on('close', code => { child = undefined; resolve({ code, output }); });
  });
}
let exitCode = 1;
try {
  if ((await run('docker', [...compose, 'up', '--build', '-d'])).code !== 0 || interrupted) throw new Error('Application startup failed or was interrupted');
  const deadline = Date.now() + 120_000;
  for (;;) {
    if (interrupted) throw new Error('Test run interrupted');
    try { if ((await fetch(`${env.PLAYWRIGHT_BASE_URL}/api/security/state`, { signal: AbortSignal.timeout(5000) })).ok) break; } catch {}
    if (Date.now() > deadline) throw new Error('Setup API did not become available');
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  exitCode = (await run(process.execPath, ['node_modules/@playwright/test/cli.js', 'test', ...process.argv.slice(2)])).code ?? 1;
} catch (error) {
  console.error(error);
} finally {
  cleaning = true;
  mkdirSync('artifacts', { recursive: true });
  try {
    const logs = await run('docker', [...compose, 'logs', '--no-color'], true);
    writeFileSync('artifacts/server.log', logs.output);
  } catch (error) { console.error('Could not collect server logs:', error); }
  try {
    if ((await run('docker', [...compose, 'down', '-v', '--remove-orphans', '--rmi', 'local'])).code !== 0) exitCode = 1;
  } catch (error) { console.error('Cleanup failed:', error); exitCode = 1; }
}
process.exitCode = interrupted ? 130 : exitCode;
