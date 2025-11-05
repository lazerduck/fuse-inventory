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
      path: '/servers',
      name: 'servers',
      component: () => import('./pages/ServersPage.vue')
    },
    {
      path: '/environments',
      name: 'environments',
      component: () => import('./pages/EnvironmentsPage.vue')
    }
  ]
})

export default router
