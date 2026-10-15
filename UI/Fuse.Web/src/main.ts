import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { Quasar, Notify, Dialog } from 'quasar'
import { VueQueryPlugin } from '@tanstack/vue-query'
import router from './router'
import { warnBeforeUnload } from './composables/useUnsavedChanges'
window.addEventListener('beforeunload', warnBeforeUnload)
import { getInitialTheme } from './utils/themePreference'

// Import icon libraries
import '@quasar/extras/material-icons/material-icons.css'

// Import Quasar css
import 'quasar/dist/quasar.sass'
import './styles/theme.scss'
import 'driver.js/dist/driver.css'

import App from './App.vue'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(VueQueryPlugin)
app.use(Quasar, {
  plugins: {
    Notify,
    Dialog
  },
  config: {
    dark: getInitialTheme(),
    brand: {
      primary: '#0057ff', secondary: '#606b73', accent: '#ffaa00',
      dark: '#242c30', 'dark-page': '#171d20',
      positive: '#237a52', negative: '#c43d49', warning: '#a66b00', info: '#0057ff'
    }
  }
})

app.mount('#app')
