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
      path: '/accounts/create',
      name: 'accountCreate',
      component: () => import('./pages/AccountEditPage.vue')
    },
    {
      path: '/accounts/:id/edit',
      name: 'accountEdit',
      component: () => import('./pages/AccountEditPage.vue')
    },
    {
      path: '/identities',
      name: 'identities',
      component: () => import('./pages/IdentitiesPage.vue')
    },
    {
      path: '/identities/create',
      name: 'identityCreate',
      component: () => import('./pages/IdentityEditPage.vue')
    },
    {
      path: '/identities/:id/edit',
      name: 'identityEdit',
      component: () => import('./pages/IdentityEditPage.vue')
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
      path: '/positions',
      name: 'positions',
      component: () => import('./pages/PositionsPage.vue')
    },
    {
      path: '/responsibility-types',
      name: 'responsibilityTypes',
      component: () => import('./pages/ResponsibilityTypesPage.vue')
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
      path: '/secret-providers',
      name: 'secretProviders',
      component: () => import('./pages/SecretProvidersPage.vue')
    },
    {
      path: '/sql-integrations',
      name: 'sqlIntegrations',
      component: () => import('./pages/SqlIntegrationsPage.vue')
    },
    {
      path: '/sql-integrations/:id/permissions',
      name: 'sqlPermissionsOverview',
      component: () => import('./pages/SqlPermissionsOverviewPage.vue')
    },
    {
      path: '/audit-logs',
      name: 'auditLogs',
      component: () => import('./pages/AuditLogsPage.vue')
    },
    {
      path: '/risks',
      name: 'risks',
      component: () => import('./pages/RisksPage.vue')
    },
    {
      path: '/risks/create',
      name: 'riskCreate',
      component: () => import('./pages/RiskEditPage.vue')
    },
    {
      path: '/risks/:id/edit',
      name: 'riskEdit',
      component: () => import('./pages/RiskEditPage.vue')
    },
    // Read-only documentation mode routes
    {
      path: '/view',
      name: 'viewHome',
      component: () => import('./pages/readonly/ViewHome.vue')
    },
    {
      path: '/view/app/:id',
      name: 'viewApp',
      component: () => import('./pages/readonly/AppView.vue')
    },
    {
      path: '/view/instance/:id',
      name: 'viewInstance',
      component: () => import('./pages/readonly/InstanceView.vue')
    },
    {
      path: '/view/datastore/:id',
      name: 'viewDatastore',
      component: () => import('./pages/readonly/DatastoreView.vue')
    },
    {
      path: '/view/dependency/:id',
      name: 'viewDependency',
      component: () => import('./pages/readonly/DependencyView.vue')
    },
    {
      path: '/view/account/:id',
      name: 'viewAccount',
      component: () => import('./pages/readonly/AccountView.vue')
    },
    {
      path: '/view/identity/:id',
      name: 'viewIdentity',
      component: () => import('./pages/readonly/IdentityView.vue')
    },
    {
      path: '/view/external/:id',
      name: 'viewExternal',
      component: () => import('./pages/readonly/ExternalView.vue')
    },
    {
      path: '/view/platform/:id',
      name: 'viewPlatform',
      component: () => import('./pages/readonly/PlatformView.vue')
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
