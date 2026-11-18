import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium, expect } from "@playwright/test";

const tag = '"AAAAAAAAAAA="',
  next = '"AAAAAAAAAAE="';
const branch = {
  id: 1,
  code: "BV",
  name: "Chi nhánh Bùi Viện",
  address: "Đường Bùi Viện",
  phone: null,
  email: null,
  timezone: "Asia/Ho_Chi_Minh",
  isActive: true,
};
const ready = {
  branchId: 1,
  activeRooms: 0,
  warehousesWithNonzeroStock: 0,
  states: [],
  canDeactivate: true,
  coordination: "READY",
  scopeResolutionComplete: true,
  hasUnresolvedStates: false,
};
const output = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode} from 'react';import{createRoot}from'react-dom/client';
import{BrowserRouter,Routes,Route}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';
import{BranchManagementPage}from'./src/branch-maintenance/BranchManagementPage';import{CreateBranchRoute}from'./src/branch-maintenance/CreateBranchRoute';import{BranchMaintenanceRoute}from'./src/branch-maintenance/BranchMaintenanceRoute';
import './src/styles/design-system.css';
const routes=<BrowserRouter><Routes><Route path="/branches" element={<BranchManagementPage/>}/><Route path="/branches/new" element={<CreateBranchRoute/>}/><Route path="/branches/:branchId/edit" element={<BranchMaintenanceRoute/>}/></Routes></BrowserRouter>;
createRoot(document.getElementById('root')).render(<StrictMode><AuthProvider fallback={routes}>{routes}</AuthProvider></StrictMode>);
`,
  },
  outfile: "branch-test.js",
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(async () => {
  mkdirSync("../artifacts/branch-management-fix/screenshots", {
    recursive: true,
  });
  browser = await chromium.launch({
    headless: true,
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
});
after(async () => browser?.close());
async function fixture(path, run, scenario = {}) {
  const context = await browser.newContext({
      viewport: { width: scenario.width ?? 1280, height: 900 },
    }),
    page = await context.newPage();
  page.setDefaultTimeout(10000);
  const calls = [],
    errors = [];
  let stored = { ...branch },
    version = tag;
  const branches = Array.from({ length: 23 }, (_, i) => ({
    ...branch,
    id: i + 1,
    code: `CN${i + 1}`,
    name: `Chi nhánh ${i + 1}`,
    isActive: i % 2 === 0,
  }));
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route("**/*", async (route) => {
    const req = route.request(),
      path = new URL(req.url()).pathname,
      method = req.method();
    calls.push({
      path,
      method,
      body: req.postDataJSON(),
      headers: req.headers(),
    });
    if (path === "/api/auth/session")
      return route.fulfill({
        json: {
          user: { id: "1", username: "fixture", name: "Quản trị viên" },
          mustChangePassword: false,
          expiresAt: "2099-01-01T00:00:00Z",
          absoluteExpiresAt: "2099-01-02T00:00:00Z",
          csrfToken: "fixture",
        },
      });
    if (path === "/api/auth/csrf")
      return route.fulfill({
        json: { headerName: "X-CSRF-TOKEN", csrfToken: "fixture" },
      });
    if (path === "/api/branches/management-options")
      return route.fulfill({
        status: scenario.optionsStatus ?? 200,
        json: scenario.optionsStatus
          ? { title: "BRANCH_ROWVERSION_SCHEMA_REQUIRED" }
          : { timezone: branch.timezone, pageSize: 20, branches },
      });
    if (path.startsWith("/api/branches/") && path.endsWith("/edit"))
      return route.fulfill({
        status: scenario.readStatus ?? 200,
        json: stored,
        headers: { ETag: version },
      });
    if (path.endsWith("/deactivation-dependencies"))
      return route.fulfill({
        status: scenario.dependencyStatus ?? 200,
        json: scenario.dependencies ?? ready,
        headers: { ETag: version },
      });
    if (path === "/api/branches" && method === "POST") {
      if (scenario.network) return route.abort();
      if (scenario.createStatus)
        return route.fulfill({
          status: scenario.createStatus,
          json: { title: "PRIVATE_SERVER_DETAIL" },
        });
      stored = { ...branch, ...req.postDataJSON(), id: 42 };
      return route.fulfill({
        status: 201,
        json: stored,
        headers: { ETag: next },
      });
    }
    if (path === "/api/branches/1" && method === "PUT") {
      await new Promise((r) => setTimeout(r, 100));
      if (scenario.network) return route.abort();
      if (scenario.updateStatus)
        return route.fulfill({
          status: scenario.updateStatus,
          json: { title: "PRIVATE_SERVER_DETAIL" },
        });
      stored = { ...stored, ...req.postDataJSON() };
      version = next;
      return route.fulfill({ json: stored, headers: { ETag: version } });
    }
    if (path.endsWith("/deactivate") && method === "POST") {
      await new Promise((r) => setTimeout(r, 100));
      if (scenario.network) return route.abort();
      if (scenario.deactivateStatus)
        return route.fulfill({
          status: scenario.deactivateStatus,
          json: { title: "PRIVATE_SERVER_DETAIL" },
        });
      stored = { ...stored, isActive: false };
      version = next;
      return route.fulfill({ json: stored, headers: { ETag: version } });
    }
    if (path.startsWith("/api/"))
      return route.fulfill({ status: 404, json: {} });
    return route.fulfill({
      contentType: "text/html",
      body: '<!doctype html><html lang="vi"><meta name="viewport" content="width=device-width,initial-scale=1"><body style="margin:0;padding:24px;box-sizing:border-box"><div id="root"></div></body></html>',
    });
  });
  await page.goto("https://hotelops.test" + path);
  await page.addStyleTag({
    content: output.outputFiles.find((f) => f.path.endsWith(".css")).text,
  });
  await page.addScriptTag({
    content: output.outputFiles.find((f) => f.path.endsWith(".js")).text,
  });
  try {
    await run(page, calls);
    assert.deepEqual(errors, []);
    assert.ok(
      !(await page.locator("body").innerText()).includes(
        "PRIVATE_SERVER_DETAIL",
      ),
    );
  } finally {
    await context.close();
  }
}
const writes = (calls) =>
  calls.filter((c) => ["POST", "PUT"].includes(c.method));
async function fillCreate(p) {
  await p.getByLabel("Mã chi nhánh", { exact: true }).fill("B-NEW");
  await p.getByLabel("Tên chi nhánh", { exact: true }).fill("Chi nhánh mới");
  await p.getByLabel("Địa chỉ", { exact: true }).fill("123 Nguyễn Trãi");
  await p
    .getByRole("button", { name: "Xem lại thông tin", exact: true })
    .click();
}
async function reviewEdit(p) {
  await p
    .getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true })
    .click();
  await p.getByLabel("Tên chi nhánh", { exact: true }).fill("Tên đã cập nhật");
  await p
    .getByLabel("Lý do thay đổi", { exact: true })
    .fill("Điều chỉnh thông tin liên hệ");
  await p
    .getByRole("button", { name: "Xem lại cập nhật", exact: true })
    .click();
}
async function reviewDeactivate(p) {
  await p
    .getByRole("button", { name: "Ngừng hoạt động chi nhánh", exact: true })
    .click();
  await p
    .getByLabel("Lý do ngừng hoạt động")
    .fill("Chi nhánh chuyển sang địa điểm mới");
  await p.getByRole("checkbox").check();
}

test("Danh sách: phân trang, tìm kiếm và bộ lọc đóng mặc định, không gọi API khi mở lọc", () =>
  fixture("/branches", async (p, calls) => {
    await expect(p.getByText("23 chi nhánh", { exact: true })).toBeVisible();
    await expect(p.getByLabel("Trạng thái", { exact: true })).toBeHidden();
    await p.getByRole("button", { name: "Trang sau" }).click();
    await expect(p.getByText("Trang 2 / 2")).toBeVisible();
    const count = calls.filter((c) =>
      c.path.includes("management-options"),
    ).length;
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    assert.equal(
      calls.filter((c) => c.path.includes("management-options")).length,
      count,
    );
    await p.getByLabel("Trạng thái", { exact: true }).selectOption("false");
    await expect(p.getByText("11 chi nhánh", { exact: true })).toBeVisible();
    await expect(p.getByText("Trang 1 / 1")).toBeVisible();
    await p.getByLabel("Tìm chi nhánh").fill("không có");
    await expect(
      p.getByText("Không tìm thấy chi nhánh phù hợp."),
    ).toBeVisible();
    assert.equal(writes(calls).length, 0);
  }));
test("Danh sách lỗi schema: thông báo rõ và cho phép thử lại", () =>
  fixture(
    "/branches",
    async (p) => {
      await expect(p.getByRole("alert")).toContainText(
        "Cơ sở dữ liệu chưa được cập nhật",
      );
      await expect(p.getByRole("link", { name: "Tạo chi nhánh" })).toHaveCount(
        0,
      );
      await expect(p.getByRole("button", { name: "Thử lại" })).toBeEnabled();
    },
    { optionsStatus: 503 },
  ));
test("Tạo mới: validation, review không gửi và xác nhận hai lần chỉ POST một lần", () =>
  fixture("/branches/new", async (p, calls) => {
    await p
      .getByRole("button", { name: "Xem lại thông tin", exact: true })
      .click();
    assert.equal(writes(calls).length, 0);
    await fillCreate(p);
    const dialog = p.getByRole("dialog");
    await expect(dialog).toContainText("Chi nhánh mới");
    assert.equal(writes(calls).length, 0);
    await dialog
      .getByRole("button", { name: "Xác nhận tạo mới", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await expect(p.getByText("Đã tạo chi nhánh thành công.")).toBeVisible();
    await expect(dialog).toHaveCount(0);
    assert.equal(writes(calls).length, 1);
    assert.equal(writes(calls)[0].body.code, "B-NEW");
  }));
test("Tạo trùng mã: giữ thông tin và sửa lại sau phản hồi 409", () =>
  fixture(
    "/branches/new",
    async (p, calls) => {
      await fillCreate(p);
      await p
        .getByRole("button", { name: "Xác nhận tạo mới", exact: true })
        .click();
      await expect(p.getByRole("dialog")).toContainText(
        "Mã chi nhánh đã tồn tại",
      );
      await p.getByRole("button", { name: "Hủy", exact: true }).click();
      await expect(p.getByLabel("Tên chi nhánh", { exact: true })).toHaveValue(
        "Chi nhánh mới",
      );
      assert.equal(writes(calls).length, 1);
    },
    { createStatus: 409 },
  ));
test("Chỉnh sửa: xác nhận nằm trong popup, lưu chính xác một lần và cập nhật chi tiết", () =>
  fixture("/branches/1/edit", async (p, calls) => {
    await reviewEdit(p);
    const dialog = p.getByRole("dialog");
    await expect(dialog).toContainText("Tên đã cập nhật");
    assert.equal(writes(calls).length, 0);
    await dialog
      .getByRole("button", { name: "Xác nhận cập nhật", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await expect(
      p.getByText("Đã cập nhật thông tin chi nhánh.", { exact: true }),
    ).toBeVisible();
    await expect(dialog).toHaveCount(0);
    await expect(p.getByRole("heading", { level: 1 })).toHaveText(
      "Tên đã cập nhật",
    );
    assert.equal(writes(calls).length, 1);
    assert.equal(writes(calls)[0].headers["if-match"], tag);
  }));
test("Hủy chỉnh sửa bỏ bản nháp và không gọi PUT", () =>
  fixture("/branches/1/edit", async (p, calls) => {
    await p
      .getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true })
      .click();
    await p.getByLabel("Tên chi nhánh", { exact: true }).fill("Bản nháp");
    p.once("dialog", (d) => d.accept());
    await p.getByRole("button", { name: "Hủy", exact: true }).click();
    await p
      .getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true })
      .click();
    await expect(p.getByLabel("Tên chi nhánh", { exact: true })).toHaveValue(
      branch.name,
    );
    assert.equal(writes(calls).length, 0);
  }));
test("Phiên bản 412: cần tải lại và review mới, không tự gửi lại", () =>
  fixture(
    "/branches/1/edit",
    async (p, calls) => {
      await reviewEdit(p);
      await p
        .getByRole("button", { name: "Xác nhận cập nhật", exact: true })
        .click();
      await expect(p.getByRole("alert")).toContainText("Phiên bản đã đổi");
      await p.getByRole("button", { name: "Tải lại thông tin" }).click();
      await expect(
        p.getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true }),
      ).toBeVisible();
      assert.equal(writes(calls).length, 1);
    },
    { updateStatus: 412 },
  ));
test("Mất kết nối sau PUT: khóa gửi lại kể cả tải lại", () =>
  fixture(
    "/branches/1/edit",
    async (p, calls) => {
      await reviewEdit(p);
      await p
        .getByRole("button", { name: "Xác nhận cập nhật", exact: true })
        .click();
      await expect(p.getByRole("dialog")).toContainText(
        "Chưa xác định kết quả cập nhật",
      );
      p.once("dialog", (d) => d.accept());
      await p.getByRole("button", { name: "Hủy", exact: true }).click();
      await p.getByRole("button", { name: "Tải lại thông tin" }).click();
      await expect(
        p.getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true }),
      ).toBeDisabled();
      assert.equal(writes(calls).length, 1);
    },
    { network: true },
  ));
test("403 đọc: không có form sửa hoặc nút ngừng hoạt động", () =>
  fixture(
    "/branches/1/edit",
    async (p) => {
      await expect(p.getByRole("alert")).toContainText("từ chối quyền");
      await expect(
        p.getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true }),
      ).toHaveCount(0);
      await expect(
        p.getByRole("button", {
          name: "Ngừng hoạt động chi nhánh",
          exact: true,
        }),
      ).toHaveCount(0);
    },
    { readStatus: 403 },
  ));
test("Phụ thuộc còn phòng: chặn ngừng hoạt động, chỉ đọc", () =>
  fixture(
    "/branches/1/edit",
    async (p, calls) => {
      await p
        .getByRole("button", { name: "Ngừng hoạt động chi nhánh", exact: true })
        .click();
      await expect(p.getByRole("dialog")).toContainText(
        "Chưa thể ngừng hoạt động",
      );
      await expect(
        p.getByRole("button", {
          name: "Xác nhận ngừng hoạt động",
          exact: true,
        }),
      ).toHaveCount(0);
      assert.equal(writes(calls).length, 0);
    },
    {
      dependencies: {
        ...ready,
        activeRooms: 3,
        canDeactivate: false,
        coordination: "DEPENDENCIES_EXIST",
      },
    },
  ));
test("Lỗi kiểm tra phụ thuộc: thông báo cấu hình rõ, không cho xác nhận", () =>
  fixture(
    "/branches/1/edit",
    async (p, calls) => {
      await p
        .getByRole("button", { name: "Ngừng hoạt động chi nhánh", exact: true })
        .click();
      await expect(p.getByRole("dialog")).toContainText(
        "Cơ sở dữ liệu cần được cập nhật",
      );
      await expect(
        p.getByRole("button", {
          name: "Xác nhận ngừng hoạt động",
          exact: true,
        }),
      ).toHaveCount(0);
      assert.equal(writes(calls).length, 0);
    },
    { dependencyStatus: 503 },
  ));
test("Ngừng hoạt động: CSRF, If-Match, ngăn gửi trùng, cập nhật trạng thái ngay", () =>
  fixture("/branches/1/edit", async (p, calls) => {
    await reviewDeactivate(p);
    await p
      .getByRole("button", { name: "Xác nhận ngừng hoạt động", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await expect(p.getByRole("dialog")).toHaveCount(0);
    await expect(
      p.getByText("Đã ngừng hoạt động chi nhánh.", { exact: true }),
    ).toBeVisible();
    await expect(
      p.getByRole("button", { name: "Ngừng hoạt động chi nhánh", exact: true }),
    ).toHaveCount(0);
    assert.equal(writes(calls).length, 1);
    assert.equal(writes(calls)[0].headers["if-match"], tag);
    assert.equal(writes(calls)[0].headers["x-csrf-token"], "fixture");
  }));
test("Ngừng hoạt động mất phản hồi: không tự gửi lại", () =>
  fixture(
    "/branches/1/edit",
    async (p, calls) => {
      await reviewDeactivate(p);
      await p
        .getByRole("button", { name: "Xác nhận ngừng hoạt động", exact: true })
        .click();
      await expect(p.getByRole("dialog")).toContainText(
        "Chưa xác định kết quả ngừng hoạt động",
      );
      await expect(
        p.getByRole("button", {
          name: "Xác nhận ngừng hoạt động",
          exact: true,
        }),
      ).toHaveCount(0);
      assert.equal(writes(calls).length, 1);
      p.once("dialog", (d) => d.accept());
      await p.getByRole("button", { name: "Hủy", exact: true }).click();
      await p.getByRole("button", { name: "Tải lại thông tin" }).click();
      await expect(
        p.getByRole("button", {
          name: "Ngừng hoạt động chi nhánh",
          exact: true,
        }),
      ).toBeDisabled();
      assert.equal(writes(calls).length, 1);
    },
    { network: true },
  ));
for (const width of [390, 1280])
  test(`Giao diện chi nhánh ${width}px: form và footer không tràn`, () =>
    fixture(
      "/branches/1/edit",
      async (p) => {
        await p
          .getByRole("button", { name: "Chỉnh sửa chi nhánh", exact: true })
          .click();
        await expect(
          p.getByLabel("Tên chi nhánh", { exact: true }),
        ).toBeVisible();
        assert.ok(
          await p
            .getByRole("dialog")
            .evaluate((el) => el.scrollWidth <= el.clientWidth + 1),
        );
        await p.screenshot({
          path: `../artifacts/branch-management-fix/screenshots/edit-${width}.png`,
          fullPage: true,
        });
      },
      { width },
    ));
