import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const out = "../artifacts/reception-rooms";
fs.mkdirSync(out, { recursive: true });
const rooms = [
  { roomId: 1, code: "101", readiness: "READY", isOccupied: false },
  { roomId: 2, code: "102", readiness: "READY", isOccupied: true },
  { roomId: 3, code: "103", readiness: "PENDING_CLEANING", isOccupied: false },
  { roomId: 4, code: "104", readiness: "BLOCKED", isOccupied: false },
].map((r) => ({
  ...r,
  branchId: 1,
  roomTypeId: 11,
  rowVersion: "fixture",
  operationalStatus: r.isOccupied ? "OCCUPIED" : "READY",
}));
const profile = {
  user: { fields: { userId: 1, fullName: "Lê Thu Hà" } },
  branches: [{ branchId: 1, name: "Chi nhánh Bùi Viện" }],
  roles: [{ code: "RECEPTION", name: "Lễ tân", branchId: 1 }],
  memberships: [],
  requiresPasswordChange: false,
  grants: [
    "ROOM_VIEW",
    "ROOM_CHECKIN",
    "ROOM_CHECKOUT",
    "ROOM_OCCUPANCY_MOVE",
    "INCIDENT_CREATE",
    "DASHBOARD_VIEW",
  ].map((code) => ({ code, branchId: 1, dataScope: "BRANCH" })),
};
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  writes = [];
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
    let json = { items: [], total: 0, page: 1, pageSize: 100 };
    if (req.method() !== "GET") {
      writes.push({ path, body: req.postDataJSON() });
      const room = rooms.find((r) => r.roomId === Number(path.split("/")[1]));
      if (path.endsWith("check-out"))
        Object.assign(room, {
          isOccupied: false,
          readiness: "PENDING_CLEANING",
          operationalStatus: "DIRTY",
        });
      if (path.endsWith("check-in"))
        Object.assign(room, {
          isOccupied: true,
          operationalStatus: "OCCUPIED",
        });
      json = {};
    } else if (path === "auth/me") json = profile;
    else if (path === "rooms") json = rooms;
    else if (path.endsWith("/history")) json = { events: [], blocks: [] };
    else if (path === "catalog/room-types") {
      assert.equal(url.searchParams.get("branchId"), "1");
      json = {
        items: [{ fields: { roomTypeId: 11, name: "Standard", capacity: 2 } }],
        total: 1,
        page: 1,
        pageSize: 100,
      };
    }
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("http://127.0.0.1:5186/#rooms");
  const occupied = page
    .locator(".room-tile")
    .filter({ has: page.locator("strong", { hasText: /^102$/ }) });
  await expect(occupied).toBeVisible();
  await expect(occupied.locator(".badge")).toHaveText("Có khách");
  await expect(
    page
      .getByRole("navigation", { name: "Điều hướng chính" })
      .getByRole("button", { name: "Tổng quan", exact: true }),
  ).toHaveCount(0);
  for (const hash of ["#dashboard", "#"]) {
    await page.goto(`http://127.0.0.1:5186/${hash}`);
    await expect(page.locator(".room-tile")).toHaveCount(4);
    await expect(page.locator(".topbar-heading")).toContainText(
      "Tình trạng phòng",
    );
  }
  await expect(occupied).toContainText("Standard");
  await expect(occupied).toContainText("2 người");
  await expect(page.getByText("Không có khách", { exact: true })).toHaveCount(
    0,
  );
  const colors = await page
    .locator(".room-tile")
    .evaluateAll((tiles) =>
      tiles.map((t) => getComputedStyle(t).backgroundColor),
    );
  assert.equal(new Set(colors).size, 4);
  assert.ok(
    colors.every((c) => c !== "rgb(255, 255, 255)" && c !== "rgba(0, 0, 0, 0)"),
  );
  await page.screenshot({ path: `${out}/board-1440.png` });
  await occupied.click();
  const drawer = page.getByRole("dialog", { name: "Phòng 102" });
  await expect(drawer).toBeVisible();
  await expect(
    drawer.getByRole("button", { name: "Nhận phòng", exact: true }),
  ).toHaveCount(0);
  await expect(
    drawer.getByRole("button", { name: "Trả phòng", exact: true }),
  ).toBeVisible();
  await expect(drawer).toContainText("Standard");
  await expect(drawer).toContainText("2 người");
  await page.waitForTimeout(260);
  const bounds = await drawer.boundingBox();
  assert.ok(Math.abs(bounds.x + bounds.width - 1440) <= 1);
  assert.ok(bounds.y <= 1 && bounds.height >= 998 && bounds.width <= 520);
  await drawer
    .getByRole("button", { name: "Chuyển phòng", exact: true })
    .click();
  await expect(
    drawer.locator('select[name="target"] option:not([disabled])'),
  ).toHaveText(["101"]);
  await page.screenshot({ path: `${out}/drawer-1440.png` });
  await page.keyboard.press("Escape");
  await expect(drawer).toHaveCount(0);
  await expect(occupied).toBeFocused();
  await occupied.click();
  await drawer.getByRole("button", { name: "Trả phòng", exact: true }).click();
  await drawer.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(drawer.locator(".badge")).toHaveText("Chờ dọn phòng");
  await expect(occupied.locator(".badge")).toHaveText("Chờ dọn phòng");
  await drawer.getByRole("button", { name: "Đóng", exact: true }).click();
  const ready = page
    .locator(".room-tile")
    .filter({ has: page.locator("strong", { hasText: /^101$/ }) });
  await ready.click();
  const readyDrawer = page.getByRole("dialog", { name: "Phòng 101" });
  await readyDrawer
    .getByRole("button", { name: "Nhận phòng", exact: true })
    .click();
  await readyDrawer
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(readyDrawer.locator(".badge")).toHaveText("Có khách");
  await expect(ready.locator(".badge")).toHaveText("Có khách");
  await page.keyboard.press("Escape");
  rooms[0].readiness = "BLOCKED";
  await page.reload();
  await expect(ready.locator(".badge")).toHaveText(["Có khách", "Đang chặn"]);
  for (const width of [768, 375]) {
    await page.setViewportSize({ width, height: 812 });
    await ready.click();
    await expect(readyDrawer).toBeVisible();
    await page.waitForTimeout(260);
    const rect = await readyDrawer.boundingBox();
    assert.ok(
      Math.abs(rect.x + rect.width - width) <= 1 && rect.width <= width,
    );
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    );
    await expect(
      readyDrawer.getByRole("button", { name: "Đóng", exact: true }),
    ).toBeInViewport();
    await page.screenshot({ path: `${out}/drawer-${width}.png` });
    await page.keyboard.press("Escape");
  }
  await page.emulateMedia({ reducedMotion: "reduce" });
  await ready.click();
  assert.equal(
    await readyDrawer.evaluate((el) => getComputedStyle(el).animationName),
    "none",
  );
  await page.keyboard.press("Escape");
  profile.roles = [
    { code: "OPERATION_MANAGER", name: "Quản lý vận hành", branchId: 1 },
  ];
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.reload();
  await expect(
    page
      .getByRole("navigation", { name: "Điều hướng chính" })
      .getByRole("button", { name: "Tổng quan vận hành", exact: true }),
  ).toBeVisible();
  rooms.splice(
    0,
    rooms.length,
    ...Array.from({ length: 48 }, (_, i) => ({
      roomId: i + 1,
      code: String(101 + i),
      branchId: 1,
      roomTypeId: 11,
      readiness: i < 25 ? "READY" : "PENDING_CLEANING",
      isOccupied: false,
      operationalStatus: i < 25 ? "READY" : "DIRTY",
      rowVersion: "fixture",
    })),
  );
  await page.goto("http://127.0.0.1:5186/#rooms");
  const tiles = page.locator(".room-tile");
  const pagination = page.getByRole("navigation", { name: "Phân trang phòng" });
  const next = pagination.getByRole("button", { name: "Tiếp", exact: true });
  const previous = pagination.getByRole("button", {
    name: "Trước",
    exact: true,
  });
  await expect(tiles).toHaveCount(15);
  await expect(previous).toBeDisabled();
  await expect(pagination).toContainText("Trang 1 / 4");
  await expect(
    page.getByRole("button", { name: "Làm mới", exact: true }),
  ).toHaveCount(0);
  const layout = await tiles.evaluateAll((nodes) =>
    nodes.map((n) => {
      const r = n.getBoundingClientRect();
      return { x: Math.round(r.x), y: Math.round(r.y), height: r.height };
    }),
  );
  assert.equal(new Set(layout.map((r) => r.x)).size, 5);
  assert.equal(new Set(layout.map((r) => r.y)).size, 3);
  assert.ok(layout.every((r) => r.height < 182));
  const main = await page.locator("main").boundingBox(),
    header = await page.locator(".topbar").boundingBox();
  assert.ok(Math.abs(main.y - header.y - header.height) <= 1);
  assert.equal(
    await page
      .locator("main")
      .evaluate((el) => getComputedStyle(el).paddingTop),
    "12px",
  );
  await page.screenshot({ path: `${out}/pagination-1440.png` });
  const codes = [];
  for (let p = 1; p <= 4; p++) {
    codes.push(...(await tiles.locator(":scope > strong").allTextContents()));
    if (p < 4) await next.click();
  }
  assert.equal(new Set(codes).size, 48);
  await expect(tiles).toHaveCount(3);
  await expect(next).toBeDisabled();
  await page.getByLabel("Tìm phòng").fill("101");
  await expect(tiles).toHaveCount(1);
  await expect(pagination).toContainText("Trang 1 / 1");
  await page.getByLabel("Tìm phòng").fill("no-room");
  await expect(tiles).toHaveCount(0);
  await expect(previous).toBeDisabled();
  await expect(next).toBeDisabled();
  await page.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
  await expect(tiles).toHaveCount(15);
  await next.click();
  await page.locator(".tabs button").filter({ hasText: "Sẵn sàng" }).click();
  await expect(pagination).toContainText("Trang 1 / 2");
  await next.click();
  await expect(tiles).toHaveCount(10);
  await tiles.last().click();
  await expect(
    page.getByRole("dialog", { name: "Phòng 125", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await page.locator(".tabs button").filter({ hasText: "Tất cả" }).click();
  for (const width of [768, 375]) {
    await page.setViewportSize({ width, height: 900 });
    await page.waitForTimeout(300);
    await expect(tiles).toHaveCount(15);
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    );
    await page.screenshot({ path: `${out}/pagination-${width}.png` });
  }
  assert.equal(errors.length, 0);
  assert.deepEqual(
    writes.map((w) => w.path),
    ["rooms/2/actions/check-out", "rooms/1/actions/check-in"],
  );
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        mockedApi: true,
        viewports: [1440, 768, 375],
        colors,
        errors,
        writes,
        verified: [
          "reception routing",
          "occupied precedence",
          "room type and capacity",
          "right drawer",
          "keyboard and focus",
          "check-in/out",
          "move eligibility",
          "occupied block warning",
          "responsive",
          "reduced motion",
          "other role dashboard",
          "48 rooms across 4 pages without duplicates",
          "5 columns and 3 rows on desktop",
          "search and status filters reset page",
          "drawer opens correct room on later page",
          "compact navbar spacing and no refresh button",
        ],
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS reception room board, drawer, metadata and occupancy regression (mocked APIs)",
  );
} finally {
  await browser.close();
}
