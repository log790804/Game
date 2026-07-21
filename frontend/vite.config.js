import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

function animalVillagePrototypeIndex() {
  return {
    name: 'animal-village-prototype-index',
    configureServer(server) {
      server.middlewares.use((req, _res, next) => {
        const path = req.url?.split('?')[0]

        if (path === '/prototypes/animal-village' || path === '/prototypes/animal-village/') {
          req.url = '/prototypes/animal-village/index.html'
        }

        next()
      })
    }
  }
}

export default defineConfig({
  plugins: [animalVillagePrototypeIndex(), vue()],
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5099',
        changeOrigin: true
      }
    }
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  }
})
