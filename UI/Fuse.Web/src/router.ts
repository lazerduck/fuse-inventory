import { createRouter, createWebHistory } from 'vue-router'

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
    }
  ]
})

export default router
