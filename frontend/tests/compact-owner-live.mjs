import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";
const database = process.env.OWNER_TEST_DATABASE,
  token = process.env.OWNER_TEST_TOKEN,
  base = process.env.OWNER_TEST_BASE,
  a = process.env.OWNER_TEST_A,
  b = process.env.OWNER_TEST_B;
assert(
  /^HotelOpsCompactApiTest[a-f0-9]{12}$/.test(database) &&
    token &&
    /^http:\/\/127\.0\.0\.1:\d+$/.test(base),
);
const out = "../artifacts/owner";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  failures = [],
  writes = [],
  views = [];
let page, context;
try {
  context = await browser.newContext({
    acceptDownloads: true,
    viewport: { width: 1440, height: 1000 },
    timezoneId: "Asia/Ho_Chi_Minh",
  });
  context.setDefaultTimeout(15000);
  context.setDefaultNavigationTimeout(15000);
  await context.addInitScript(
    (v) => sessionStorage.setItem("hotelops.compact.session", v),
    token,
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url());
    let response;
    try {
      response = await route.fetch({ url: base + url.pathname + url.search });
    } catch (error) {
      failures.push({
        path: url.pathname,
        error: String(error?.message || error).replaceAll(token, "[redacted]"),
      });
      await route.abort().catch(() => {});
      return;
    }
    if (req.method() !== "GET")
      writes.push({ path: url.pathname, status: response.status() });
    if (response.status() >= 400)
      failures.push({ path: url.pathname, status: response.status() });
    try {
      await route.fulfill({ response });
    } catch (error) {
      if (
        !/already handled|Target.*closed|context.*disposed/i.test(
          String(error?.message),
        )
      )
        failures.push({
          path: url.pathname,
          error: String(error?.message || error).replaceAll(
            token,
            "[redacted]",
          ),
        });
    }
  });
  page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => {
    if (m.type() === "error" && !/net::ERR_ABORTED/.test(m.text()))
      errors.push(m.text());
  });
  const routes = [
    "dashboard",
    "branches",
    "rooms",
    "work",
    "technical",
    "technical?tab=maintenance",
    "technical?tab=preventive",
    "resources",
    "resources?tab=stock",
    "resources?tab=materials",
    "resources?tab=documents",
    "reports",
    "reports?report=tasks",
    "reports?report=staffing",
    "notifications",
  ];
  for (const width of [1440, 900, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const route of routes) {
      await page.goto(`http://127.0.0.1:5186/#${route}`);
      await page.waitForLoadState("networkidle");
      await expect(page.locator(".topbar h1")).toBeVisible();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
      await expect(page.locator(".alert")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${route} overflow at ${width}`,
      );
      for (const button of [
        "Tạo công việc",
        "Phân công",
        "Bắt đầu",
        "Gửi hoàn thành",
        "Chấp nhận",
        "Yêu cầu xử lý lại",
        "Nhận phòng",
        "Chặn phòng",
        "+ Thêm tài sản",
      ])
        await expect(
          page.getByRole("button", { name: button, exact: true }),
        ).toHaveCount(0);
      views.push({ route, width });
    }
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("http://127.0.0.1:5186/#dashboard");
  await expect(
    page.locator("nav[aria-label='Điều hướng chính'] button"),
  ).toHaveCount(7);
  await expect(
    page.getByRole("button", { name: "Nhật ký hoạt động", exact: true }),
  ).toHaveCount(0);
  const selector = page.getByRole("combobox", { name: "Chi nhánh giám sát" });
  await expect(selector).toHaveValue("0");
  await expect(page.locator("tbody tr")).toHaveCount(2);
  await selector.selectOption(a);
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await selector.selectOption(b);
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await selector.selectOption("0");
  await expect(page.locator("tbody tr")).toHaveCount(2);
  await page
    .getByRole("button", { name: "Xem chi tiết", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.evaluate(() => scrollTo(0, 0));
  await page.screenshot({
    path: `${out}/global-dashboard.png`,
    fullPage: true,
  });
  await page.goto("http://127.0.0.1:5186/#reports?report=tasks");
  await expect(page.getByRole("status")).toContainText("Trang 1 /");
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("Trang 2 /");
  await page
    .getByRole("textbox", { name: "Tìm trong báo cáo", exact: true })
    .fill("Paged monitored work 25");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("button", { name: "Xuất trang CSV", exact: true }).click(),
  ]);
  await download.saveAs(`${out}/filtered-report.csv`);
  const csv = fs.readFileSync(`${out}/filtered-report.csv`, "utf8");
  assert(
    csv.includes("Paged monitored work 25") &&
      !/passwordHash|tokenHash|securityStamp/.test(csv),
  );
  await page.getByRole("link", { name: "Xem chi tiết", exact: true }).click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await page.goto(
    `http://127.0.0.1:5186/#work?kind=MAINTENANCE&id=${process.env.OWNER_TEST_WORK}`,
  );
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await page.goto(
    `http://127.0.0.1:5186/#rooms?room=${process.env.OWNER_TEST_ROOM}`,
  );
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page
    .locator("dialog.room-drawer")
    .evaluate((el) =>
      Promise.allSettled(el.getAnimations().map((a) => a.finished)),
    );
  await page.screenshot({ path: `${out}/room-drawer.png`, fullPage: true });
  await page.keyboard.press("Escape");
  await page.goto(
    `http://127.0.0.1:5186/#assets?id=${process.env.OWNER_TEST_ASSET}`,
  );
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await page.goto("http://127.0.0.1:5186/#technical?tab=preventive");
  await page
    .getByRole("button", { name: "Xem chi tiết", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await page.goto("http://127.0.0.1:5186/#resources?tab=materials");
  await page
    .getByRole("button", { name: "Chi tiết", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog form")).toHaveCount(0);
  await page.keyboard.press("Escape");
  await page.goto("http://127.0.0.1:5186/#audit");
  await expect(page.locator(".topbar h1")).toHaveText("Tổng quan hệ thống");
  await page.waitForLoadState("networkidle");
  await context.unrouteAll({ behavior: "ignoreErrors" });
  assert.equal(errors.length, 0, errors.join("\n"));
  assert.equal(failures.length, 0, JSON.stringify(failures));
  assert.equal(writes.length, 0, JSON.stringify(writes));
  fs.writeFileSync(
    `${out}/live-verification.json`,
    JSON.stringify(
      { database, passed: true, views, writes, errors, failures },
      null,
      2,
    ),
  );
  console.log(
    `PASS Owner ${views.length} real API responsive views, global selector, report paging/search, readonly drawers, zero mutations`,
  );
} catch (error) {
  fs.writeFileSync(
    `${out}/live-failure.json`,
    JSON.stringify(
      {
        database,
        views,
        writes,
        errors,
        failures,
        body: await page?.locator("body").innerText(),
      },
      null,
      2,
    ),
  );
  await page?.screenshot({ path: `${out}/live-failure.png`, fullPage: true });
  throw new Error(
    String(error?.message || error).replaceAll(token, "[redacted]"),
  );
} finally {
  await context?.unrouteAll({ behavior: "ignoreErrors" });
  await browser.close();
}
