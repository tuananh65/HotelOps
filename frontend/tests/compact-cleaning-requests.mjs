import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

// UI fixtures only: real API authorization/lifecycle is covered by test-housekeeping-staff.ps1.
const out = "../artifacts/cleaning-requests";
fs.mkdirSync(out, { recursive: true });
const grants =
  "ROOM_VIEW CLEANING_ASSIGN CLEANING_VIEW INSPECTION_PERFORM INSPECTION_VIEW CHECKLIST_MANAGE DASHBOARD_VIEW ATTENDANCE_RECORD".split(
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
  grants: grants.map((code) => ({ code, branchId: 1, dataScope: "BRANCH" })),
};
const rooms = Array.from({ length: 26 }, (_, i) => ({
  roomId: i + 1,
  code: String(101 + i),
  floor: "Tầng 1",
  roomType: "Standard",
  capacity: 2,
  readiness: "PENDING_CLEANING",
  rowVersion: "room-v1",
}));
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
    let created = null,
      assigned = false,
      conflict = false,
      onShift = true,
      staffAvailable = true;
    const writes = [];
    const context = await browser.newContext({
      viewport: { width, height: 950 },
    });
    await context.addInitScript(() =>
      sessionStorage.setItem("hotelops.compact.session", "fixture"),
    );
    await context.route("**/api/v2/**", async (route) => {
      const req = route.request(),
        url = new URL(req.url()),
        path = url.pathname.replace("/api/v2/", "");
      let json = { items: [], total: 0, page: 1, pageSize: 50 };
      if (path === "auth/me") json = profile;
      else if (path === "workspace/options")
        json = { rooms, departments: [], references: [], checklists: [] };
      else if (path === "work/cleaning-rooms") {
        const term = url.searchParams.get("search") || "",
          roomId = Number(url.searchParams.get("roomId"));
        const filtered = rooms.filter(
          (r) =>
            (!created || r.roomId !== created.roomId) &&
            r.code.includes(term) &&
            (!roomId || r.roomId === roomId),
        );
        const page = Number(url.searchParams.get("page")) || 1;
        json = {
          items: filtered.slice((page - 1) * 25, page * 25),
          total: filtered.length,
          page,
          pageSize: 25,
        };
      } else if (path === "work/cleaning-queue")
        json = {
          items: created
            ? [
                {
                  ...created,
                  room: "101",
                  assignee: assigned ? "Nhân viên A" : "",
                },
              ]
            : [],
          counts: created ? [{ status: created.status, count: 1 }] : [],
          total: created ? 1 : 0,
          pageSize: 50,
        };
      else if (path === "work/cleaning-creation-options")
        json = { canCreate: onShift, candidates: staffAvailable ? [{ userId: 10, fullName: "Nhân viên A", employeeCode: "HK001" }] : [] };
      else if (path === "work/candidates")
        json = [{ userId: 10, fullName: "Nhân viên A", employeeCode: "HK001" }];
      else if (path === "work" && req.method() === "POST") {
        const body = req.postDataJSON();
        writes.push({ path, body });
        if (conflict) {
          await route.fulfill({
            status: 409,
            json: { title: "STALE_ROW_VERSION", status: 409 },
          });
          return;
        }
        created = {
          ...body,
          workId: 50,
          status: "ASSIGNED",
          rowVersion: "work-v1",
          createdBy: 9,
          progressPercent: 0,
        };
        json = { fields: created };
      } else if (path === "work/50/actions/assign") {
        writes.push({ path, body: req.postDataJSON() });
        assigned = true;
        created.status = "ASSIGNED";
        json = { fields: created };
      } else if (path.endsWith("/related")) json = [];
      else if (path === "work/50")
        json = {
          work: { fields: created },
          assignments: [],
          events: [],
          checklist: [],
          findings: [],
        };
      await route.fulfill({ json });
    });
    const page = await context.newPage();
    page.on("pageerror", (e) => errors.push(e.message));
    await page.goto(
      `${process.env.UX_BASE || "http://127.0.0.1:5186"}/#work?kind=CLEANING`,
    );
    await expect(
      page.getByRole("button", { name: "Phòng cần tạo việc", exact: true }),
    ).toHaveAttribute("aria-pressed", "true");
    const pending = page.getByRole("region", {
      name: "Phòng cần tạo việc dọn",
    });
    await expect(
      pending.getByRole("button", { name: "Tạo yêu cầu" }),
    ).toHaveCount(25);
    await pending.getByRole("button", { name: "Tiếp", exact: true }).click();
    await expect(
      pending.getByRole("button", { name: "Tạo yêu cầu" }),
    ).toHaveCount(1);
    await page
      .getByRole("textbox", { name: "Tìm phòng / công việc" })
      .fill("101");
    await expect(
      pending.getByRole("button", { name: "Tạo yêu cầu" }),
    ).toHaveCount(1);
    await page.screenshot({
      path: `${out}/${width}-pending-room.png`,
      fullPage: true,
    });
    const createBounds = await pending
      .getByRole("button", { name: "Tạo yêu cầu" })
      .boundingBox();
    if (!(createBounds && createBounds.x >= 0 && createBounds.x + createBounds.width <= width + 1)) {
      console.log(JSON.stringify({width,createBounds,cells:await pending.locator('td').evaluateAll(cells=>cells.map(cell=>({text:cell.innerText,rect:cell.getBoundingClientRect().toJSON(),display:getComputedStyle(cell).display,gridColumn:getComputedStyle(cell).gridColumn,gridRow:getComputedStyle(cell).gridRow}))) }));
    }
    assert(
      createBounds &&
        createBounds.x >= 0 &&
        createBounds.x + createBounds.width <= width + 1,
      "Create action must be visible without horizontal scrolling",
    );
    await pending.getByRole("button", { name: "Tạo yêu cầu" }).click();
    await expect(
      page.getByRole("textbox", { name: "Tiêu đề", exact: true }),
    ).toHaveValue("Dọn phòng 101");
    await expect(page.locator('select[name="departmentId"]')).toHaveCount(0);
    await expect(page.getByRole("combobox", {name: "Nhân viên dọn phòng"})).toBeVisible();
    await page.locator('select[name="assigneeUserId"]').selectOption("10");
    await page.screenshot({path: `${out}/${width}-create-on-shift.png`, fullPage: true});
    await page
      .getByRole("button", { name: "Tạo công việc", exact: true })
      .click();
    await expect.poll(() => created?.status).toBe("ASSIGNED");
    assigned = true;
    assert.equal(writes.length, 1);
    assert.equal(writes[0].body.roomId, 1);
    assert.equal(writes[0].body.roomRowVersion, "room-v1");
    assert.equal(writes[0].body.assigneeUserId, 10);
    assert.equal(writes[0].body.departmentId, null);
    await expect(page.locator("form.action-form")).toHaveCount(0);
    // A refresh removes the room from the creation list; the request is retained in its work queue.
    await page.goto(
      `${process.env.UX_BASE || "http://127.0.0.1:5186"}/#work?kind=CLEANING`,
    );
    await page.reload();
    await expect(pending.getByText("Phòng 101", { exact: true })).toHaveCount(
      0,
    );
    await page.getByRole("button", { name: /^Đang dọn/ }).click();
    await expect(page.getByText("Nhân viên A", { exact: true })).toBeVisible();
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await page.screenshot({
      path: `${out}/${width}-queue.png`,
      fullPage: true,
    });
    // Conflict does not report success or lose the create form.
    created = null;
    assigned = false;
    conflict = true;
    await page.goto(
      `${process.env.UX_BASE || "http://127.0.0.1:5186"}/#work?kind=CLEANING&create=1&room=1`,
    );
    await page.reload();
    await expect(
      page.getByRole("textbox", { name: "Tiêu đề", exact: true }),
    ).toHaveValue("Dọn phòng 101");
    await page.locator('select[name="assigneeUserId"]').selectOption("10");
    await page
      .getByRole("button", { name: "Tạo công việc", exact: true })
      .click();
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("textbox", { name: "Tiêu đề", exact: true }),
    ).toBeVisible();
    assert.equal(created, null);
    onShift = false;
    await page.reload();
    await expect(page.getByRole("button",{name:"Tạo công việc",exact:true})).toBeDisabled();
    await expect(page.getByText(/Bạn phải bắt đầu ca và đang trong giờ/)).toBeVisible();
    onShift = true; staffAvailable = false;
    await page.reload();
    await expect(page.getByText("Chưa có nhân viên dọn phòng đang trong ca tại chi nhánh này.")).toBeVisible();
    await expect(page.getByRole("button",{name:"Tạo công việc",exact:true})).toBeDisabled();
    checks.push({ width, passed: true });
    await context.unrouteAll({ behavior: "ignoreErrors" });
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify({ checks, errors }, null, 2),
  );
  console.log(
    "PASS pending rooms, atomic creation form, off-duty creator, empty staff, conflict and responsive layout at 1440/768/375 (mocked APIs)",
  );
} finally {
  await browser.close();
}
