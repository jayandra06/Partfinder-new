import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const defaultTarget =
    mode === 'development' ? 'http://127.0.0.1:3000' : 'https://backend.partfinder.store'
  let rawTarget = env.VITE_API_URL || defaultTarget
  if (!/^https?:\/\//i.test(rawTarget)) rawTarget = `https://${rawTarget}`
  const target = rawTarget.replace(/\/api\/?$/, '')

  return {
    plugins: [react()],
    preview: {
      host: '127.0.0.1',
      port: 4173,
      strictPort: true,
    },
    server: {
      host: '127.0.0.1',
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': {
          target,
          changeOrigin: true,
        },
      },
    },
  }
})

