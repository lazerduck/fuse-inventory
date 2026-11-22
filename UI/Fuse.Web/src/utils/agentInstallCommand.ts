import type { IAgentAssetChecksums } from 'api/client'

function shellQuote(value: string) {
  return "'" + value.replaceAll("'", "'\\''") + "'"
}

function fuseAddress(baseUrl: string) {
  const base = new URL(baseUrl || '/', window.location.origin)
  if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password || base.search || base.hash) {
    throw new Error('The agent requires an HTTP or HTTPS Fuse address.')
  }
  return base.href.replace(/\/$/, '')
}

export function agentInstallCommand(
  baseUrl: string, pairing: { pin?: string; ticket?: string; artifacts?: IAgentAssetChecksums }
) {
  if (!pairing.pin || !pairing.ticket) throw new Error('Fuse returned an incomplete pairing invitation.')
  const origin = fuseAddress(baseUrl)
  const url = origin.replace(/^http/, 'ws') + '/agent/connect'
  const bundle = btoa(JSON.stringify({ url, pin: pairing.pin, ticket: pairing.ticket }))
    .replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '')
  return bootstrapCommand(origin, pairing.artifacts, "printf '%s\\n' " + shellQuote(bundle) + ' | sh "$f"')
}

export function agentUpgradeCommand(baseUrl: string, release: { artifacts?: IAgentAssetChecksums }) {
  return bootstrapCommand(fuseAddress(baseUrl), release.artifacts, 'sh "$f" --upgrade')
}

function bootstrapCommand(origin: string, artifacts: IAgentAssetChecksums | undefined, launch: string) {
  const { installerSha256, amd64Sha256, arm64Sha256 } = artifacts ?? {}
  const installerUrl = origin + '/agent/install.sh'
  const steps = [
    "printf '%s\\n' " + shellQuote(`Downloading the Fuse agent installer from ${installerUrl}`),
    'f=$(mktemp)',
    'trap \'rm -f "$f"\' 0',
    'curl -fsS --connect-timeout 10 --max-time 30 ' + shellQuote(installerUrl) + ' -o "$f"',
    "printf '%s  %s\\n' " + hashArgument(installerSha256) + ' "$f" | sha256sum --check --status || ' +
      "{ printf '%s\\n' 'Fuse installer checksum verification failed. Copy a fresh command from Fuse.' >&2; exit 1; }",
    launch + ' --url ' + shellQuote(origin) +
      ' --sha256-amd64 ' + hashArgument(amd64Sha256) + ' --sha256-arm64 ' + hashArgument(arm64Sha256)
  ]
  return '( set -e; ' + steps.join('; ') + ' )'
}

function hashArgument(value?: string) {
  if (!value || !/^[0-9a-f]{64}$/i.test(value)) throw new Error('Fuse returned an incomplete agent package checksum.')
  return shellQuote(value.toLowerCase())
}
