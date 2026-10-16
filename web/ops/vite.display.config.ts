import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The display-only build (spec 09 section 15): one page, display.html, built to dist-display and served by its own container
// (Dockerfile.display). `npm run build:display`. The API address is baked in at build time (VITE_API_BASE_URL).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: 'dist-display',
    emptyOutDir: true,
    rollupOptions: { input: { display: 'display.html' } },
  },
})
