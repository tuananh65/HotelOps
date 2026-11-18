import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium, expect } from "@playwright/test";

const output = await build({
  entryPoints: ["src/main.tsx"],
  bundle: true,
  write: false,
  outfile: "app.js",
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
  browser = await chromium.launch({
    headless: true,
    ...(existsSync(edge) ? { executablePath: edge } : {}),
  });
  mkdirSync("../artifacts/design-system-v2/screenshots", { recursive: true });
});
after(async () => browser?.close());
const dashboard = {
  snapshot: "2026-11-18T15:00:00Z",
  filters: {
    branchIds: [1],
    departmentId: null,
    from: "2026-11-01",
    to: "2026-11-18",
    calendar: "UTC",
  },
  metrics: [
    { key: "activeRooms", label: "Active rooms", value: 12, unit: "phòng" },
    { key: "readyRooms", label: "Ready rooms", value: 5, unit: "phòng" },
    { key: "readyRatio", label: "Ready ratio", value: 41.67, unit: "%" },
    { key: "openIncidents", label: "Open incidents", value: null, unit: "" },
  ],
  distributions: [
    {
      source: "rooms.effective_readiness",
      status: "READY",
      count: 5,
      severity: null,
    },
    {
      source: "rooms.effective_readiness",
      status: "PENDING_CLEANING",
      count: 7,
      severity: null,
    },
  ],
  quantities: [],
  missingSources: ["incidents"],
  categoryQuantities: [],
};
const defaultPaths = [
  "/dashboard",
  "/rooms/status",
  "/users",
  "/profile",
  "/notifications",
];
async function fixture(path, options, run, width = 1440) {
  const context = await browser.newContext({
    viewport: { width, height: 1000 },
  });
  const page = await context.newPage();
  const calls = [],
    errors = [];
  page.setDefaultTimeout(30000);
  page.setDefaultNavigationTimeout(60000);
  page.on("pageerror", (error) => errors.push(error.message));
  await page.route("**/*", async (route) => {
    const request = route.request(),
      url = new URL(request.url());
    if (request.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<html lang="vi"><meta name="viewport" content="width=device-width, initial-scale=1"><div id="root"></div></html>',
      });
    if (!url.pathname.startsWith("/api/")) return route.abort();
    calls.push({
      path: url.pathname,
      query: url.search,
      method: request.method(),
    });
    const reply = (body, status = 200) =>
      route.fulfill({
        contentType: "application/json",
        status,
        body: JSON.stringify(body),
      });
    if (url.pathname === "/api/auth/session")
      return reply({
        user: { id: "7", username: "review", name: "Nhân viên kiểm thử" },
        expiresAt: "2099-01-01T00:00:00Z",
        absoluteExpiresAt: "2099-01-02T00:00:00Z",
        mustChangePassword: false,
        csrfToken: "fixture-csrf",
      });
    if (url.pathname === "/api/auth/navigation")
      return reply({ paths: options.paths ?? defaultPaths });
    if (
      url.pathname === "/api/dashboards/options" ||
      url.pathname === "/api/dashboards/rooms/options"
    )
      return reply({
        branches: [{ id: 1, code: "BV", name: "Chi nhánh Bùi Viện" }],
        departments: [{ id: 11, branchId: 1, name: "Buồng phòng" }],
      });
    if (
      url.pathname === "/api/dashboards/operations" ||
      url.pathname === "/api/dashboards/rooms"
    )
      return options.denied
        ? reply({}, 403)
        : reply(options.dashboard ?? dashboard);
    if (url.pathname === "/api/notifications")
      return options.notificationError
        ? reply({}, 503)
        : reply({
            items: options.emptyInbox
              ? []
              : [
                  {
                    id: 91,
                    type: "TEST",
                    title: "Thông báo kiểm thử",
                    message: "Thông tin từ API hộp thư",
                    priority: "NORMAL",
                    createdAt: "2026-11-18T15:00:00Z",
                    readAt: null,
                  },
                ],
            page: 1,
            pageSize: Number(url.searchParams.get("pageSize")),
            unreadCount: options.emptyInbox ? 0 : 1,
            totalCount: options.emptyInbox ? 0 : 1,
            snapshot: dashboard.snapshot,
          });
    if (url.pathname.startsWith("/api/dashboards/sources/"))
      return reply({
        missingSources: [],
        totalCount: 0,
        page: 1,
        pageSize: 25,
        snapshot: dashboard.snapshot,
        records: { columns: [], rows: [] },
      });
    return reply({}, 403);
  });
  try {
    await page.goto(`http://hotelops.test${path}`);
    for (const file of output.outputFiles.filter((file) =>
      file.path.endsWith(".css"),
    ))
      await page.addStyleTag({ content: file.text });
    await page.addScriptTag({
      content: output.outputFiles.find((file) => file.path.endsWith(".js"))
        .text,
    });
    await expect(page.locator(".ds-shell")).toBeVisible();
    await run(page, calls);
    assert.deepEqual(errors, []);
    assert.ok(
      calls.every((call) => call.method === "GET"),
      "Design changes must not issue writes during reads",
    );
  } finally {
    await context.close();
  }
}
test("V2 sidebar collapses to 72px and keeps authorized navigation", async () =>
  fixture("/dashboard", {}, async (page) => {
    await expect(page.locator(".ds-sidebar")).toHaveCSS(
      "background-color",
      "rgb(255, 255, 255)",
    );
    await expect(page.locator(".ds-sidebar")).toHaveCSS("width", "240px");
    await page
      .getByRole("button", { name: "Thu gọn thanh điều hướng", exact: true })
      .click();
    await expect(page.locator(".ds-sidebar")).toHaveCSS("width", "72px");
    await page.getByRole("button", { name: "Người dùng", exact: true }).click();
    await expect(page.locator(".ds-sidebar")).toHaveCSS("width", "240px");
    await expect(page.locator('.ds-sidebar a[href="/users"]')).toBeVisible();
    assert.equal(
      await page
        .locator('.ds-sidebar a[href="/operations/maintenance"]')
        .count(),
      0,
    );
  }));
