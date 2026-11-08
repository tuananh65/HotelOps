import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests", testMatch: ["cleaning-assignment.spec.ts", "cleaning-create.spec.ts", "cleaning-progress.spec.ts", "cleaning-execution.spec.ts", "cleaning-monitor.spec.ts", "cleaning-cancellation.spec.ts", "rework-execution.spec.ts", "turnover-history.spec.ts"],
  // FE-03 room board is outside this extracted release; its test source is retained.
  grepInvert: /room board ignores a canceled stale branch request/,
  workers: 1, use: { baseURL: "http://127.0.0.1:5189", channel: "msedge" },
  webServer: { command: "node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5189", url: "http://127.0.0.1:5189", reuseExistingServer: false }, reporter: "list"
});
