// Live read-only business checks. Back up DB first: authentication writes sessions.
// Set STAFFING_APPROVAL_FILE to the locally approved credential document.
import fs from "node:fs";
import path from "node:path";
import assert from "node:assert/strict";
import { chromium, expect as baseExpect } from "@playwright/test";

const expect = baseExpect.configure({ timeout: 30000 });

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const output = path.resolve("../artifacts/admin-ux");
fs.mkdirSync(output, { recursive: true });
const approval = fs.readFileSync(process.env.STAFFING_APPROVAL_FILE, "utf8");
const password = approval
  .slice(approval.indexOf("SHARED TEST CREDENTIAL"))
  .match(/`([^`]+)`/)[1];
const results = [],
  errors = [],
  failedResponses = [],
  dashboardRequests = [];
const browser = await chromium.launch({
  executablePath:
    process.env.CHROME_PATH ||
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
  args: ["--enable-unsafe-swiftshader"],
});
let context, page, token;
async function check(name, fn) {
  try {
    await fn();
    results.push({ name, status: "PASS" });
    console.log("PASS " + name);
  } catch (e) {
    results.push({ name, status: "FAIL", error: e.message });
    console.error("FAIL " + name + ": " + e.message);
  }
}
async function open(username) {
  if (context) await context.close();
  const response = await fetch(base + "/api/v2/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });
  assert.equal(response.status, 200, "Login status for " + username);
  token = (await response.json()).accessToken;
  context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(
    (value) => sessionStorage.setItem("hotelops.compact.session", value),
    token,
  );
  page = await context.newPage();
  page.on("request", (request) => {
    if (request.url().includes("/api/v2/"))
      dashboardRequests.push(new URL(request.url()).pathname);
  });
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("response", (r) => {
    if (r.url().includes("/api/v2/") && r.status() >= 400)
      failedResponses.push({
        path: new URL(r.url()).pathname,
        status: r.status(),
      });
  });
  await page.goto(base);
  await expect(page.locator(".app-shell")).toBeVisible();
}
async function route(hash) {
  await page.evaluate((hash) => {
    location.hash = hash;
  }, hash);
  await expect(page.locator("main .spinner")).toHaveCount(0);
}
async function fit() {
  assert(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
    "No horizontal page overflow",
  );
}
try {
  await check("Admin: real dashboard totals and clean navigation", async () => {
    await open("NamNH1");
    await route("dashboard");
    await expect(page.locator(".stat-chart")).toHaveCount(2, { timeout: 30000 });
    assert.deepEqual(await page.locator(".dashboard-metrics h2").allTextContents(), [
      "Chi nhánh hoạt động", "Tài khoản hoạt động", "Phòng",
    ]);
    assert.deepEqual(await page.locator(".chart-heading h2").allTextContents(), [
      "Số phòng theo khách sạn", "Trạng thái tài khoản",
    ]);
    assert(!dashboardRequests.some(path => /catalog\/(roles|permissions|departments)|users\/roles/.test(path)));
    const response = await fetch(base + "/api/v2/insights/administration", {
      headers: { Authorization: `Bearer ${token}` },
    });
    assert.equal(response.status, 200);
    const data = await response.json();
    assert.equal(
      Number(await page.locator(".chart-total").textContent()),
      data.users.reduce((n, r) => n + r.count, 0),
    );
    const get = async (path) => {
      const r = await fetch(base + "/api/v2/" + path, {
        headers: { Authorization: `Bearer ${token}` },
      });
      assert.equal(r.status, 200);
      return r.json();
    };
    const hotels = (await get("catalog/branches?pageSize=100")).items.map(
      (r) => r.fields,
    );
    let rooms = 0;
    for (const hotel of hotels) {
      rooms += (
        await get(`catalog/rooms?branchId=${hotel.branchId}&pageSize=1`)
      ).total;
    }
    assert.equal(
      Number(await page.locator('[data-stat="rooms"]').textContent()),
      rooms,
    );
    assert.equal(
      new Set(
        await page
          .locator(".dashboard-metrics .panel")
          .evaluateAll((elements) =>
            elements.map((e) => getComputedStyle(e).backgroundColor),
          ),
      ).size,
      3,
    );
    await expect(page.locator(".topbar select")).toHaveCount(0);
    await expect(
      page
        .locator(".sidebar")
        .getByRole("button", { name: "Thông báo", exact: true }),
    ).toHaveCount(0);
    await expect(
      page
        .locator(".sidebar")
        .getByRole("button", { name: "Nhật ký", exact: true }),
    ).toHaveCount(0);
  });
  await check(
    "Account search handles Vietnamese, case, code, empty result and reset",
    async () => {
      await route("users");
      await expect(page.locator("tbody tr")).toHaveCount(56);
      await page.getByLabel("Tìm tài khoản").fill("nguyen hoang nam");
      await expect(page.locator("tbody tr")).toHaveCount(1);
      await expect(page.locator("tbody")).toContainText("NamNH1");
      await page.getByLabel("Tìm tài khoản").fill("EMP050");
      await expect(page.locator("tbody")).toContainText("KhangTD1");
      await expect(page.getByLabel("Trạng thái", { exact: true })).toHaveCount(0);
      await page.getByLabel("Tìm tài khoản").fill("no-such-account-xyz");
      await expect(page.locator("tbody")).toContainText("Không tìm thấy");
      await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
      await expect(page.locator("tbody tr")).toHaveCount(56);
    },
  );
  await check(
    "Account bar and table header stay visible on page and list scroll",
    async () => {
      await page.locator(".table-scroll").evaluate((e) => {
        e.scrollTop = 240;
      });
      await page.evaluate(() => scrollTo(0, 300));
      await page.waitForTimeout(200);
      const top = await page.locator(".topbar").boundingBox();
      const head = await page.locator("thead th").first().boundingBox();
      assert(Math.abs(top.y) <= 1);
      assert(head.y >= top.height - 2, JSON.stringify({ top, head }));
      await page.evaluate(() => scrollTo(0, 0));
    },
  );
  await check(
    "System settings are separate and still expose role permissions",
    async () => {
      await route("settings?resource=roles");
      await expect(
        page.getByRole("heading", { name: "Cấu hình hệ thống", exact: true }),
      ).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Phân quyền", exact: true }).first(),
      ).toBeVisible();
      await expect(page.locator(".catalog-sections")).toHaveCount(0);
      await expect(page.locator(".structure-trail")).toHaveCount(0);
    },
  );
  await check(
    "3D hotels render and keyboard selection opens correct hotel structure",
    async () => {
      await route("catalog");
      await expect(page.locator(".hotel-preview img").first()).toBeVisible({
        timeout: 25000,
      });
      await expect(page.locator(".hotel-detail-nav")).toHaveCount(0);
      await expect(page.locator(".catalog-sections")).toHaveCount(0);
      await expect(page.locator(".hotel-hud")).toHaveCount(0);
      await expect(page.locator(".hotel-options button")).toHaveCount(2);
      const second = page.locator(".hotel-options button").nth(1);
      await second.focus();
      await page.keyboard.press("Enter");
      await expect(
        page.getByRole("heading", { name: "Chi nhánh Cao Thắng", exact: true }),
      ).toBeVisible();
      await expect(page.locator(".hotel-detail-nav")).toBeVisible();
      await page.locator(".floor-open").first().click();
      await expect(page.locator("table")).toBeVisible();
      await expect(page.locator(".catalog-sections")).toHaveCount(0);
      await page.screenshot({
        path: path.join(output, "hotel-rooms.png"),
        fullPage: true,
      });
    },
  );
  await check(
    "Responsive dashboard, accounts and 3D at 375/768/1440",
    async () => {
      for (const width of [375, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        for (const hash of ["dashboard", "users", "catalog"]) {
          await route(hash);
          if (hash === "catalog")
            await expect(
              page.locator(".hotel-preview img").first(),
            ).toBeVisible({ timeout: 20000 });
          else if (hash === "users")
            await expect(page.locator("table")).toBeVisible();
          else await expect(page.locator(".stat-chart")).toHaveCount(2, { timeout: 30000 });
          await page.waitForTimeout(300);
          await fit();
          await page.screenshot({
            path: path.join(output, `${hash}-${width}.png`),
            fullPage: true,
          });
        }
      }
    },
  );
  await check(
    "Hotel selection still works when WebGL is unavailable",
    async () => {
      const fallback = await browser.newContext();
      try {
        await fallback.addInitScript((value) => {
          sessionStorage.setItem("hotelops.compact.session", value);
          const original = HTMLCanvasElement.prototype.getContext;
          HTMLCanvasElement.prototype.getContext = function (type, ...args) {
            return type.includes("webgl")
              ? null
              : original.call(this, type, ...args);
          };
        }, token);
        const testPage = await fallback.newPage();
        await testPage.goto(base + "/#catalog");
        await expect(
          testPage.getByText(
            "Không tải được mô hình 3D. Bạn vẫn có thể chọn khách sạn.",
          ),
        ).toBeVisible();
        await testPage.locator(".hotel-options button").first().click();
        await expect(testPage.locator(".hotel-detail-nav")).toBeVisible();
      } finally {
        await fallback.close();
      }
    },
  );
  await check(
    "Notification bell opens notifications; Audit deep link remains hidden",
    async () => {
      await page
        .locator(".topbar")
        .getByRole("button", { name: "Thông báo", exact: true })
        .click();
      await expect(
        page.getByRole("heading", { name: "Thông báo", exact: true }),
      ).toBeVisible();
      await route("audit");
      await expect(
        page.getByRole("heading", { name: "Tổng quan quản trị", exact: true }),
      ).toBeVisible();
    },
  );
  await check(
    "Owner retains read-only charts and in-page hotel switching",
    async () => {
      await open("HanLG1");
      await route("dashboard");
      await expect(page.locator(".chart-grid")).toBeVisible();
      await expect(page.locator(".chart-heading a")).toHaveCount(0);
      await page
        .getByRole("button", { name: "Đổi khách sạn", exact: true })
        .click();
      await expect(page.locator("dialog .hotel-options button")).toHaveCount(2);
      await page.locator("dialog .hotel-options button").nth(1).click();
      await page
        .getByRole("button", { name: "Sử dụng khách sạn này", exact: true })
        .click();
      await expect(page.locator(".branch-context > span")).toContainText(
        "Cao Thắng",
      );
      await expect(page.locator(".chart-grid")).toBeVisible();
      await page.screenshot({
        path: path.join(output, "owner.png"),
        fullPage: true,
      });
    },
  );
  await check(
    "Storekeeper retains scoped inventory catalogs without admin settings",
    async () => {
      await open("KhangTD1");
      await expect(page.locator(".sidebar")).not.toContainText(
        "Cấu hình hệ thống",
      );
      await route("supplies?resource=items");
      await expect(
        page.getByRole("heading", {
          name: "Danh mục vật tư & tài sản",
          exact: true,
        }),
      ).toBeVisible();
      await expect(page.locator(".branch-context > span")).toContainText(
        "Cao Thắng",
      );
      await expect(page.locator("table")).toBeVisible();
    },
  );
  await check(
    "Department Head and housekeeping retain task pages",
    async () => {
      await open("MinhNQ1");
      await route("workday");
      await expect(
        page.getByRole("heading", { name: "Lịch toàn chi nhánh", exact: true }),
      ).toBeVisible();
      await open("KhoiTM1");
      await route("work");
      await expect(page.locator("main")).not.toContainText(
        "Bạn không có quyền",
      );
      await expect(page.locator(".sidebar")).not.toContainText(
        "Nhân sự & truy cập",
      );
    },
  );
  await check("No browser exceptions or failed API reads", async () => {
    assert.deepEqual(errors, []);
    assert.deepEqual(failedResponses, []);
  });
} finally {
  await browser.close();
  fs.writeFileSync(
    path.join(output, "results.json"),
    JSON.stringify(
      { at: new Date().toISOString(), base, results, errors, failedResponses },
      null,
      2,
    ),
  );
}
if (results.some((r) => r.status === "FAIL")) process.exitCode = 1;
