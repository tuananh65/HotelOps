import { chromium, expect } from "@playwright/test";
import assert from "node:assert/strict";
let input = ""; for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
const checks = []; let browser, stage = "login";
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(30000);
  const origin = "https://localhost:7143";
  await page.goto(origin + "/inspection-history"); await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/inspection-history");
  await page.getByLabel("Branch ID", { exact: true }).fill(String(fixture.branch));
  await page.getByRole("button", { name: "View inspection history", exact: true }).click();
  async function get(query, status = 200, detail = false) {
    const path = detail ? `/api/inspection-history/${fixture.inspection}?branchId=${fixture.branch}`
      : "/api/inspection-history?" + new URLSearchParams({ branchId: String(fixture.branch), ...query });
    const response = await page.evaluate(async path => { const r = await fetch(path, { credentials: "include", cache: "no-store", redirect: "error" }); return { status: r.status, body: await r.json() }; }, path);
    assert.equal(response.status, status, path); checks.push({ path, status }); return response.body;
  }
  if (fixture.mode === "revoked") {
    await page.getByRole("alert").filter({ hasText: "unavailable or access has changed" }).waitFor();
    await get({}, 403); await get({}, 403, true);
    assert.equal(await page.getByRole("table").count(), 0);
    checks.push("revoked branch denies list and detail, workspace hidden");
  } else {
    stage = "scoped list and combined filters";
    await page.getByRole("table", { name: "Lịch sử Inspection" }).waitFor();
    const list = await get({}); assert.ok(list.total >= 2);
    assert.ok(list.items.every(row => row.branchId === fixture.branch));
    for (let index = 1; index < list.items.length; ++index) {
      const a = list.items[index - 1], b = list.items[index];
      assert.ok(a.at > b.at || a.at === b.at && a.id > b.id, "deterministic newest-first ordering");
    }
    const detail = await get({}, 200, true), selected = detail.inspection;
    assert.equal(selected.id, fixture.inspection); assert.equal(selected.result, "FAIL");
    assert.equal(selected.defectCount, detail.defects.length); assert.equal(selected.reinspectionCount, 1);
    assert.equal(selected.cycleFailedInspectionCount, 1);
    assert.ok(list.items.every(row => row.reinspectionCount === 1 && row.cycleFailedInspectionCount === 1));
    checks.push("per-inspection defect count and explicit cycle reinspection/failure counts");
    assert.ok(detail.criteria.length && detail.defects.length && detail.reworks.length && detail.cycleInspections.length >= 2);
    assert.ok(detail.cycleInspections.some(row => row.result === "PASS"));
    assert.equal(detail.completedBy.userId, fixture.cleaner);
    assert.ok(detail.housekeepers.some(row => row.userId === fixture.actor));
    const time = /Z$|[+-]\d\d:\d\d$/.test(selected.at) ? selected.at : selected.at + "Z";
    const combined = await get({ roomId: fixture.room, inspectorId: fixture.actor, housekeeperId: fixture.cleaner,
      result: "FAIL", search: selected.number, from: time, to: time });
    assert.deepEqual(combined.items.map(row => row.id), [fixture.inspection]);
    assert.equal((await get({ housekeeperId: fixture.actor })).total, 0, "cancelled assignee is not actual cleaner");
    assert.equal((await get({ roomId: 2147483647 })).total, 0);
    assert.equal((await get({ inspectorId: fixture.cleaner })).total, 0);
    assert.equal((await get({ search: "%not-a-wildcard%" })).total, 0);
    await get({ branchId: 2147483647 }, 403);
    for (const invalid of [{ page: 0 }, { pageSize: 101 }, { page: 2147483647 }, { result: "INVENTED" },
      { from: "2026-11-15T00:00:00Z", to: "2026-11-14T00:00:00Z" }]) await get(invalid, 400);
    checks.push("historical criteria/defects/photos/reworks/reinspection and original cleaner preserved");
    stage = "browser pagination";
    async function apply() {
      const response = page.waitForResponse(r => r.url().includes("/api/inspection-history?") && r.request().method() === "GET");
      await page.getByRole("button", { name: "Áp dụng bộ lọc", exact: true }).click();
      await (await response).finished(); await expect(page.getByText("Đang tải danh sách…", { exact: true })).toHaveCount(0);
    }
    await page.getByLabel("Số dòng", { exact: true }).selectOption("1"); await apply();
    const table = page.getByRole("table", { name: "Lịch sử Inspection" });
    await expect(table.locator("tbody tr")).toHaveCount(1); const first = await table.locator("tbody tr").innerText();
    const next = page.waitForResponse(r => r.url().includes("/api/inspection-history?") && r.url().includes("page=2"));
    await page.getByRole("button", { name: "Trang sau", exact: true }).click(); await (await next).finished();
    await expect(page.getByText("Đang tải danh sách…", { exact: true })).toHaveCount(0);
    assert.notEqual(await table.locator("tbody tr").innerText(), first); checks.push("browser page 1 and 2 distinct; total retained");
    stage = "browser filters and empty recovery";
    await page.getByLabel("Tìm kiếm", { exact: true }).fill("not-an-inspection"); await apply();
    await page.getByText("MSG10 — Không có lịch sử khớp bộ lọc đã áp dụng.", { exact: true }).waitFor();
    assert.equal(await page.getByLabel("Tìm kiếm", { exact: true }).inputValue(), "not-an-inspection");
    await page.getByLabel("Tìm kiếm", { exact: true }).fill(selected.number);
    await page.getByLabel("ID phòng", { exact: true }).fill(String(fixture.room));
    await page.getByLabel("ID người kiểm tra", { exact: true }).fill(String(fixture.actor));
    await page.getByLabel("ID nhân viên dọn phòng", { exact: true }).fill(String(fixture.cleaner));
    await page.getByLabel("Từ (UTC)", { exact: true }).fill(time);
    await page.getByLabel("Đến (UTC)", { exact: true }).fill(time);
    await page.getByLabel("Kết quả", { exact: true }).selectOption("FAIL"); await apply();
    await expect(table.locator("tbody tr")).toHaveCount(1); await table.getByRole("button", { name: selected.number, exact: true }).click();
    await page.getByRole("heading", { name: selected.number, exact: true }).waitFor();
    await page.getByRole("heading", { name: "Người hoàn tất dọn phòng", exact: true }).waitFor();
    checks.push("all browser filters, MSG10 and recovery");
    stage = "private evidence and reinspection navigation";
    assert.ok(detail.evidence.length);
    const download = page.waitForEvent("download");
    await page.getByRole("button", { name: `Tải evidence #${detail.evidence[0].id}`, exact: true }).click();
    const file = await download; assert.equal(await file.failure(), null);
    const passed = detail.cycleInspections.find(row => row.result === "PASS");
    await page.getByRole("button", { name: `Xem lịch sử #${passed.id}`, exact: true }).click();
    await page.getByRole("heading", { name: passed.number, exact: true }).waitFor();
    assert.equal(await page.getByRole("button", { name: /^(Approve|Reject|Confirm conclusion)$/ }).count(), 0);
    await page.goto(origin + `/inspection-history/${fixture.inspection}?branchId=${fixture.branch}`);
    await page.getByRole("heading", { name: selected.number, exact: true }).waitFor(); await page.reload();
    await page.getByRole("heading", { name: selected.number, exact: true }).waitFor();
    checks.push("private download, historical Passed navigation and original Failed deep-link reload");
  }
  // End this browser's real cookie session; keep business rows unchanged.
  await page.goto(origin + "/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL(origin + "/login");
  process.stdout.write(JSON.stringify({ success: true, mode: fixture.mode, checks }));
} catch (error) {
  process.stdout.write(JSON.stringify({ success: false, mode: fixture.mode, stage, checks, error: String(error.message).split("\n")[0] })); process.exitCode = 1;
} finally { await browser?.close(); }
