import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const database = process.env.LEAD_TEST_DATABASE,
  token = process.env.LEAD_TEST_TOKEN;
assert(
  /^HotelOpsCompactApiTest[a-f0-9]{12}$/.test(database) && token,
  "Isolated fixture required",
);
const base = process.env.LEAD_TEST_BASE;
assert(/^http:\/\/127\.0\.0\.1:\d+$/.test(base));
const work = Number(process.env.LEAD_TEST_WORK),
  review = Number(process.env.LEAD_TEST_REVIEW),
  tech = Number(process.env.LEAD_TEST_TECH),
  asset = Number(process.env.LEAD_TEST_ASSET);
const out = "../artifacts/maintenance-lead";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  writes = [],
  failures = [],
  views = [];
let page;
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    timezoneId: "Asia/Ho_Chi_Minh",
  });
  await context.addInitScript(
    (value) => sessionStorage.setItem("hotelops.compact.session", value),
    token,
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url());
    const response = await route.fetch({
      url: base + url.pathname + url.search,
    });
    if (req.method() !== "GET")
      writes.push({
        path: url.pathname,
        method: req.method(),
        status: response.status(),
      });
    if (response.status() >= 400)
      failures.push({ path: url.pathname, status: response.status() });
    await route.fulfill({ response });
  });
  page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  for (const width of [1440, 900, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const route of [
      "dashboard",
      "work",
      "review",
      "incidents",
      "templates",
      "assets",
      "technicians",
      "requests",
      "workday",
    ]) {
      await page.goto(`http://127.0.0.1:5186/#${route}`);
      await expect(page.locator(".topbar h1")).toBeVisible();
      await expect(page.getByText("Đang tải…", { exact: true })).toHaveCount(0);
      await expect(page.locator(".alert")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${route} overflow ${width}`,
      );
      if (
        ["work", "review", "incidents", "templates", "assets"].includes(route)
      ) {
        const filter = page.getByRole("button", { name: /^Bộ lọc/ });
        await expect(filter).toHaveAttribute("aria-expanded", "false");
      }
      views.push({ route, width });
    }
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto(`http://127.0.0.1:5186/#work?kind=MAINTENANCE&id=${work}`);
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Bắt đầu", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Phân công", exact: true }).click();
  let modal = page.locator("dialog").last();
  await expect(modal).not.toHaveClass(/room-drawer/);
  await page.keyboard.press("Escape");
  await expect(page.locator("dialog")).toHaveCount(1);
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page.getByRole("button", { name: "Phân công", exact: true }).click();
  modal = page.locator("dialog").last();
  await modal
    .getByRole("combobox", { name: "Nhân viên", exact: true })
    .selectOption(String(tech));
  await modal.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(page.locator("dialog")).toHaveCount(1);
  await expect(page.locator("dialog.room-drawer")).toContainText("qa_tech_a");
  await page.goto(`http://127.0.0.1:5186/#review?id=${review}`);
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page.getByRole("button", { name: "Chấp nhận", exact: true }).click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Chấp nhận", exact: true }),
  ).toHaveCount(0);
  await expect(page.locator("dialog.room-drawer")).toContainText("qa_lead_a");
  await page.screenshot({ path: `${out}/live-review.png`, fullPage: true });
  await page.goto("http://127.0.0.1:5186/#templates");
  await page
    .getByRole("button", { name: "+ Tạo kế hoạch", exact: true })
    .click();
  modal = page.locator("dialog").last();
  await modal.getByLabel("Mã kế hoạch", { exact: true }).fill("BROWSER-PM");
  await modal
    .getByLabel("Tên kế hoạch", { exact: true })
    .fill("Live browser PM plan");
  await modal
    .getByRole("combobox", { name: "Thiết bị", exact: true })
    .selectOption(String(asset));
  await modal
    .getByLabel("Lần đến hạn", { exact: true })
    .fill("2026-12-01T08:00");
  await modal
    .getByRole("button", { name: "Lưu kế hoạch", exact: true })
    .click();
  await expect(page.locator("dialog")).toHaveCount(0);
  const row = page.getByRole("row").filter({ hasText: "Live browser PM plan" });
  await expect(row).toBeVisible();
  await row.getByRole("button", { name: "Chỉnh sửa", exact: true }).click();
  modal = page.locator("dialog").last();
  await modal
    .getByLabel("Tên kế hoạch", { exact: true })
    .fill("Live browser PM plan updated");
  await modal
    .getByRole("button", { name: "Lưu kế hoạch", exact: true })
    .click();
  await expect(page.locator("dialog")).toHaveCount(0);
  await expect(
    page.getByRole("row").filter({ hasText: "Live browser PM plan updated" }),
  ).toContainText("08:00:00");
  await page.screenshot({ path: `${out}/live-pm.png`, fullPage: true });
  assert.equal(errors.length, 0, errors.join("\n"));
  assert.equal(failures.length, 0, JSON.stringify(failures));
  assert(
    writes.some((w) => w.path.endsWith("/actions/assign")) &&
      writes.some((w) => w.path.endsWith("/actions/verify")),
  );
  assert(writes.every((w) => w.status === 200));
  fs.writeFileSync(
    `${out}/live-verification.json`,
    JSON.stringify(
      { database, passed: true, views, writes, errors, failures },
      null,
      2,
    ),
  );
  console.log(
    `PASS Maintenance Lead ${views.length} responsive real-API views; browser assignment/review/PM create-edit and nested Escape`,
  );
} catch (error) {
  fs.writeFileSync(
    `${out}/live-failure.json`,
    JSON.stringify(
      {
        database,
        errors,
        failures,
        writes,
        views,
        body: await page?.locator("body").innerText(),
      },
      null,
      2,
    ),
  );
  await page?.screenshot({ path: `${out}/live-failure.png`, fullPage: true });
  throw error;
} finally {
  await browser.close();
}
