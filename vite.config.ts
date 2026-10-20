import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import basicSsl from "@vitejs/plugin-basic-ssl";

/** Development only: HTTPS on the FE and a same-origin proxy to the BE1 API. */
export default defineConfig({
  plugins: [react(), basicSsl()],
  server: {
    host: "localhost",
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": {
        target: "https://localhost:7019",
        changeOrigin: true,
        secure: false, // Self-signed ASP.NET Core developer certificate; local only.
      },
    },
  },
});
