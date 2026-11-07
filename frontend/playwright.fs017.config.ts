import { defineConfig } from "@playwright/test";
import baseline from "./playwright.dev1.config";
export default defineConfig({
  ...baseline,
  testMatch: ["turnover-history.spec.ts", "cleaning-monitor.spec.ts"],
  outputDir: "test-results/fs017",
});
