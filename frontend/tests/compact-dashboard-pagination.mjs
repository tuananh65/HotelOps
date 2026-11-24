import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/dashboard-pagination";
fs.mkdirSync(out, { recursive: true });
const branches = [
  { branchId: 1, name: "Chi nhánh Bùi Viện", isActive: true },
  { branchId: 2, name: "Chi nhánh Cao Thắng", isActive: true },
];
const users = Array.from({ length: 56 }, (_, i) => ({
  userId: i + 1,
  fullName: i === 55 ? "Đặng Hoài Phương" : `Nhân viên ${i + 1}`,
  employeeCode: `EMP${i + 1}`,
  username: i === 55 ? "PhuongDH1" : `user${i + 1}`,
  accountStatus: "ACTIVE",
  employmentStatus: "ACTIVE",
  branches: [{ branchId: 1, branchName: branches[0].name }],
  employments: [],
  roles: [
    {
      roleId: i === 55 ? 9 : 10,
      name: i === 55 ? "Quản lý vận hành" : "Nhân viên",
      branchId: 1,
    },
  ],
}));
const profile = {
  user: { fields: { userId: 1, fullName: "Nguyễn Hoàng Nam" } },
  branches,
  roles: [{ code: "SYS_ADMIN", name: "Quản trị hệ thống", branchId: null }],
  memberships: [],
  requiresPasswordChange: false,
  grants: ["MASTER_DATA_MANAGE", "USER_VIEW", "USER_MANAGE", "ROLE_MANAGE"].map(
    (code) => ({ code, branchId: null, dataScope: "ALL" }),
  ),
};
const pageOf = (rows, total = rows.length) => ({
  items: rows.map((fields) => ({ fields })),
  total,
  page: 1,
  pageSize: 100,
});
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  writes = [],
  charts = [];
const networkErrors = [],
  navigations = [];
