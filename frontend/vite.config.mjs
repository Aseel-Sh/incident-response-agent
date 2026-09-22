import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

// Only the local development proxy uses the self-signed ASP.NET certificate.
// This configuration is not used by the production ASP.NET-hosted frontend.
const localApiProxy = {
  target: "https://localhost:7104",
  // OIDC must use the registered HTTPS API origin for callbacks and cookies.
  changeOrigin: true,
  secure: false
};

export default defineConfig({
  root: resolve(import.meta.dirname),
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": { ...localApiProxy },
      "/auth": { ...localApiProxy },
      "/health": { ...localApiProxy },
      "/ready": { ...localApiProxy }
    }
  },
  build: {
    outDir: resolve(import.meta.dirname, "../IncidentResponseAgent.Api/wwwroot"),
    emptyOutDir: true,
    sourcemap: false
  }
});
