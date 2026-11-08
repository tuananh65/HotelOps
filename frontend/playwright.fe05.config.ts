import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "fe05-checklists.spec.ts",
  workers: 1,
  timeout: 60000,
  reporter: [["list"]],
  outputDir: "test-results/fe05-checklists",
  use: {
    baseURL: "http://127.0.0.1:5187",
    launchOptions: {
      executablePath:
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    },
  },
  webServer: {
    command: "npm.cmd run dev -- --port 5187 --strictPort",
    url: "http://127.0.0.1:5187",
    reuseExistingServer: false,
  },
});
