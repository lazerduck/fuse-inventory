import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { quasar, transformAssetUrls } from '@quasar/vite-plugin'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue({
      template: { transformAssetUrls }
    }),
    quasar({
      sassVariables: 'src/quasar-variables.sass'
    })
  ],
  server: {
    port: 5173,
    // proxy API calls to .NET in dev
    proxy: { '/api': { target: 'http://localhost:5180', changeOrigin: true } }
  },
  build: { outDir: 'dist' },
})
