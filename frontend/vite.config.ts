import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The .NET API listens on http://localhost:5080 (see backend launchSettings.json).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': { target: process.env.API_URL ?? 'http://localhost:5080', changeOrigin: true },
    },
  },
})
