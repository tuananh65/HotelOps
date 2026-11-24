import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/structure-settings";
fs.mkdirSync(out, { recursive: true });
const branches = [
  { branchId: 1, name: "Khách sạn Bùi Viện", isActive: true },
  { branchId: 2, name: "Khách sạn Cao Thắng", isActive: true },
];
const profile = {
  user: { fields: { userId: 1, fullName: "Quản trị viên" } },
  branches,
  roles: [{ code: "SYS_ADMIN", name: "Quản trị hệ thống", branchId: null }],
  memberships: [],
  requiresPasswordChange: false,
  grants: ["MASTER_DATA_MANAGE", "ROLE_MANAGE", "USER_VIEW"].map((code) => ({
    code,
    dataScope: "ALL",
    branchId: null,
  })),
};
const reads = [],
  writes = [],
  errors = [];
const pageOf = (rows) => ({
  items: rows.map((fields) => ({ fields })),
  total: rows.length,
  page: 1,
  pageSize: 100,
});
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname.replace("/api/v2/", ""),
      branch = Number(url.searchParams.get("branchId"));
    if (req.method() !== "GET") {
      writes.push({
        path,
        branch,
        method: req.method(),
        body: req.postDataJSON(),
      });
      return route.fulfill({ status: 200, json: {} });
    }
    reads.push({ path, branch });
    let json = pageOf([]);
    if (path === "auth/me") json = profile;
    else if (path === "catalog/branches") json = pageOf(branches);
    else if (path === "catalog/departments")
      json = pageOf([
        {
          departmentId: branch * 10,
          code: `HK${branch}`,
          name: `Buồng phòng ${branch}`,
          isActive: true,
          rowVersion: "version",
        },
      ]);
    else if (path === "catalog/room-types")
      json = pageOf([
        {
          roomTypeId: branch * 10,
          code: `STD${branch}`,
          name: `Phòng tiêu chuẩn ${branch}`,
          capacity: 2,
          description: "Hai khách",
          isActive: true,
          rowVersion: "version",
        },
      ]);
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(`${base}/#catalog?hotel=1`);
  await expect(page.locator(".topbar h1")).toHaveText(branches[0].name);
  await expect(
    page.getByRole("button", { name: "Bộ phận", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Loại phòng", exact: true }),
  ).toHaveCount(0);
  await page.goto(`${base}/#settings`);
  await expect(page.getByLabel("Nội dung cấu hình")).toHaveValue("roles");
  await expect(
    page.getByLabel("Nội dung cấu hình").locator("option"),
  ).toHaveText([
    "Danh mục dùng chung",
    "Bộ phận",
    "Loại phòng",
    "Vai trò",
    "Quyền hệ thống",
  ]);
  for (const [resource, title, label] of [
    ["departments", "Bộ phận", "Buồng phòng"],
    ["room-types", "Loại phòng", "Phòng tiêu chuẩn"],
  ]) {
    await page.getByLabel("Nội dung cấu hình").selectOption(resource);
    await expect(page.locator(".topbar h1")).toHaveText("Cấu hình hệ thống");
    await expect(page.locator(".branch-context")).toHaveCount(0);
    await expect(page.locator("tbody")).toContainText(`${label} 1`);
    await page.goto(`${base}/#catalog?hotel=2&resource=${resource}`);
    await expect(page.locator("tbody")).toContainText(`${label} 2`);
    await expect(page.locator("tbody")).not.toContainText(`${label} 1`);
    await page.getByRole("button", { name: "Chỉnh sửa", exact: true }).click();
    await expect(page.locator("dialog input[name=name]")).toHaveValue(
      `${label} 2`,
    );
    await page.locator("dialog input[name=name]").fill(`${label} đã sửa`);
    await page.locator("dialog button.primary").click();
    await expect(page.locator("dialog")).toHaveCount(0);
    assert.equal(writes.at(-1).path, `catalog/${resource}/20`);
    assert.equal(writes.at(-1).branch, 2);
    assert.equal(writes.at(-1).method, "PUT");
    assert.equal(writes.at(-1).body.rowVersion, "version");
    await page
      .getByRole("button", {
        name: `+ Thêm ${title.toLocaleLowerCase()}`,
        exact: true,
      })
      .click();
    await expect(page.locator("dialog input[name=code]")).toHaveCount(0);
    await page.locator("dialog input[name=name]").fill("Danh mục kiểm thử");
    await page.locator("dialog button.primary").click();
    await expect(page.locator("dialog")).toHaveCount(0);
    assert.equal(writes.at(-1).path, `catalog/${resource}`);
    assert.equal(writes.at(-1).branch, 2);
    assert.equal(writes.at(-1).method, "POST");
    assert.equal(Object.hasOwn(writes.at(-1).body.fields, "code"), false);
    // Reload auth to start the next check from hotel 1.
    await page.reload();
    await expect(page.locator(".branch-context")).toHaveCount(0);
  }
  for (const resource of ["departments", "room-types"]) {
    await page.goto(`${base}/#catalog?hotel=2&resource=${resource}`);
    await expect(page).toHaveURL(
      new RegExp(`#settings\\?resource=${resource}$`),
    );
    await expect(page.locator(".branch-context")).toHaveCount(0);
    await expect(page.getByLabel("Nội dung cấu hình")).toHaveValue(resource);
  }
  for (const width of [1440, 375]) {
    await page.setViewportSize({ width, height: 900 });
    await page.waitForTimeout(400);
    if (width === 1440) {
      await page
        .getByRole("button", { name: "Thu gọn điều hướng", exact: true })
        .click();
    } else {
      await page
        .getByRole("button", { name: "Mở điều hướng", exact: true })
        .click();
    }
    await expect(page.getByRole("button", {name:"Đổi khách sạn",exact:true})).toHaveCount(0);
    if (width === 1440) {
      await page
        .getByRole("button", { name: "Mở rộng điều hướng", exact: true })
        .click();
    }
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await page.screenshot({
      path: `${out}/settings-${width}.png`,
      animations: "disabled",
    });
  }
  profile.branches = [];
  await page.reload();
  await expect(
    page.getByText(
      "Chưa có khách sạn trong phạm vi được cấp để cấu hình danh mục này.",
    ),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "+ Thêm loại phòng", exact: true }),
  ).toHaveCount(0);
  assert(
    !reads.some(
      (r) =>
        ["catalog/departments", "catalog/room-types"].includes(r.path) &&
        r.branch === 0,
    ),
  );
  assert.equal(writes.length, 4);
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        mockedWrites: writes.map(({ path, branch, method }) => ({
          path,
          branch,
          method,
        })),
        browserErrors: errors,
        fixturesOnly: true,
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS settings navigation, two hotel scopes, create/edit request routing, legacy redirects, desktop/mobile and missing-branch guard (all API calls mocked)",
  );
} finally {
  await browser.close();
}
