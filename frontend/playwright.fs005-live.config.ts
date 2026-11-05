import { defineConfig } from "@playwright/test";
if (process.env.FS005_LIVE_ENABLED !== "APPROVED_FS005_READ_ONLY")
  throw Error("Explicit FS005 read-only approval required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs005-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 60000,
  outputDir: "test-results/fs005-live",
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