test("V2 header searches allowed routes with keyboard and hides unavailable routes", async () =>
  fixture("/dashboard", {}, async (page) => {
    const input = page.getByRole("combobox", { name: "Tìm chức năng" });
    await input.fill("người dùng");
    await expect(
      page.getByRole("option", { name: "Người dùng", exact: true }),
    ).toBeVisible();
    await input.press("ArrowDown");
    await expect(
      page.getByRole("option", { name: "Người dùng", exact: true }),
    ).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(input).toBeFocused();
    await input.fill("bảo trì");
    await expect(
      page.getByText("Không tìm thấy chức năng được phép truy cập."),
    ).toBeVisible();
  }));
test("V2 dashboard uses API metrics and never replaces null by zero", async () =>
  fixture("/dashboard", {}, async (page) => {
    await expect(page.locator(".ds-kpi")).toHaveCount(4);
    await expect(
      page.getByText("Chưa có thông tin", { exact: true }),
    ).toBeVisible();
    await expect(page.locator(".ds-readiness-heading")).toContainText(
      "12 phòng",
    );
    await page.screenshot({
      path: "../artifacts/design-system-v2/screenshots/dashboard-desktop.png",
      fullPage: true,
    });
  }));
test("P1 dashboard filters defer requests, preserve applied values and clear chips", async () =>
  fixture("/dashboard", {}, async (page, calls) => {
    await expect(page.locator(".ds-kpi")).toHaveCount(4);
    const reads = () =>
      calls.filter((c) => c.path === "/api/dashboards/operations").length;
    const before = reads();
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    const panel = page.locator(".ds-filter-panel");
    await panel.getByLabel("Chi nhánh", { exact: true }).selectOption("1");
    assert.equal(reads(), before);
    await page.getByRole("button", { name: "Ẩn bộ lọc", exact: true }).click();
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await expect(panel.getByLabel("Chi nhánh", { exact: true })).toHaveValue(
      "",
    );
    await panel.getByLabel("Chi nhánh", { exact: true }).selectOption("1");
    await panel.getByLabel("Bộ phận", { exact: true }).selectOption("11");
    await panel.getByRole("button", { name: "Áp dụng bộ lọc" }).click();
    await expect.poll(reads).toBe(before + 1);
    await page
      .getByRole("button", { name: "Ẩn bộ lọc (2)", exact: true })
      .click();
    await page.getByRole("button", { name: "Bộ lọc (2)", exact: true }).click();
    await expect(panel.getByLabel("Bộ phận", { exact: true })).toHaveValue(
      "11",
    );
    await page
      .getByRole("button", { name: "Ẩn bộ lọc (2)", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Xóa bộ lọc bộ phận", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Bộ lọc (1)", exact: true }),
    ).toBeVisible();
  }));
