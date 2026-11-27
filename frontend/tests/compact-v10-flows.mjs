import { chromium, expect } from "@playwright/test";
import fs from "node:fs";
const base = "http://127.0.0.1:5179",
  apiBase = "http://127.0.0.1:5770/api/v2",
  out = "../artifacts/actor-lists";
const creds = JSON.parse(
  fs
    .readFileSync(
      "../artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json",
      "utf8",
    )
    .replace(/^\uFEFF/, ""),
);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const results = [],
  errors = [],
  sessions = new Map();
let page, context;
async function call(
  user,
  path,
  body,
  method = body === undefined ? "GET" : "POST",
  expected = 200,
) {
  if (!sessions.has(user)) {
    let r = await fetch(apiBase + "/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: user, password: creds.password }),
    });
    if (r.status === 429) {
      await new Promise((r) => setTimeout(r, 61000));
      r = await fetch(apiBase + "/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username: user, password: creds.password }),
      });
    }
    expect(r.status, "login " + user).toBe(200);
    const login = await r.json();
    sessions.set(user, login.accessToken);
  }
  const r = await fetch(apiBase + "/" + path, {
    method,
    headers: {
      Authorization: "Bearer " + sessions.get(user),
      "Content-Type": "application/json",
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await r.json().catch(() => null);
  expect(r.status, `${user} ${method} ${path}: ${data?.title || ""}`).toBe(
    expected,
  );
  return data;
}
async function login(user, width = 1440) {
  await call(user, "auth/me");
  if (context) await context.close();
  context = await browser.newContext({
    viewport: { width, height: 850 },
    reducedMotion: "reduce",
  });
  await context.addInitScript(
    (token) => sessionStorage.setItem("hotelops.compact.session", token),
    sessions.get(user),
  );
  page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(base);
  await ready();
}
async function ready() {
  await page.locator("main h1").waitFor();
  await page.waitForLoadState("networkidle");
  await expect(page.locator("main [role=alert]")).toHaveCount(0);
}
async function go(hash) {
  await page.evaluate((h) => (location.hash = h), hash);
  await ready();
}
const dialog = () => page.locator("dialog[open]");
async function close() {
  await dialog().getByRole("button", { name: "Đóng", exact: true }).click();
  await expect(dialog()).toHaveCount(0);
}
async function save() {
  await expect(
    dialog().getByRole("button", { name: "Lưu thay đổi", exact: true }),
  ).toBeEnabled();
  const response = page.waitForResponse(
    (r) =>
      r.url().includes("/catalog/") &&
      ["POST", "PUT"].includes(r.request().method()),
  );
  await dialog()
    .getByRole("button", { name: "Lưu thay đổi", exact: true })
    .click();
  expect((await response).status()).toBe(200);
  await expect(dialog()).toHaveCount(0);
  await ready();
}
async function check(name, fn) {
  try {
    await fn();
    results.push({ name, pass: true });
    console.log("PASS " + name);
  } catch (e) {
    results.push({ name, pass: false, error: e.message });
    if (page)
      await page.screenshot({ path: out + "/v10-failure.png", fullPage: true });
    throw e;
  }
}
const stamp = Date.now().toString().slice(-8),
  act = (row, extra = {}) => ({
    rowVersion: row.rowVersion,
    operationKey: crypto.randomUUID(),
    ...extra,
  });
let branch, roomId, cleaningId, inspectionId, admin, profile;
try {
  profile = await call("demo_admin", "auth/me");
  branch =
    profile.branches.find((b) => b.code === "DEMO-SG")?.branchId ||
    profile.branches[0].branchId;
  await check(
    "Admin overview and branch create/edit/deactivate/reactivate through UI",
    async () => {
      await login("demo_admin");
      await expect(
        page.getByRole("heading", { name: "Tổng quan quản trị" }),
      ).toBeVisible();
      await go("catalog?resource=branches");
      await page
        .getByRole("button", { name: "+ Thêm chi nhánh", exact: true })
        .click();
      await dialog()
        .getByLabel("Mã", { exact: true })
        .fill("UX" + stamp);
      await dialog()
        .getByLabel("Tên", { exact: true })
        .fill("Chi nhánh UX " + stamp);
      await save();
      const row = () =>
        page.locator("tbody tr").filter({ hasText: "UX" + stamp });
      await row()
        .getByRole("button", { name: "Chỉnh sửa", exact: true })
        .click();
      await dialog()
        .getByLabel("Tên", { exact: true })
        .fill("Chi nhánh UX cập nhật " + stamp);
      await save();
      await row()
        .getByRole("button", { name: "Chỉnh sửa", exact: true })
        .click();
      await dialog().getByLabel("Đang hoạt động", { exact: true }).uncheck();
      await save();
      await expect(row()).toContainText("Đã ngừng");
      await row()
        .getByRole("button", { name: "Chỉnh sửa", exact: true })
        .click();
      await dialog().getByLabel("Đang hoạt động", { exact: true }).check();
      await save();
      await expect(row()).toContainText("Hoạt động");
      await page.screenshot({
        path: out + "/admin-branches.png",
        fullPage: true,
      });
    },
  );
  await check(
    "Admin branch-to-floor-to-room flow creates a dirty room",
    async () => {
      await page
        .locator("tbody tr")
        .filter({ hasText: "DEMO-SG" })
        .getByRole("link", { name: "Mở tầng / phòng" })
        .click();
      await ready();
      await page
        .getByRole("button", { name: "+ Thêm tầng / khu vực", exact: true })
        .click();
      await dialog()
        .getByLabel("Mã", { exact: true })
        .fill("F" + stamp);
      await dialog()
        .getByLabel("Tên", { exact: true })
        .fill("Tầng kiểm thử " + stamp);
      await dialog().getByLabel("Số tầng", { exact: true }).fill("9");
      await save();
      await page
        .locator("tbody tr")
        .filter({ hasText: "F" + stamp })
        .getByRole("button", { name: "Xem phòng" })
        .click();
      await ready();
      await page
        .getByRole("button", { name: "+ Thêm danh mục phòng", exact: true })
        .click();
      await dialog()
        .getByLabel("Mã", { exact: true })
        .fill("R" + stamp);
      await dialog()
        .locator("select[name=roomTypeId] option")
        .nth(1)
        .waitFor({ state: "attached" });
      await dialog()
        .locator("select[name=roomTypeId]")
        .selectOption({ index: 1 });
      await save();
      const rooms = await call("demo_reception", `rooms?branchId=${branch}`),
        room = rooms.find((r) => r.code === "R" + stamp);
      expect(room.readiness).not.toBe("READY");
      roomId = room.roomId;
    },
  );
  await check(
    "Catalog validation, stale version, permissions and cross-branch isolation",
    async () => {
      await call(
        "demo_admin",
        "catalog/branches",
        {
          fields: {
            code: "BAD" + stamp,
            name: "Invalid zone",
            timezone: "Invalid/Nowhere",
          },
        },
        "POST",
        400,
      );
      const branches = await call(
          "demo_admin",
          `catalog/branches?branchId=${branch}`,
        ),
        b = branches.items.find((r) => r.fields.code === "UX" + stamp).fields;
      const updated = await call(
        "demo_admin",
        `catalog/branches/${b.branchId}`,
        { rowVersion: b.rowVersion, fields: { name: b.name + " v2" } },
        "PUT",
      );
      await call(
        "demo_admin",
        `catalog/branches/${b.branchId}`,
        { rowVersion: b.rowVersion, fields: { name: "stale" } },
        "PUT",
        409,
      );
      await call("demo_hk1", "insights/administration", undefined, "GET", 403);
      await call(
        "demo_hk1",
        `insights/stock?branchId=${branch}`,
        undefined,
        "GET",
        403,
      );
      await call(
        "demo_owner",
        `catalog/branches/${b.branchId}`,
        {
          rowVersion: updated.fields.rowVersion,
          fields: { name: "forbidden" },
        },
        "PUT",
        403,
      );
      const foreign = profile.branches.find(
        (b) => b.branchId !== branch,
      ).branchId;
      await call(
        "demo_store",
        `insights/stock?branchId=${foreign}`,
        undefined,
        "GET",
        403,
      );
      await call(
        "demo_store",
        `catalog/branches`,
        { fields: { code: "FORBIDDEN", name: "No" } },
        "POST",
        403,
      );
    },
  );
  await check(
    "Storekeeper can manage approved shared items but cannot edit global reference settings",
    async () => {
      await login("demo_store");
      await go("catalog?resource=items");
      await page
        .getByRole("button", { name: "+ Thêm vật tư", exact: true })
        .click();
      await dialog()
        .getByLabel("Mã", { exact: true })
        .fill("I" + stamp);
      await dialog()
        .getByLabel("Tên", { exact: true })
        .fill("Vật tư UX " + stamp);
      await dialog()
        .locator("select[name=baseUnitId] option")
        .nth(1)
        .waitFor({ state: "attached" });
      await dialog()
        .locator("select[name=baseUnitId]")
        .selectOption({ index: 1 });
      await save();
      const row = page.locator("tbody tr").filter({ hasText: "I" + stamp });
      await row.getByRole("button", { name: "Chỉnh sửa" }).click();
      await dialog()
        .getByLabel("Tên", { exact: true })
        .fill("Vật tư dùng chung " + stamp);
      await save();
      await call(
        "demo_store",
        `catalog/references?branchId=${branch}`,
        { fields: { kind: "UOM", code: "NO", name: "No" } },
        "POST",
        403,
      );
      await go("stock");
      await expect(page.locator("thead")).toContainText("Tồn cơ sở");
    },
  );
  await check(
    "Notification inbox handles concurrent reads without deadlocks",
    async () => {
      await call("demo_manager", "auth/me");
      await Promise.all(
        Array.from({ length: 12 }, () => call("demo_manager", "notifications")),
      );
    },
  );
  await check(
    "Housekeeping V10 queue offers correct contextual steps and linked inspection",
    async () => {
      const work = await call("demo_hks", "work", {
        branchId: branch,
        kind: "CLEANING",
        title: "V10 cleaning " + stamp,
        roomId,
      });
      cleaningId = work.fields.workId;
      await login("demo_hks");
      await go("work?kind=CLEANING");
      await expect(page.locator("h1")).toHaveText("Điều phối buồng phòng");
      await expect(page.locator("thead th")).toHaveText([
        "Phòng",
        "Người dọn",
        "Trạng thái",
        "Thao tác",
      ]);
      const row = page
        .locator("tbody tr")
        .filter({ hasText: "V10 cleaning " + stamp });
      await row.getByRole("button", { name: "Phân công", exact: true }).click();
      await dialog()
        .locator("select[name=userId] option")
        .nth(1)
        .waitFor({ state: "attached" });
      const candidates = await call(
          "demo_hks",
          `work/candidates?branchId=${branch}&kind=CLEANING`,
        ),
        worker = candidates.find((u) => u.employeeCode === "DEMO_HK1");
      await dialog()
        .locator("select[name=userId]")
        .selectOption(String(worker.userId));
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await expect(dialog().locator(".action-form")).toHaveCount(0);
      await close();
      const detail = await call("demo_hk1", `work/${cleaningId}`);
      await call(
        "demo_hk1",
        `work/${cleaningId}/actions/accept`,
        act(detail.work.fields),
      );
      const running = await call(
        "demo_hk1",
        `insights/time?branchId=${branch}`,
      );
      for (const t of running.filter((t) => !t.fields.closedAt)) {
        const d = await call("demo_hk1", `work/${t.fields.workId}`);
        await call(
          "demo_hk1",
          `work/${t.fields.workId}/actions/pause`,
          act(d.work.fields, { reason: "Tạm dừng để kiểm thử V10" }),
        );
      }
      let d = await call("demo_hk1", `work/${cleaningId}`);
      await call(
        "demo_hk1",
        `work/${cleaningId}/actions/start`,
        act(d.work.fields),
      );
      d = await call("demo_hk1", `work/${cleaningId}`);
      await call(
        "demo_hk1",
        `work/${cleaningId}/actions/complete`,
        act(d.work.fields),
      );
      await page.reload();
      await ready();
      await page.getByRole("button", { name: /^Chờ kiểm tra / }).click();
      await row.getByRole("button", { name: "Kiểm tra", exact: true }).click();
      await dialog()
        .getByRole("button", { name: "Tạo phiếu kiểm tra", exact: true })
        .click();
      await dialog()
        .locator("select[name=checklistTemplateId] option")
        .nth(1)
        .waitFor({ state: "attached" });
      await dialog()
        .locator("select[name=checklistTemplateId]")
        .selectOption({ index: 1 });
      const response = page.waitForResponse(
        (r) => r.request().method() === "POST" && r.url().endsWith("/work"),
      );
      await dialog().getByRole("button", { name: "Tạo công việc" }).click();
      const created = await response;
      expect(created.status()).toBe(200);
      inspectionId = (await created.json()).fields.workId;
      await expect(dialog()).toContainText("V10 cleaning " + stamp);
      await page.screenshot({
        path: out + "/hks-inspection.png",
        fullPage: true,
      });
    },
  );
  await check(
    "Inspection fail, linked rework, resolve finding and independent reinspection release the room",
    async () => {
      const hks = (await call("demo_hks", "auth/me")).user.fields.userId;
      async function actOn(user, id, command, extra = {}) {
        const d = await call(user, `work/${id}`);
        return call(
          user,
          `work/${id}/actions/${command}`,
          act(d.work.fields, extra),
        );
      }
      await actOn("demo_hks", inspectionId, "assign", { userId: hks });
      await actOn("demo_hks", inspectionId, "accept");
      await actOn("demo_hks", inspectionId, "start");
      let detail = await call("demo_hks", `work/${inspectionId}`);
      for (const c of detail.checklist)
        await call(
          "demo_hks",
          `work/${inspectionId}/checklist/${c.fields.workChecklistItemId}`,
          {
            rowVersion: c.fields.rowVersion,
            outcome: "FAIL",
            note: "V10 requires rework",
          },
          "PUT",
        );
      detail = await call("demo_hks", `work/${inspectionId}`);
      const finding = await call("demo_hks", `work/${inspectionId}/findings`, {
        rowVersion: detail.work.fields.rowVersion,
        description: "Gương chưa sạch, cần dọn lại",
      });
      await actOn("demo_hks", inspectionId, "complete");
      await actOn("demo_hks", inspectionId, "fail", {
        reason: "Phòng chưa đạt chất lượng kiểm tra",
      });
      await go(`work?kind=CLEANING&id=${cleaningId}`);
      await dialog()
        .getByRole("button", { name: "Tạo việc dọn lại", exact: true })
        .click();
      const created = page.waitForResponse(
        (r) => r.request().method() === "POST" && r.url().endsWith("/work"),
      );
      await dialog()
        .getByRole("button", { name: "Tạo công việc", exact: true })
        .click();
      const reworkResponse = await created;
      expect(reworkResponse.status()).toBe(200);
      const rework = (await reworkResponse.json()).fields;
      const hk = (await call("demo_hk1", "auth/me")).user.fields.userId;
      await actOn("demo_hks", rework.workId, "assign", { userId: hk });
      await actOn("demo_hk1", rework.workId, "accept");
      await actOn("demo_hk1", rework.workId, "start");
      await actOn("demo_hk1", rework.workId, "complete");
      await go(`work?kind=INSPECTION&id=${inspectionId}`);
      await dialog()
        .getByLabel("Công việc đã dọn lại")
        .selectOption(String(rework.workId));
      await dialog()
        .getByLabel("Kết quả xác minh")
        .fill("Đã xác minh gương được làm sạch đúng chuẩn");
      await dialog()
        .getByRole("button", { name: "Xác nhận lỗi đã xử lý", exact: true })
        .click();
      await expect(
        dialog().getByText("Đã xử lý", { exact: true }),
      ).toBeVisible();
      await go(`work?kind=CLEANING&id=${cleaningId}`);
      await dialog()
        .getByRole("button", { name: "Tạo phiếu kiểm tra", exact: true })
        .click();
      await dialog()
        .locator("select[name=checklistTemplateId] option")
        .nth(1)
        .waitFor({ state: "attached" });
      await dialog()
        .locator("select[name=checklistTemplateId]")
        .selectOption({ index: 1 });
      const secondResponse = page.waitForResponse(
        (r) => r.request().method() === "POST" && r.url().endsWith("/work"),
      );
      await dialog()
        .getByRole("button", { name: "Tạo công việc", exact: true })
        .click();
      const second = (await (await secondResponse).json()).fields;
      await actOn("demo_hks", second.workId, "assign", { userId: hks });
      await actOn("demo_hks", second.workId, "accept");
      await actOn("demo_hks", second.workId, "start");
      detail = await call("demo_hks", `work/${second.workId}`);
      for (const c of detail.checklist)
        await call(
          "demo_hks",
          `work/${second.workId}/checklist/${c.fields.workChecklistItemId}`,
          { rowVersion: c.fields.rowVersion, outcome: "PASS" },
          "PUT",
        );
      await actOn("demo_hks", second.workId, "complete");
      await actOn("demo_hks", second.workId, "pass");
      const room = (
        await call("demo_reception", `rooms?branchId=${branch}`)
      ).find((r) => r.roomId === roomId);
      expect(room.readiness).toBe("READY");
    },
  );
  await check(
    "New supervisor shift activates automatically and only its owner can end it",
    async () => {
      const week = await call(
        "demo_head",
        `workday?branchId=${branch}&date=${new Date().toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" })}`,
      );
      const shift = week.shifts.find(
          (s) => s.role === "Giám sát buồng phòng" && s.startMinute === 960,
        ),
        who = (await call("demo_hks", "auth/me")).user.fields.userId;
      expect(shift.capacity).toBe(1);
      const assigned = await call("demo_head", "workday/assignments", {
        shiftId: shift.shiftId,
        userId: who,
        workDate: week.today,
        rowVersion: shift.rowVersion,
        operationKey: crypto.randomUUID(),
      });
      const current = await call("demo_hks", `workday?branchId=${branch}&date=${week.today}`);
      const started = current.assignments.find(a => a.assignmentId === assigned.fields.assignmentId);
      expect(started.status).toBe("WORKING");
      await call(
        "demo_hk1",
        `workday/assignments/${assigned.fields.assignmentId}/end`,
        act(started),
        "POST",
        403,
      );
      await call(
        "demo_hks",
        `workday/assignments/${assigned.fields.assignmentId}/end`,
        act(started),
      );
      await login("demo_hks");
      await go("workday");
      await expect(page.locator("main")).toContainText("Đã kết thúc");
    },
  );
  await check(
    "Approved branch managers can create and route requests across departments in their own branch",
    async () => {
      const options = await call(
          "demo_om",
          `workspace/options?branchId=${branch}`,
        ),
        dept = options.departments[1].departmentId;
      const request = (
        await call("demo_om", "work", {
          branchId: branch,
          kind: "INTERNAL_REQUEST",
          title: "V10 cross-department " + stamp,
          requesterDepartmentId: dept,
          departmentId: dept,
        })
      ).fields;
      const submitted = (
        await call(
          "demo_om",
          `work/${request.workId}/actions/submit`,
          act(request),
        )
      ).fields;
      await call(
        "demo_head",
        `work/${request.workId}/actions/route`,
        act(submitted, { relatedId: options.departments[2].departmentId }),
      );
      const other = profile.branches.find(
        (b) => b.branchId !== branch,
      ).branchId;
      await call(
        "demo_om",
        "work",
        {
          branchId: other,
          kind: "INTERNAL_REQUEST",
          title: "Forbidden cross-branch",
          requesterDepartmentId: dept,
        },
        "POST",
        403,
      );
    },
  );
  await check(
    "Sticky headers track inner, page and horizontal scrolling at desktop/mobile",
    async () => {
      for (const width of [1440, 390]) {
        await login("demo_admin", width);
        await go("users");
        await page.locator("tbody tr").first().waitFor();
        const geometry = await page.evaluate(async () => {
          const box = document.querySelector(".table-scroll"),
            body = box.querySelector("tbody"),
            row = body.firstElementChild;
          // Layout stress only: cloned DOM rows, no fabricated API/database records.
          for (let i = 0; i < 60; i++) body.appendChild(row.cloneNode(true));
          const spacer = document.createElement("div");
          spacer.style.height = "1500px";
          document.querySelector("main").appendChild(spacer);
          box.scrollTop = 300;
          box.scrollLeft = 120;
          await new Promise((r) => setTimeout(r, 100));
          const inner =
            box.querySelector("th").getBoundingClientRect().top -
            box.getBoundingClientRect().top;
          scrollTo(0, box.getBoundingClientRect().top + scrollY + 80);
          await new Promise((r) => setTimeout(r, 100));
          const header = box.querySelector("th").getBoundingClientRect(),
            cell =
              body.firstElementChild.firstElementChild.getBoundingClientRect();
          return {
            inner,
            top: header.top,
            alignment: header.left - cell.left,
            overflow: document.documentElement.scrollWidth - innerWidth,
          };
        });
        expect(Math.abs(geometry.inner)).toBeLessThanOrEqual(2);
        expect(Math.abs(geometry.top)).toBeLessThanOrEqual(2);
        expect(Math.abs(geometry.alignment)).toBeLessThanOrEqual(1);
        expect(geometry.overflow).toBeLessThanOrEqual(1);
        results.push({
          name: "Scroll geometry " + width,
          pass: true,
          geometry,
        });
        await page.screenshot({ path: out + "/sticky-" + width + ".png" });
      }
    },
  );
  expect(errors).toEqual([]);
} finally {
  fs.writeFileSync(
    out + "/v10-flows.json",
    JSON.stringify({ results, errors }, null, 2),
  );
  await browser.close();
}
