import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// In dev, /api calls are proxied to the ASP.NET Core backend.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/vitest.setup.ts'],
  },
})
