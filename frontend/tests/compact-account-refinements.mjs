// Isolated browser fixtures: every API request is intercepted; no SQL or login writes.
import assert from "node:assert/strict";
import { chromium, expect } from "@playwright/test";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const branches = [{ branchId: 1, name: "Chi nhánh A" }, { branchId: 2, name: "Chi nhánh B" }];
const departments = [{ departmentId: 11, branchId: 1, name: "Buồng phòng" }, { departmentId: 22, branchId: 2, name: "Bảo trì" }];
const users = Array.from({ length: 12 }, (_, index) => ({
  userId: index + 1, fullName: `Nhân viên ${index + 1}`, username: `user${index + 1}`,
  employeeCode: `NV${index + 1}`, phone: `09000000${String(index).padStart(2, "0")}`,
  email: `user${index + 1}@example.test`, accountStatus: "ACTIVE", rowVersion: "AAAAAAAAAAE=",
  branches: [{ branchId: 1, branchName: branches[0].name }], employments: [],
  departments: index === 11 ? [{ branchId: 1, branchName: branches[0].name, departmentId: 11, departmentName: "Buồng phòng", kind: "DEPARTMENT_AUTHORITY" }] : [],
  roles: [],
}));
const profile = {
  user: { fields: users[0] }, branches, memberships: [],
  roles: [{ code: "SYS_ADMIN", name: "Quản trị hệ thống", branchId: null }],
  grants: ["USER_VIEW", "USER_MANAGE", "ROLE_MANAGE"].map(code => ({ code, dataScope: "ALL", branchId: null })),
  requiresPasswordChange: false,
};
let personal = { ...users[0] };
let stale = false;
const writes = [];
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  context.setDefaultTimeout(15000);
  context.setDefaultNavigationTimeout(30000);
  await context.addInitScript(() => sessionStorage.setItem("hotelops.compact.session", "account-fixture"));
  await context.route("**/api/v2/**", async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname.replace("/api/v2/", "");
    let status = 200, result = {};
    if (request.method() === "GET") {
      if (path === "auth/me") result = profile;
      else if (path === "users") result = users.map(fields => ({ type: "User", fields }));
      else if (path === "users/creation-options") result = { branches, departments };
      else if (path === "auth/profile") result = personal;
      else if (path.startsWith("catalog/")) result = { items: [], total: 0, page: 1, pageSize: 100 };
      else result = { items: [], total: 0 };
    } else {
      const body = request.postDataJSON(); writes.push({ path, body });
      if (path === "auth/profile") {
        if (stale) { status = 409; result = { title: "STALE_ROW_VERSION" }; }
        else { personal = { ...personal, ...body, rowVersion: "AAAAAAAAAAI=" }; result = personal; }
      } else if (path === "users") result = { type: "User", fields: { ...body, userId: 100, employeeCode: "NV000100" } };
      else { status = 400; result = { title: "UNEXPECTED_TEST_WRITE" }; }
    }
    await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(result) });
  });
  const page = await context.newPage();
  const errors = []; page.on("pageerror", error => { errors.push(error.message); console.error("BROWSER: " + error.message); });
  await page.goto(`${base}/#users`);
  await expect(page.locator("tbody tr")).toHaveCount(10);
  await expect(page.locator("tbody tr").first().locator("td").first()).toHaveText("1");
  await expect(page.locator("tbody tr").first()).toContainText("0900000000");
  await page.getByRole("navigation", { name: "Phân trang tài khoản" }).getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr").first().locator("td").first()).toHaveText("11");
  await page.getByRole("button", { name: /^Bộ lọc/ }).click();
  await page.getByRole("combobox", { name: "Bộ phận", exact: true }).selectOption("11");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await expect(page.locator("tbody tr")).toContainText("user12");
  await expect(page.locator(".account-filter-summary")).toContainText("1 / 12");
  await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
  console.log("PASS phone, continuous STT, department authority filter and count");

  await page.getByRole("button", { name: "+ Tạo tài khoản", exact: true }).click();
  console.log("CHECK create dialog opened");
  const dialog = page.getByRole("dialog");
  await expect(dialog.locator('[name="employeeCode"]')).toHaveCount(0);
  await dialog.getByLabel("Tên đăng nhập", { exact: true }).fill(" USER1 ");
  await expect(dialog.getByText("Tên đăng nhập đã được sử dụng.", { exact: true })).toBeVisible();
  await dialog.getByLabel("Tên đăng nhập", { exact: true }).fill("newuser");
  await dialog.getByLabel("Họ tên", { exact: true }).fill(users[0].fullName);
  await dialog.getByLabel("Email", { exact: true }).fill(users[0].email);
  await expect(dialog.getByText("Email đã được sử dụng bởi tài khoản khác.", { exact: true })).toBeVisible();
  await dialog.getByLabel("Email", { exact: true }).fill("new@example.test");
  await dialog.getByLabel("Mật khẩu tạm thời").fill("Abcd12!");
  const create = dialog.getByRole("button", { name: "Tạo tài khoản", exact: true });
  await expect(create).toBeDisabled();
  await dialog.getByLabel("Mật khẩu tạm thời").fill("Abcd123!");
  await expect(dialog.getByRole("list", { name: "Yêu cầu mật khẩu" }).getByText("Đã đạt", { exact: false })).toHaveCount(5);
  await dialog.getByRole("combobox", { name: "Bộ phận", exact: true }).selectOption("11");
  await dialog.getByRole("combobox", { name: "Chi nhánh", exact: true }).selectOption("2");
  await expect(dialog.getByRole("combobox", { name: "Bộ phận", exact: true })).toHaveValue("");
  await expect(dialog.getByRole("combobox", { name: "Bộ phận", exact: true }).locator('option[value="11"]')).toHaveCount(0);
  await dialog.getByRole("combobox", { name: "Bộ phận", exact: true }).selectOption("22");
  await create.click();
  await expect(dialog).toHaveCount(0);
  assert.equal(writes.length, 1);
  assert.equal(writes[0].body.branchId, 2);
  assert.equal(writes[0].body.departmentId, 22);
  assert.equal(writes[0].body.fullName, users[0].fullName);
  assert.equal("employeeCode" in writes[0].body, false);
  console.log("PASS create: duplicate validation, live password8 policy, same name allowed, atomic payload");

  // Mount the exported component separately until the parent owns shell integration.
  profile.roles = [{ code: "HK_STAFF", name: "Nhân viên", branchId: 1 }]; profile.grants = [];
  await page.evaluate(async () => {
    const reactModule = await import("/node_modules/.vite/deps/react.js");
    const React = reactModule.default ?? reactModule;
    const clientModule = await import("/node_modules/.vite/deps/react-dom_client.js");
    const { createRoot } = clientModule.default ?? clientModule;
    // Use the exact HMR URL imported by the component, avoiding a second auth context.
    const componentSource = await fetch("/src/compact/personal-profile.tsx").then(response => response.text());
    const authUrl = componentSource.match(/from ["']([^"']*\/auth\.tsx[^"']*)["']/)?.[1];
    if (!authUrl) throw new Error("Cannot resolve profile auth context");
    const { Auth } = await import(authUrl);
    const { default: PersonalProfile } = await import("/src/compact/personal-profile.tsx");
    document.getElementById("root").hidden = true;
    const mount = document.createElement("div"); document.body.appendChild(mount);
    createRoot(mount).render(React.createElement(Auth, null, React.createElement(PersonalProfile, { close() {} })));
  });
  const personalDialog = page.getByRole("dialog", { name: "Thông tin cá nhân" });
  try { await expect(personalDialog.getByLabel("Họ tên", { exact: true })).toBeVisible({ timeout: 15000 }); }
  catch (error) { console.error(await page.locator("body").innerText()); throw error; }
  await personalDialog.getByLabel("Họ tên", { exact: true }).fill("Tên đã cập nhật");
  await personalDialog.getByRole("button", { name: "Lưu thông tin", exact: true }).click();
  await expect(personalDialog.getByText("Đã lưu thông tin cá nhân.", { exact: true })).toBeVisible();
  assert.deepEqual(Object.keys(writes.at(-1).body).sort(), ["email", "fullName", "phone", "rowVersion"]);
  assert.equal(writes.at(-1).path, "auth/profile");
  stale = true;
  await personalDialog.getByLabel("Họ tên", { exact: true }).fill("Tên cũ cần giữ");
  await personalDialog.getByRole("button", { name: "Lưu thông tin", exact: true }).click();
  await expect(personalDialog.getByText(/Thông tin đã thay đổi/)).toBeVisible();
  await expect(personalDialog.getByRole("button", { name: "Lưu thông tin", exact: true })).toBeDisabled();
  await expect(personalDialog.getByLabel("Họ tên", { exact: true })).toHaveValue("Tên cũ cần giữ");
  personal = { ...personal, fullName: "Tên từ máy chủ", rowVersion: "AAAAAAAAAAM=" };
  await personalDialog.getByRole("button", { name: "Tải lại hồ sơ", exact: true }).click();
  await expect(personalDialog.getByLabel("Họ tên", { exact: true })).toHaveValue("Tên từ máy chủ");
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 900 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `page overflow at ${width}`);
  }
  assert.deepEqual(errors, []);
  console.log("PASS self profile: no grants required, safe payload, stale edit retained, reload, responsive widths");
} finally { await browser.close(); }
