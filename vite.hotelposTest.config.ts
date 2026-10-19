import { mergeConfig } from "vite";
import originalConfig from "./vite.config";

// Run this merged copy without using or stopping the original servers.
export default mergeConfig(originalConfig, {
  server: {
    port: 5175,
    proxy: {
      "/api": { target: "https://localhost:7029" },
    },
  },
});
