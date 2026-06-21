import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { Quasar } from 'quasar'
import App from './App.vue'
import router from './router'
import '@quasar/extras/material-icons/material-icons.css'
import 'quasar/src/css/index.sass'
import './styles/global.scss'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(Quasar, {
  config: {
    notify: {}
  }
})

app.mount('#app')
