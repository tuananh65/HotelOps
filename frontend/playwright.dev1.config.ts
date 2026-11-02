import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./tests",
  testMatch: "dev1-ui.browser.spec.ts",
  workers: 1,
  use: {
    baseURL: "https://localhost:5173",
    ignoreHTTPSErrors: true,
    viewport: { width: 1440, height: 1000 },
  },
  webServer: {
    command: "npm run dev",
    url: "https://localhost:5173",
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
    env: { VITE_AUTH_MODE: "cookie", VITE_API_BASE_URL: "" },
  },
  reporter: "list",
});
