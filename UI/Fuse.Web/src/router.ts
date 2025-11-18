import { createRouter, createWebHistory } from 'vue-router'
import { useFuseStore } from './stores/FuseStore'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('./pages/HomePage.vue')
    },
    {
      path: '/applications',
      name: 'applications',
      component: () => import('./pages/ApplicationsPage.vue')
    },
    {
      path: '/applications/:id',
      name: 'applicationEdit',
      component: () => import('./pages/ApplicationEditPage.vue')
    },
    {
      path: '/applications/:applicationId/instances/:instanceId',
      name: 'instanceEdit',
      component: () => import('./pages/InstanceEditPage.vue')
    },
    {
      path: '/accounts',
      name: 'accounts',
      component: () => import('./pages/AccountsPage.vue')
    },
    {
      path: '/data-stores',
      name: 'dataStores',
      component: () => import('./pages/DataStoresPage.vue')
    },
    {
      path: '/platforms',
      name: 'platforms',
      component: () => import('./pages/PlatformsPage.vue')
    },
    {
      path: '/environments',
      name: 'environments',
      component: () => import('./pages/EnvironmentsPage.vue')
    },
    {
      path: '/external-resources',
      name: 'externalResources',
      component: () => import('./pages/ExternalResourcesPage.vue')
    },
    {
      path: '/tags',
      name: 'tags',
      component: () => import('./pages/TagsPage.vue')
    },
    {
      path: '/security',
      name: 'security',
      component: () => import('./pages/Security.vue')
    },
    {
      path: '/graph',
      name: 'graph',
      component: () => import('./pages/Graph.vue')
    },
    {
      path: '/config',
      name: 'config',
      component: () => import('./pages/ConfigPage.vue')
    },
    {
      path: '/kuma-integrations',
      name: 'kumaIntegrations',
      component: () => import('./pages/KumaIntegrationsPage.vue')
    },
    {
      path: '/audit-logs',
      name: 'auditLogs',
      component: () => import('./pages/AuditLogsPage.vue')
    }
  ]
})

router.beforeEach(async (to, from, next) => {
  const fuseStore = useFuseStore()
  
  // Check if setup is required
  if (fuseStore.requireSetup && to.name !== 'security') {
    // Redirect to security page if setup is required
    next({ name: 'security' })
  } else if (!fuseStore.requireSetup && to.name === 'security' && from.name !== null) {
    // Allow navigation away from security page only if setup is complete
    next()
  } else {
    next()
  }
})

export default router
