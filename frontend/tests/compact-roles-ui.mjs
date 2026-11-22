import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect } from "@playwright/test";

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
const result = await fetch(base + "/api/v2/catalog/roles?pageSize=100", {
  headers: { Authorization: `Bearer ${token}` },
});
assert.equal(result.status, 200);
const roles = (await result.json()).items.map((r) => r.fields);
assert.equal(roles.length, 10);
assert(
  roles.every(
    (r) =>
      r.isActive &&
      r.description?.length > 20 &&
      !/DEMO|V10 mapping/.test(r.description),
  ),
);
assert(!roles.some((r) => /^DEMO_/.test(r.code)));
assert.equal(
  roles.find((r) => r.code === "SYS_ADMIN").name,
  "Quản trị hệ thống",
);
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  observations = [];
const out = "../artifacts/role-cleanup";
fs.mkdirSync(out, { recursive: true });
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
  await page.goto(base + "/#settings?resource=roles");
  await expect(page.locator(".roles-table tbody tr")).toHaveCount(10);
  assert.deepEqual(await page.locator(".roles-table th").allTextContents(), [
    "Tên",
    "Mô tả",
    "Thao tác",
  ]);
  await expect(page.locator(".roles-table")).not.toContainText("SYS_ADMIN");
  await expect(page.locator(".roles-table .status-light")).toHaveCount(0);
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 900 });
    await page.waitForTimeout(350);
    const edit = page.locator(".role-edit").first(),
      grants = page.locator(".role-grants").first();
    await grants.scrollIntoViewIfNeeded();
    const a = await edit.boundingBox(),
      b = await grants.boundingBox();
    assert.equal(a.width, b.width);
    assert.equal(a.height, b.height);
    assert(a.height >= 44);
    if (width === 1440) {
      assert.equal(a.y, b.y);
      assert(b.x - a.x - a.width >= 8);
    } else {
      assert.equal(a.x, b.x);
      assert(b.y - a.y - a.height >= 8);
    }
    assert.notEqual(
      await edit.evaluate((el) => getComputedStyle(el).backgroundColor),
      await grants.evaluate((el) => getComputedStyle(el).backgroundColor),
    );
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    );
    await page.screenshot({
      path: `${out}/roles-${width}.png`,
      fullPage: true,
    });
    observations.push({ width, equalButtons: true, pageOverflow: false });
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.getByLabel("Tìm trong danh mục").fill("bảo trì");
  await expect(page.locator(".roles-table tbody tr")).toHaveCount(2);
  await page.getByLabel("Tìm trong danh mục").fill("");
  await page.locator(".role-edit").first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(
    page.getByRole("dialog").getByLabel("Tên", { exact: true }),
  ).toHaveValue("Quản trị hệ thống");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Đóng", exact: true })
    .click();
  await page.locator(".role-grants").first().click();
  await expect(page.locator("dialog tbody tr")).toHaveCount(67);
  await expect(page.getByRole("dialog")).toContainText(
    "Quyền Admin được bảo vệ",
  );
  assert(
    await page
      .locator('dialog input[type="checkbox"]')
      .evaluateAll((inputs) => inputs.every((input) => input.disabled)),
  );
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/ui-results.json`,
    JSON.stringify(
      {
        status: "PASS",
        roles: roles.length,
        observations,
        editDialog: true,
        protectedAdminGrants: true,
        consoleErrors: errors,
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS: 10 active Vietnamese roles; code column removed; aligned colored actions at 1440/768/375; search, edit dialog and protected Admin grants; no browser errors.",
  );
} finally {
  await browser.close();
}
