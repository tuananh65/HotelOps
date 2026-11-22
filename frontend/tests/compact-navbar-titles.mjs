import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";
import { permissionCopy } from "../src/compact/permission-copy.ts";

// UI-only fixtures: every API request is intercepted; no login or DB writes.
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/navbar-page-titles";
fs.mkdirSync(out, { recursive: true });
const hotel = { branchId: 1, name: "Khách sạn Bùi Viện", isActive: true };
const profile = {
  user: { fields: { userId: 1, fullName: "Nguyễn Hoàng Nam" } },
  branches: [hotel],
  roles: [{ code: "SYS_ADMIN", name: "Quản trị hệ thống", branchId: null }],
  memberships: [],
  requiresPasswordChange: false,
  grants: Object.keys(permissionCopy).map((code) => ({
    code,
    branchId: null,
    dataScope: "ALL",
  })),
};
const pageOf = (rows = []) => ({
  items: rows.map((fields) => ({ fields })),
  total: rows.length,
  page: 1,
  pageSize: 100,
});
const roles = Array.from({ length: 30 }, (_, i) => ({
  roleId: i + 1,
  code: `ROLE_${i}`,
  name: `Vai trò kiểm thử ${i + 1}`,
  description: "Mô tả vai trò kiểm thử giao diện",
  isActive: true,
}));
const errors = [],
  checks = [],
  writes = [];
