import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: ["inspection.browser.spec.ts", "inspection-reads.browser.spec.ts"],
  workers: 1,
  use: {
    baseURL: "http://127.0.0.1:5278",
    launchOptions: {
      executablePath:
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    },
  },
  webServer: {
    command: "npm.cmd run dev -- --port 5278",
    url: "http://127.0.0.1:5278",
    reuseExistingServer: false,
  },
  reporter: [["list"]],
});
