import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/workday-scheduling";
fs.mkdirSync(out, { recursive: true });
const shift = {
  shiftId: 10,
  departmentId: 1,
  department: "Buồng phòng",
  roleId: 2,
  role: "Nhân viên",
  name: "Ca sáng",
  startMinute: 480,
  endMinute: 960,
  capacity: 3,
  isActive: true,
  rowVersion: "AAAAAAAAAAE=",
  hasHistory: true,
};
const employees = [
  { userId: 8, fullName: "Trần An", employeeCode: "EMP008" },
  { userId: 9, fullName: "Nguyễn Bình", employeeCode: "EMP009" },
];
const writes = [],
  errors = [],
  candidateQueries = [];
let manages = true,
  rejectSave = false,
  overtime = false,
  hoursRevision = 1,
  yearBoundary = false;
let page;
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
    sessionStorage.setItem("hotelops.compact.session", "fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const request = route.request(),
      url = new URL(request.url()),
      path = url.pathname.replace("/api/v2/", "");
    if (request.method() !== "GET") {
      const body = request.postDataJSON();
      writes.push({ path, body, method: request.method() });
      if (rejectSave)
        return route.fulfill({
          status: 409,
          json: { title: overtime ? "SHIFT_HOURS_CONFIRMATION_REQUIRED" : "SHIFT_CAPACITY_REACHED" },
        });
      return route.fulfill({
        status: 200,
        json: {
          count: (body.userIds?.length ?? 0) * (body.workDates?.length ?? 0),
        },
      });
    }
    let json = { items: [], total: 0 };
    if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Trưởng bộ phận" } },
        branches: [{ branchId: 1, name: "Chi nhánh A" }],
        roles: [
          {
            code: manages ? "DEPT_MANAGER" : "HK_STAFF",
            name: "Nhân sự",
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
    else if (path === "workday")
      json = {
        weekStart: yearBoundary ? "2026-12-28" : "2026-11-23",
        today: "2026-11-23",
        canManage: manages,
        shifts: [shift],
        assignments: [],
        active: null,
      };
    else if (path === "workday/calendar")
      json = {
        markers: yearBoundary ? (url.searchParams.get("year") === "2027" ? [{date:"2027-01-01",name:"Tết Dương lịch",kind:"HOLIDAY"}] : []) : [
          {
            date: "2026-11-24",
            name: "Ngày Văn hóa Việt Nam",
            kind: "HOLIDAY",
            source: "https://xaydungchinhsach.chinhphu.vn",
          },
        ],
        notice: "Mốc lịch tham khảo; không tự ghi nhận nghỉ phép hay nghỉ bù.",
      };
    else if (path === "workday/candidates/batch") {
      const dates = url.searchParams.getAll("dates");
      candidateQueries.push(dates);
      json = {
        users: employees.map(u => ({...u, hoursToken: `review-${u.userId}-${hoursRevision}-${dates.join()}`,
          hourWarnings: !overtime ? [] : [
            {period:"DAY",from:dates[0],to:dates[0],existingMinutes:480,addedMinutes:60,totalMinutes:540,limitMinutes:480},
            {period:"WEEK",from:dates[0],to:dates.at(-1),existingMinutes:2400,addedMinutes:480,totalMinutes:2880,limitMinutes:2400},
          ]})),
        slots: dates.map((workDate) => ({ workDate, remaining: 3 })),
      };
    } else if (path === "workday/configuration")
      json = {
        shifts: [shift],
        departments: [{ departmentId: 1, name: "Buồng phòng" }, {departmentId:3,name:"Kỹ thuật"}],
        roles: [{ roleId: 2, name: "Nhân viên" }, {roleId:4,name:"Nhân viên bảo trì"}],
        departmentRoles: [{ departmentId: 1, roleId: 2 }, {departmentId:3,roleId:4}],
      };
    await route.fulfill({ status: 200, json });
  });
  page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(`${base}/#workday`);
  // Vite cold module loading is separate from the five-second interaction assertions.
  await expect(page.locator(".topbar h1")).toBeVisible({ timeout: 30000 });
  await expect(page.locator(".workday-calendar-marker")).toContainText(
    "Ngày Văn hóa Việt Nam",
  );
  await page
    .getByRole("button", { name: "Phân Ca sáng ngày 2026-11-23", exact: true })
    .click();
  const dialog = page.getByRole("dialog"),
    submit = dialog.getByRole("button", {
      name: "Xác nhận phân ca",
      exact: true,
    });
  await expect(submit).toBeDisabled();
  await dialog.getByLabel("Phạm vi phân ca").selectOption("month");
  await expect(dialog.getByText(/tất cả 8 ngày/)).toBeVisible();
  await dialog.getByRole("checkbox", { name: /Chọn tất cả/ }).check();
  await expect(dialog.getByText(/2 người × 8 ngày = 16/)).toBeVisible();
  await expect(submit).toBeDisabled();
  await dialog.getByRole("checkbox", { name: /Tôi đã xem/ }).check();
  await expect(submit).toBeEnabled();
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 950 });
    await page.waitForTimeout(300);
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      `overflow at ${width}`,
    );
    await page.screenshot({ path: `${out}/monthly-${width}.png` });
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await submit.click();
  await expect(dialog).toHaveCount(0);
  assert.equal(writes.length, 1);
  assert.equal(writes[0].path, "workday/assignments/batch");
  assert.deepEqual(writes[0].body.userIds, [8, 9]);
  assert.equal(writes[0].body.workDates.length, 8);
  assert.equal(writes[0].body.holidaysAcknowledged, true);
  assert(
    writes[0].body.workDates.every(
      (d) => d >= "2026-11-23" && d <= "2026-11-30",
    ),
  );

  await page
    .getByRole("button", { name: "Phân Ca sáng ngày 2026-11-23", exact: true })
    .click();
  await dialog.getByLabel("Phạm vi phân ca").selectOption("month");
  await dialog.getByRole("checkbox", { name: "Thứ 7", exact: true }).uncheck();
  await dialog
    .getByRole("checkbox", { name: "Chủ nhật", exact: true })
    .uncheck();
  await dialog.getByRole("button", { name: "Bỏ các ngày có mốc lễ" }).click();
  await expect(dialog.getByText(/tất cả 5 ngày/)).toBeVisible();
  await expect(
    dialog.getByRole("checkbox", { name: /Tôi đã xem/ }),
  ).toHaveCount(0);
  await dialog.getByRole("checkbox", { name: /Trần An/ }).check();
  rejectSave = true;
  await submit.click();
  await expect(dialog.getByRole("alert")).toBeVisible();
  await expect(dialog).toBeVisible();
  assert.deepEqual(writes[1].body.workDates, [
    "2026-11-23",
    "2026-11-25",
    "2026-11-26",
    "2026-11-27",
    "2026-11-30",
  ]);
  rejectSave = false;
  await submit.click();
  await expect(dialog).toHaveCount(0);
  assert.equal(
    writes[1].body.operationKey,
    writes[2].body.operationKey,
    "retry keeps operation key",
  );

  await page.getByRole("button", { name: "Cấu hình ca", exact: true }).click();
  await page.getByRole("button", { name: "Sửa Ca sáng", exact: true }).click();
  await expect(
    dialog.getByRole("combobox", { name: "Bộ phận", exact: true }),
  ).toBeDisabled();
  await expect(
    dialog.getByRole("combobox", { name: "Đối tượng", exact: true }),
  ).toBeDisabled();
  await expect(dialog.getByLabel("Bắt đầu", { exact: true })).toBeDisabled();
  await dialog.getByLabel("Tên ca", { exact: true }).fill("Ca sáng mới");
  await dialog.getByRole("button", { name: "Lưu ca", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  assert.equal(writes[3].method, "PUT");
  assert.equal(writes[3].body.name, "Ca sáng mới");
  assert.equal(writes[3].body.startMinute, 480);
  assert.equal(writes[3].body.branchId, 1);
  await page.getByRole("button", { name: "Tạo ca", exact: true }).click();
  const department = dialog.locator('select[name="departmentId"]');
  const target = dialog.locator('select[name="roleId"]');
  await expect(target).toBeDisabled();
  await department.selectOption("3");
  await expect(target).toBeEnabled();
  assert.deepEqual(await target.locator('option').allTextContents(), ["Chọn đối tượng", "Nhân viên bảo trì"]);
  await target.selectOption("4");
  await department.selectOption("1");
  await expect(target).toHaveValue("");
  assert.deepEqual(await target.locator('option').allTextContents(), ["Chọn đối tượng", "Nhân viên"]);
  await dialog.getByLabel("Tên ca", { exact: true }).fill("Ca chiều");
  await dialog
    .getByRole("combobox", { name: "Bộ phận", exact: true })
    .selectOption("1");
  await dialog
    .getByRole("combobox", { name: "Đối tượng", exact: true })
    .selectOption("2");
  await dialog.getByLabel("Bắt đầu", { exact: true }).fill("16:00");
  await dialog.getByLabel("Kết thúc", { exact: true }).fill("24:00");
  await dialog.getByRole("button", { name: "Lưu ca", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  assert.equal(writes[4].body.endMinute, 1440);
  assert.equal(writes[4].path, "workday/shifts");
  await page.getByRole("button", {name:"Quay lại lịch"}).click();
  overtime = true;
  await page.getByRole("button", {name:"Phân Ca sáng ngày 2026-11-23",exact:true}).click();
  await dialog.getByLabel("Phạm vi phân ca").selectOption("week");
  await expect(dialog.getByText(/tất cả 7 ngày/)).toBeVisible();
  await dialog.getByRole("checkbox", {name:/Trần An/}).check();
  const warning = dialog.getByRole("region", {name:"Cảnh báo vượt giờ"});
  await expect(warning).toContainText("9 giờ");
  await expect(warning).toContainText("48 giờ");
  await dialog.getByRole("checkbox", {name:/Tôi đã xem 1 ngày/}).check();
  await expect(submit).toBeDisabled();
  const confirmHours = dialog.getByRole("checkbox", {name:/Tôi đã xem số giờ vượt/});
  await confirmHours.check();
  await expect(submit).toBeEnabled();
  await dialog.getByRole("checkbox", {name:/Nguyễn Bình/}).check();
  await expect(confirmHours).not.toBeChecked();
  await expect(submit).toBeDisabled();
  await confirmHours.check();
  rejectSave = true;
  hoursRevision++;
  await submit.click();
  await expect(dialog.getByRole("alert").filter({ hasText: /^Lịch vượt/ })).toContainText("xác nhận");
  await expect(confirmHours).not.toBeChecked();
  await expect(submit).toBeDisabled();
  rejectSave = false;
  await confirmHours.check();
  await page.screenshot({path:`${out}/weekly-overtime.png`});
  await submit.click();
  await expect(dialog).toHaveCount(0);
  const accepted = writes.at(-1).body;
  assert.equal(accepted.workDates.length,7);
  assert.equal(Object.keys(accepted.hoursAcknowledgements).length,2);
  assert(accepted.hoursAcknowledgements['8'].includes('review-8-2-'));
  assert.equal(writes.at(-2).body.operationKey, accepted.operationKey);

  yearBoundary = true;
  overtime = false;
  await page.reload();
  await page.getByRole("button", {name:"Phân Ca sáng ngày 2026-12-28",exact:true}).click();
  await dialog.getByLabel("Phạm vi phân ca").selectOption("week");
  await expect(dialog.getByText("Tết Dương lịch", {exact:true})).toBeVisible();
  await dialog.getByRole("checkbox", {name:/Trần An/}).check();
  await expect(submit).toBeDisabled();
  await dialog.getByRole("checkbox", {name:/Tôi đã xem 1 ngày/}).check();
  await submit.click();
  await expect(dialog).toHaveCount(0);
  assert.deepEqual(writes.at(-1).body.workDates,["2026-12-28","2026-12-29","2026-12-30","2026-12-31","2027-01-01","2027-01-02","2027-01-03"]);
  manages = false;
  await page.reload();
  await expect(page.locator(".topbar h1")).toBeVisible({ timeout: 30000 });
  await expect(page.locator(".shift-list")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Cấu hình ca", exact: true }),
  ).toHaveCount(0);
  assert.deepEqual(errors, []);
  const loginContext = await browser.newContext();
  const login = await loginContext.newPage();
  await login.goto(base);
  await expect(login.locator('.login-card')).toBeVisible();
  const logo = login.locator('.login-card .brand-logo');
  await expect(logo).toBeVisible();
  await expect(logo).toHaveJSProperty('complete', true);
  for (const width of [1440, 375]) {
    await login.setViewportSize({width,height:950});
    const cardBox = await login.locator('.login-card').boundingBox();
    const logoBox = await logo.boundingBox();
    assert(Math.abs(cardBox.x + cardBox.width / 2 - logoBox.x - logoBox.width / 2) < 2, 'login logo centered');
    assert(await login.locator('.login-page').evaluate(e => getComputedStyle(e).backgroundImage.includes('0.55')), 'darker background overlay');
    assert(await login.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'login does not overflow');
    await login.screenshot({path:`${out}/login-${width}.png`});
  }
  await loginContext.close();
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        fixtureOnly: true,
        primaryWrites: false,
        candidateQueries,
        writes,
        errors,
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS department-role dependency, week/month/multi-person selection, overtime confirmation and stale retry, cross-year holidays, history lock, OWN view and 3 viewports (fixture APIs).",
  );
} catch (error) {
  if (page) {
    await page.screenshot({ path: `${out}/failure.png`, fullPage: true });
    fs.writeFileSync(
      `${out}/failure.json`,
      JSON.stringify(
        { url: page.url(), errors, message: String(error) },
        null,
        2,
      ),
    );
  }
  throw error;
} finally {
  await browser.close();
}
