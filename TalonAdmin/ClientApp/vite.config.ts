import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// The client is published into the ASP.NET app's wwwroot and served from /app/, which is where
// the AngularJS client used to live — so existing links and bookmarks still work.
export default defineConfig({
  plugins: [vue()],
  base: '/app/',
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  build: {
    outDir: '../wwwroot/app',
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
