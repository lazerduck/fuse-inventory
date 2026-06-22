import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import '@fortawesome/fontawesome-free/css/all.css'
import './styles/global.scss'

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')

// SEO: Apply canonical for current path on first paint
document.addEventListener('DOMContentLoaded', () => {
  const canon = document.querySelector('link[rel="canonical"]') as HTMLLinkElement | null
  if (canon) {
    canon.setAttribute('href', `https://fuse-inventory.dev${location.pathname.replace(/\/$/, '') || '/'}`)
  }
})