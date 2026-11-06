import { defineConfig } from "@playwright/test";
if (process.env.FS008_LIVE_ENABLED !== "APPROVED_FS008_LOCAL_TRIAL")
  throw Error("FS008 opt-in required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs008-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 60000,
  outputDir: "test-results/fs008-live",
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
