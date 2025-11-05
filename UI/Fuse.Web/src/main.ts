import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { Quasar, Notify } from 'quasar'
import { VueQueryPlugin } from '@tanstack/vue-query'
import router from './router'

// Import icon libraries
import '@quasar/extras/material-icons/material-icons.css'

// Import Quasar css
import 'quasar/dist/quasar.sass'

import App from './App.vue'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(VueQueryPlugin)
app.use(Quasar, { 
  plugins: { 
    Notify 
  }
})

app.mount('#app')
