import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "reinspection-workflow.spec.ts",
  workers: 1,
  reporter: "list",
  outputDir: "../TestResults/fast015/browser",
  use: { baseURL: "http://127.0.0.1:5187", channel: "msedge" },
  webServer: {
    command:
      "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5187 --strictPort",
    url: "http://127.0.0.1:5187",
    reuseExistingServer: false,
  },
});
