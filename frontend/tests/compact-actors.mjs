import { chromium, expect } from "@playwright/test";
import fs from "node:fs";
const out = "../artifacts/actor-lists";
fs.mkdirSync(out, { recursive: true });
const credentials = JSON.parse(
  fs
    .readFileSync(
      "../artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json",
      "utf8",
    )
    .replace(/^\uFEFF/, ""),
);
const baseline = process.env.ACTOR_BASELINE === "1";
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const results = [];
try {
  for (const username of new Set([
    ...credentials.usernames,
    "demo_om",
    "demo_hks",
    "demo_maintenance_lead",
    "demo_owner",
  ])) {
    const context = await browser.newContext({
        viewport: { width: 1440, height: 900 },
      }),
      page = await context.newPage();
    const result = { username, menus: [], pages: [], errors: [] };
    results.push(result);
    page.on("pageerror", (e) => result.errors.push(e.message));
    await page.goto("http://127.0.0.1:5179");
    await page.getByLabel("Tên đăng nhập", { exact: true }).fill(username);
    await page
      .getByLabel("Mật khẩu", { exact: true })
      .fill(credentials.password);
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    await page.waitForTimeout(250);
    if (
      (await page.locator("[role=alert]").allTextContents()).some((t) =>
        t.includes("Quá nhiều"),
      )
    ) {
      await page.waitForTimeout(61000);
      await page
        .getByRole("button", { name: "Đăng nhập", exact: true })
        .click();
    }
    await page.locator("main h1").waitFor();
    result.menus = await page
      .getByRole("navigation")
      .getByRole("button")
      .allTextContents();
    for (const name of result.menus) {
      await page
        .getByRole("navigation")
        .getByRole("button", { name: name.trim(), exact: true })
        .click();
      await page.waitForLoadState("networkidle");
      await page.locator("main .spinner").waitFor({ state: "hidden" });
      const errors = await page.locator("main [role=alert]").allTextContents();
      result.pages.push({
        name: name.trim(),
        heading: await page.locator("main h1").innerText(),
        errors,
      });
      if (!baseline) expect(errors, username + " " + name).toEqual([]);
    }
    if (username === "demo_admin") {
      if (!baseline) {
        expect(result.menus).toContain("Tổng quan");
        expect(result.menus).toContain("Chi nhánh & danh mục");
      }
      await page
        .getByRole("navigation")
        .getByRole("button", { name: "Nhân sự & truy cập", exact: true })
        .click();
      await page.locator("tbody tr").first().waitFor();
      await page.evaluate(() =>
        scrollTo(0, document.documentElement.scrollHeight),
      );
      await page.waitForTimeout(150);
      result.header = await page.locator("thead th").first().boundingBox();
      await page.screenshot({
        path: out + (baseline ? "/before-admin.png" : "/after-admin.png"),
      });
    }
    expect(result.errors).toEqual([]);
    console.log("AUDIT " + username + ": " + result.menus.join(", "));
    await context.close();
  }
} finally {
  fs.writeFileSync(
    out + (baseline ? "/before.json" : "/after.json"),
    JSON.stringify(results, null, 2),
  );
  await browser.close();
}
