import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView
    },
    {
      path: '/features',
      name: 'features',
      // route level code-splitting — this gets split into its own chunk
      component: () => import('@/views/FeaturesView.vue')
    },
    {
      path: '/screenshots',
      name: 'screenshots',
      component: () => import('@/views/ScreenshotsView.vue')
    }
  ]
})

export default router