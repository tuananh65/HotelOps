import { defineConfig } from "@playwright/test";
if (process.env.FS006_LIVE_ENABLED !== "APPROVED_FS006_READ_ONLY")
  throw Error("Explicit FS006 read-only opt-in required");
export default defineConfig({
  testDir: "./tests",
  testMatch: "fs006-live.spec.ts",
  workers: 1,
  retries: 0,
  timeout: 60000,
  outputDir: "test-results/fs006-live",
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
