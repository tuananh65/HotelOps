import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "inspection-assignment.spec.ts",
  workers: 1,
  reporter: "list",
  outputDir: "../TestResults/fast017/browser",
  use: { baseURL: "http://127.0.0.1:5185", channel: "msedge" },
  webServer: {
    command:
      "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5185 --strictPort",
    url: "http://127.0.0.1:5185",
    reuseExistingServer: false,
  },
});
