import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, existsSync, mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium, expect } from "@playwright/test";

const bundle = await build({
  stdin: {
    contents: 'import "./src/main.tsx";',
    loader: "tsx",
    resolveDir: process.cwd(),
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  loader: { ".css": "empty" },
  define: { "process.env.NODE_ENV": '"test"', "import.meta.env": "{}" },
});
const css = [
  "src/styles/tokens.css",
  "src/styles.css",
  "src/styles/cacao.css",
  "src/users/user-workspace.css",
  "src/components/loading-indicator.css",
  "src/styles/design-system-tokens.css",
  "src/styles/design-system.css",
]
  .map((path) =>
    readFileSync(path, "utf8")
      .replace(/\uFEFF/g, "")
      .replace(/@import[^;]+;/g, ""),
  )
  .join("\n");
const branches = [
  { id: 1, code: "BV", name: "Chi nhánh Bùi Viện", isActive: true },
  { id: 2, code: "BT", name: "Chi nhánh Bình Thạnh", isActive: true },
  { id: 3, code: "OLD", name: "Chi nhánh đã đóng", isActive: false },
];
const departments = (branchId) => [
  { id: branchId * 10 + 1, code: "HK", name: "Buồng phòng", isActive: true },
  { id: branchId * 10 + 2, code: "MT", name: "Kỹ thuật", isActive: true },
];
const roles = [
  {
    roleId: 1,
    roleCode: "HK_STAFF",
    roleName: "Nhân viên buồng phòng",
    isActive: true,
  },
  {
    roleId: 2,
    roleCode: "HK_SUPERVISOR",
    roleName: "Giám sát buồng phòng",
    isActive: true,
  },
  {
    roleId: 3,
    roleCode: "MAINTENANCE_STAFF",
    roleName: "Nhân viên kỹ thuật",
    isActive: true,
  },
  {
    roleId: 4,
    roleCode: "SYS_ADMIN",
    roleName: "Quản trị viên",
    isActive: true,
  },
];
const user = {
  id: 42,
  fullName: "Nguyễn Văn An",
  employeeCode: "NV042",
  username: "an.nguyen",
  email: "an@example.test",
  accountStatus: "ACTIVE",
  primaryBranch: { id: 1, code: "BV", name: branches[0].name },
  department: { id: 11, code: "HK", name: "Buồng phòng" },
  roles: [
    {
      id: 1,
      code: "HK_STAFF",
      name: "Nhân viên buồng phòng",
      branch: { id: 1, code: "BV", name: branches[0].name },
    },
  ],
};
let browser;
before(async () => {
  const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
  browser = await chromium.launch({
    headless: true,
    ...(existsSync(edge) ? { executablePath: edge } : {}),
  });
});
after(async () => {
  await browser?.close();
});
async function fixture(path, options, run) {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  const calls = [],
    errors = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.route("**/*", async (route) => {
    const request = route.request(),
      url = new URL(request.url());
    if (request.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<html lang="vi"><head><meta name="viewport" content="width=device-width, initial-scale=1"></head><body><div id="root"></div></body></html>',
      });
    if (!url.pathname.startsWith("/api/")) return route.abort();
    calls.push({
      path: url.pathname,
      query: url.searchParams.toString(),
      method: request.method(),
      body: request.postDataJSON(),
    });
    const reply = (body, status = 200, headers) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
        headers,
      });
    if (url.pathname === "/api/auth/session")
      return reply({
        user: { id: "7", username: "admin", name: "Quản trị viên" },
        expiresAt: "2099-01-01T00:00:00Z",
        absoluteExpiresAt: "2099-01-02T00:00:00Z",
        mustChangePassword: false,
        csrfToken: "fixture-csrf",
      });
    if (url.pathname === "/api/auth/navigation/operation-grants")
      return reply({
        grants: options.creationDenied
          ? []
          : ["USER_MANAGE", "ROLE_MANAGE"].map((permission) => ({
              permission,
              branchId: null,
              scope: "GLOBAL",
            })),
      });
    if (url.pathname === "/api/auth/navigation")
      return reply({ paths: ["/users", "/roles", "/profile"] });
    if (url.pathname === "/api/auth/csrf")
      return reply({ csrfToken: "fixture-csrf", headerName: "X-CSRF-TOKEN" });
    if (url.pathname === "/api/branches/structure-options")
      return reply(branches);
    if (url.pathname === "/api/roles")
      return options.lookupFailure
        ? reply({}, 503)
        : reply({ items: roles, total: roles.length, page: 1, pageSize: 100 });
    const match = /^\/api\/branches\/(\d+)\/structure$/.exec(url.pathname);
    if (match) {
      if (options.delayFirstBranch && match[1] === "1")
        await new Promise((resolve) => setTimeout(resolve, 300));
      return reply({
        departments: {
          items: departments(Number(match[1])),
          page: 1,
          pageSize: 100,
          totalCount: 2,
        },
      });
    }
    if (url.pathname === "/api/users" && request.method() === "GET")
      return reply({
        items: options.empty ? [] : [user],
        page: Number(url.searchParams.get("page") || 1),
        pageSize: Number(url.searchParams.get("pageSize") || 20),
        totalCount: options.empty ? 0 : 41,
      });
    if (url.pathname === "/api/users" && request.method() === "POST") {
      if (options.duplicate)
        return reply(
          {
            status: 409,
            duplicateFields: ["username"],
            title: "Employee code, username or email already exists",
          },
          409,
        );
      if (options.unknown) return route.abort("failed");
      const input = request.postDataJSON();
      return reply(
        {
          user: { id: 42, ...input, accountStatus: "ACTIVE" },
          temporaryCredential: {
            password: "Fixture-only-secret!",
            expiresAtUtc: "2099-01-01T00:00:00Z",
          },
        },
        201,
        { Location: "/api/users/42" },
      );
    }
    return reply({}, 404);
  });
  try {
    await page.goto(`http://hotelops.test${path}`);
    await page.addStyleTag({ content: css });
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await run(page, calls);
    assert.deepEqual(errors, []);
  } finally {
    await context.close();
  }
}
async function fill(page) {
  await page
    .getByRole("heading", { name: "Tạo tài khoản nhân viên", exact: true })
    .waitFor();
  await page.locator('[name="fullName"]').fill("Nguyễn Văn An");
  await page.locator('[name="employeeCode"]').fill("NV042");
  await page.locator('[name="username"]').fill("an.nguyen");
  await page.locator('[name="email"]').fill("an@example.test");
  await page.locator('[name="branchId"]').selectOption("1");
  await page.locator('[name="departmentId"]').selectOption("11");
  await page.locator('[name="roleCode"]').selectOption("HK_STAFF");
}
test("directory: named filters, search, pagination, quick view and reset", async () =>
  fixture("/users", {}, async (page, calls) => {
    await expect(
      page.getByRole("heading", { name: "Quản lý người dùng" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.getByLabel("Chi nhánh chính", { exact: true }).selectOption("1");
    await page.getByLabel("Bộ phận", { exact: true }).selectOption("11");
    await page.getByLabel("Vai trò", { exact: true }).selectOption("1");
    await page.getByRole("searchbox").fill("An");
    await page.getByRole("button", { name: "Tìm kiếm", exact: true }).click();
    await expect
      .poll(() => calls.filter((c) => c.path === "/api/users").at(-1)?.query)
      .toContain("departmentId=11");
    await page.getByRole("button", { name: "Thao tác Nguyễn Văn An" }).click();
    await page
      .getByRole("menuitem", { name: "Xem nhanh", exact: true })
      .click();
    await expect(
      page.getByRole("dialog", { name: "Xem nhanh tài khoản" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Đóng xem nhanh" }).click();
    await page.getByRole("button", { name: "Trang sau", exact: true }).click();
    await expect
      .poll(() => calls.filter((c) => c.path === "/api/users").at(-1)?.query)
      .toContain("page=2");
    await page
      .getByRole("navigation", { name: "Phân trang", exact: true })
      .getByLabel("Số dòng mỗi trang", { exact: true })
      .selectOption("10");
    await expect
      .poll(
        () => calls.filter((call) => call.path === "/api/users").at(-1)?.query,
      )
      .toContain("pageSize=10");
    assert.ok(
      calls
        .filter((call) => call.path === "/api/users")
        .at(-1)
        ?.query.includes("search=An"),
    );
    await page.getByRole("button", { name: "Xóa bộ lọc" }).click();
    await expect(page.getByRole("searchbox")).toHaveValue("");
    mkdirSync("../artifacts/user-management-ui", { recursive: true });
    await page.screenshot({
      path: "../artifacts/user-management-ui/directory-desktop.png",
      fullPage: true,
    });
    await page.setViewportSize({ width: 390, height: 844 });
    await expect
      .poll(() =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth + 1,
        ),
      )
      .toBe(true);
  }));
test("create: department/role dependencies, no inactive branches or administrator role", async () =>
  fixture("/users/new", {}, async (page) => {
    await fill(page);
    assert.equal(
      await page.locator('[name="branchId"] option[value="3"]').count(),
      0,
    );
    assert.equal(
      await page.locator('[name="roleCode"] option[value="SYS_ADMIN"]').count(),
      0,
    );
    assert.equal(
      await page
        .locator('[name="roleCode"] option[value="MAINTENANCE_STAFF"]')
        .count(),
      0,
    );
    await page.screenshot({
      path: "../artifacts/user-management-ui/create-desktop.png",
      fullPage: true,
    });
    await page.locator('[name="branchId"]').selectOption("2");
    await expect(page.locator('[name="departmentId"]')).toHaveValue("");
    await expect(page.locator('[name="roleCode"]')).toHaveValue("");
    await expect(
      page.getByRole("button", { name: "Tạo tài khoản", exact: true }),
    ).toBeDisabled();
    await page.locator('[name="departmentId"]').selectOption("22");
    await page.locator('[name="roleCode"]').selectOption("MAINTENANCE_STAFF");
    await page.setViewportSize({ width: 390, height: 844 });
    await expect
      .poll(() =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth + 1,
        ),
      )
      .toBe(true);
    await page.screenshot({
      path: "../artifacts/user-management-ui/create-mobile.png",
      fullPage: true,
    });
  }));
test("create: validated IDs sent once, green success and one-time password handoff", async () =>
  fixture("/users/new", {}, async (page, calls) => {
    await fill(page);
    await page
      .getByRole("button", { name: "Tạo tài khoản", exact: true })
      .click();
    await expect(page.locator(".user-success")).toBeVisible();
    const posts = calls.filter((c) => c.method === "POST");
    assert.equal(posts.length, 1);
    assert.equal(posts[0].body.branchId, 1);
    assert.equal(posts[0].body.departmentId, 11);
    assert.equal(posts[0].body.roleCode, "HK_STAFF");
    assert.ok(
      !(await page.locator("body").innerText()).includes(
        "Fixture-only-secret!",
      ),
    );
    await page
      .getByRole("button", { name: "Hiện mật khẩu", exact: true })
      .click();
    await expect(
      page.getByLabel("Mật khẩu ban đầu", { exact: true }),
    ).toHaveText("Fixture-only-secret!");
    await page
      .getByRole("button", { name: "Tôi đã bàn giao mật khẩu an toàn" })
      .click();
    assert.ok(
      !(await page.locator("body").innerText()).includes(
        "Fixture-only-secret!",
      ),
    );
  }));
test("create: duplicate username stays editable with inline error", async () =>
  fixture("/users/new", { duplicate: true }, async (page) => {
    await fill(page);
    await page
      .getByRole("button", { name: "Tạo tài khoản", exact: true })
      .click();
    await expect(page.locator('[name="username"]')).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    await expect(page.locator("#create-username-duplicate")).toContainText(
      "đã được sử dụng",
    );
    await expect(page.locator('[name="fullName"]')).toHaveValue(
      "Nguyễn Văn An",
    );
  }));
test("create: ambiguous network outcome cannot resubmit", async () =>
  fixture("/users/new", { unknown: true }, async (page, calls) => {
    await fill(page);
    await page
      .getByRole("button", { name: "Tạo tài khoản", exact: true })
      .click();
    await expect(page.getByRole("alert")).toContainText("Không gửi lại");
    await expect(
      page.getByRole("button", { name: "Tạo tài khoản", exact: true }),
    ).toHaveCount(0);
    assert.equal(calls.filter((c) => c.method === "POST").length, 1);
  }));
test("create: failed lookup offers retry and blocks submission", async () =>
  fixture("/users/new", { lookupFailure: true }, async (page) => {
    await expect(
      page.getByRole("button", { name: "Tải lại vai trò" }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Tạo tài khoản", exact: true }),
    ).toBeDisabled();
  }));
test("directory: empty results explain how to recover", async () =>
  fixture("/users", { empty: true }, async (page) => {
    await expect(
      page.getByText("Không tìm thấy người dùng", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Trang sau", exact: true }),
    ).toBeDisabled();
  }));
test("create: switching branch ignores delayed old departments", async () =>
  fixture("/users/new", { delayFirstBranch: true }, async (page) => {
    await page.locator('[name="branchId"]').selectOption("1");
    await page.locator('[name="branchId"]').selectOption("2");
    await page.locator('[name="departmentId"]').selectOption("21");
    await page.waitForTimeout(400);
    await expect(page.locator('[name="departmentId"]')).toHaveValue("21");
    assert.equal(
      await page.locator('[name="departmentId"] option[value="11"]').count(),
      0,
    );
  }));

test("read-only users cannot see create action or direct create form", async () => {
  await fixture("/users", { creationDenied: true }, async (page) => {
    await expect(page.locator(".user-directory-name")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Tạo tài khoản", exact: false }),
    ).toHaveCount(0);
  });
  await fixture("/users/new", { creationDenied: true }, async (page, calls) => {
    await expect(page.getByRole("alert")).toContainText("chưa có quyền");
    await expect(page.locator('[name="username"]')).toHaveCount(0);
    assert.equal(
      calls.some((call) => call.method === "POST"),
      false,
    );
  });
});
