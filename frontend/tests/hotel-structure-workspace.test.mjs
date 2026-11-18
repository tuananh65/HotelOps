import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium, expect } from "@playwright/test";
import { structureReply } from "./independent-fixtures.mjs";

const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React from 'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';
import{AuthProvider}from'./src/auth/AuthProvider';import{BranchStructureTree}from'./src/hotel/BranchStructureTree';
import './src/styles.css';import './src/styles/operations.css';import './src/inventory/inventory.css';import './src/dev2/dev2.css';import './src/styles/cacao.css';import './src/styles/design-system.css';
const page=<BrowserRouter><BranchStructureTree/></BrowserRouter>;
createRoot(document.getElementById('root')).render(<React.StrictMode><AuthProvider fallback={page}>{page}</AuthProvider></React.StrictMode>);
`,
  },
  outfile: "structure-test.js",
  write: false,
  bundle: true,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(async () => {
  mkdirSync("../artifacts/hotel-structure-ui/screenshots", { recursive: true });
  browser = await chromium.launch({
    headless: true,
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
});
after(async () => browser?.close());

async function fixture(run, scenario = {}) {
  const context = await browser.newContext({
    viewport: { width: scenario.width ?? 1280, height: 900 },
  });
  const p = await context.newPage();
  p.setDefaultTimeout(10000);
  const calls = [],
    errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", async (route) => {
    const request = route.request(),
      url = new URL(request.url());
    if (url.pathname.startsWith("/api/"))
      calls.push({ url: url.href, method: request.method() });
    if (url.pathname === "/api/auth/session")
      return route.fulfill({
        json: {
          user: { id: "1", username: "fixture", name: "Quản trị viên" },
          mustChangePassword: false,
          expiresAt: "2099-01-01T00:00:00Z",
          absoluteExpiresAt: "2099-01-02T00:00:00Z",
          csrfToken: "fixture",
        },
      });
    if (url.pathname === "/api/branches/structure-options")
      return route.fulfill({
        status: scenario.optionsStatus ?? 200,
        json: scenario.optionsStatus
          ? {}
          : [
              {
                id: 1,
                code: "DVBV",
                name: "Khách sạn Đức Vượng — Chi nhánh Bùi Viện",
                isActive: true,
              },
              {
                id: 2,
                code: "CN02",
                name: "Chi nhánh thứ hai",
                isActive: false,
              },
            ],
      });
    if (/^\/api\/branches\/\d+\/structure$/.test(url.pathname)) {
      if (scenario.delay)
        await new Promise((r) => setTimeout(r, scenario.delay));
      if (scenario.status)
        return route.fulfill({
          status: scenario.status,
          json: { title: "PRIVATE_DATABASE_DETAIL" },
        });
      const data = structureReply(url.href, scenario.management !== false);
      data.visibility = scenario.management === false ? "basic" : "management";
      data.branch.name =
        data.branch.id === 1
          ? "Khách sạn Đức Vượng — Chi nhánh Bùi Viện"
          : "Chi nhánh thứ hai";
      for (const field of ["floors", "areas", "departments"])
        for (const item of data[field]?.items ?? [])
          item.name =
            field === "floors"
              ? "Tầng " + (item.id - 10)
              : field === "areas"
                ? "Khu vực " + (item.id - 11)
                : "Buồng phòng";
      if (data.selectedFloor)
        data.selectedFloor.name = "Tầng " + (data.selectedFloor.id - 10);
      if (data.selectedArea)
        data.selectedArea.name = "Khu vực " + (data.selectedArea.id - 11);
      if (scenario.visual && data.floors) {
        data.floors.items = data.floors.items.slice(0, 7);
        data.floors.totalCount = 7;
      }
      if (scenario.empty && data[data.collection]) {
        data[data.collection].items = [];
        data[data.collection].totalCount = 0;
      }
      return route.fulfill({ json: data });
    }
    if (url.pathname.startsWith("/api/"))
      return route.fulfill({ status: 404, json: {} });
    return route.fulfill({
      contentType: "text/html",
      body: '<!doctype html><html lang="vi"><meta name="viewport" content="width=device-width,initial-scale=1"><body style="padding:24px;box-sizing:border-box"><div id="root" class="cacao-content" style="padding:0"></div></body></html>',
    });
  });
  await p.goto("https://hotelops.test/hotel");
  await p.addStyleTag({
    content: bundle.outputFiles.find((f) => f.path.endsWith(".css")).text,
  });
  await p.addScriptTag({
    content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
  });
  try {
    await run(p, calls);
    assert.deepEqual(errors, []);
    assert.ok(calls.every((c) => c.method === "GET"));
    assert.ok(
      !(await p.locator("body").innerText()).includes(
        "PRIVATE_DATABASE_DETAIL",
      ),
    );
  } finally {
    await context.close();
  }
}
async function load(p, branch = "1") {
  await expect(p.locator("#structure-branch")).toBeEnabled();
  await p.getByLabel("Chi nhánh", { exact: true }).selectOption(branch);
  await p.getByRole("button", { name: "Xem cấu trúc", exact: true }).click();
}
const reads = (calls) => calls.filter((c) => /\/structure\?/.test(c.url));

test("Cấu trúc: đi từ tầng tới khu vực, phòng và quay lại đúng cấp", () =>
  fixture(async (p, calls) => {
    await load(p);
    await p.getByRole("button", { name: "Xem Tầng 1", exact: true }).click();
    await p.getByRole("button", { name: "Xem Khu vực 1", exact: true }).click();
    await p.getByRole("button", { name: "Xem Phòng 101", exact: true }).click();
    await expect(
      p.getByRole("complementary", { name: "Chi tiết mục đã chọn" }),
    ).toContainText("Số phòng");
    assert.deepEqual(
      reads(calls).map((c) => new URL(c.url).searchParams.get("nodeType")),
      ["branch", "floor", "area", "room"],
    );
    await p
      .getByRole("navigation", { name: "Đường dẫn cấu trúc" })
      .getByRole("button", { name: "Tầng 1", exact: true })
      .click();
    await expect(
      p.getByRole("button", { name: "Xem Khu vực 1", exact: true }),
    ).toBeVisible();
    assert.equal(
      new URL(reads(calls).at(-1).url).searchParams.get("nodeId"),
      "11",
    );
  }));
test("Bộ lọc thu gọn, phân trang và đổi chi nhánh không dùng dữ liệu cũ", () =>
  fixture(async (p, calls) => {
    await load(p);
    await expect(p.getByLabel("Bộ lọc hoạt động")).toBeHidden();
    await p.getByRole("button", { name: "Trang sau", exact: true }).click();
    await expect(p.getByText("Trang 2 / 2")).toBeVisible();
    const count = reads(calls).length;
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    assert.equal(reads(calls).length, count);
    await p.getByLabel("Bộ lọc hoạt động").selectOption("false");
    await expect(p.getByText("Trang 1 / 2")).toBeVisible();
    assert.equal(
      new URL(reads(calls).at(-1).url).searchParams.get("isActive"),
      "false",
    );
    await p.getByLabel("Chi nhánh", { exact: true }).selectOption("2");
    await expect(
      p.getByRole("region", { name: "Cấu trúc chi nhánh theo từng cấp" }),
    ).toHaveCount(0);
    await p.getByRole("button", { name: "Xem cấu trúc", exact: true }).click();
    await expect(
      p.getByRole("navigation", { name: "Đường dẫn cấu trúc" }),
    ).toContainText("Chi nhánh thứ hai");
  }));
for (const management of [false, true])
  test("Nhóm quản lý theo phản hồi máy chủ: " + management, () =>
    fixture(
      async (p) => {
        await load(p);
        await expect(
          p.getByRole("button", { name: "Xem Tầng 1", exact: true }),
        ).toBeVisible();
        assert.equal(
          await p.locator(".hotel-structure-management").count(),
          management ? 1 : 0,
        );
        assert.equal(
          await p
            .getByRole("navigation", { name: "Danh mục cấu trúc" })
            .getByRole("button", { name: "Bộ phận", exact: true })
            .count(),
          management ? 1 : 0,
        );
        if (management) {
          await p.getByText("Quản lý cấu trúc", { exact: true }).click();
          await expect(
            p.getByRole("link", { name: "Thêm tầng", exact: true }),
          ).toHaveAttribute(
            "href",
            "/structure/floors?branchId=1&action=create",
          );
        }
      },
      { management },
    ),
  );
test("Dữ liệu trống có hướng dẫn; không hiển thị mã kỹ thuật", () =>
  fixture(
    async (p) => {
      await load(p);
      await expect(
        p.getByText("Không có mục phù hợp", { exact: true }),
      ).toBeVisible();
      assert.ok(
        !/MSG10|isActive|floors|branchId/.test(
          await p.locator("main").innerText(),
        ),
      );
    },
    { empty: true },
  ));
test("Lỗi tải có thông báo, không biến thành danh sách rỗng", () =>
  fixture(
    async (p) => {
      await load(p);
      await expect(p.getByRole("alert")).toBeVisible();
      await expect(
        p.getByText("Không có mục phù hợp", { exact: true }),
      ).toHaveCount(0);
    },
    { status: 503 },
  ));
test("Đang tải dùng spinner và phản hồi cũ không xuất hiện sau đổi chi nhánh", () =>
  fixture(
    async (p) => {
      await load(p);
      await expect(
        p.getByRole("status", { name: "Đang tải cấu trúc khách sạn" }),
      ).toBeVisible();
      await p.getByLabel("Chi nhánh", { exact: true }).selectOption("2");
      await p
        .getByRole("button", { name: "Xem cấu trúc", exact: true })
        .click();
      await expect(
        p.getByRole("navigation", { name: "Đường dẫn cấu trúc" }),
      ).toContainText("Chi nhánh thứ hai");
      await expect(
        p.getByRole("complementary", { name: "Chi tiết mục đã chọn" }),
      ).not.toContainText("Bùi Viện");
    },
    { delay: 500 },
  ));
for (const width of [390, 1024, 1440])
  test("Bố cục cấu trúc " + width + "px", () =>
    fixture(
      async (p) => {
        await load(p);
        await expect(
          p.getByRole("button", { name: "Xem Tầng 1", exact: true }),
        ).toBeVisible();
        assert.ok(
          await p.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        );
        await p.screenshot({
          path:
            "../artifacts/hotel-structure-ui/screenshots/structure-" +
            width +
            ".png",
          fullPage: true,
        });
        await p.getByText("Quản lý cấu trúc", { exact: true }).click();
        assert.ok(
          await p.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth + 1,
          ),
        );
        await p.screenshot({
          path:
            "../artifacts/hotel-structure-ui/screenshots/management-" +
            width +
            ".png",
          fullPage: true,
        });
      },
      { width, visual: true },
    ),
  );
