import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// The client is published into the ASP.NET app's wwwroot and served from /next/ while the
// AngularJS client still lives at /app/. Flip the base and outDir to 'app' to cut over.
export default defineConfig({
  plugins: [vue()],
  base: '/next/',
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  build: {
    outDir: '../wwwroot/next',
    emptyOutDir: true,
    sourcemap: true
  },
  server: {
    port: 5173,
    // `npm run dev` talks to the API on the .NET dev port.
    proxy: Object.fromEntries(
      ['/Token', '/api', '/breeze', '/Home', '/signalr'].map(path => [
        path,
        { target: 'http://localhost:5080', changeOrigin: true, ws: path === '/signalr' }
      ])
    )
  }
})