test("P1 filters are collapsible regions rather than modal focus traps", async () =>
  fixture("/dashboard", {}, async (page) => {
    await expect(page.locator(".ds-filter-panel")).toBeHidden();
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await expect(page.locator(".ds-filter-panel")).toBeVisible();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: "Ẩn bộ lọc", exact: true }),
    ).toHaveAttribute("aria-expanded", "true");
    await page.getByRole("button", { name: "Ẩn bộ lọc", exact: true }).click();
    await expect(page.locator(".ds-filter-panel")).toBeHidden();
  }));
test("V2 dashboard access denial has retry and no stale metrics", async () =>
  fixture("/dashboard", { denied: true }, async (page) => {
    await expect(page.getByRole("alert")).toContainText("không có quyền");
    await expect(page.locator(".ds-kpi")).toHaveCount(0);
  }));
test("V2 notification preview only appears with access and uses the real inbox contract", async () =>
  fixture("/dashboard", {}, async (page, calls) => {
    assert.equal(
      calls.filter(
        (call) =>
          call.path === "/api/notifications" &&
          call.query.includes("pageSize=5"),
      ).length,
      0,
    );
    await page
      .getByRole("button", { name: "Mở thông báo", exact: true })
      .click();
    await expect(
      page.getByText("Thông báo kiểm thử", { exact: true }),
    ).toBeVisible();
    assert.ok(
      calls.some(
        (call) =>
          call.path === "/api/notifications" &&
          call.query.includes("pageSize=5"),
      ),
    );
    await page.keyboard.press("Escape");
    await expect(
      page.getByRole("button", { name: "Mở thông báo", exact: true }),
    ).toBeFocused();
  }));
test("V2 notification action is absent without navigation access", async () =>
  fixture(
    "/dashboard",
    { paths: ["/dashboard", "/profile"] },
    async (page, calls) => {
      await expect(page.locator(".ds-kpi")).toHaveCount(4);
      assert.equal(
        await page
          .getByRole("button", { name: "Mở thông báo", exact: true })
          .count(),
        0,
      );
      assert.equal(
        calls.some((call) => call.path === "/api/notifications"),
        false,
      );
    },
  ));
test("V2 tablet and mobile navigation remain usable without page overflow", async () => {
  for (const width of [800, 390])
    await fixture(
      "/dashboard",
      {},
      async (page) => {
        await expect(page.locator(".ds-kpi")).toHaveCount(4);
        assert.equal(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
          true,
        );
        await page
          .getByRole("button", { name: "Mở điều hướng", exact: true })
          .click();
        const dialog = page.getByRole("dialog", {
          name: "Điều hướng HotelOps",
          exact: true,
        });
        await expect(dialog).toBeVisible();
        await page.keyboard.press("Escape");
        await page.screenshot({
          path: `../artifacts/design-system-v2/screenshots/dashboard-${width}.png`,
          fullPage: true,
        });
      },
      width,
    );
});
test("V2 reduced motion disables spinner and skeleton animations", async () =>
  fixture("/dashboard", {}, async (page) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page
      .getByRole("button", { name: "Mở thông báo", exact: true })
      .click();
    await expect(
      page.getByText("Thông báo kiểm thử", { exact: true }),
    ).toBeVisible();
    const animations = await page
      .locator(".ds-spin, .loading-indicator__ring, .ds-skeleton > span")
      .evaluateAll((elements) =>
        elements.map((element) => getComputedStyle(element).animationName),
      );
    assert.ok(animations.every((name) => name === "none"));
  }));
