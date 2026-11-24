import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

// Exercise the real application shell. All API traffic, including writes, stays in fixtures.
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/ux-role-integration";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const branches = [
  {
    branchId: 1,
    name: "Khách sạn kiểm thử",
    isActive: true,
    address: "10 Đường kiểm thử",
    phone: "0901234567",
    representativeName: "Nguyễn Đại Diện",
    rowVersion: "branch-v1",
  },
];
const locations = [
  {
    locationId: 10,
    name: "Tầng một",
    parentLocationId: null,
    kind: "FLOOR",
    isActive: true,
  },
  {
    locationId: 11,
    name: "Khu phía đông",
    parentLocationId: 10,
    kind: "AREA",
    isActive: true,
  },
];
const rooms = [
  [1, "101", "READY", false, 10],
  [2, "102", "PENDING_CLEANING", false, 11],
  [3, "103", "PENDING_INSPECTION", false, 11],
  [4, "104", "REWORK_REQUIRED", false, 10],
  [5, "105", "CLEANING_IN_PROGRESS", false, 10],
  [6, "106", "BLOCKED", false, 10],
  [7, "107", "PENDING_INSPECTION", true, 11],
].map(([roomId, code, readiness, isOccupied, locationId]) => ({
  roomId,
  code,
  readiness,
  isOccupied,
  locationId,
  branchId: 1,
  roomTypeId: 1,
  isActive: true,
  rowVersion: "room-v1",
  isBlocked: readiness === "BLOCKED",
}));
const permissions = [
  {
    permissionId: 1,
    code: "ROOM_VIEW",
    description: "Xem phòng",
    isActive: true,
    rowVersion: "permission-v1",
  },
  {
    permissionId: 2,
    code: "ASSET_VIEW",
    description: "Xem tài sản",
    isActive: false,
    rowVersion: "permission-v2",
  },
];
const personal = {
  userId: 1,
  username: "fixture.user",
  employeeCode: "NV000001",
  fullName: "Người kiểm thử",
  email: "fixture@example.test",
  phone: "0901234567",
  rowVersion: "profile-v1",
};
const profile = {
  user: { fields: personal },
  branches,
  roles: [{ code: "SYS_ADMIN", branchId: null, name: "Quản trị hệ thống" }],
  memberships: [],
  requiresPasswordChange: false,
  grants: ["MASTER_DATA_MANAGE", "ROLE_MANAGE", "USER_VIEW"].map((code) => ({
    code,
    branchId: null,
    dataScope: "ALL",
  })),
};
const pageOf = (rows) => ({
  items: rows.map((fields) => ({ fields })),
  total: rows.length,
  page: 1,
  pageSize: 100,
});
const writes = [],
  unexpected = [],
  errors = [],
  checks = [];
