import { randomUUID } from "node:crypto";
import { defineConfig } from "@playwright/test";
if (process.env.FS013_LIVE_ENABLED !== "APPROVED_FS013_LOCAL_TRIAL")
  throw Error("FS013 opt-in required");
process.env.FS013_LIVE_RUN_ID ??= randomUUID();
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs013-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 300000,
  outputDir: "test-results/fs013-live",
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
