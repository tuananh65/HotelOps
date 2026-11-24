import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const base = process.env.BASE_URL || "https://127.0.0.1:5082";
const out = "../artifacts/room-alert-popups";
fs.mkdirSync(out, { recursive: true });
const profile = {
  user: { fields: { userId: 1, fullName: "Lễ tân kiểm thử" } },
  branches: [{ branchId: 1, name: "Chi nhánh kiểm thử" }],
  roles: [{ code: "RECEPTION", name: "Lễ tân", branchId: 1 }],
  memberships: [], requiresPasswordChange: false,
  grants: ["ROOM_VIEW", "ROOM_CHECKIN", "ROOM_CHECKOUT", "ROOM_OCCUPANCY_MOVE"].map(code => ({ code, branchId: 1, dataScope: "BRANCH" })),
};
const rooms = [
  { roomId: 1, code: "101", operationalStatus: "RESERVED", readiness: "READY" },
  { roomId: 2, code: "102", operationalStatus: "READY", readiness: "BLOCKED", isBlocked: true },
  { roomId: 3, code: "103", operationalStatus: "READY", readiness: "READY" },
].map(r => ({ ...r, branchId: 1, canOperate: true, isOccupied: false, roomTypeId: 11, rowVersion: "fixture" }));
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
const errors = [], writes = [];
let failRooms = true, failTypes = true, rejectAction = true;
try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  await context.addInitScript(() => sessionStorage.setItem("hotelops.compact.session", "fixture"));
  await context.route("**/api/v2/**", async route => {
    const req = route.request(), path = new URL(req.url()).pathname.replace("/api/v2/", "");
    if (req.method() !== "GET") {
      writes.push(path);
      if (rejectAction) return route.fulfill({ status: 409, json: { message: "Phòng đã thay đổi. Vui lòng kiểm tra lại." } });
      rooms[0].operationalStatus = "READY";
      return route.fulfill({ status: 200, json: {} });
    }
    if ((path === "rooms" && failRooms) || (path === "catalog/room-types" && failTypes)) return route.abort();
    let json = { items: [], total: 0 };
    if (path === "auth/me") json = profile;
    if (path === "auth/profile") json = { fullName:"Lễ tân kiểm thử", username:"fixture", employeeCode:"EMP001", rowVersion:"fixture" };
    if (path === "rooms") json = rooms;
    if (path.endsWith("/history")) json = { events: [], blocks: [] };
    if (path === "catalog/room-types") json = { items: [{ fields: { roomTypeId: 11, name: "Standard", capacity: 2 } }], total: 1 };
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", error => errors.push(error.message));
  await page.goto(`${base}/#rooms`);
  await expect(page.locator(".alert-popup")).toHaveCount(2);
  async function assertLayout() {
    const result = await page.locator(".alert-popup").evaluateAll(nodes => nodes.map(n => {
      const r = n.getBoundingClientRect();
      return { x:r.x, y:r.y, right:r.right, bottom:r.bottom, position:getComputedStyle(n).position };
    }));
    const size = page.viewportSize();
    for (const r of result) {
      assert.equal(r.position, "fixed");
      assert.ok(r.x >= 0 && r.right <= size.width + 1 && r.y >= 0 && r.bottom <= size.height + 1, JSON.stringify(r));
    }
    const sorted = result.sort((a,b) => a.y-b.y);
    for(let i=1;i<sorted.length;i++) assert.ok(sorted[i].y >= sorted[i-1].bottom, "Alerts must not overlap");
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), "No horizontal overflow");
  }
  for(const width of [1440,375,320]) {
    await page.setViewportSize({ width, height: 812 });
    await page.waitForTimeout(100);
    await assertLayout();
    await page.screenshot({ path: `${out}/errors-${width}.png` });
  }
  await page.setViewportSize({ width:320, height:140 });
  await page.waitForTimeout(100);
  await assertLayout();
  await page.setViewportSize({ width:320, height:812 });
  const first = page.locator(".alert-popup").first();
  await first.getByRole("button", { name: "Thu gọn", exact: true }).click();
  await expect(first.getByRole("button", { name: "Mở ra", exact: true })).toHaveAttribute("aria-expanded", "false");
  await expect(first.locator(".alert-popup-body")).toBeHidden();
  await first.getByRole("button", { name: "Mở ra", exact: true }).focus();
  await page.keyboard.press("Enter");
  await expect(first.locator(".alert-popup-body")).toBeVisible();
  failRooms = false; failTypes = false;
  await first.getByRole("button", { name: "Thử lại", exact: true }).click();
  await expect(page.locator(".alert-popup")).toHaveCount(0);
  await expect(page.locator(".room-tile")).toHaveCount(3);
  await expect(page.getByRole("button", { name: /^Bảo trì\s+1$/ })).toBeVisible();
  await expect(page.getByText("Đang chặn", { exact:true })).toHaveCount(0);
  const tile = page.locator(".room-tile").filter({ has: page.locator("strong", { hasText: /^101$/ }) });
  await tile.click();
  const dialog = page.getByRole("dialog", { name: "Phòng 101" });
  const cancel = dialog.getByRole("button", { name: "Hủy đặt phòng", exact:true });
  const move = dialog.getByRole("button", { name: "Chuyển phòng", exact:true });
  const colors = await Promise.all([cancel,move].map(button => button.evaluate(el => getComputedStyle(el).backgroundColor)));
  assert.notEqual(colors[0], colors[1]);
  assert.match(await cancel.getAttribute("class"), /room-action-cancel/);
  assert.match(await move.getAttribute("class"), /room-action-move/);
  await cancel.click();
  await dialog.getByRole("button", { name: "Xác nhận", exact:true }).click();
  await expect(dialog.getByRole("alert")).toContainText("Dữ liệu đã thay đổi");
  await dialog.getByRole("button", { name: "Thu gọn", exact:true }).click();
  await dialog.getByRole("button", { name: "Mở ra", exact:true }).click();
  assert.equal(writes.length, 1, "Toggling popup must never submit the form");
  await dialog.getByRole("button", { name:"Đóng", exact:true }).click({trial:true});
  await assertLayout();
  await page.screenshot({ path: `${out}/dialog-320.png` });
  rejectAction = false;
  await dialog.getByRole("button", { name: "Xác nhận", exact:true }).click();
  await expect(dialog.locator(".badge")).toHaveText("Sẵn sàng");
  await expect(dialog.locator(".alert-popup")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole("status")).toContainText("Đã cập nhật phòng");
  await page.locator(".alert-popup.success").getByRole("button", {name:"Thu gọn",exact:true}).click();
  await expect(page.locator(".alert-popup.success .alert-popup-body")).toBeHidden();
  await page.getByRole("button", {name:"Thông tin cá nhân",exact:true}).click();
  const personal = page.getByRole("dialog", {name:"Thông tin cá nhân",exact:true});
  await personal.getByLabel("Họ tên",{exact:true}).fill("Tên kiểm thử mới");
  await personal.getByRole("button",{name:"Lưu thông tin",exact:true}).click();
  await expect(personal.locator(".alert-popup.success")).toContainText("Đã lưu thông tin cá nhân.");
  await personal.getByRole("button",{name:"Thu gọn",exact:true}).click();
  await expect(personal.locator(".alert-popup-body")).toBeHidden();
  await personal.getByRole("button",{name:"Mở ra",exact:true}).click();
  await expect(personal.getByRole("status")).toBeVisible();
  await personal.getByRole("button",{name:"Đóng",exact:true}).click();
  assert.deepEqual(errors, []);
  await context.close();

  const login = await browser.newContext({ viewport: { width: 320, height: 640 } });
  let logins = 0;
  await login.route("**/api/v2/**", async route => {
    if(route.request().method() !== "GET") logins++;
    await route.fulfill({status:401,json:{message:"Tên đăng nhập hoặc mật khẩu không đúng."}});
  });
  const loginPage = await login.newPage();
  await loginPage.goto(base);
  await loginPage.getByLabel("Tên đăng nhập",{exact:true}).fill("fixture");
  await loginPage.getByLabel("Mật khẩu",{exact:true}).fill("fixture-password");
  await loginPage.getByRole("button",{name:"Đăng nhập",exact:true}).click();
  await expect(loginPage.getByRole("alert")).toBeVisible();
  await loginPage.getByRole("button",{name:"Thu gọn",exact:true}).click();
  await loginPage.getByRole("button",{name:"Mở ra",exact:true}).click();
  assert.equal(logins,1);
  await login.close();
  fs.writeFileSync(`${out}/verification.json`,JSON.stringify({passed:true,fixtureOnly:true,primaryWrites:false,checks:["multiple alerts stack","collapse and keyboard reopen","retry","modal alert interaction","320px no overflow","login no accidental submit","maintenance label","room button colors"],colors,writes,errors},null,2));
  console.log("PASS popup stacking, collapse, retry, modal, login, mobile and room actions");
} finally { await browser.close(); }