let rejectPermission = false;
const context = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
});
await context.addInitScript(() =>
  sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
);
await context.route("**/api/**", async (route) => {
  const req = route.request(),
    url = new URL(req.url());
  const path = url.pathname.replace("/api/v2/", "");
  if (req.method() !== "GET") {
    const body = req.postDataJSON();
    writes.push({ path, method: req.method(), body });
    if (path === "catalog/branches/1" && req.method() === "PUT") {
      Object.assign(branches[0], body.fields);
      return route.fulfill({ json: {} });
    }
    if (/^catalog\/permissions\/[12]$/.test(path) && req.method() === "PUT") {
      if (rejectPermission)
        return route.fulfill({
          status: 409,
          json: { title: "PERMISSION_USED_BY_ACTIVE_USER" },
        });
      Object.assign(
        permissions.find((p) => p.permissionId === Number(path.split("/")[2])),
        body.fields,
      );
      return route.fulfill({ json: {} });
    }
  } else {
    const fixtures = {
      "auth/me": profile,
      "auth/profile": personal,
      "catalog/branches": pageOf(branches),
      "catalog/locations": pageOf(locations),
      "catalog/rooms": pageOf(rooms),
      "catalog/room-types": pageOf([
        { roomTypeId: 1, name: "Tiêu chuẩn", capacity: 2, isActive: true },
      ]),
      "catalog/permissions": pageOf(permissions),
      rooms: rooms,
      "catalog/roles": pageOf([]),
      "catalog/departments": pageOf([]),
      "catalog/references": pageOf([]),
    };
    if (Object.hasOwn(fixtures, path))
      return route.fulfill({ json: fixtures[path] });
    if (/^rooms\/\d+\/history$/.test(path))
      return route.fulfill({ json: { events: [], blocks: [] } });
  }
  unexpected.push(`${req.method()} ${url.pathname}${url.search}`);
  return route.fulfill({
    status: 500,
    json: { title: "UNEXPECTED_FIXTURE_REQUEST" },
  });
});
const page = await context.newPage();
page.on("pageerror", (error) => errors.push(error.message));
async function check(name, run) {
  try {
    await run();
    checks.push({ name, passed: true });
    console.log(`PASS ${name}`);
  } catch (error) {
    checks.push({ name, passed: false, error: String(error) });
    console.error(`FAIL ${name}: ${error}`);
    await page.screenshot({
      path: `${out}/failure-${checks.length}.png`,
      fullPage: true,
    });
    await page.keyboard.press("Escape");
  }
}
try {
  await check("Nested hotel tree and branch contact form", async () => {
    await page.goto(`${base}/#catalog?hotel=1`);
    await expect(page.locator(".branch-information")).toContainText(
      "Nguyễn Đại Diện",
    );
    const tree = page.getByRole("list", { name: "Sơ đồ tầng và phòng" });
    const floor = tree.getByRole("button", { name: /^Tầng một/ });
    await expect(floor).toHaveAttribute("aria-expanded", "false");
    await floor.click();
    const area = tree.getByRole("button", { name: /^Khu phía đông/ });
    await expect(area).toBeVisible();
    await area.click();
    await tree.getByRole("button", { name: "Phòng 102", exact: true }).click();
    await expect(page).toHaveURL(/resource=rooms&location=11$/);
    await expect(page.locator("tbody tr")).toHaveCount(3);
    await expect(page.locator("tbody tr td:nth-child(2)")).toHaveText([
      "102",
      "103",
      "107",
    ]);
    await expect(page.getByRole("status")).toHaveText(
      "Hiển thị 1–3 / 3 bản ghi · Trang 1 / 1",
    );
    await page.goto(`${base}/#catalog?hotel=1`);
    const opener = page.getByRole("button", {
      name: "Chỉnh sửa khách sạn",
      exact: true,
    });
    await opener.click();
    const dialog = page.getByRole("dialog", { name: "Chỉnh sửa khách sạn" });
    await expect(dialog.getByLabel("Người đại diện")).toHaveValue(
      "Nguyễn Đại Diện",
    );
    await dialog.getByLabel("Người đại diện").fill("Trần Đại Diện");
    await dialog.getByLabel("Địa chỉ").fill("20 Đường kiểm thử");
    await dialog.getByLabel("Số điện thoại").fill("0987654321");
    await dialog.getByRole("button", { name: "Lưu thay đổi" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page.locator(".branch-information")).toContainText(
      "Trần Đại Diện",
    );
    assert.deepEqual(writes.at(-1), {
      path: "catalog/branches/1",
      method: "PUT",
      body: {
        fields: {
          name: branches[0].name,
          address: "20 Đường kiểm thử",
          phone: "0987654321",
          representativeName: "Trần Đại Diện",
          isActive: true,
        },
        rowVersion: "branch-v1",
      },
    });
  });
  await check(
    "Permission activation payload and server refusal remain visible",
    async () => {
      await page.goto(`${base}/#settings?resource=permissions`);
      await expect(page.getByRole("button", { name: /^\+ Thêm/ })).toHaveCount(
        0,
      );
      const rows = page.locator("tbody tr");
      await expect(rows).toHaveCount(2);
      await rows.nth(1).getByRole("button", { name: "Chỉnh sửa" }).click();
      let dialog = page.getByRole("dialog");
      await expect(
        dialog.locator("input:not([type=checkbox]),select,textarea"),
      ).toHaveCount(0);
      await dialog.getByLabel("Đang hoạt động").check();
      await dialog.getByRole("button", { name: "Lưu thay đổi" }).click();
      await expect(dialog).toHaveCount(0);
      assert.deepEqual(writes.at(-1), {
        path: "catalog/permissions/2",
        method: "PUT",
        body: { fields: { isActive: true }, rowVersion: "permission-v2" },
      });
      rejectPermission = true;
      await rows.first().getByRole("button", { name: "Chỉnh sửa" }).click();
      dialog = page.getByRole("dialog");
      await dialog.getByLabel("Đang hoạt động").uncheck();
      await dialog.getByRole("button", { name: "Lưu thay đổi" }).click();
      await expect(dialog.getByRole("alert")).toHaveText(
        "Không thể ẩn quyền đang được cấp cho tài khoản hoạt động.",
      );
      await expect(dialog).toBeVisible();
      assert.deepEqual(writes.at(-1).body, {
        fields: { isActive: false },
        rowVersion: "permission-v1",
      });
      assert.equal(permissions[0].isActive, true);
      await page.keyboard.press("Escape");
      rejectPermission = false;
    },
  );
  await check(
    "Branch contact rejects an invalid phone before submission",
    async () => {
      await page.goto(`${base}/#catalog?hotel=1`);
      await page
        .getByRole("button", { name: "Chỉnh sửa khách sạn", exact: true })
        .click();
      const dialog = page.getByRole("dialog", { name: "Chỉnh sửa khách sạn" });
      const phone = dialog.getByLabel("Số điện thoại");
      await phone.fill("not-a-phone");
      const validity = await phone.evaluate((input) => ({
        valid: input.checkValidity(),
        patternMismatch: input.validity.patternMismatch,
        pattern: input.pattern,
      }));
      assert.equal(
        validity.valid,
        false,
        `Invalid phone accepted: ${JSON.stringify(validity)}`,
      );
      const before = writes.length;
      await dialog.getByRole("button", { name: "Lưu thay đổi" }).click();
      await expect(dialog).toBeVisible();
      assert.equal(writes.length, before);
      await page.keyboard.press("Escape");
    },
  );
  await check(
    "Profile button opens from shell and restores keyboard focus",
    async () => {
      profile.roles = [{ code: "RECEPTION", name: "Lễ tân", branchId: 1 }];
      profile.grants = [
        "ROOM_VIEW",
        "ROOM_CHECKIN",
        "ROOM_CHECKOUT",
        "ROOM_OCCUPANCY_MOVE",
        "INCIDENT_CREATE",
      ].map((code) => ({ code, branchId: 1, dataScope: "BRANCH" }));
      await page.goto(`${base}/#rooms`);
      await page.reload();
      await expect(page.locator(".room-tile")).toHaveCount(7);
      for (const width of [1440, 768, 375]) {
        await page.setViewportSize({ width, height: 900 });
        const opener = page.getByRole("button", {
          name: "Thông tin cá nhân",
          exact: true,
        });
        await opener.focus();
        await page.keyboard.press("Enter");
        const dialog = page.getByRole("dialog", { name: "Thông tin cá nhân" });
        await expect(dialog.getByLabel("Họ tên")).toHaveValue(
          personal.fullName,
        );
        await expect(dialog.getByLabel("Email")).toHaveValue(personal.email);
        await expect(dialog.getByLabel("Số điện thoại")).toHaveValue(
          personal.phone,
        );
        const field = dialog.getByLabel("Họ tên");
        await field.focus();
        const styles = await field.evaluate((el) => {
          const s = getComputedStyle(el);
          return {
            height: el.getBoundingClientRect().height,
            outline: s.outlineWidth,
            shadow: s.boxShadow,
            border: s.borderColor,
          };
        });
        assert(
          styles.height >= 29 && styles.height <= 33,
          JSON.stringify(styles),
        );
        assert.equal(styles.outline, "2px");
        assert.notEqual(styles.shadow, "none");
        assert(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        );
        await page.screenshot({
          path: `${out}/profile-focus-${width}.png`,
          animations: "disabled",
        });
        await page.keyboard.press("Escape");
        await expect(dialog).toHaveCount(0);
        await expect(opener).toBeFocused();
      }
    },
  );
  await check(
    "Reception groups pending/rework rooms and cannot create incidents",
    async () => {
      await page.setViewportSize({ width: 1440, height: 1000 });
      await page.goto(`${base}/#rooms`);
      await page.reload();
      const tiles = page.locator(".room-tile");
      await expect(tiles).toHaveCount(7);
      await page.getByRole("button", { name: /^Cần dọn dẹp 3$/ }).click();
      await expect(tiles).toHaveCount(3);
      await expect(tiles.locator(":scope > strong")).toHaveText([
        "102",
        "103",
        "104",
      ]);
      await expect(tiles.locator(".badge")).toHaveText([
        "Cần dọn dẹp",
        "Cần dọn dẹp",
        "Cần dọn dẹp",
      ]);
      await page.getByRole("button", { name: /^Đang dọn dẹp 1$/ }).click();
      await expect(tiles).toHaveCount(1);
      await expect(tiles).toContainText("105");
      await page.getByRole("button", { name: /^Có khách 1$/ }).click();
      await expect(tiles).toHaveCount(1);
      await expect(tiles).toContainText("107");
      await tiles.click();
      const dialog = page.getByRole("dialog", { name: "Phòng 107" });
      await expect(dialog).toBeVisible();
      await expect(dialog.getByRole("button", { name: /sự cố/i })).toHaveCount(
        0,
      );
      await expect(
        dialog.getByRole("button", { name: "Trả phòng", exact: true }),
      ).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(
        page.getByRole("navigation", { name: "Điều hướng chính" }),
      ).not.toContainText("Sự cố");
      await page.getByRole("button", { name: /^Tất cả 7$/ }).click();
      for (const width of [1440, 768, 375]) {
        await page.setViewportSize({ width, height: 900 });
        await expect(tiles).toHaveCount(7);
        assert(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        );
        await page.screenshot({
          path: `${out}/reception-groups-${width}.png`,
          animations: "disabled",
        });
      }
      assert(
        !writes.some(
          (w) => w.path.startsWith("work") || w.path.startsWith("rooms"),
        ),
      );
    },
  );
} finally {
  const passed =
    checks.every((c) => c.passed) &&
    errors.length === 0 &&
    unexpected.length === 0;
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      { passed, checks, errors, unexpected, writes, fixturesOnly: true },
      null,
      2,
    ),
  );
  await browser.close();
  if (!passed) process.exitCode = 1;
}
