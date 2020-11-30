import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

// Each widget is a separate entry. Bundles are emitted as predictable file
// names into ../wwwroot/js so the Razor views can reference them directly
// (e.g. <script type="module" src="~/js/buyer.js">). React is bundled in.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: resolve(__dirname, '../wwwroot/js'),
    emptyOutDir: false,
    rollupOptions: {
      input: {
        buyer: resolve(__dirname, 'src/buyer/main.jsx'),
        'challan-sender': resolve(__dirname, 'src/challan/main.jsx'),
      },
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash][extname]',
      },
    },
  },
});
