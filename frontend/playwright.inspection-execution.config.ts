import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  testMatch: "fe05-inspection-execution.spec.ts",
  workers: 1,
  reporter: "list",
  outputDir: "../TestResults/inspection-execution/browser",
  use: { channel: "msedge" },
});
