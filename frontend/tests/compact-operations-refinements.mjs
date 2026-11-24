import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const output = "../artifacts/operations-refinements";
fs.mkdirSync(output, { recursive: true });
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const requests = [],
  errors = [];
const assets = Array.from({ length: 28 }, (_, i) => ({
  assetId: i + 1,
  branchId: 1,
  assetCode: `A${i + 1}`,
  name: `Thiết bị ${i + 1}`,
  roomId: i < 26 ? 1 : null,
  roomCode: i < 26 ? "101" : null,
  locationName: i === 26 ? "Sảnh" : null,
  conditionCode: ["GOOD", "FAULTY", "UNDER_REPAIR", "FAIR", "POOR", "LOST"][
    i % 6
  ],
  isActive: i !== 27,
}));
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    assert.equal(
      route.request().method(),
      "GET",
      "Never write to a real database",
    );
    const url = new URL(route.request().url()),
      path = url.pathname.replace("/api/v2/", "");
    requests.push({ path, period: url.searchParams.get("taskPeriod") });
    let json;
    if (path === "auth/me")
      json = {
        user: { fields: { userId: 1, fullName: "Quản lý kiểm thử" } },
        roles: [
          { code: "OPERATION_MANAGER", name: "Quản lý vận hành", branchId: 1 },
        ],
        branches: [{ branchId: 1, name: "Chi nhánh kiểm thử" }],
        grants: ["DASHBOARD_VIEW", "TASK_VIEW", "ASSET_VIEW", "ROOM_VIEW"].map(
          (code) => ({ code, branchId: 1, dataScope: "BRANCH" }),
        ),
        memberships: [],
        requiresPasswordChange: false,
      };
    else if (path === "insights/dashboard") {
      const period = url.searchParams.get("taskPeriod");
      assert.ok(["day", "week", "month"].includes(period));
      json = {
        rooms: null,
        stock: null,
        monitoring: [],
        attention: [],
        work: [
          { status: "OPEN", count: { day: 2, week: 7, month: 18 }[period] },
        ],
        taskWindow: {
          period,
          fromDate: "2026-11-23",
          toDate: "2026-11-23",
          timezone: "Asia/Ho_Chi_Minh",
        },
        assets: [
          { condition: "GOOD", count: 7 },
          { condition: "FAULTY", count: 3 },
          { condition: "UNDER_REPAIR", count: 2 },
        ],
      };
    } else if (path === "assets") json = assets;
    else if (path === "workspace/options")
      json = {
        rooms: [],
        departments: [],
        references: [],
        users: [],
        assets: [],
      };
    else if (path.startsWith("catalog/"))
      json = { items: [], total: 0, page: 1, pageSize: 100 };
    else if (path === "assets/1")
      json = {
        asset: { fields: assets[0] },
        room: "101",
        history: [],
        work: [],
        plans: [],
      };
    else json = [];
    await route.fulfill({ json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(
    `${process.env.UX_BASE || "http://127.0.0.1:5186"}/#dashboard`,
  );
  const period = page.getByRole("combobox", {
    name: "Thống kê công việc vận hành",
    exact: true,
  });
  await expect(period).toHaveValue("day");
  await expect(
    page.locator(".operations-task-statistics .chart-values strong"),
  ).toHaveText("2");
  await expect(page.locator(".asset-condition-bar")).toHaveCount(1);
  await expect(page.locator(".asset-condition-bar")).toHaveAttribute(
    "aria-label",
    "Tốt: 7, Hỏng: 3, Khác: 2",
  );
  await period.selectOption("week");
  await expect(
    page.locator(".operations-task-statistics .chart-values strong"),
  ).toHaveText("7");
  await period.selectOption("month");
  await expect(
    page.locator(".operations-task-statistics .chart-values strong"),
  ).toHaveText("18");
  console.log("PASS dashboard period requests and segmented counts");
  await page.goto(`${process.env.UX_BASE || "http://127.0.0.1:5186"}/#assets`);
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    25,
  );
  await expect(page.locator(".asset-group-heading")).toHaveText("Phòng 101");
  await expect(page.locator("tbody")).toContainText("Đang sửa chữa");
  await expect(page.locator("tbody")).not.toContainText("UNDER_REPAIR");
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    3,
  );
  await expect(page.locator("tbody")).toContainText(
    "Ngoài phòng / chưa bố trí",
  );
  await page.getByLabel("Tìm tài sản", { exact: true }).fill("101");
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    25,
  );
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page
    .getByRole("combobox", { name: "Tình trạng", exact: true })
    .selectOption("FAULTY");
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    5,
  );
  await expect(page.locator(".asset-condition-bar")).toHaveAttribute(
    "aria-label",
    "Tốt: 0, Hỏng: 5, Khác: 0",
  );
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      `assets overflow at ${width}`,
    );
  }
  await page.screenshot({ path: `${output}/assets-375.png`, fullPage: true });
  await page.getByLabel("Tìm tài sản", { exact: true }).fill("không có");
  await expect(page.getByText("Không có tài sản phù hợp.")).toBeVisible();
  await page.getByRole("button", { name: "Xóa bộ lọc" }).click();
  await expect(page.locator("tbody tr:not(.asset-group-heading)")).toHaveCount(
    25,
  );
  await expect(
    page.getByRole("button", { name: "+ Thêm tài sản" }),
  ).toHaveCount(0);
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${output}/verification.json`,
    JSON.stringify({ passed: true, requests }, null, 2),
  );
  console.log(
    "PASS OM daily/weekly/monthly requests, segmented counts, room grouping, search/filter/pagination, Vietnamese conditions and responsive assets.",
  );
} finally {
  await browser.close();
}
