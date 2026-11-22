import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const database = process.env.STORE_TEST_DATABASE,
  token = process.env.STORE_TEST_TOKEN;
assert(
  database?.startsWith("HotelOpsCompactApiTest") && token,
  "Isolated fixture required",
);
const base = process.env.STORE_TEST_BASE;
assert(/^http:\/\/127\.0\.0\.1:\d+$/.test(base));
const warehouse = Number(process.env.STORE_TEST_WAREHOUSE),
  requestId = Number(process.env.STORE_TEST_REQUEST);
const out = "../artifacts/storekeeper";
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  writes = [];
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
      writes.push({ path: url.pathname, status: response.status() });
    await route.fulfill({ response });
  });
  page = await context.newPage();
  page.on("pageerror", (e) => {
    errors.push(e.message);
    console.error("PAGE ERROR", e.message);
  });
  await page.goto("http://127.0.0.1:5186/#dashboard");
  await expect(page.locator(".branch-metric-value")).toHaveCount(10);
  await page.goto("http://127.0.0.1:5186/#transactions?kind=RECEIPT");
  await page.getByRole("button", { name: "Tạo nhập kho", exact: true }).click();
  let modal = page.locator("dialog").last();
  await modal
    .getByRole("combobox", { name: "Kho nhận", exact: true })
    .selectOption(String(warehouse));
  await modal
    .getByLabel("Lý do", { exact: true })
    .fill("Live isolated receipt through browser");
  await modal
    .getByRole("button", { name: "Tạo bản nháp", exact: true })
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page
    .getByRole("button", { name: "+ Thêm vật tư", exact: true })
    .click();
  modal = page.locator("dialog").last();
  await modal.getByLabel(/^Số lượng ·/).fill("3");
  await modal.getByRole("button", { name: "Lưu vật tư", exact: true }).click();
  await expect(page.locator("dialog")).toHaveCount(1);
  await page
    .getByRole("button", { name: "Xác nhận ghi sổ", exact: true })
    .click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  await page.goto(
    `http://127.0.0.1:5186/#fulfillment?kind=MATERIAL_REQUEST&id=${requestId}`,
  );
  await page
    .getByRole("button", { name: "Cấp phát vật tư", exact: true })
    .click();
  modal = page.locator("dialog").last();
  await modal.getByRole("spinbutton").fill("1");
  await modal
    .getByLabel("Lý do", { exact: true })
    .fill("Live isolated fulfillment through browser");
  await modal
    .getByRole("button", { name: "Tạo chứng từ liên kết", exact: true })
    .click();
  await expect.poll(() => page.url()).toContain("kind=ISSUE");
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page
    .getByRole("button", { name: "Xác nhận ghi sổ", exact: true })
    .click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  await page.screenshot({ path: `${out}/live-issue.png`, fullPage: true });
  assert.equal(errors.length, 0, errors.join("\n"));
  assert.deepEqual(
    writes.map((w) => w.path.replace(/\/\d+/g, "/id")),
    [
      "/api/v2/documents",
      "/api/v2/documents/id/lines",
      "/api/v2/documents/id/actions/post",
      "/api/v2/documents/id/derive",
      "/api/v2/documents/id/actions/post",
    ],
  );
  assert(writes.every((w) => w.status === 200));
  fs.writeFileSync(
    `${out}/live-verification.json`,
    JSON.stringify({ database, passed: true, writes, errors }, null, 2),
  );
  console.log(
    "PASS live browser receipt and MR fulfillment against isolated SQL fixture",
  );
} catch (error) {
  fs.writeFileSync(
    `${out}/live-failure.json`,
    JSON.stringify(
      {
        database,
        errors,
        writes,
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
