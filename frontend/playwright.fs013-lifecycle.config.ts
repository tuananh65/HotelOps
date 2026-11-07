import { defineConfig } from "@playwright/test";
if (process.env.FS013_LIVE_ENABLED !== "APPROVED_FS013_LOCAL_TRIAL")
  throw Error("FS013 opt-in required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs013-lifecycle.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 600000,
  outputDir: "test-results/fs013-lifecycle",
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
