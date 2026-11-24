import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";
const base = process.env.UX_BASE || "https://127.0.0.1:5082";
const out = "../artifacts/attendance-browser";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  checks = [];
try {
  for (const role of [
    "HOUSEKEEPING_SUPERVISOR",
    "HK_STAFF",
    "MAINTENANCE_STAFF",
    "MAINTENANCE_LEAD",
    "STOREKEEPER",
    "RECEPTION",
    "DEPT_MANAGER",
    "OPERATION_MANAGER",
  ]) {
    const context = await browser.newContext({
      viewport: { width: 1440, height: 1000 },
    });
    await context.addInitScript(() =>
      sessionStorage.setItem("hotelops.compact.session", "fixture"),
    );
    let state = "PLANNED",
      completed = 2,
      summaryFailure = false;
    const calls = [],
      writes = [];
    const grants = [
      "ATTENDANCE_RECORD",
      "CLEANING_VIEW",
      "INSPECTION_VIEW",
      "MAINTENANCE_VIEW",
      "INVENTORY_VIEW",
    ];
    if (role === "HOUSEKEEPING_SUPERVISOR")
      grants.push("DASHBOARD_VIEW", "CLEANING_ASSIGN");
    if (
      ["MAINTENANCE_LEAD", "DEPT_MANAGER", "OPERATION_MANAGER"].includes(role)
    )
      grants.push("DASHBOARD_VIEW");
    await context.route("**/api/v2/**", async (route) => {
      const req = route.request(),
        url = new URL(req.url()),
        path = url.pathname.replace("/api/v2/", "");
      calls.push(path);
      let json = { items: [], total: 0, hasMore: false };
      if (path === "auth/me")
        json = {
          user: { fields: { userId: 9, fullName: "Nhân viên thử nghiệm" } },
          branches: [{ branchId: 1, name: "Bùi Viện" }],
          roles: [{ code: role, name: role, branchId: 1 }],
          memberships: [],
          requiresPasswordChange: false,
          grants: grants.map((code) => ({
            code,
            branchId: 1,
            dataScope: code === "ATTENDANCE_RECORD" ? "OWN" : "BRANCH",
          })),
        };
      else if (path === "workday/personal-summary") {
        if (summaryFailure)
          return route.fulfill({
            status: 503,
            json: { title: "SERVICE_UNAVAILABLE" },
          });
        json = {
          timezone: "Asia/Ho_Chi_Minh",
          updatedAt: "2026-11-23T06:00:00Z",
          periods: [
            {
              period: "WEEK",
              from: "2026-11-23",
              to: "2026-11-29",
              completedShifts: completed,
              workedHours: 15.5,
              workingShifts: state === "WORKING" ? 1 : 0,
            },
            {
              period: "MONTH",
              from: "2026-11-01",
              to: "2026-11-30",
              completedShifts: 10,
              workedHours: 77.5,
              workingShifts: 0,
            },
          ],
        };
      } else if (path.startsWith("insights/")) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const metrics = Object.fromEntries(
          [
            "assignedToday",
            "awaitingAcceptance",
            "inProgress",
            "completedToday",
            "pendingInspection",
            "rework",
            "overdue",
            "unreadNotifications",
            "roomsNeedCleaning",
            "roomsInProgress",
            "roomsPendingInspection",
            "roomsReady",
            "unassigned",
            "incidents",
            "defects",
            "pendingReview",
            "accepted",
            "maintenanceRework",
            "reportedIncidents",
            "open",
            "closed",
            "severeIncidents",
            "slaOverdue",
            "pmDue",
            "pmOverdueWork",
            "affectedAssets",
            "trackedItems",
            "stockedItems",
            "emptyItems",
            "lowStock",
            "pendingRequests",
            "receipts",
            "issues",
            "transfers",
            "openStocktakes",
            "openReplenishments",
          ].map((key) => [key, 2]),
        );
        json = {
          metrics,
          priority: [],
          shifts: [],
          today: "2026-11-23",
          timezone: "Asia/Ho_Chi_Minh",
          progress: [],
          timeline: [],
          lowStock: [],
          recent: [],
          updatedAt: "2026-11-23T06:00:00Z",
          scheduling: {
            assignedStaff: 4,
            workingStaff: 2,
            underfilledShifts: 1,
            weekStart: "2026-11-23",
          },
          operations: {
            activeTasks: 3,
            overdueTasks: 1,
            pendingRequests: 2,
            pendingMaterials: 1,
          },
          monitoring: [
            { kind: "CLEANING", active: 3, overdue: 0 },
            { kind: "INCIDENT", active: 1, overdue: 1 },
          ],
        };
      } else if (path === "workday/calendar")
        json = { markers: [], notice: "" };
      else if (path === "workday")
        json = {
          today: "2026-11-23",
          weekStart: "2026-11-23",
          currentMinute: 600,
          canManage: false,
          timezone: "Asia/Ho_Chi_Minh",
          shifts: [
            {
              shiftId: 1,
              name: "Ca sáng giám sát",
              startMinute: 480,
              endMinute: 960,
            },
          ],
          assignments: [
            {
              assignmentId: 42,
              shiftId: 1,
              userId: 9,
              workDate: "2026-11-23",
              status: state,
              rowVersion: "version-" + state,
            },
          ],
          active:
            state === "WORKING"
              ? {
                  assignmentId: 42,
                  name: "Ca sáng giám sát",
                  startedAt: "2026-11-23T01:00:00Z",
                  rowVersion: "version-WORKING",
                }
              : null,
        };
      else if (
        path.startsWith("workday/assignments/") &&
        req.method() === "POST"
      ) {
        writes.push({ path, body: req.postDataJSON() });
        state = path.endsWith("/start") ? "WORKING" : "ENDED";
        if (state === "ENDED") completed++;
      }
      await route.fulfill({ status: 200, json });
    });
    const page = await context.newPage();
    await page.clock.setFixedTime(new Date('2026-11-23T03:00:00Z'));
    page.on("pageerror", (e) => errors.push(`${role}: ${e.message}`));
    await page.goto(`${base}/#dashboard`);
    const summary = page.getByRole("region", {
      name: "Thống kê ca làm của tôi",
    });
    await expect(summary).toContainText("15,5 giờ");
    await expect(summary).toContainText("77,5 giờ");
    assert(calls.includes("workday/personal-summary"));
    await expect(summary).toHaveCount(1);
    if (role !== "RECEPTION") {
      await expect(page.locator(".metric-card").first()).toBeVisible();
      await expect
        .poll(
          () => calls.filter((p) => p === "workday/personal-summary").length,
        )
        .toBe(1);
      for (const width of [1440, 375, 320]) {
        await page.setViewportSize({ width, height: 1000 });
        const layout = await page.evaluate(() => ({
          summary: document
            .querySelector(".attendance-summary")
            .getBoundingClientRect().top,
          metrics: [...document.querySelectorAll(".metric-card")].map((n) => ({
            bottom: n.getBoundingClientRect().bottom,
            color: getComputedStyle(n).backgroundColor,
          })),
          overflow: document.documentElement.scrollWidth > innerWidth + 1,
        }));
        assert(!layout.overflow, `${role} horizontal overflow at ${width}`);
        assert(
          layout.metrics.every((m) => m.bottom <= layout.summary),
          `${role} metrics must precede attendance`,
        );
        assert(
          layout.metrics.every(
            (m) =>
              m.color !== "rgb(255, 255, 255)" &&
              m.color !== "rgba(0, 0, 0, 0)",
          ),
          `${role} metrics need color`,
        );
      }
      await page.setViewportSize({ width: 1440, height: 1000 });
    }
    await expect(summary.locator(".shift-working").first()).toHaveCSS(
      "background-color",
      "rgb(22, 113, 61)",
    );
    await expect(summary.locator(".shift-ended").first()).toHaveCSS(
      "background-color",
      "rgb(180, 35, 24)",
    );
    if (role === "RECEPTION")
      assert(
        !calls.includes("work"),
        "reception attendance dashboard must not request cleaning work",
      );
    if (role === "HOUSEKEEPING_SUPERVISOR") {
      for (const width of [1440, 768, 375]) {
        await page.setViewportSize({ width, height: 1000 });
        assert(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        );
        await page.screenshot({ path: `${out}/summary-${width}.png`, animations: 'disabled' });
      }
      await page.setViewportSize({ width: 1440, height: 1000 });
      const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
      await expect(
        nav.getByRole("button", { name: "Công việc", exact: true }),
      ).toBeVisible();
      await expect(
        nav.getByRole("button", { name: "Kiểm tra phòng", exact: true }),
      ).toHaveCount(0);
      await nav
        .getByRole("button", { name: "Lịch làm việc", exact: true })
        .click();
      await page
        .getByRole("button", { name: "Bắt đầu ca", exact: true })
        .click();
      await expect(
        page.getByRole("button", { name: "Kết thúc ca", exact: true }),
      ).toBeVisible();
      await expect(page.locator(".badge.shift-working").first()).toHaveCSS(
        "background-color",
        "rgb(22, 113, 61)",
      );
      await page
        .getByRole("button", { name: "Kết thúc ca", exact: true })
        .click();
      await expect(
        page.getByRole("button", { name: "Kết thúc ca", exact: true }),
      ).toHaveCount(0);
      await expect(page.locator(".badge.shift-ended").first()).toHaveCSS(
        "background-color",
        "rgb(180, 35, 24)",
      );
      assert.equal(writes.length, 2);
      assert.equal(writes[1].body.rowVersion, "version-WORKING");
      await nav
        .getByRole("button", { name: "Tổng quan Housekeeping", exact: true })
        .click();
      await expect(
        summary.getByRole("row").filter({ hasText: "Tuần này" }),
      ).toContainText("3");
      summaryFailure = true;
      await page.reload();
      await expect(summary.getByRole("alert")).toBeVisible();
      summaryFailure = false;
      await summary.getByRole("button", { name: "Thử lại" }).click();
      await expect(summary).toContainText("15,5 giờ");
    }
    checks.push(role);
    await context.close();
  }
  // A role without attendance must not call the summary endpoint or show attendance controls.
  const context = await browser.newContext();
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "fixture"),
  );
  let summaryCalls = 0;
  await context.route("**/api/v2/**", async (r) => {
    const path = new URL(r.request().url()).pathname;
    if (path.includes("personal-summary")) summaryCalls++;
    await r.fulfill({
      json: path.endsWith("auth/me")
        ? {
            user: { fields: { userId: 10, fullName: "Không có quyền ca" } },
            roles: [{ code: "RECEPTION", branchId: 1 }],
            branches: [{ branchId: 1, name: "Bùi Viện" }],
            grants: [{ code: "ROOM_VIEW", branchId: 1, dataScope: "BRANCH" }],
            memberships: [],
            requiresPasswordChange: false,
          }
        : path.endsWith("/rooms")
          ? []
          : { items: [], total: 0 },
    });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(`without attendance: ${e.message}`));
  await page.goto(`${base}/#rooms`);
  await expect(page.locator(".topbar h1")).toBeVisible();
  await expect(page.locator("main .empty")).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Thống kê ca làm của tôi" }),
  ).toHaveCount(0);
  assert.equal(summaryCalls, 0);
  await context.close();
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      { passed: true, fixtureOnly: true, roles: checks, errors },
      null,
      2,
    ),
  );
  console.log(
    "PASS attendance summary on eight role dashboards, OWN calendar start/end, error/retry, permission guard and responsive layouts.",
  );
} finally {
  await browser.close();
}
