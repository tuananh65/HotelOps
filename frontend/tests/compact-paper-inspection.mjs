import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const out = "../artifacts/paper-inspection-ui";
fs.mkdirSync(out, { recursive: true });
const base = process.env.UX_BASE || "https://127.0.0.1:5082";
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
const writes = [], reads = [], errors = [];
let conflict = true;
let reviews = [], relatedWork = [];
const work = { workId: 7, workNo: "PRIVATE-WORK-CODE", branchId: 1, kind: "CLEANING", title: "Dọn phòng 101", status: "PENDING_INSPECTION", roomId: 1, completedAt: "2026-11-23T03:00:00Z", rowVersion: "v1", createdBy: 9 };
const grants = "CLEANING_VIEW CLEANING_ASSIGN INSPECTION_VIEW INSPECTION_PERFORM DEFECT_RESOLVE ROOM_VIEW ATTENDANCE_RECORD DASHBOARD_VIEW".split(" ");
try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, timezoneId: "Asia/Ho_Chi_Minh" });
  await context.addInitScript(() => sessionStorage.setItem("hotelops.compact.session", "fixture"));
  await context.route("**/api/v2/**", async route => {
    const req = route.request(), url = new URL(req.url()), path = url.pathname.replace("/api/v2/", "");
    let json = {};
    if (req.method() !== "GET") {
      assert.equal(path, "work/7/inspection-result");
      const body = req.postDataJSON(); writes.push(body);
      await new Promise(resolve => setTimeout(resolve, 150));
      if (conflict) { conflict = false; await route.fulfill({ status: 409, json: { title: "STALE_ROW_VERSION" } }); return; }
      work.status = body.result === "PASS" ? "COMPLETED" : "REWORK_REQUIRED"; work.rowVersion = "v2";
      json = { work: { fields: work }, inspection: { fields: { result: body.result } } };
    } else {
      reads.push(url.pathname + url.search);
      if (path === "auth/me") json = {
        user: { fields: { userId: 9, fullName: "Giám sát QA" } }, branches: [{ branchId: 1, name: "Bùi Viện" }],
        roles: [{ code: "HOUSEKEEPING_SUPERVISOR", name: "Giám sát buồng phòng", branchId: 1 }],
        memberships: [{ branchId: 1, departmentId: 1, kind: "EMPLOYMENT" }],
        grants: grants.map(code => ({ code, branchId: 1, dataScope: code === "ATTENDANCE_RECORD" ? "OWN" : "BRANCH" })), requiresPasswordChange: false,
      };
      else if (path === "work/7") json = { work: { fields: work }, assignments: [], assignmentDetails: [], checklist: [], reviews, events: [], attachments: [], context: { room: "101", floor: "Tầng 1", branch: "Bùi Viện" } };
      else if (path === "work/7/related") json = [{ fields: work }, ...relatedWork.map(fields => ({ fields }))];
      else if (path === "work/housekeeping-history") json = { date: url.searchParams.get("date") || "2026-11-23", timezone: "Asia/Ho_Chi_Minh", items: [{ ...work, room: "101", performer: "Nhân viên QA" }], total: 1, pageSize: 25 };
      else if (path === "work/cleaning-queue") json = { items: [work], counts: [{ status: work.status, count: 1 }], total: 1 };
      else if (path === "work/cleaning-rooms") json = { items: [], total: 0 };
      else if (path === "work/inspection-queue") json = { items: [{ ...work, room: "101" }], total: 1 };
      else if (path === "workspace/options") json = { rooms: [{ roomId: 1, code: "101" }], departments: [], references: [], checklists: [] };
      else if (path === "notifications") json = { items: [], hasMore: false };
      else if (path === "work") json = { items: [], total: 0 };
    }
    await route.fulfill({ json });
  });
  const page = await context.newPage();
  page.on("pageerror", e => errors.push(e.message));
  await page.goto(`${base}/#work?kind=CLEANING&id=7`);
  const drawer = page.locator("dialog.room-drawer");
  await expect(drawer).toBeVisible();
  await expect(drawer).not.toContainText("PRIVATE-WORK-CODE");
  await expect(drawer).not.toContainText("Mở phiếu đang xử lý");
  await expect(drawer.getByRole("button", { name: "Tạo phiếu kiểm tra" })).toHaveCount(0);
  await drawer.getByRole("button", { name: "Xác nhận hoàn thành", exact: true }).click();
  const modal = page.getByRole("dialog", { name: "Ghi kết quả kiểm tra phòng" });
  await expect(modal).toContainText("checklist giấy");
  await modal.getByRole("button", { name: "Lưu kết quả" }).click();
  await expect(modal.getByRole("alert")).toContainText("Dữ liệu đã thay đổi");
  assert.equal(work.status, "PENDING_INSPECTION");
  await modal.getByLabel("Kết quả", { exact: true }).selectOption("FAIL");
  await expect(modal.locator("textarea")).toHaveAttribute("required", "");
  await modal.getByLabel("Kết quả", { exact: true }).selectOption("PASS");
  await expect(modal.locator("textarea")).not.toHaveAttribute("required", "");
  await modal.getByRole("button", { name: "Lưu kết quả" }).click();
  await expect(modal).toHaveCount(0);
  await expect(drawer).toContainText("Hoàn thành");
  assert.equal(writes.length, 2); assert.equal(writes[1].note, null);
  assert.equal(writes[0].operationKey, writes[1].operationKey);
  await page.keyboard.press("Escape");
  await page.getByRole("link", { name: "Lịch sử vệ sinh", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Lịch sử vệ sinh" })).toBeVisible();
  await page.goto(`${base}/#staff`);
  await expect(page.getByRole("heading", { name: "Lịch sử vệ sinh" })).toBeVisible();
  await expect(page.locator('nav button[aria-current="page"]')).toHaveAttribute('aria-label','Lịch sử vệ sinh');
  await expect(page.getByText('Nhân viên & Ca trực',{exact:true})).toHaveCount(0);
  await expect(page.getByRole("table")).toContainText("Nhân viên QA");
  await page.getByRole("button", { name: "Ngày trước", exact: true }).click();
  await expect.poll(() => reads.some(r => r.includes("housekeeping-history") && r.includes("date=2026-11-22"))).toBe(true);
  await page.getByRole("button", { name: "Ngày sau", exact: true }).click();
  await expect(page.getByLabel("Ngày hoàn thành")).toHaveValue("2026-11-23");
  for (const width of [1440, 768, 375, 320]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.screenshot({ path: `${out}/history-${width}.png`, fullPage: true });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  }
  work.status = "REWORK_REQUIRED"; work.rowVersion = "v3";
  reviews = [{ workId: 8, result: "FAIL", status: "FAILED", verifiedAt: "2026-11-23T04:00:00Z", findings: [{ description: "Sàn chưa sạch", status: "OPEN" }] }];
  relatedWork = [{ workId: 9, kind: "REWORK", title: "Dọn lại phòng 101", status: "PENDING_REINSPECTION", completedAt: "2026-11-23T05:00:00Z" }];
  await page.goto(`${base}/#work?kind=CLEANING&id=7`);
  await drawer.getByRole("button", { name: "Xác nhận hoàn thành", exact: true }).click();
  await expect(modal).toContainText("Sàn chưa sạch");
  await modal.getByRole("checkbox").check();
  await modal.locator("textarea").fill("Đã kiểm tra lại, sàn sạch và khô.");
  await page.screenshot({ path: `${out}/reinspection-mobile.png`, fullPage: true });
  await modal.getByRole("button", { name: "Lưu kết quả" }).click();
  await expect(modal).toHaveCount(0);
  assert.equal(writes.length, 3); assert.equal(writes[2].resolveFindings, true);
  assert.deepEqual(errors, []);
  fs.writeFileSync(`${out}/verification.json`, JSON.stringify({ passed: true, writes: writes.length, errors, checks: ["paper result", "conflict", "required fail note", "reinspection confirmation", "no legacy creation or codes", "local date navigation", "responsive history"] }, null, 2));
  console.log("PASS: paper inspection UI, conflicts, notes, daily history, responsive layouts");
} finally { await browser.close(); }
