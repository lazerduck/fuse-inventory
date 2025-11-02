import { createRouter, createWebHistory } from 'vue-router'
import Home from '../pages/Home.vue'
import Services from '../pages/Services.vue'

const routes = [
  {
    path: '/',
    name: 'home',
    component: Home
  },
  {
    path: '/services',
    name: 'services',
    component: Services
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
