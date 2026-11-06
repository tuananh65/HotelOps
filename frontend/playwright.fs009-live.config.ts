import { randomUUID } from "node:crypto";
import { defineConfig } from "@playwright/test";
if (process.env.FS009_LIVE_ENABLED !== "APPROVED_FS009_LOCAL_TRIAL")
  throw Error("FS009 opt-in required");
process.env.FS009_LIVE_RUN_ID ??= randomUUID();
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs009-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 300000,
  outputDir: "test-results/fs009-live",
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