let failAssets = false;
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
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
    const request = route.request(),
      path = new URL(request.url()).pathname.replace("/api/v2/", "");
    if (request.method() !== "GET") writes.push(path);
    if (failAssets && path === "catalog/assets") {
      await route.fulfill({
        status: 503,
        json: { message: "Không thể tải tài sản." },
      });
      return;
    }
    let json = pageOf();
    if (path === "auth/me") json = profile;
    else if (path === "catalog/branches") json = pageOf([hotel]);
    else if (path === "catalog/roles") json = pageOf(roles);
    else if (path === "insights/administration")
      json = {
        branches: [{ active: true, count: 1 }],
        users: [{ active: true, count: 3 }],
      };
    else if (path === "workday")
      json = {
        today: "2026-11-22",
        weekStart: "2026-11-16",
        canManage: true,
        shifts: [],
        assignments: [],
      };
    else if (
      ["users", "rooms", "insights/stock", "insights/time"].includes(path)
    )
      json = [];
    else if (path === "workspace/options")
      json = {
        branches: [hotel],
        references: [],
        departments: [],
        rooms: [],
        locations: [],
        users: [],
        warehouses: [],
        items: [],
        assets: [],
      };
    else if (path === "templates") json = { checklists: [], schedules: [] };
    else if (
      path === "work" &&
      new URL(request.url()).searchParams.get("kind") === "OPERATIONAL"
    )
      json = pageOf([{ workId: 1, title: "Kiểm tra phòng", status: "OPEN" }]);
    else if (path === "insights/personal")
      json = {
        today: "2026-11-23",
        timezone: "Asia/Ho_Chi_Minh",
        metrics: {
          assignedToday: 0,
          awaitingAcceptance: 0,
          inProgress: 0,
          completedToday: 0,
          pendingInspection: 0,
          rework: 0,
          overdue: 0,
          unreadNotifications: 0,
        },
        priority: [],
        shifts: null,
      };
    else if (path === "insights/dashboard")
      json = { rooms: [], work: [], stock: [] };
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (error) => {
    errors.push(error.message);
    console.error("Browser error:", error.stack);
  });
  const routes = [
    ["dashboard", "Tổng quan quản trị"],
    ["catalog", "Khách sạn"],
    ["catalog?hotel=1", hotel.name],
    ["catalog?hotel=1&resource=departments", "Cấu hình hệ thống"],
    ["settings", "Cấu hình hệ thống"],
    ["users", "Nhân sự & truy cập"],
    ["rooms", "Tình trạng phòng"],
    ["work?kind=OPERATIONAL", "Công việc & yêu cầu"],
    ["workday", "Lịch làm việc"],
    ["documents", "Vật tư & chứng từ"],
    ["assets", "Tài sản"],
    ["stock", "Tồn kho vật tư"],
    ["supplies", "Danh mục vật tư & tài sản"],
    ["templates", "Checklist & lịch định kỳ"],
    ["reports", "Báo cáo"],
    ["notifications", "Thông báo"],
  ];
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 900 });
    for (const [route, title] of routes) {
      console.log(`Checking ${width} ${route}`);
      await page.goto(`${base}/#${route}`);
      await expect(page.locator(".topbar h1")).toHaveText(title);
      await expect(page.locator("main h1")).toHaveCount(0);
      await expect(page.locator("h1")).toHaveCount(1);
      await expect(page.locator(".sidebar > .branch-context")).toHaveCount(1);
      await expect(page.locator("main .branch-context")).toHaveCount(0);
      await expect(page.locator(".sidebar .branch-current")).toContainText(
        hotel.name,
      );
      const brandBox = await page.locator(".sidebar .brand").boundingBox();
      const branchBox = await page
        .locator(".sidebar > .branch-context")
        .boundingBox();
      const navigationBox = await page.locator(".sidebar nav").boundingBox();
      assert(
        branchBox.y >= brandBox.y + brandBox.height - 1 &&
          branchBox.y + branchBox.height <= navigationBox.y + 1,
        "Branch must sit between logo and navigation",
      );
      await page.waitForTimeout(300);
      await expect(
        page.getByRole("button", { name: "Làm mới", exact: true }),
      ).toHaveCount(0);
      const topGap = await page
        .locator("main")
        .evaluate((el) => parseFloat(getComputedStyle(el).paddingTop));
      assert(
        topGap <= 12,
        `${route}: excessive top padding ${topGap} at ${width}`,
      );
      const emptyHeadings = await page
        .locator("main .page-heading")
        .evaluateAll(
          (nodes) =>
            nodes.filter(
              (el) =>
                !el.textContent.trim() && el.getBoundingClientRect().height > 0,
            ).length,
        );
      assert.equal(emptyHeadings, 0, `${route}: empty heading row at ${width}`);
      if (width >= 768) {
        assert(
          await page
            .locator(".sidebar nav button")
            .evaluateAll((nodes) =>
              nodes.every((el) => el.scrollWidth <= el.clientWidth + 1),
            ),
          `Sidebar text clipped at ${width}`,
        );
      }
      if (route === "workday") {
        const empty = await page
          .getByText("Chưa có ca làm việc cho đối tượng đã chọn.")
          .boundingBox();
        assert(
          empty && empty.x >= 0 && empty.x + empty.width <= width,
          `Empty calendar message outside viewport ${width}`,
        );
      }
      await page.screenshot({
        path: `${out}/page-${route.replace(/[^a-z0-9]/gi, "-")}-${width}.png`,
        animations: "disabled",
      });
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${route}: overflow at ${width}`,
      );
      const boxes = await page
        .locator(".topbar-heading, .topbar-account")
        .evaluateAll((nodes) =>
          nodes.map((n) => {
            const r = n.getBoundingClientRect();
            return {
              left: r.left,
              right: r.right,
              top: r.top,
              bottom: r.bottom,
            };
          }),
        );
      assert(
        boxes[0].right <= boxes[1].left + 1 ||
          boxes[0].top >= boxes[1].bottom - 1,
        `${route}: title overlaps account at ${width}`,
      );
      checks.push(`${width} ${route}`);
    }
    await page.goto(`${base}/#catalog`);
    await expect(page.locator(".topbar h1")).toHaveText("Khách sạn");
    await expect(page.locator(".topbar-heading p")).toHaveText(
      "Chọn khách sạn để xem và quản lý cấu trúc.",
    );
    await page
      .getByRole("button", { name: "+ Thêm khách sạn", exact: true })
      .click();
    await expect(page.locator("dialog")).toBeVisible();
    // Padding inside a dialog is not the backdrop and must not lose a draft.
    const modalBounds = await page.locator("dialog").boundingBox();
    await page.mouse.click(modalBounds.x + 8, modalBounds.y + 8);
    await expect(page.locator("dialog")).toBeVisible();
    assert.equal(
      await page.evaluate(() => getComputedStyle(document.body).overflow),
      "hidden",
    );
    await expect(page.locator(".topbar h1")).toHaveText("Khách sạn");
    await expect(page.locator("h1")).toHaveCount(1);
    await page.keyboard.press("Escape");
    await expect(
      page.getByRole("button", { name: "+ Thêm khách sạn", exact: true }),
    ).toBeFocused();
    assert.notEqual(
      await page.evaluate(() => getComputedStyle(document.body).overflow),
      "hidden",
    );
    await page.screenshot({
      path: `${out}/hotels-${width}.png`,
      animations: "disabled",
    });
    await page.goto(`${base}/#settings`);
    await expect(page.locator(".roles-table tbody tr")).toHaveCount(30);
    await page.evaluate(() =>
      scrollTo(0, document.documentElement.scrollHeight),
    );
    await page.waitForTimeout(200);
    const navbar = await page.locator(".topbar").boundingBox();
    assert(Math.abs(navbar.y) <= 1, `Navbar not sticky at ${width}`);
    await page.locator(".table-scroll").evaluate((el) => {
      el.scrollTop = 300;
    });
    await page.waitForTimeout(200);
    const th = await page.locator(".roles-table th").first().boundingBox();
    assert(
      th.y >= navbar.y + navbar.height - 2,
      `Table heading hidden by navbar at ${width}`,
    );
    await page.screenshot({
      path: `${out}/roles-scrolled-${width}.png`,
      animations: "disabled",
    });
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${base}/#work?kind=OPERATIONAL`);
  await expect(page.locator("tbody tr")).toHaveCount(1);
  const search = page.getByRole("textbox", { name: "Tìm trong trang" });
  await search.fill("không khớp");
  await expect(
    page.getByText("Không có công việc khớp tìm kiếm trong trang này."),
  ).toBeVisible();
  await page.getByRole("button", { name: "Xóa tìm kiếm" }).click();
  await expect(page.locator("tbody tr")).toHaveCount(1);
  const controlBoxes = await page
    .locator(".page-toolbar input, .page-toolbar .primary")
    .evaluateAll((nodes) =>
      nodes.map((el) => el.getBoundingClientRect().bottom),
    );
  assert.equal(controlBoxes.length, 2);
  assert(
    Math.abs(controlBoxes[0] - controlBoxes[1]) < 2,
    "Search and create action must align",
  );
  await page.locator(".skip-link").focus();
  await page.keyboard.press("Enter");
  await expect(page.locator("main")).toBeFocused();
  assert(
    page.url().endsWith("#work?kind=OPERATIONAL"),
    "Skip navigation must preserve route",
  );

  await page.goto(`${base}/#settings`);
  await expect(page.locator(".roles-table tbody tr")).toHaveCount(30);
  await page.evaluate(() => scrollTo(0, 500));
  await page.evaluate(() => {
    location.hash = "stock";
  });
  await expect(page.locator(".topbar h1")).toHaveText("Tồn kho vật tư");
  assert.equal(
    await page.evaluate(() => scrollY),
    0,
    "New route inherits old scroll position",
  );

  failAssets = true;
  await page.goto(`${base}/#assets`);
  await expect(page.getByRole("button", { name: "Thử lại" })).toBeVisible();
  failAssets = false;
  await page.getByRole("button", { name: "Thử lại" }).click();
  await expect(page.locator("main .alert")).toHaveCount(0);
  await expect(page.locator("main .table-scroll")).toBeVisible();
  // Same mounted shell: title portal cleanup on route transitions.
  for (const [route, title] of routes.slice(0, 6)) {
    await page.evaluate((route) => {
      location.hash = route;
    }, route);
    await expect(page.locator(".topbar h1")).toHaveText(title);
    await expect(page.locator("h1")).toHaveCount(1);
  }
  // Role-dependent dashboard/work titles still come from the current page.
  profile.grants = [{ code: "CLEANING_VIEW", branchId: 1, dataScope: "OWN" }];
  profile.roles = [
    { code: "HK_STAFF", name: "Nhân viên buồng phòng", branchId: 1 },
  ];
  await page.goto(`${base}/#dashboard`);
  await page.reload();
  await expect(page.locator(".topbar h1")).toHaveText("Tổng quan cá nhân");
  profile.grants.push({
    code: "DASHBOARD_VIEW",
    branchId: 1,
    dataScope: "BRANCH",
  });
  profile.roles = [
    { code: "OPERATION_MANAGER", name: "Quản lý vận hành", branchId: 1 },
  ];
  await page.reload();
  await expect(page.locator(".topbar h1")).toHaveText("Tổng quan vận hành");
  assert.deepEqual(errors, [], "Browser errors");
  assert.deepEqual(writes, [], "Unexpected API mutations");
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        checks,
        dynamicDashboardTitles: true,
        dialogTitlePreserved: true,
        stickyNavbarAndTableHeaders: true,
        browserErrors: errors,
        writes,
        fixturesOnly: true,
        sharedUx: [
          "compact top spacing",
          "no empty heading rows",
          "no refresh buttons",
          "dialog padding/draft protection",
          "modal scroll lock and focus restore",
          "search empty state and clear",
          "aligned search/actions",
          "skip link preserves route",
          "route resets scroll",
          "query retry",
        ],
      },
      null,
      2,
    ),
  );
  console.log(
    `PASS ${checks.length} route/viewport checks, dynamic titles, dialogs, sticky navbar/table headers, no API mutations`,
  );
} finally {
  await browser.close();
}
