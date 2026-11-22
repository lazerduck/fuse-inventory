import { describe, expect, it } from 'vitest'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { agentInstallCommand } from '../src/utils/agentInstallCommand'

const pairing = {
  pin: 'example-public-key-pin=', ticket: 'single-use-ticket',
  artifacts: { installerSha256: 'a'.repeat(64), amd64Sha256: 'b'.repeat(64), arm64Sha256: 'c'.repeat(64) }
}

describe('platform pairing command', () => {
  it.each([['http', 'ws'], ['https', 'wss']])('uses the selected %s API origin for downloads and pairing', (http, ws) => {
    const command = agentInstallCommand(`${http}://fuse.home.lan:8080/`, pairing)
    expect(command).not.toMatch(/[\r\n]/)
    expect(command).toContain(`curl -fsS --connect-timeout 10 --max-time 30 '${http}://fuse.home.lan:8080/agent/install.sh'`)
    expect(command.indexOf('sha256sum --check --status')).toBeLessThan(command.indexOf('sh "$f" --url'))
    expect(command).toContain(`--sha256-amd64 '${'b'.repeat(64)}' --sha256-arm64 '${'c'.repeat(64)}'`)
    const bundle = /'([A-Za-z0-9_-]+)' \| sh "\$f" --url/.exec(command)![1]!
    const installerArgs = command.split('sh "$f" --url')[1]!
    expect(installerArgs).not.toContain(bundle)
    expect(installerArgs).not.toContain('--pair')
    expect(JSON.parse(Buffer.from(bundle, 'base64url').toString())).toEqual({
      url: `${ws}://fuse.home.lan:8080/agent/connect`, ticket: pairing.ticket, pin: pairing.pin
    })
  })

  it.skipIf(process.platform !== 'linux').each(['/bin/sh', '/bin/bash'])('prints progress, then verifies the download before running it under %s', shell => {
    const source = 'IFS= read -r bundle && printf "installed %s\\n" "$bundle"\n'
    const installerSha256 = createHash('sha256').update(source).digest('hex')
    const command = agentInstallCommand('http://fuse.home.lan', { ...pairing, artifacts: { ...pairing.artifacts, installerSha256 } })
    // Only the download is stubbed. The shell executes the generated command, checksum and staged installer.
    const mockDownload = 'curl() { for output do :; done; echo fetched; printf "%s" "$BOOTSTRAP" > "$output"; }\n'
    const run = (body: string) => spawnSync(shell, ['-c', mockDownload + command], {
      env: { ...process.env, BOOTSTRAP: body }, encoding: 'utf8', timeout: 5000
    })
    const success = run(source)
    expect(success.status).toBe(0)
    expect(success.stdout).toMatch(/^Downloading the Fuse agent installer[^\n]*\nfetched\ninstalled [A-Za-z0-9_-]+\n$/)
    const mismatch = run('echo unverified')
    expect(mismatch.status).toBe(1)
    expect(mismatch.stderr).toContain('checksum verification failed')
    expect(mismatch.stdout).not.toContain('unverified')
  })

  it('rejects incomplete hash data and unexpected origins', () => {
    expect(() => agentInstallCommand('', { ...pairing, artifacts: { ...pairing.artifacts, amd64Sha256: '' } })).toThrow('checksum')
    expect(() => agentInstallCommand('ftp://fuse.home.lan', pairing)).toThrow('HTTP')
  })
})
