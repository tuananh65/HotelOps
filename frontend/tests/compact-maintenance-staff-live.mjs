import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";
const database = process.env.TECH_TEST_DATABASE,
  token = process.env.TECH_TEST_TOKEN,
  lead = process.env.TECH_TEST_LEAD,
  base = process.env.TECH_TEST_BASE,
  work = Number(process.env.TECH_TEST_WORK);
assert(
  /^HotelOpsCompactApiTest[a-f0-9]{12}$/.test(database) &&
    token &&
    lead &&
    /^http:\/\/127\.0\.0\.1:\d+$/.test(base),
);
const out = "../artifacts/maintenance-staff";
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
let page;
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    timezoneId: "Asia/Ho_Chi_Minh",
  });
  await context.addInitScript(
    (v) => sessionStorage.setItem("hotelops.compact.session", v),
    token,
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url());
    const response = await route.fetch({
      url: base + url.pathname + url.search,
    });
    if (req.method() !== "GET")
      writes.push({ path: url.pathname, status: response.status() });
    if (response.status() >= 400)
      failures.push({ path: url.pathname, status: response.status() });
    await route.fulfill({ response });
  });
  page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  for (const width of [1440, 900, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const route of [
      "dashboard",
      "work",
      "incidents",
      "assets",
      "materials",
      "requests",
      "otherwork",
      "workday",
      "notifications",
      "history",
    ]) {
      await page.goto(`http://127.0.0.1:5186/#${route}`);
      await expect(page.locator(".topbar h1")).toBeVisible();
      await expect(page.locator('[aria-busy="true"]')).toHaveCount(0);
      await expect(page.locator(".alert")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${route} overflow ${width}`,
      );
      if (
        [
          "work",
          "incidents",
          "assets",
          "materials",
          "requests",
          "otherwork",
          "history",
        ].includes(route)
      )
        await expect(
          page.getByRole("button", { name: /^Bộ lọc/ }),
        ).toHaveAttribute("aria-expanded", "false");
      views.push({ route, width });
    }
  }
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto(`http://127.0.0.1:5186/#work?kind=MAINTENANCE&id=${work}`);
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  for (const name of [
    "Phân công",
    "Chấp nhận",
    "Yêu cầu xử lý lại",
    "Từ chối công việc",
  ])
    await expect(page.getByRole("button", { name, exact: true })).toHaveCount(
      0,
    );
  async function command(name, prepare) {
    await page.getByRole("button", { name, exact: true }).click();
    const modal = page.locator("dialog").last();
    await expect(modal).not.toHaveClass(/room-drawer/);
    if (prepare) await prepare(modal);
    await modal.getByRole("button", { name: "Xác nhận", exact: true }).click();
    await expect(page.locator("dialog")).toHaveCount(1);
  }
  await command("Nhận việc");
  await command("Bắt đầu");
  await command("Cập nhật tiến độ", async (modal) => {
    await modal.getByRole("spinbutton").fill("65");
    await modal
      .getByLabel("Lý do / ghi chú", { exact: true })
      .fill("Live technician repair progress notes");
  });
  const drawer = page.locator("dialog.room-drawer");
  await drawer.locator('input[type="file"]').setInputFiles({
    name: "repair.png",
    mimeType: "image/png",
    buffer: Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=",
      "base64",
    ),
  });
  await drawer
    .getByRole("button", { name: "Tải minh chứng", exact: true })
    .click();
  await expect(
    drawer.getByRole("button", { name: "repair.png", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("link", { name: "Yêu cầu vật tư cho công việc", exact: true })
    .click();
  let modal = page.locator("dialog").last();
  await expect(modal).toBeVisible();
  await modal
    .getByRole("combobox", { name: "Bộ phận", exact: true })
    .selectOption({ label: "Engineering" });
  await modal.getByRole("button", { name: /Tạo/ }).click();
  await expect(
    page.getByRole("button", { name: "+ Thêm vật tư", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "+ Thêm vật tư", exact: true })
    .click();
  modal = page.locator("dialog").last();
  await modal
    .getByRole("combobox", { name: "Vật tư", exact: true })
    .selectOption({ label: "Repair part · TECH-PART" });
  await modal.getByLabel("Số lượng", { exact: true }).fill("2");
  await modal.getByRole("button", { name: "Lưu vật tư", exact: true }).click();
  await expect(page.locator("dialog")).toHaveCount(1);
  await page.getByRole("button", { name: "Gửi duyệt", exact: true }).click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(page.locator("dialog")).toHaveCount(1);
  await page.goto(`http://127.0.0.1:5186/#work?kind=MAINTENANCE&id=${work}`);
  await command("Gửi hoàn thành", async (modal) => {
    await modal
      .getByLabel("Lý do / ghi chú", { exact: true })
      .fill("Live repair completed; waiting for lead review");
  });
  await expect(
    page.getByRole("button", { name: "Bắt đầu", exact: true }),
  ).toHaveCount(0);
  const detail = await context.request.get(`${base}/api/v2/work/${work}`, {
    headers: { Authorization: `Bearer ${lead}` },
  });
  assert(detail.ok());
  const fields = (await detail.json()).work.fields;
  const reopened = await context.request.post(
    `${base}/api/v2/work/${work}/actions/reopen`,
    {
      headers: { Authorization: `Bearer ${lead}` },
      data: {
        rowVersion: fields.rowVersion,
        operationKey: crypto.randomUUID(),
        reason: "Live lead review: tighten wiring and repeat repair",
      },
    },
  );
  assert.equal(reopened.status(), 200);
  await page.reload();
  await expect(page.locator("dialog.room-drawer")).toContainText(
    "Live lead review: tighten wiring and repeat repair",
  );
  await command("Bắt đầu");
  await command("Gửi hoàn thành", async (modal) => {
    await modal
      .getByLabel("Lý do / ghi chú", { exact: true })
      .fill("Live rework completed and sent for review");
  });
  await page.screenshot({ path: `${out}/live-rework.png`, fullPage: true });
  await page.goto("http://127.0.0.1:5186/#dashboard");
  await expect(page.locator(".branch-metric-value")).toHaveCount(11);
  await page.screenshot({ path: `${out}/live-dashboard.png`, fullPage: true });
  assert.equal(errors.length, 0, errors.join("\n"));
  assert.equal(failures.length, 0, JSON.stringify(failures));
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
    `PASS Maintenance Staff ${views.length} real API responsive views; accept/start/progress/evidence/MR/complete/rework`,
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
  throw error;
} finally {
  await browser.close();
}
