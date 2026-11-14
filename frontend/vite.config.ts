/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Encaminha as chamadas da API para o backend ASP.NET Core em desenvolvimento.
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080',
    },
  },
  // Testes do frontend (npm test): Vitest com DOM simulado (jsdom). Câmera e API são simuladas nos testes.
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
})
