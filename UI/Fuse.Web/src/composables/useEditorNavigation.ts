import { useRouter, type RouteLocationRaw } from 'vue-router'

/** Remember the originating list/parent, without sending a saved editor back to another editor. */
export function useEditorNavigation(fallback: RouteLocationRaw) {
  const router = useRouter()
  const back = router.options.history.state.back
  const allowed = new Set(['applications', 'applicationEdit', 'accounts', 'identities', 'risks',
    'sqlPermissionsOverview', 'documentationCompleteness', 'dataStores', 'platforms',
    'externalResources', 'messageBrokers', 'home'])
  const destination = typeof back === 'string' && back.startsWith('/') && !back.startsWith('//') &&
    allowed.has(String(router.resolve(back).name)) ? back : fallback
  return () => router.push(destination)
}
