import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
export default defineConfig({
  plugins: [vue()],
  resolve: { alias: {
    // jsdom needs the browser build; Node otherwise resolves Quasar's SSR bundle.
    quasar: fileURLToPath(new URL('./node_modules/quasar/dist/quasar.client.js', import.meta.url)),
    api: fileURLToPath(new URL('./src/api', import.meta.url)),
    permissions: fileURLToPath(new URL('./src/permissions', import.meta.url)),
  } },
  test: { environment: 'jsdom', include: ['tests/**/*.test.ts'], clearMocks: true },
})
