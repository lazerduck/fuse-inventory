import { defineConfig } from 'vite'
import vueDevTools from 'vite-plugin-vue-devtools'
import vue from '@vitejs/plugin-vue'
import { quasar, transformAssetUrls } from '@quasar/vite-plugin'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue({
      template: { transformAssetUrls }
    }),
    quasar({
      sassVariables: true
    }),
    vueDevTools()
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5071',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
