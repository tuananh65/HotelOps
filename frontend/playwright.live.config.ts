import { defineConfig } from "@playwright/test";
if (process.env.FS004_LIVE_ENABLED !== "APPROVED_FS004_LOCAL_TRIAL")
  throw new Error(
    "Live suite requires explicit approved trial opt-in; no server/setup is started automatically.",
  );
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs004-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 60000,
  outputDir: "test-results/fs004-live",
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
