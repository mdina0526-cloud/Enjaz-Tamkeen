import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  server: {
    port: 5173,
    proxy: {
      // During development the API runs on :5224; same-origin proxy avoids CORS.
      '/api': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5224', changeOrigin: true },
    },
  },
  build: {
    chunkSizeWarningLimit: 1200,
  },
})
