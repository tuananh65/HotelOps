import { defineConfig } from "@playwright/test";
if (process.env.FS011_LIVE_ENABLED !== "APPROVED_FS011_LOCAL_TRIAL")
  throw Error("FS011 opt-in required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs011-default-off.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 180000,
  outputDir: "test-results/fs011-default-off",
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
