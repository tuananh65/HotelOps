import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const out = "../artifacts/housekeeping-staff";
fs.mkdirSync(out, { recursive: true });
const reads = [],
  writes = [],
  errors = [];
let proofUploaded = false;
const png = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=",
  "base64",
);
let conflict = false,
  reduced = false;
const permissions =
  "ATTENDANCE_RECORD CLEANING_EXECUTE CLEANING_VIEW INCIDENT_CREATE INCIDENT_VIEW IR_CREATE IR_PROCESS IR_VIEW MR_CREATE MR_VIEW ROOM_VIEW TASK_EXECUTE TASK_VIEW".split(
    " ",
  );
const pageOf = (rows, page = 1, total = rows.length) => ({
  items: rows.map((fields) => ({ fields })),
  page,
  total,
  pageSize: 50,
});
const work = {
  workId: 7,
  workNo: "HK-7",
  kind: "CLEANING",
  title: "Dọn phòng 101",
  description: "Vệ sinh theo checklist đã giao",
  roomId: 1,
  status: "ASSIGNED",
  createdBy: 9,
  progressPercent: 0,
  rowVersion: "v1",
  dueAt: "2026-11-23T04:00:00Z",
};
const assignment = {
  assignmentId: 1,
  assigneeUserId: 1,
  assignmentStatus: "ACTIVE",
  acceptedAt: null,
};
const checklist = {
  workChecklistItemId: 1,
  descriptionSnapshot: "Lau bề mặt",
  requiredSnapshot: true,
  outcome: null,
  rowVersion: "c1",
};
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    timezoneId: "Asia/Ho_Chi_Minh",
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const request = route.request(),
      url = new URL(request.url()),
      path = url.pathname.replace("/api/v2/", ""),
      method = request.method();
    const params = url.searchParams;
    const branch = Number(params.get("branchId") || 1);
    let json = {};
    if (method !== "GET") {
      const body = request
        .headers()
        ["content-type"]?.startsWith("multipart/form-data")
        ? { multipart: request.postData() }
        : request.postDataJSON();
      writes.push({ path, body });
      if (conflict) {
        conflict = false;
        await route.fulfill({
          status: 409,
          json: { title: "STALE_ROW_VERSION" },
        });
        return;
      }
      if (path.includes("/actions/")) {
        const verb = path.split("/").at(-1);
        if (verb === "accept") assignment.acceptedAt = "2026-11-23T00:00:00Z";
        if (verb === "start") { work.status = "IN_PROGRESS"; work.actualStartAt = "2026-11-23T01:00:00Z"; }
        if (verb === "progress") work.progressPercent = body.progress;
        if (verb === "complete") work.status = "PENDING_INSPECTION";
        work.rowVersion += "x";
        json = { fields: work };
      } else if (path.includes("/checklist/")) {
        Object.assign(checklist, body);
        checklist.rowVersion += "x";
        json = { fields: checklist };
      } else if (path === "evidence") {
        assert(
          body.multipart.includes('name="branchId"') &&
            body.multipart.includes('name="workId"') &&
            body.multipart.includes('filename="staff.png"'),
        );
        proofUploaded = true;
        json = { fields: { attachmentId: 1, originalName: "staff.png" } };
      } else json = { fields: { readAt: "2026-11-23T00:00:00Z" } };
      await route.fulfill({ status: 200, json });
      return;
    }
    reads.push({ path, params: url.search });
    if (path === "evidence/1") {
      await route.fulfill({ status: 200, contentType: "image/png", body: png });
      return;
    }
    if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Nhân viên buồng phòng QA" } },
        branches: [
          { branchId: 1, name: "Chi nhánh Bùi Viện" },
          { branchId: 2, name: "Chi nhánh Cao Thắng" },
        ],
        roles: [1, 2].map((branchId) => ({
          code: "HK_STAFF",
          name: "Nhân viên buồng phòng",
          branchId,
        })),
        memberships: [{ kind: "EMPLOYMENT", branchId: 1, departmentId: 1 }],
        grants: (reduced ? ["ROOM_VIEW"] : permissions).flatMap((code) =>
          [1, 2].map((branchId) => ({
            code,
            branchId,
            dataScope: code === "ATTENDANCE_RECORD" ? "OWN" : "BRANCH",
          })),
        ),
        requiresPasswordChange: false,
      };
    else if (path === "workday/personal-summary")
      json = { timezone: "Asia/Ho_Chi_Minh", updatedAt: "2026-11-23T03:00:00Z", periods: [
        { period: "WEEK", from: "2026-11-23", to: "2026-11-29", completedShifts: 1, workedHours: 8, workingShifts: 0 },
        { period: "MONTH", from: "2026-11-01", to: "2026-11-30", completedShifts: 10, workedHours: 80, workingShifts: 0 },
      ] };
    else if (path === "workday/calendar")
      json = { markers: [], notice: "" };
    else if (path === "insights/personal")
      json = {
        today: "2026-11-23",
        timezone: "Asia/Ho_Chi_Minh",
        metrics: {
          assignedToday: 3,
          awaitingAcceptance: 2,
          inProgress: 1,
          completedToday: 0,
          pendingInspection: 1,
          rework: 1,
          overdue: 0,
          unreadNotifications: 2,
        },
        priority: [work],
        shifts: [
          {
            assignmentId: 1,
            workDate: "2026-11-23",
            name: "Buồng phòng sáng",
            startMinute: 0,
            endMinute: 480,
            status: "PLANNED",
          },
        ],
      };
    else if (path === "work") {
      const kind = params.get("kind"),
        page = Number(params.get("page") || 1);
      if (params.get("search")) json = pageOf([work]);
      else if (kind === "CLEANING")
        json = pageOf(
          Array.from({ length: page === 1 ? 50 : 1 }, (_, i) => ({
            ...work,
            workId: i === 0 ? 7 : 100 + i,
            title: i === 0 ? work.title : `Dọn phòng ${201 + i}`,
          })),
          page,
          51,
        );
      else
        json = pageOf([
          {
            ...work,
            kind,
            workId: kind === "REWORK" ? 8 : 9,
            title:
              kind === "REWORK"
                ? "Làm lại bề mặt"
                : kind === "INCIDENT"
                  ? "Đèn hỏng"
                  : "Nhiệm vụ được giao",
          },
        ]);
    } else if (path === "work/7")
      json = {
        work: { fields: work },
        assignments: [{ fields: assignment }],
        checklist: [{ fields: checklist }],
        attachments: proofUploaded
          ? [{ attachmentId: 1, originalName: "staff.png" }]
          : [],
        context: { branch: "Chi nhánh Bùi Viện", floor: "Tầng 1", room: "101" },
        assignmentDetails: [
          {
            assignmentId: 1,
            assignedBy: "Giám sát QA",
            assignedAt: "2026-11-23T00:00:00Z",
            assignmentStatus: "ACTIVE",
          },
        ],
        reviews: [
          {
            workId: 20,
            status: "FAILED",
            result: "FAIL",
            findings: [
              { description: "Bề mặt cần vệ sinh lại", status: "OPEN" },
            ],
          },
        ],
        events: [],
      };
    else if (path === "work/9")
      json = {
        work: {
          fields: {
            ...work,
            workId: 9,
            kind: "INTERNAL_REQUEST",
            status: "SUBMITTED",
          },
        },
        assignments: [],
        checklist: [],
        events: [],
        capabilities: { canCoordinateRequest: false, canHandleRequest: false },
      };
    else if (path.endsWith("/related") || path === "insights/time") json = [];
    else if (path === "workspace/options")
      json = {
        rooms: [{ roomId: 1, code: "101" }],
        departments: [{ departmentId: 1, name: "Buồng phòng" }],
        references: [],
        items: [],
        warehouses: [],
        checklists: [],
      };
    else if (path === "rooms")
      json = [
        {
          roomId: 1,
          code: "101",
          roomTypeId: 1,
          locationId: 1,
          readiness: "PENDING_INSPECTION",
          isOccupied: false,
          isBlocked: false,
        },
      ];
    else if (path === "catalog/locations")
      json = pageOf([{ locationId: 1, name: "Tầng 1", kind: "FLOOR" }]);
    else if (path === "catalog/room-types")
      json = pageOf([{ roomTypeId: 1, name: "Standard", capacity: 2 }]);
    else if (path === "rooms/1/history") json = { events: [], blocks: [] };
    else if (path === "documents")
      json = pageOf([
        {
          documentId: 1,
          documentNo: "MR-1",
          kind: "MATERIAL_REQUEST",
          status: "DRAFT",
          createdBy: 1,
        },
      ]);
    else if (path === "documents/1")
      json = {
        document: {
          fields: {
            documentId: 1,
            documentNo: "MR-1",
            kind: "MATERIAL_REQUEST",
            status: "DRAFT",
            createdBy: 1,
          },
        },
        canApprove: false,
        lines: [],
        events: [],
      };
    else if (path === "notifications")
      json = {
        items: [
          {
            fields: {
              notificationId: 1,
              title: "Được giao dọn phòng",
              message: "Dọn phòng 101",
              workId: 7,
              requiresAcknowledgment: false,
              recipientUserId: 1,
            },
          },
          {
            fields: {
              notificationId: 2,
              title: "Yêu cầu vật tư",
              message: "MR-1",
              documentId: 1,
              requiresAcknowledgment: false,
              recipientUserId: 1,
            },
          },
        ],
        hasMore: false,
      };
    else if (path === "workday")
      json = {
        weekStart: "2026-11-23",
        today: "2026-11-23",
        canManage: false,
        active: null,
        shifts: [
          {
            shiftId: 1,
            roleId: 4,
            departmentId: 1,
            role: "Buồng phòng",
            department: "Buồng phòng",
            name: "Buồng phòng sáng",
            startMinute: 0,
            endMinute: 480,
            capacity: 4,
          },
        ],
        assignments: [
          {
            assignmentId: 1,
            shiftId: 1,
            userId: 1,
            fullName: "Nhân viên QA",
            workDate: "2026-11-23",
            status: "PLANNED",
            rowVersion: "s1",
          },
        ],
      };
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  const open = async (hash) => {
    await page.goto(`${process.env.UX_BASE || "http://127.0.0.1:5186"}/#${hash}`);
    await expect(page.locator(".topbar h1")).toBeVisible();
  };
  await open("dashboard");
  const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
  await expect(nav.getByRole("button")).toHaveCount(6);
  await expect(nav.getByRole("button", { name: "Phòng & Checklist" })).toHaveCount(0);
  await expect(page.locator(".topbar h1")).toHaveText("Tổng quan cá nhân");
  await expect(page.locator(".branch-metrics .panel")).toHaveCount(8);
  await expect(page.locator("main")).toContainText("Ca làm việc của tôi");
  assert(!reads.some((r) => r.path === "insights/dashboard"));
  await open("cleaning");
  await expect(page.locator("tbody tr")).toHaveCount(50);
  await expect(
    page.getByRole("button", { name: "Bộ lọc", exact: true }),
  ).toHaveAttribute("aria-expanded", "false");
  await expect(page.getByRole("button", { name: /Đang xem/ })).toHaveCount(0);
  assert(
    reads.some(
      (r) =>
        r.path === "work" &&
        r.params.includes("assignedToMe=true") &&
        r.params.includes("mine=false"),
    ),
  );
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await page.getByLabel("Tìm công việc / yêu cầu").fill("101");
  await expect
    .poll(() =>
      reads.some(
        (r) => r.params.includes("search=101") && r.params.includes("page=1"),
      ),
    )
    .toBe(true);
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page.getByLabel("Sắp xếp").selectOption("due");
  await expect
    .poll(() => reads.some((r) => r.params.includes("sort=due")))
    .toBe(true);
  await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
  const drawer = page.locator("dialog.room-drawer");
  await expect(drawer).toBeVisible();
  await expect(drawer).toContainText("Giám sát QA");
  await expect(drawer).toContainText("Bề mặt cần vệ sinh lại");
  for (const name of [
    "Phân công",
    "Tạo phiếu kiểm tra",
    "Kết luận đạt",
    "Gỡ chặn phòng",
  ])
    await expect(drawer.getByRole("button", { name, exact: true })).toHaveCount(
      0,
    );
  await drawer.getByRole("button", { name: "Nhận việc", exact: true }).click();
  await expect(drawer.getByLabel("Lý do / ghi chú")).toHaveCount(0);
  await expect(drawer).not.toContainText("HK-7");
  await drawer.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(
    drawer.getByRole("button", { name: "Bắt đầu", exact: true }),
  ).toBeVisible();
  conflict = true;
  await drawer.getByRole("button", { name: "Bắt đầu", exact: true }).click();
  await expect(drawer.getByLabel("Lý do / ghi chú")).toHaveCount(0);
  await drawer.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(drawer.getByRole("alert")).toContainText("Dữ liệu đã thay đổi");
  assert.equal(work.status, "ASSIGNED");
  await drawer.getByRole("button", { name: "Bắt đầu", exact: true }).click();
  await drawer.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(
    drawer.getByRole("button", { name: "Cập nhật tiến độ", exact: true }),
  ).toHaveCount(0);
  await expect(drawer.getByRole("button", { name: "Đề nghị bàn giao", exact: true })).toHaveCount(0);
  await drawer
    .getByLabel("Ảnh / tệp minh chứng")
    .setInputFiles({ name: "staff.png", mimeType: "image/png", buffer: png });
  await drawer
    .getByRole("button", { name: "Tải minh chứng", exact: true })
    .click();
  await expect(
    drawer.getByRole("button", { name: "staff.png", exact: true }),
  ).toBeVisible();
  const [download] = await Promise.all([
    page.waitForEvent("download"),
    drawer.getByRole("button", { name: "staff.png", exact: true }).click(),
  ]);
  assert.equal(download.suggestedFilename(), "staff.png");
  await expect(drawer.getByLabel("Kết quả Lau bề mặt")).toHaveCount(0);
  await drawer
    .getByRole("button", { name: "Gửi hoàn thành", exact: true })
    .click();
  await expect(drawer.getByLabel("Lý do / ghi chú")).toHaveCount(0);
  await drawer.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(drawer).toContainText("Chờ kiểm tra");
  await expect(drawer.getByRole("button", { name: "Đề nghị bàn giao", exact: true })).toHaveCount(0);
  assert(writes.filter(w => /actions\/(accept|start|complete)$/.test(w.path)).every(w => w.body.reason === null));
  await page.keyboard.press("Escape");
  await open("work?kind=REWORK");
  await expect(page.locator(".topbar h1")).toHaveText("Công việc dọn phòng");
  await expect(
    page.getByRole("button", { name: "Làm lại", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await open("requests");
  await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
  for (const name of ["Phân công", "Hủy công việc", "Chuyển bộ phận"])
    await expect(
      page.getByRole("dialog").getByRole("button", { name, exact: true }),
    ).toHaveCount(0);
  await page.keyboard.press("Escape");
  await open("documents");
  await expect(
    page.getByRole("button", { name: "Xuất kho", exact: true }),
  ).toHaveCount(0);
  await open("notifications");
  await page
    .getByRole("button", { name: "Mở yêu cầu vật tư", exact: true })
    .click();
  await expect(page.getByRole("dialog")).toContainText("MR-1");
  await page.keyboard.press("Escape");
  await open("rooms");
  await expect(page.locator(".topbar h1")).toHaveText("Tổng quan cá nhân");
  await expect(page.locator(".room-tile")).toHaveCount(0);
  assert(!reads.some((r) => r.path === "rooms"));
  await open("work");
  await expect(page.locator(".topbar")).toContainText("Công việc vận hành được giao cho bạn");
  assert(reads.some((r) => r.path === "work" && new URLSearchParams(r.params).get("kind") === "OPERATIONAL" && new URLSearchParams(r.params).get("assignedToMe") === "true"));
  await open("workday");
  await expect(page.getByLabel("Đối tượng", { exact: true })).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "+ Phân ca", exact: true }),
  ).toHaveCount(0);
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const hash of [
      "dashboard",
      "cleaning",
      "rooms",
      "work",
      "requests",
      "documents",
      "workday",
      "incidents",
      "notifications",
    ]) {
      await open(hash);
      await expect(page.locator("main h1")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${hash} overflow at ${width}`,
      );
    }
    await open("dashboard");
    await page.screenshot({
      path: `${out}/dashboard-${width}.png`,
      fullPage: true,
    });
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  reduced = true;
  await page.reload();
  await expect(nav.getByRole("button")).toHaveCount(1);
  assert.deepEqual(errors, []);
  assert(
    !reads.some(
      (r) =>
        r.path === "users" ||
        r.path === "work/candidates" ||
        r.path === "templates",
    ),
  );
  fs.writeFileSync(
    `${out}/ui-verification.json`,
    JSON.stringify(
      {
        passed: true,
        responsiveChecks: 27,
        reads: reads.length,
        mockedWrites: writes.map((r) => r.path),
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS HK Staff navigation, personal API, reasonless execution, paper checklist UI, no progress or handover after start, conflict handling, requests, Workday and 27 responsive views",
  );
} finally {
  await browser.close();
}
