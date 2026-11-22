import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

// UI fixtures only. All API calls intercepted, no production login or writes.
const out = "../artifacts/department-head";
fs.mkdirSync(out, { recursive: true });
const permissions = [
  "DASHBOARD_VIEW",
  "SHIFT_MANAGE",
  "TASK_MANAGE",
  "TASK_VIEW",
  "IR_CREATE",
  "IR_VIEW",
  "IR_PROCESS",
  "IR_OVERDUE_HANDLE",
  "MR_VIEW",
  "MR_APPROVE_L1",
  "MR_APPROVE_FALLBACK",
  "REPORT_VIEW",
  "ROOM_VIEW",
  "INVENTORY_VIEW",
];
let allowApproval = false,
  reduced = false;
const reads = [],
  errors = [];
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const pageOf = (rows) => ({
  items: rows.map((fields) => ({ fields })),
  total: rows.length,
  page: 1,
  pageSize: 50,
});
const shifts = [
  {
    shiftId: 11,
    departmentId: 1,
    department: "Buồng phòng",
    roleId: 2,
    role: "Nhân viên buồng phòng",
    name: "Buồng phòng · sáng",
    isActive: true,
    startMinute: 480,
    endMinute: 960,
    capacity: 4,
    rowVersion: "fixture",
  },
  {
    shiftId: 12,
    departmentId: 2,
    department: "Lễ tân",
    roleId: 3,
    role: "Lễ tân",
    name: "Lễ tân · sáng",
    isActive: true,
    startMinute: 480,
    endMinute: 960,
    capacity: 2,
    rowVersion: "fixture",
  },
];
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const u = new URL(route.request().url()),
      path = u.pathname.replace("/api/v2/", "");
    reads.push({ path, params: u.search });
    assert.equal(
      route.request().method(),
      "GET",
      "Read-only UI checks must not write",
    );
    let json = {};
    if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Hoàng Gia Bảo" } },
        branches: [
          { branchId: 1, name: "Chi nhánh Bùi Viện" },
          { branchId: 2, name: "Chi nhánh Cao Thắng" },
        ],
        roles: [1, 2].map((branchId) => ({
          code: "DEPT_MANAGER",
          name: "Trưởng bộ phận",
          branchId,
        })),
        grants: (reduced ? ["SHIFT_MANAGE"] : permissions).flatMap((code) =>
          [1, 2].map((branchId) => ({ code, branchId, dataScope: "BRANCH" })),
        ),
        memberships: [],
        requiresPasswordChange: false,
      };
    else if (path === "workday")
      json = {
        canManage: true,
        weekStart: "2026-11-23",
        today: "2026-11-23",
        shifts,
        assignments: [
          {
            assignmentId: 1,
            userId: 10,
            shiftId: 12,
            workDate: "2026-11-24",
            status: "PLANNED",
          },
        ],
        active: null,
      };
    else if (path === "workday/candidates")
      json = Array.from({ length: 12 }, (_, i) => ({
        userId: 10 + i,
        employeeCode: `EMP${i}`,
        fullName: `${u.searchParams.get("shiftId") === "12" ? "Lễ tân" : "Nhân viên"} ${i}`,
      }));
    else if (path === "workspace/options")
      json = {
        departments: [
          { departmentId: 1, name: "Buồng phòng" },
          { departmentId: 2, name: "Lễ tân" },
        ],
        rooms: [],
        warehouses: [],
        items: [],
        references: [],
      };
    else if (path === "work")
      json = pageOf([
        {
          workId: 1,
          title: "Kiểm tra theo yêu cầu",
          workNo: "TASK-123",
          status: "PENDING_ASSIGNMENT",
          dueAt: null,
        },
      ]);
    else if (path === "documents")
      json = pageOf([
        {
          documentId: 1,
          documentNo: "MR-1",
          kind: "MATERIAL_REQUEST",
          status: "PENDING_APPROVAL",
          createdBy: 2,
          rowVersion: "fixture",
        },
      ]);
    else if (path === "documents/1")
      json = {
        document: {
          fields: {
            documentId: 1,
            documentNo: "MR-1",
            kind: "MATERIAL_REQUEST",
            status: "PENDING_APPROVAL",
            createdBy: 2,
            rowVersion: "fixture",
          },
        },
        canApprove: allowApproval,
        lines: [],
        events: [],
        fulfillment: [],
      };
    else if (path === "insights/dashboard")
      json = {
        rooms: [],
        work: [],
        stock: [],
        scheduling: {
          weekStart: "2026-11-23",
          assignedStaff: 8,
          workingStaff: 2,
          underfilledShifts: 6,
        },
        operations: {
          activeTasks: 4,
          overdueTasks: 1,
          pendingRequests: 3,
          pendingMaterials: 2,
        },
      };
    else if (path === "insights/reports/workday")
      json = Array.from({ length: 26 }, (_, i) => ({
        assignmentId: i + 1,
        employeeCode: `EMP${i}`,
        fullName: `Nhân viên ${i}`,
        department: "Lễ tân",
        shift: "Ca sáng",
        workDate: "2026-11-23",
        status: "PLANNED",
        startedAt: null,
        endedAt: null,
      }));
    else if (path.startsWith("insights/")) json = [];
    else throw new Error(`Unexpected API ${path}`);
    await route.fulfill({ json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("http://127.0.0.1:5186/#dashboard");
  const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
  await expect(nav.getByRole("button")).toHaveCount(7);
  assert.deepEqual(await nav.getByRole("button").allTextContents(), [
    "Tổng quan",
    "Lịch làm việc",
    "Công việc",
    "Yêu cầu nội bộ",
    "Yêu cầu vật tư",
    "Báo cáo",
    "Nhân sự",
  ]);
  await expect(page.locator(".branch-metric-value")).toHaveText([
    "8",
    "2",
    "6",
    "4",
    "1",
    "3",
    "2",
  ]);
  await nav.getByRole("button", { name: "Nhân sự", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(10);
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(2);
  await page.getByLabel("Tìm nhân viên").fill("Nhân viên 0");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await page.getByRole("button", { name: "1 ca · Xem lịch" }).click();
  await expect(page.getByRole("dialog")).toContainText("Lễ tân · sáng");
  await page.keyboard.press("Escape");
  await page.getByLabel("Tìm nhân viên").fill("");
  await page.getByLabel("Ca cần bố trí").selectOption("12");
  await expect(page.locator("tbody tr").first()).toContainText("Lễ tân 0");
  await nav
    .getByRole("button", { name: "Yêu cầu nội bộ", exact: true })
    .click();
  await expect(page.locator(".topbar h1")).toHaveText("Yêu cầu nội bộ");
  await expect(page.getByLabel("Bộ phận xử lý")).toHaveCount(0);
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page.getByLabel("Bộ phận xử lý").selectOption("2");
  await page.getByLabel(/^Trạng thái/).selectOption("OVERDUE");
  await page.getByLabel("Tìm công việc / yêu cầu").fill("TASK-123");
  await expect
    .poll(() =>
      reads.some(
        (r) =>
          r.path === "work" &&
          r.params.includes("kind=INTERNAL_REQUEST") &&
          r.params.includes("departmentId=2") &&
          r.params.includes("overdue=true") &&
          r.params.includes("search=TASK-123"),
      ),
    )
    .toBe(true);
  await expect(page.locator("tbody tr")).toHaveCount(1); // work number search must not be discarded by local title filter.
  await nav
    .getByRole("button", { name: "Yêu cầu vật tư", exact: true })
    .click();
  await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
  await expect(
    page
      .getByRole("dialog")
      .getByRole("button", { name: "Duyệt", exact: true }),
  ).toHaveCount(0);
  await page.keyboard.press("Escape");
  allowApproval = true;
  await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
  await expect(
    page
      .getByRole("dialog")
      .getByRole("button", { name: "Duyệt", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await nav.getByRole("button", { name: "Lịch làm việc", exact: true }).click();
  await expect(page.getByLabel("Đối tượng").locator("option")).toHaveCount(2);
  await expect(page.getByLabel("Đối tượng")).not.toContainText(
    "Tất cả đối tượng",
  );
  await nav.getByRole("button", { name: "Báo cáo", exact: true }).click();
  await page.getByLabel("Loại báo cáo").selectOption("workday");
  await expect(page.locator("tbody tr")).toHaveCount(25);
  await expect(
    page.getByRole("columnheader", { name: "Nhân viên", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await page.getByLabel("Loại báo cáo").selectOption("materials");
  await expect(
    page.getByRole("button", { name: "Trước", exact: true }),
  ).toBeDisabled();
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const route of [
      "dashboard",
      "workday",
      "work",
      "requests",
      "documents",
      "reports",
      "staff",
    ]) {
      await page.goto(`http://127.0.0.1:5186/#${route}`);
      await expect(page.locator(".topbar h1")).toHaveCount(1);
      await page.waitForTimeout(150);
      assert.ok(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${route} overflow at ${width}`,
      );
      await expect(page.locator("main .branch-context")).toHaveCount(0);
      await page.screenshot({
        path: `${out}/${route}-${width}.png`,
        fullPage: true,
      });
    }
  }
  assert.ok(
    !reads.some((r) => r.path === "users" || r.path.startsWith("users/")),
    "Never use user admin API",
  );
  reduced = true;
  await page.goto("http://127.0.0.1:5186/#documents");
  await page.reload();
  await expect(nav.getByRole("button")).toHaveCount(2); // workday + staff; role alone must not expose other menus.
  await expect(page.locator(".topbar h1")).toHaveText("Lịch làm việc");
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        viewportRoutes: 21,
        readOnly: true,
        noUserApi: true,
        permissionGates: true,
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS: Department Head navigation, candidate pagination, filters, approval capability and 21 responsive routes",
  );
} finally {
  await browser.close();
}
