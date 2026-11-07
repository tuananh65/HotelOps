import { defineConfig } from "@playwright/test";
import baseline from "./playwright.dev1.config";
export default defineConfig({
  ...baseline,
  testMatch: "cleaning-execution.spec.ts",
  outputDir: "test-results/fs013-ui",
});
