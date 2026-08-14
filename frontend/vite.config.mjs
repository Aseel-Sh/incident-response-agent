import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

export default defineConfig({
  root: resolve(import.meta.dirname),
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": "https://localhost:7104",
      "/auth": "https://localhost:7104",
      "/health": "https://localhost:7104",
      "/ready": "https://localhost:7104"
    }
  },
  build: {
    outDir: resolve(import.meta.dirname, "../IncidentResponseAgent.Api/wwwroot"),
    emptyOutDir: true,
    sourcemap: false
  }
});
