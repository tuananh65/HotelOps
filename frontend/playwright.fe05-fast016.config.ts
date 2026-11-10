import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: [
    "reinspection-workflow.spec.ts",
    "reinspection-conclusion.spec.ts",
  ],
  workers: 1,
  reporter: "list",
  outputDir: "../TestResults/fast016/browser",
  use: { baseURL: "http://127.0.0.1:5186", channel: "msedge" },
  webServer: {
    command:
      "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5186 --strictPort",
    url: "http://127.0.0.1:5186",
    reuseExistingServer: false,
  },
});
