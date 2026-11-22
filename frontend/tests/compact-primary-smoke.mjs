import { chromium, expect } from "@playwright/test";
import fs from "node:fs";
const credentials = JSON.parse(
  fs
    .readFileSync(
      "../artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json",
      "utf8",
    )
    .replace(/^\uFEFF/, ""),
);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const results = [];
try {
  for (const [username, heading] of [
    ["demo_admin", "Tổng quan quản trị"],
    ["demo_hks", "Điều phối buồng phòng"],
  ]) {
    const context = await browser.newContext({
        viewport: { width: 1440, height: 900 },
      }),
      page = await context.newPage();
    await page.goto("http://127.0.0.1:5178");
    await page.getByLabel("Tên đăng nhập", { exact: true }).fill(username);
    await page
      .getByLabel("Mật khẩu", { exact: true })
      .fill(credentials.password);
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await expect(page.locator("main h1")).toHaveText(heading);
    await page.waitForLoadState("networkidle");
    await expect(page.locator("main [role=alert]")).toHaveCount(0);
    await page.screenshot({
      path: `../artifacts/actor-lists/primary-${username}.png`,
      fullPage: true,
    });
    results.push({ username, heading, pass: true });
    await context.close();
  }
  console.log(JSON.stringify(results));
} finally {
  fs.writeFileSync(
    "../artifacts/actor-lists/primary-browser.json",
    JSON.stringify(results, null, 2),
  );
  await browser.close();
}
