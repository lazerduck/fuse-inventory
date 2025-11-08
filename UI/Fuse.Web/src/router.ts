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
      path: '/servers',
      name: 'servers',
      component: () => import('./pages/ServersPage.vue')
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
