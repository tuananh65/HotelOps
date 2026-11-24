import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const out = "../artifacts/workday-filter";
fs.mkdirSync(out, { recursive: true });
const groups = [
  {
    departmentId: 1,
    department: "Buồng phòng",
    roleId: 2,
    role: "Nhân viên buồng phòng",
  },
  {
    departmentId: 1,
    department: "Buồng phòng",
    roleId: 3,
    role: "Giám sát buồng phòng",
  },
  { departmentId: 2, department: "Lễ tân", roleId: 4, role: "Lễ tân" },
  { departmentId: 3, department: "Sảnh", roleId: 4, role: "Lễ tân" },
];
let shifts = groups.flatMap((g, index) =>
  [0, 1, 2].map((slot) => ({
    ...g,
    shiftId: index * 3 + slot + 1,
    name: `${g.role} · ${["đêm", "sáng", "chiều"][slot]}`,
    startMinute: slot * 480,
    endMinute: (slot + 1) * 480,
    capacity: 4,
    isActive: true,
    rowVersion: "fixture",
  })),
);
let manages = true;
const assignments = [];
const writes = [],
  reads = [],
  errors = [];
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
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
    let json = { items: [], total: 0 };
    if (req.method() !== "GET") {
      writes.push({ path, body: req.postDataJSON() });
      const body = req.postDataJSON();
      assignments.push({
        ...body,
        userId: body.userIds[0],
        workDate: body.workDates[0],
        assignmentId: 1,
        fullName: "Trần An",
        status: "PLANNED",
      });
      json = { count: body.userIds.length * body.workDates.length };
    } else if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Hoàng Gia Bảo" } },
        branches: [{ branchId: 1, name: "Chi nhánh Bùi Viện" }],
        roles: [
          {
            code: manages ? "DEPARTMENT_HEAD" : "HOUSEKEEPING",
            name: manages ? "Trưởng bộ phận" : "Nhân viên buồng phòng",
            branchId: 1,
          },
        ],
        memberships: [],
        requiresPasswordChange: false,
        grants: [manages ? "SHIFT_MANAGE" : "ATTENDANCE_RECORD"].map(
          (code) => ({
            code,
            branchId: 1,
            dataScope: manages ? "BRANCH" : "OWN",
          }),
        ),
      };
    else if (path === "workday") {
      reads.push(url.searchParams.get("date"));
      json = {
        weekStart: "2026-11-23",
        today: "2026-11-23",
        canManage: manages,
        shifts,
        assignments,
        active: null,
      };
    } else if (path === "workday/calendar") {
      json = {
        markers: [],
        notice: "Mốc lịch tham khảo; không tự ghi nhận nghỉ phép.",
      };
    } else if (path === "workday/candidates/batch") {
      reads.push(`shift:${url.searchParams.get("shiftId")}`);
      json = {
        users: [{ userId: 8, fullName: "Trần An", employeeCode: "EMP008" }],
        slots: url.searchParams
          .getAll("dates")
          .map((workDate) => ({ workDate, remaining: 4 })),
      };
    }
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("http://127.0.0.1:5186/#workday");
  const filter = page.getByRole("combobox", { name: "Đối tượng", exact: true });
  const rows = page.locator(".calendar tbody tr");
  await expect(filter.locator("option")).toHaveCount(4);
  await expect(filter.locator('option[value="ALL"]')).toHaveCount(0);
  await expect(rows).toHaveCount(3);
  await expect(
    page.getByRole("heading", { name: "Lịch toàn chi nhánh" }),
  ).toHaveCount(0);
  await filter.selectOption("1:3");
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText("Giám sát buồng phòng");
  const oldDate = await page.getByLabel("Ngày trong tuần").inputValue();
  await page.getByRole("button", { name: "Tuần sau", exact: true }).click();
  await expect(page.getByLabel("Ngày trong tuần")).not.toHaveValue(oldDate);
  await expect(filter).toHaveValue("1:3");
  await expect(rows).toHaveCount(3);
  await filter.selectOption("3:4");
  await expect(rows).toHaveCount(3);
  await rows.first().locator("button.add-shift").first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.getByRole("checkbox", { name: "Trần An · EMP008" }).check();
  await page
    .getByRole("button", { name: "Xác nhận phân ca", exact: true })
    .click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  assert.equal(writes[0].body.shiftId, 10);
  assert.deepEqual(writes[0].body.workDates, ["2026-11-23"]);
  assert.deepEqual(writes[0].body.userIds, [8]);
  assert.ok(reads.includes("shift:10"));
  await expect(filter).toHaveValue("3:4");
  await expect(rows.first()).toContainText("Trần An");
  await filter.selectOption("1:2");
  await expect(rows).toHaveCount(3);
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 900 });
    // Wait for the existing sidebar transition after switching breakpoints.
    await page.waitForTimeout(300);
    await filter.selectOption("1:2");
    await expect(rows).toHaveCount(3);
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    );
    const rect = await filter.boundingBox();
    assert.ok(rect.width >= 180 && rect.x + rect.width <= width);
    await page.screenshot({ path: `${out}/filter-${width}.png` });
  }
  manages = false;
  await page.reload();
  await expect(page.locator(".shift-list")).toBeVisible();
  await expect(filter).toHaveCount(0);
  manages = true;
  shifts = [];
  await page.reload();
  await expect(filter.locator("option")).toHaveCount(1);
  await expect(filter).toBeDisabled();
  await expect(filter).toContainText("Chưa có đối tượng");
  const emptySchedule = page.getByText(
    "Chưa có ca làm việc cho đối tượng đã chọn.",
  );
  await expect(emptySchedule).toBeVisible();
  await expect(page.locator(".calendar")).toHaveCount(0);
  const emptyBounds = await emptySchedule.boundingBox();
  assert(
    emptyBounds.x >= 0 &&
      emptyBounds.x + emptyBounds.width <= page.viewportSize().width,
    "Empty schedule message must fit without horizontal scrolling",
  );
  assert.equal(errors.length, 0);
  assert.equal(writes.length, 1);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        mockedApi: true,
        viewports: [1440, 768, 375],
        groups: groups.length,
        errors,
        writes,
        verified: [
          "distinct department-role groups",
          "three shifts per group",
          "week navigation preserves filter",
          "assignment uses selected shift",
          "all groups",
          "personal view",
          "empty calendar",
          "responsive",
        ],
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS workday audience filtering, weekly navigation, assignment routing and responsive layout (mocked APIs)",
  );
} finally {
  await browser.close();
}
