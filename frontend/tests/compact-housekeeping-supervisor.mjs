import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

// UI fixtures only. SQL-backed workflow/RBAC checks run in Test-Backend -HousekeepingSupervisor.
const out = "../artifacts/housekeeping-supervisor";
fs.mkdirSync(out, { recursive: true });
const grants =
  "ATTENDANCE_RECORD CHECKLIST_MANAGE CLEANING_ASSIGN CLEANING_VIEW DASHBOARD_VIEW DEFECT_RESOLVE INCIDENT_CREATE INCIDENT_VIEW INSPECTION_PERFORM INSPECTION_VIEW ROOM_VIEW".split(
    " ",
  );
const profile = {
  user: { fields: { userId: 9, fullName: "Giám sát buồng phòng" } },
  branches: [{ branchId: 1, name: "Bùi Viện" }],
  roles: [
    {
      code: "HOUSEKEEPING_SUPERVISOR",
      name: "Giám sát buồng phòng",
      branchId: 1,
    },
  ],
  memberships: [],
  requiresPasswordChange: false,
  grants: grants.map((code) => ({
    code,
    branchId: 1,
    dataScope: code === "ATTENDANCE_RECORD" ? "OWN" : "BRANCH",
  })),
};
const root = {
  workId: 999,
  kind: "CLEANING",
  title: "Vệ sinh phòng 101",
  roomId: 1,
  status: "PENDING_INSPECTION",
  rowVersion: "root-v1",
  createdBy: 8,
};
const template = {
  checklistTemplateId: 2,
  code: "HK-QA",
  name: "Kiểm tra chất lượng",
  kind: "INSPECTION",
  versionNo: 1,
  isActive: true,
  publishedAt: "2026-11-23T01:00:00Z",
};
const rooms = Array.from({ length: 48 }, (_, i) => ({
  roomId: i + 1,
  code: String(101 + i),
  locationId: Math.floor(i / 6) + 1,
  floor: `Tầng ${Math.floor(i / 6) + 1}`,
  roomTypeName: "Standard",
  capacity: 2,
  isOccupied: i === 1,
  isBlocked: i === 2,
  readiness: i === 0 ? "READY" : i === 2 ? "BLOCKED" : "PENDING_CLEANING",
  cleaningStatus: i === 0 ? "COMPLETED" : "PENDING_ASSIGNMENT",
  inspectionStatus: i === 0 ? "PASSED" : null,
  rowVersion: `room-${i}`,
}));
const pageData = (items) => ({
  items,
  total: items.length,
  page: 1,
  pageSize: 25,
});
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  checks = [];
try {
  for (const width of [1440, 768, 375]) {
    const context = await browser.newContext({
        viewport: { width, height: 950 },
      }),
      writes = [],
      requests = [];
    let created = null;
    await context.addInitScript(() =>
      sessionStorage.setItem("hotelops.compact.session", "fixture"),
    );
    await context.route("**/api/v2/**", async (route) => {
      const req = route.request(),
        url = new URL(req.url()),
        path = url.pathname.replace("/api/v2/", "");
      requests.push(path);
      let json = {};
      if (path === "auth/me") json = profile;
      else if (path === "auth/logout") json = {};
      else if (path === "notifications") json = { items: [], hasMore: false };
      else if (path === "workday/personal-summary") json = {timezone:"Asia/Ho_Chi_Minh",updatedAt:"2026-11-23T06:00:00Z",periods:[{period:"WEEK",from:"2026-11-23",to:"2026-11-29",completedShifts:1,workedHours:7.5,workingShifts:0},{period:"MONTH",from:"2026-11-01",to:"2026-11-30",completedShifts:12,workedHours:90,workingShifts:0}]};
      else if (path === "insights/housekeeping")
        json = {
          updatedAt: "2026-11-23T01:00:00Z",
          metrics: {
            roomsNeedCleaning: 47,
            roomsInProgress: 0,
            roomsPendingInspection: 1,
            roomsReady: 1,
            unassigned: 0,
            inProgress: 0,
            overdue: 0,
            pendingInspection: 0,
            rework: 0,
            incidents: 0,
            defects: 1,
          },
          turnover: [
            { status: "PENDING_CLEANING", occupied: false, count: 47 },
            { status: "READY", occupied: false, count: 1 },
          ],
          workload: [{ status: "PENDING_INSPECTION", count: 1 }],
          priority: [root],
          waiting: [root],
          overdue: [],
        };
      else if (path === "rooms") json = rooms;
      else if (path.endsWith("/workflow"))
        json = {
          turnoverNo: 1,
          cleaning: [root],
          inspections: [],
          incidents: [],
        };
      else if (path.endsWith("/history")) json = { events: [], blocks: [] };
      else if (path === "workspace/options")
        json = {
          rooms,
          departments: [],
          references: [],
          checklists: [template],
        };
      else if (path === "work/inspection-queue")
        json = pageData([
          { ...root, room: "101", completedAt: "2026-11-23T01:00:00Z" },
        ]);
      else if (path === "work/defects")
        json = pageData([
          {
            findingId: 5,
            inspectionWorkId: 42,
            parentWorkId: 999,
            description: "Lỗi vệ sinh",
            status: "OPEN",
            room: "101",
          },
        ]);
      else if (path === "work/housekeeping-history")
        json = {date:"2026-11-23",timezone:"Asia/Ho_Chi_Minh",items:[],total:0,page:1,pageSize:25};
      else if (path === "work/housekeepers")
        json = {
          date: url.searchParams.get("date") || "2026-11-23",
          timezone: "Asia/Ho_Chi_Minh",
          items: [
            {
              userId: 10,
              fullName: "Nhân viên HK",
              employeeCode: "HK001",
              accountStatus: "ACTIVE",
              open: 1,
              completed: 2,
              busy: false,
              shifts: [
                {
                  name: "Ca sáng",
                  startMinute: 0,
                  endMinute: 480,
                  status: "PLANNED",
                },
              ],
            },
          ],
        };
      else if (path === "work/cleaning-rooms") json = pageData([]);
      else if (path === "work/cleaning-queue")
        json = { ...pageData([]), counts: [] };
      else if (path === "work/candidates")
        json = [
          {
            userId: 9,
            fullName: "Giám sát buồng phòng",
            employeeCode: "HKS001",
          },
        ];
      else if (path === "work/999")
        json = {
          work: { fields: root },
          assignments: [],
          events: [],
          checklist: [],
          findings: [],
        };
      else if (path === "work" && req.method() === "POST") {
        const body = req.postDataJSON();
        writes.push({ path, body });
        created = {
          ...body,
          workId: 42,
          status: "ASSIGNED",
          rowVersion: "inspection-v1",
          createdBy: 9,
        };
        json = { fields: created };
      } else if (path === "work/42/actions/assign") {
        writes.push({ path, body: req.postDataJSON() });
        json = { fields: created };
      } else if (path === "work/42")
        json = {
          work: {
            fields: created || { ...root, kind: "INSPECTION", workId: 42 },
          },
          context: { branch: "Bùi Viện", room: "101", floor: "Tầng 1" },
          assignments: [],
          events: [],
          checklist: [],
          findings: [],
          assignmentDetails: [],
          attachments: [],
        };
      else if (path.endsWith("/related")) json = [];
      else if (path === "work") json = pageData([]);
      else if (path === "templates")
        json = { checklists: [{ fields: template }], schedules: [] };
      else if (path === "templates/checklists/2")
        json = {
          template: { fields: template },
          items: [
            {
              fields: {
                checklistItemId: 3,
                description: "Kiểm tra bề mặt",
                isRequired: true,
                weight: 1,
              },
            },
          ],
        };
      else if (path === "templates/checklists" && req.method() === "POST") {
        writes.push({ path, body: req.postDataJSON() });
        json = { fields: { ...template, versionNo: 2 } };
      } else if (path === "workday")
        json = {
          date: "2026-11-23",
          weekStart: "2026-11-23",
          days: [],
          shifts: [],
          assignments: [],
        };
      else {
        await route.fulfill({
          status: 404,
          json: { title: `UNMOCKED_${path}` },
        });
        return;
      }
      await route.fulfill({ json });
    });
    const page = await context.newPage();
    page.on("pageerror", (e) => errors.push(`${width}: ${e.message}`));
    const base = process.env.UX_BASE || "http://127.0.0.1:5186";
    for (const [route, title] of [
      ["dashboard", "Tổng quan Housekeeping"],
      ["rooms", "Tình trạng phòng"],
      ["cleaning", "Phân công dọn phòng"],
      ["inspection", "Phòng chờ kiểm tra"],
      ["rework", "Xử lý làm lại"],
      ["staff", "Lịch sử vệ sinh"],
      ["incidents", "Sự cố & Thông báo"],
    ]) {
      await page.goto(`${base}/#${route}`);
      await expect(
        page.getByRole("heading", { level: 1, name: title, exact: true }),
      ).toBeVisible();
      await expect(page.locator("main .alert")).toHaveCount(0);
      await expect(page.locator("main .spinner")).toHaveCount(0);
      assert.equal(await page.locator(".sidebar nav button").count(), 6);
      await expect(page.locator('.sidebar nav').getByRole('button',{name:'Công việc',exact:true})).toBeVisible();
      await expect(page.locator('.sidebar nav').getByRole('button',{name:'Lịch làm việc',exact:true})).toBeVisible();
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${width} ${route}: page overflow`,
      );
      checks.push({ width, route, title: true, noPageOverflow: true });
      if (["dashboard", "staff"].includes(route))
        await page.screenshot({
          path: `${out}/${route}-${width}.png`,
          fullPage: true,
        });
    }
    await page.goto(`${base}/#work`);
    const workTabs = page.locator('.tabs[aria-label="Công việc buồng phòng"]');
    await workTabs.getByRole('link', {name:'Kiểm tra phòng',exact:true}).click();
    await expect(page.getByRole('heading', {level:1,name:'Phòng chờ kiểm tra',exact:true})).toBeVisible();
    await workTabs.getByRole('link', {name:'Xử lý làm lại',exact:true}).click();
    await expect(page.getByRole('heading', {level:1,name:'Xử lý làm lại',exact:true})).toBeVisible();
    await workTabs.getByRole('link', {name:'Phân công dọn phòng',exact:true}).click();
    await expect(page.getByRole('heading', {level:1,name:'Phân công dọn phòng',exact:true})).toBeVisible();
    await page.goto(`${base}/#rooms`);
    await expect(page.locator(".room-tile")).toHaveCount(15);
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.getByLabel("Tầng / khu vực").selectOption("2");
    await expect(page.locator(".room-tile")).toHaveCount(6);
    await page.getByLabel("Tầng / khu vực").selectOption("");
    await page.getByLabel(/^Vệ sinh/).selectOption("PENDING_ASSIGNMENT");
    await page.getByLabel("Chặn vận hành").selectOption("blocked");
    await expect(page.locator(".room-tile")).toHaveCount(1);
    await page
      .getByRole("button", { name: "Xem dạng bảng", exact: true })
      .click();
    await expect(page.locator("main table tbody tr")).toHaveCount(1);
    await page
      .locator("main table")
      .getByRole("button", { name: "Chi tiết", exact: true })
      .first()
      .click();
    await expect(page.locator(".room-drawer")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Nhận phòng", exact: true }),
    ).toHaveCount(0);
    await page.goto(`${base}/#inspection`);
    await page
      .getByRole("link", { name: "Tạo phiếu kiểm tra", exact: true })
      .click();
    const create = page.getByRole("dialog", {
      name: "Tạo kiểm tra phòng",
      exact: true,
    });
    await expect(create.getByLabel("Tiêu đề")).toHaveValue(
      "Kiểm tra · Vệ sinh phòng 101",
    );
    await create.getByLabel("Mẫu checklist").selectOption("2");
    await create
      .getByRole("button", { name: "Tạo công việc", exact: true })
      .click();
    const assignment = page.getByRole("dialog", {
      name: "Phân công",
      exact: true,
    });
    await expect(assignment).toBeVisible();
    await assignment.locator('select[name="userId"]').selectOption("9");
    await assignment
      .getByRole("button", { name: "Xác nhận", exact: true })
      .click();
    assert.equal(writes.find((w) => w.path === "work").body.parentWorkId, 999);
    assert.equal(
      writes.find((w) => w.path === "work/42/actions/assign").body.userId,
      9,
    );
    await page.goto(`${base}/#inspection?tab=checklists`);
    await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
    await page
      .getByRole("button", { name: "Tạo phiên bản mới", exact: true })
      .click();
    const version = page.getByRole("dialog", {
      name: "Tạo phiên bản checklist mới",
      exact: true,
    });
    await expect(version.getByLabel("Mã mẫu")).toHaveValue("HK-QA");
    await expect(version.getByLabel("Mã mẫu")).toHaveAttribute("readonly", "");
    await version
      .getByRole("button", { name: "Công bố mẫu", exact: true })
      .click();
    assert.equal(
      writes.find((w) => w.path === "templates/checklists").body.items[0]
        .description,
      "Kiểm tra bề mặt",
    );
    await page.goto(`${base}/#rework?tab=defects`);
    await expect(
      page.getByRole("combobox", { name: "Trạng thái", exact: true }),
    ).toHaveCount(0);
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page
      .getByRole("combobox", { name: "Trạng thái", exact: true })
      .selectOption("OPEN");
    await page.reload();
    await expect(
      page.getByRole("cell", { name: "Lỗi vệ sinh #5", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("combobox", { name: "Trạng thái", exact: true }),
    ).toHaveCount(0);
    await page.screenshot({
      path: `${out}/defects-${width}.png`,
      fullPage: true,
    });
    assert(
      !requests.some((p) => p === "users" || p.startsWith("catalog/roles")),
      "No privileged user API",
    );
    checks.push({
      width,
      roomPagingAndFloor: true,
      roomDrawer: true,
      inspectionExactRootAndAssignment: true,
      immutableChecklistVersion: true,
      hiddenFilters: true,
    });
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        mockedApis: true,
        checks,
        errors,
        verifiedAt: new Date().toISOString(),
      },
      null,
      2,
    ),
  );
  console.log(
    `PASS HKS seven pages, room filters/pagination/drawer, exact-parent inspection/assignment, checklist versioning and hidden filters at 1440/768/375 (${checks.length} groups, mocked APIs)`,
  );
} finally {
  await browser.close();
}
