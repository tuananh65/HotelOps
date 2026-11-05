import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  testMatch: ["dev1-room-read.spec.ts", "cleaning-progress.spec.ts"],
  workers: 1,
  use: {
    baseURL: "http://127.0.0.1:5189",
    channel: "msedge",
  },
  webServer: process.env.DEV1_TEST_EXTERNAL_SERVER
    ? undefined
    : {
        command:
          "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5189",
        url: "http://127.0.0.1:5189",
        reuseExistingServer: false,
      },
  reporter: "list",
});
