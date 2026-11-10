import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "room-status.spec.ts",
  workers: 1,
  use: { baseURL: "http://127.0.0.1:5193", channel: "msedge" },
  webServer: {
    command: "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5193",
    url: "http://127.0.0.1:5193",
    reuseExistingServer: false,
  },
  reporter: "list",
});
