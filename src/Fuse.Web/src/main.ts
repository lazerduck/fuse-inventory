import { createApp } from 'vue'
import { Quasar } from 'quasar'
import '@quasar/extras/material-icons/material-icons.css'
import 'quasar/dist/quasar.css'
import App from './App.vue'
import router from './router'
import { createPinia } from 'pinia'

export const pinia = createPinia()
const app = createApp(App)

app.use(Quasar, {
  plugins: {}, // import Quasar plugins and add here
})
app.use(router)
app.use(pinia)

app.mount('#app')
