import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs010-restart.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 120000,
  outputDir: "test-results/fs010-restart",
  reporter: "list",
  use: {
    baseURL: "https://localhost:5173",
    channel: "msedge",
    ignoreHTTPSErrors: false,
    trace: "off",
    screenshot: "off",
    video: "off",
  },
});
