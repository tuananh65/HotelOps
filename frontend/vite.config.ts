import { readFileSync } from "node:fs";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  if (Boolean(env.DEV_HTTPS_CERT) !== Boolean(env.DEV_HTTPS_KEY)) {
    throw new Error("Set both DEV_HTTPS_CERT and DEV_HTTPS_KEY for HTTPS development.");
  }
  return {
    server: {
      host: "localhost",
      port: 5173,
      strictPort: true,
      https: env.DEV_HTTPS_CERT
        ? { cert: readFileSync(env.DEV_HTTPS_CERT), key: readFileSync(env.DEV_HTTPS_KEY) }
        : undefined,
      proxy: {
        "/api": {
          target: env.API_PROXY_TARGET || "https://localhost:7443",
          secure: true,
          changeOrigin: false,
        },
      },
    },
  };
});
