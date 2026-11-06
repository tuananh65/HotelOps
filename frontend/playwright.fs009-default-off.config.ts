import { defineConfig } from "@playwright/test";
if (process.env.FS009_LIVE_ENABLED !== "APPROVED_FS009_LOCAL_TRIAL")
  throw Error("FS009 opt-in required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs009-default-off.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 180000,
  outputDir: "test-results/fs009-default-off",
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