let page;
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname.replace("/api/v2/", "");
    if (req.method() !== "GET") writes.push(path);
    let json = pageOf([]);
    if (path === "auth/me") json = profile;
    else if (path === "users") json = users.map((fields) => ({ fields }));
    else if (/^users\/\d+$/.test(path))
      json = {
        user: {
          fields: users.find((u) => u.userId === Number(path.split("/")[1])),
        },
        memberships: [],
        roles: [],
        emailConfigured: false,
      };
    else if (path === "catalog/branches") json = pageOf(branches);
    else if (path === "catalog/rooms")
      json = pageOf([], url.searchParams.get("branchId") === "1" ? 12 : 4);
    else if (path === "insights/administration")
      json = {
        branches: [{ active: true, count: 2 }],
        users: [{ status: "ACTIVE", count: 56 }],
      };
    else if (path === "insights/personal")
      json = {
        today: "2026-11-23",
        timezone: "Asia/Ho_Chi_Minh",
        metrics: {
          assignedToday: 2,
          awaitingAcceptance: 1,
          inProgress: 1,
          completedToday: 0,
          pendingInspection: 0,
          rework: 0,
          overdue: 0,
          unreadNotifications: 0,
        },
        priority: [],
        shifts: null,
      };
    else if (path === "insights/owner")
      json = {
        updatedAt: new Date().toISOString(),
        metrics: {
          branches: 2,
          rooms: 16,
          ready: 12,
          occupied: 4,
          created: 8,
          completed: 3,
        },
        branches: branches.map((b, i) => ({
          ...b,
          code: i ? "DVCT" : "DVBV",
          timezone: "Asia/Ho_Chi_Minh",
          metrics: {
            rooms: i ? 4 : 12,
            occupied: i ? 4 : 0,
            created: i ? 3 : 5,
            completed: i ? 1 : 2,
          },
        })),
        timeline: [
          { day: "2026-11-23", branchId: 1, created: 5, completed: 2 },
          { day: "2026-11-23", branchId: 2, created: 3, completed: 1 },
        ],
        attention: [],
        critical: [],
        assetStates: [{ condition: "GOOD", count: 3 }],
      };
    else if (path === "insights/dashboard")
      json = {
        taskWindow: {
          period: url.searchParams.get("taskPeriod") || "day",
          timezone: "Asia/Ho_Chi_Minh",
          fromDate: "2026-11-23",
          toDate: "2026-11-23",
        },
        rooms: [
          { readiness: "READY", isOccupied: false, count: 12 },
          { readiness: "OCCUPIED", isOccupied: true, count: 4 },
        ],
        work: [
          { status: "ASSIGNED", count: 5 },
          { status: "COMPLETED", count: 3 },
        ],
        stock: [{ baseQty: 12 }, { baseQty: 0 }],
      };
    await route.fulfill({ status: 200, json });
  });
  page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("requestfailed", (request) =>
    networkErrors.push({
      url: request.url(),
      error: request.failure()?.errorText,
    }),
  );
  page.on("framenavigated", (frame) => {
    if (frame === page.mainFrame()) navigations.push(frame.url());
  });
  await page.goto(`${base}/#users`);
  await expect(page.locator(".topbar h1")).toBeVisible({ timeout: 30000 });
  const rows = page.locator("tbody tr:has(td:nth-child(2))"),
    pager = page.getByRole("navigation", { name: "Phân trang tài khoản" });
  const next = pager.getByRole("button", { name: "Tiếp", exact: true }),
    prev = pager.getByRole("button", { name: "Trước", exact: true });
  await expect(rows).toHaveCount(10);
  await expect(prev).toBeDisabled();
  await expect(page.locator(".account-page-range")).toHaveText(
    "1–10 / 56 tài khoản · 6 trang",
  );
  for (let i = 0; i < 5; i++) {
    await next.click();
    await expect(pager).toContainText(`Trang ${i + 2} / 6`);
  }
  await expect(rows).toHaveCount(6);
  await expect(next).toBeDisabled();
  await expect(page.locator("tbody")).toContainText("PhuongDH1");
  await rows
    .filter({ hasText: "PhuongDH1" })
    .getByRole("button", { name: "Quản lý" })
    .click();
  await expect(page.getByRole("dialog")).toContainText("Đặng Hoài Phương");
  await page.keyboard.press("Escape");
  await page.getByLabel("Tìm tài khoản").fill("user1");
  await expect(rows).toHaveCount(10); // user1 + user10..19: 11 matches.
  await expect(prev).toBeDisabled();
  await expect(page.locator(".account-page-range")).toHaveText(
    "1–10 / 11 tài khoản · 2 trang",
  );
  await next.click();
  await expect(rows).toHaveCount(1);
  await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
  await expect(rows).toHaveCount(10);
  await page.getByLabel("Số dòng mỗi trang").selectOption("20");
  await expect(rows).toHaveCount(20);
  await next.click();
  await next.click();
  await expect(rows).toHaveCount(16);
  await page.getByLabel("Số dòng mỗi trang").selectOption("50");
  await expect(rows).toHaveCount(50);
  await expect(prev).toBeDisabled();
  await page.getByRole("button", { name: /^Bộ lọc/ }).click();
  await page
    .getByRole("combobox", { name: "Chi nhánh", exact: true })
    .selectOption("1");
  await page
    .getByRole("combobox", { name: "Vai trò", exact: true })
    .selectOption("9");
  await expect(rows).toHaveCount(1);
  await expect(page.locator("tbody")).toContainText("PhuongDH1");
  await page.getByLabel("Tìm tài khoản").fill("no-match");
  await expect(rows).toHaveCount(0);
  await expect(prev).toBeDisabled();
  await expect(next).toBeDisabled();
  await expect(page.locator(".account-page-range")).toHaveText(
    "0–0 / 0 tài khoản · 1 trang",
  );
  await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
  await page.getByLabel("Số dòng mỗi trang").selectOption("10");
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.waitForTimeout(400);
    const overflow = await page.evaluate(() => ({
      width: innerWidth,
      scroll: document.documentElement.scrollWidth,
      elements: [...document.querySelectorAll("body *")]
        .map((el) => ({
          tag: el.tagName,
          class: el.className,
          right: el.getBoundingClientRect().right,
          width: el.getBoundingClientRect().width,
        }))
        .filter((el) => el.right > innerWidth + 1)
        .slice(0, 20),
    }));
    if (overflow.scroll > width + 1)
      fs.writeFileSync(
        `${out}/overflow-${width}.json`,
        JSON.stringify(overflow, null, 2),
      );
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await pager.scrollIntoViewIfNeeded();
    await page.screenshot({
      path: `${out}/users-${width}.png`,
      animations: "disabled",
      fullPage: true,
    });
  }
  for (const [width, height] of [
    [1440, 820],
    [768, 820],
    [375, 812],
    [812, 375],
  ]) {
    await page.setViewportSize({ width, height });
    const toggle = page.getByRole("button", { name: /^Bộ lọc/ });
    for (const expanded of [true, false]) {
      if ((await toggle.getAttribute("aria-expanded")) !== String(expanded))
        await toggle.click();
      await page.evaluate(() => {
        document.querySelector(".account-filter-panel").scrollTop = 0;
        window.scrollTo(0, 280);
        document.querySelector(".table-scroll").scrollTop = 160;
      });
      await page.waitForTimeout(300);
      const bounds = await page.evaluate(() => {
        const nav = document.querySelector(".topbar").getBoundingClientRect();
        const panel = document
          .querySelector(".account-filter-panel")
          .getBoundingClientRect();
        const heading = document
          .querySelector("thead th")
          .getBoundingClientRect();
        return {
          top: panel.top,
          bottom: panel.bottom,
          navBottom: nav.bottom,
          headingTop: heading.top,
          scrollY,
        };
      });
      assert(bounds.scrollY > 0, "Page must actually scroll");
      assert(
        Math.abs(bounds.top - bounds.navBottom) < 2,
        `${width}: filters left navbar ${JSON.stringify(bounds)}`,
      );
      assert(
        bounds.headingTop >= bounds.bottom - 2,
        `${width}: table header hidden by filters`,
      );
      await expect(page.getByLabel("Tìm tài khoản")).toBeInViewport();
      await expect(
        page.getByRole("button", { name: "+ Tạo tài khoản", exact: true }),
      ).toBeInViewport();
      if (expanded) {
        const roleFilter = page
          .locator("#account-filter-details select")
          .last();
        await roleFilter.scrollIntoViewIfNeeded();
        await expect(roleFilter).toBeInViewport();
      }
      await page.screenshot({
        path: `${out}/sticky-filters-${width}-${expanded}.png`,
      });
    }
  }
  for (const variant of ["admin", "operations", "owner", "personal"]) {
    if (variant !== "admin") {
      profile.roles = [
        {
          code:
            variant === "owner"
              ? "OWNER"
              : variant === "operations"
                ? "OPERATION_MANAGER"
                : "HK_STAFF",
          name: variant,
          branchId: 1,
        },
      ];
      profile.grants = (
        variant === "personal" ? ["CLEANING_VIEW"] : ["DASHBOARD_VIEW"]
      ).map((code) => ({
        code,
        branchId: 1,
        dataScope: variant === "personal" ? "OWN" : "BRANCH",
      }));
    }
    if (variant === "owner") {
      profile.roles = [{ code: "OWNER", name: "Owner", branchId: null }];
      profile.grants = [
        "DASHBOARD_VIEW",
        "ROOM_VIEW",
        "CLEANING_VIEW",
        "INSPECTION_VIEW",
        "MAINTENANCE_VIEW",
        "INCIDENT_VIEW",
        "TASK_VIEW",
        "ASSET_VIEW",
        "INVENTORY_VIEW",
        "REPORT_VIEW",
        "MR_VIEW",
        "IR_VIEW",
      ].map((code) => ({ code, branchId: null, dataScope: "ALL" }));
    }
    await page.goto(`${base}/`);
    await page.reload();
    await page.evaluate(() => {
      location.hash = "dashboard";
    });
    for (const width of [1440, 768, 375]) {
      await page.setViewportSize({ width, height: 1000 });
      if (variant === "personal")
        await expect(page.locator(".metric-icon svg")).toHaveCount(9);
      else {
        await expect(page.locator(".stat-chart")).toHaveCount(
          variant === "admin" ? 2 : variant === "owner" ? 6 : 4,
        );
        await expect(page.locator(".chart-heading h2 svg")).toHaveCount(
          variant === "admin" ? 2 : variant === "owner" ? 6 : 4,
        );
        await expect(page.locator(".chart-body.donut svg")).toBeVisible();
        const size = await page.locator(".chart-body.donut svg").boundingBox();
        assert(size.width >= 210, `${variant}/${width}: donut too small`);
        const bars = await page
          .locator(".chart-track")
          .evaluateAll((nodes) =>
            nodes.map((n) => n.getBoundingClientRect().height),
          );
        assert(bars.length && bars.every((h) => h === 12));
        if (variant === "admin")
          await expect(page.locator(".metric-icon svg")).toHaveCount(3);
        if (variant === "owner")
          await expect(page.locator(".stat-chart a")).toHaveCount(0);
        charts.push({ variant, width, donutWidth: size.width, barHeight: 12 });
      }
      await page.waitForTimeout(400);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${variant}/${width}: overflow`,
      );
      await page.screenshot({
        path: `${out}/${variant}-${width}.png`,
        animations: "disabled",
        fullPage: true,
      });
      if (variant === "admin" && width === 1440) {
        const html = (await page.content())
          .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "")
          .replace("<head>", `<head><base href="${base}/">`);
        fs.writeFileSync(`${out}/admin-preview.html`, html);
      }
    }
  }
  assert.deepEqual(errors, []);
  assert.deepEqual(writes, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        charts,
        pagination:
          "56 users; 10/20/50 rows; boundaries/search/filter/reset/management",
        dashboardVariants: 4,
        browserErrors: errors,
        writes,
        fixturesOnly: true,
        stickyAccountFilters:
          "page/table scroll, expanded/collapsed, desktop/tablet/mobile/landscape",
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS account pagination/filter regression, icons, larger charts and bars across admin/operations/owner/personal dashboards at 1440/768/375 (mocked APIs)",
  );
} catch (error) {
  if (page) {
    await page.screenshot({ path: `${out}/failure.png`, fullPage: true });
    fs.writeFileSync(
      `${out}/failure.json`,
      JSON.stringify(
        { message: String(error), errors, networkErrors, navigations },
        null,
        2,
      ),
    );
  }
  throw error;
} finally {
  await browser.close();
}
