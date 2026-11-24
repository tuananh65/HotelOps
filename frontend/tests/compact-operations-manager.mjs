import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const permissions =
  "ASSET_MANAGE ASSET_RETIRE ASSET_TRANSFER ASSET_VIEW AUDIT_VIEW CLEANING_VIEW DASHBOARD_VIEW INCIDENT_CREATE INCIDENT_ESCALATION_HANDLE INCIDENT_MANAGE INCIDENT_TRIAGE INCIDENT_VIEW INSPECTION_VIEW INVENTORY_VIEW IR_CREATE IR_OVERDUE_HANDLE IR_PROCESS IR_VIEW MAINTENANCE_VIEW MR_VIEW REPORT_VIEW ROOM_BLOCK_MANAGE ROOM_VIEW TASK_VIEW".split(
    " ",
  );
const reads = [],
  errors = [];
let reduced = false;
const out = "../artifacts/operations-manager";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const paged = (rows) => ({
  items: rows.map((fields) => ({ fields })),
  total: rows.length,
  page: 1,
  pageSize: 100,
});
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    assert.equal(
      route.request().method(),
      "GET",
      "UI fixtures must never write to the real database",
    );
    const u = new URL(route.request().url()),
      path = u.pathname.replace("/api/v2/", "");
    reads.push({ path, params: u.search });
    const branchId = Number(u.searchParams.get("branchId") || 1);
    let json = {};
    if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Quản lý vận hành QA" } },
        branches: [
          { branchId: 1, name: "Chi nhánh Bùi Viện" },
          { branchId: 2, name: "Chi nhánh Cao Thắng" },
        ],
        roles: [1, 2].map((branchId) => ({
          code: "OPERATION_MANAGER",
          name: "Quản lý vận hành",
          branchId,
        })),
        grants: (reduced ? ["ROOM_VIEW"] : permissions).flatMap((code) =>
          [1, 2].map((branchId) => ({ code, branchId, dataScope: "BRANCH" })),
        ),
        memberships: [],
        requiresPasswordChange: false,
      };
    else if (path === "insights/dashboard")
      json = {
        taskWindow: {
          period: u.searchParams.get("taskPeriod") || "day",
          timezone: "Asia/Ho_Chi_Minh",
          fromDate: "2026-11-23",
          toDate: "2026-11-23",
        },
        rooms: [{ readiness: "READY", isOccupied: false, count: 2 }],
        work: [],
        stock: [],
        assets: [{ condition: "GOOD", count: 3 }],
        monitoring: [
          { kind: "INCIDENT", active: 2, overdue: 1 },
          { kind: "CLEANING", active: 3, overdue: 0 },
        ],
        attention: [
          {
            workId: 7,
            workNo: "INC-7",
            kind: "INCIDENT",
            title: "Sự cố cần xử lý",
            dueAt: "2026-11-20T00:00:00Z",
            status: "OPEN",
          },
        ],
      };
    else if (path === "work")
      json = paged([
        {
          workId: 7,
          workNo: "QA-7",
          title: "Việc của nhân viên khác",
          kind: u.searchParams.get("kind"),
          status:
            u.searchParams.get("kind") === "INCIDENT" ? "OPEN" : "ASSIGNED",
          dueAt: "2026-11-20T00:00:00Z",
        },
      ]);
    else if (path === "workspace/options")
      json = {
        departments: [{ departmentId: 1, name: "Buồng phòng" }],
        rooms: [{ roomId: 1, code: "101" }],
        references: [
          {
            referenceId: 1,
            kind: "ASSET_TYPE",
            name: "Điều hòa",
            isActive: true,
          },
        ],
        users: [],
        assets: [],
      };
    else if (path === "insights/time") json = [];
    else if (path === "rooms")
      json = Array.from({ length: 18 }, (_, i) => ({
        roomId: i + 1,
        code: String(101 + i),
        roomTypeId: 1,
        locationId: 1,
        isOccupied: i === 1,
        isBlocked: i === 1,
        readiness: i === 1 ? "BLOCKED" : "READY",
        rowVersion: "fixture",
      }));
    else if (path.endsWith("/history")) json = { events: [], blocks: [] };
    else if (path === "catalog/room-types")
      json = paged([
        { roomTypeId: 1, name: "Standard", capacity: 2, isActive: true },
      ]);
    else if (path === "assets")
      json = Array.from({ length: 26 }, (_, i) => ({
        assetId: i + 1,
        name: `Tài sản ${i + 1}`,
        assetCode: `A${i + 1}`,
        conditionCode: "GOOD",
        roomId: 1,
        roomCode: "101",
        locationName: "Tầng 1",
        isActive: true,
        rowVersion: "fixture",
      }));
    else if (path === "catalog/rooms")
      json = paged([{ roomId: 1, code: "101", isActive: true }]);
    else if (path === "catalog/locations")
      json = paged([{ locationId: 1, name: "Tầng 1", isActive: true }]);
    else if (path === "insights/audit")
      json = Array.from(
        {
          length: u.searchParams.get("search")
            ? 1
            : u.searchParams.get("page") === "2"
              ? 1
              : 100,
        },
        (_, i) => ({
          auditId: i + 1,
          branchId,
          actorUserId: 2,
          entityType: "Asset",
          entityKey: String(i + 1),
          action: "UPDATE",
          occurredAt: "2026-11-22T00:00:00Z",
        }),
      );
    else if (path.startsWith("insights/reports/") || path === "insights/stock")
      json = [];
    else if (path === "documents") json = paged([]);
    else if (path.startsWith("catalog/")) json = paged([]);
    await route.fulfill({ json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  const open = async (hash) => {
    await page.goto(`${process.env.BASE_URL || "http://127.0.0.1:5186"}/#${hash}`);
    await expect(page.locator(".topbar h1")).toBeVisible();
  };
  await open("dashboard");
  await expect(
    page
      .getByRole("navigation", { name: "Điều hướng chính" })
      .getByRole("button"),
  ).toHaveCount(9);
  await expect(
    page.getByRole("navigation", { name: "Điều hướng chính" }),
  ).not.toContainText("Lịch làm việc");
  await expect(page.locator(".branch-metrics")).toContainText("Sự cố");
  await page.screenshot({ path: `${out}/dashboard-1440.png`, fullPage: true });
  await open("housekeeping");
  await expect(
    page.getByRole("button", { name: "Đang xem trong phạm vi quyền" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "+ Tạo mới", exact: true }),
  ).toHaveCount(0);
  assert(
    reads.some(
      (r) =>
        r.path === "work" &&
        r.params.includes("kind=CLEANING") &&
        r.params.includes("mine=false"),
    ),
  );
  await open("work?kind=INCIDENT");
  await expect(page.locator(".topbar h1")).toHaveText("Bảo trì & sự cố");
  await expect(
    page.getByRole("button", { name: "+ Tạo mới", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page.getByLabel(/^Trạng thái/).selectOption("OVERDUE");
  await expect
    .poll(() =>
      reads.some((r) => r.path === "work" && r.params.includes("overdue=true")),
    )
    .toBe(true);
  await page.getByRole("button", { name: "Bảo trì", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "+ Tạo mới", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel(/^Trạng thái/)).toHaveValue("");
  await open("rooms");
  await expect(page.locator(".room-tile")).toHaveCount(15);
  await page.getByRole("button", { name: /Bảo trì 1/ }).click();
  await expect(page.locator(".room-tile")).toHaveCount(1);
  await expect(page.locator(".room-tile")).toContainText("102");
  await page.locator(".room-tile").click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Nhận phòng", exact: true }),
  ).toHaveCount(0);
  await page.keyboard.press("Escape");
  await open("assets");
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    25,
  );
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    1,
  );
  await page
    .getByRole("button", { name: "+ Thêm tài sản", exact: true })
    .click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(page.getByLabel("Tình trạng ban đầu")).toHaveValue("GOOD");
  await page.keyboard.press("Escape");
  await open("audit");
  await expect(page.locator("tbody tr")).toHaveCount(100);
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await page.getByLabel("Tìm nhật ký").fill("Asset");
  await expect
    .poll(() =>
      reads.some(
        (r) =>
          r.path === "insights/audit" &&
          r.params.includes("page=1") &&
          r.params.includes("search=Asset"),
      ),
    )
    .toBe(true);
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const hash of [
      "dashboard",
      "housekeeping",
      "technical",
      "work",
      "rooms",
      "assets",
      "stock",
      "audit",
      "settings",
    ]) {
      await open(hash);
      await expect(page.locator("main .page-heading h1")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${hash} overflow at ${width}`,
      );
    }
  }
  await page.screenshot({ path: `${out}/audit-375.png`, fullPage: true });
  reduced = true;
  await open("housekeeping");
  await page.reload();
  await expect(page.locator(".topbar h1")).toHaveText("Tình trạng phòng");
  await expect(
    page
      .getByRole("navigation", { name: "Điều hướng chính" })
      .getByRole("button"),
  ).toHaveCount(2);
  await open("settings");
  await expect(page.locator(".topbar h1")).toHaveText("Cấu hình vận hành");
  await expect(page.getByRole("button", { name: /^\+ Thêm/ })).toHaveCount(0);
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/ui-verification.json`,
    JSON.stringify(
      { passed: true, responsiveChecks: 27, readRequests: reads.length },
      null,
      2,
    ),
  );
  console.log(
    "PASS OM navigation, permission guards, monitoring filters, occupied blocked room, asset form/paging, audit search/paging, read-only settings and 27 responsive views",
  );
} finally {
  await browser.close();
}
