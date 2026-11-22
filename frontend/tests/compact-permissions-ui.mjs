import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect } from "@playwright/test";
import { permissionCopy } from "../src/compact/permission-copy.ts";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const approval = fs.readFileSync(process.env.STAFFING_APPROVAL_FILE, "utf8");
const password = approval
  .slice(approval.indexOf("SHARED TEST CREDENTIAL"))
  .match(/`([^`]+)`/)[1];
const response = await fetch(base + "/api/v2/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username: "NamNH1", password }),
});
assert.equal(response.status, 200);
const token = (await response.json()).accessToken;
const result = await fetch(base + "/api/v2/catalog/permissions?pageSize=100", {
  headers: { Authorization: `Bearer ${token}` },
});
assert.equal(result.status, 200);
const permissions = (await result.json()).items.map((r) => r.fields);
assert.equal(permissions.length, 67);
for (const p of permissions)
  assert(
    permissionCopy[p.code]?.every((value) => value.length > 5),
    "Missing Vietnamese copy: " + p.code,
  );
console.log(
  "PASS All 67 database permissions have Vietnamese names and descriptions",
);
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [];
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(
    (value) => sessionStorage.setItem("hotelops.compact.session", value),
    token,
  );
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(base + "/#settings?resource=permissions");
  await expect(page.getByLabel("Nội dung cấu hình")).toHaveValue("roles");
  await expect(
    page.getByLabel("Nội dung cấu hình").locator("option"),
  ).toHaveCount(3);
  await expect(page.locator(".roles-table")).toBeVisible();
  await expect(page.locator(".catalog-sections")).toHaveCount(0);
  console.log(
    "PASS Permission catalog hidden from dropdown and legacy direct URL falls back to visible settings",
  );
  await page.screenshot({
    path: "../artifacts/admin-ux/permissions-1440.png",
    fullPage: true,
  });
  await expect(
    page.getByRole("button", { name: "Phân quyền", exact: true }).first(),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Phân quyền", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog tbody tr")).toHaveCount(67);
  await expect(page.locator("dialog")).not.toContainText("ASSET_MANAGE");
  await expect(
    page.getByLabel("Cấp quyền Xem tài sản", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByLabel("Phạm vi Xem tài sản", { exact: true }),
  ).toHaveCount(1);
  await page.keyboard.press("Escape");
  await page.goto(base + "/#settings?resource=references");
  await expect(
    page.getByRole("button", {
      name: "+ Thêm cấu hình nghiệp vụ",
      exact: true,
    }),
  ).toHaveCount(0);
  console.log(
    "PASS Remaining settings sections and role permission dialog remain functional",
  );
  await expect(page.locator(".roles-table tbody tr")).toHaveCount(10);
  for (const width of [375, 768]) {
    await page.setViewportSize({ width, height: 900 });
    await page.waitForTimeout(350);
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await page.screenshot({
      path: `../artifacts/admin-ux/permissions-${width}.png`,
      fullPage: true,
    });
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    "../artifacts/admin-ux/permission-results.json",
    JSON.stringify(
      {
        at: new Date().toISOString(),
        permissionCount: permissions.length,
        permissionCatalogHidden: true,
        status: "PASS",
        browserErrors: errors,
      },
      null,
      2,
    ),
  );
} finally {
  await browser.close();
}
