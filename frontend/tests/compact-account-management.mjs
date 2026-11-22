// Real directory/detail reads. All browser writes are intercepted; reset DB tests use a separate restored database.
import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect as baseExpect } from "@playwright/test";
const expect = baseExpect.configure({ timeout: 30000 });
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/account-management";
fs.mkdirSync(out, { recursive: true });
const approval = fs.readFileSync(process.env.STAFFING_APPROVAL_FILE, "utf8");
const password = approval
  .slice(approval.indexOf("SHARED TEST CREDENTIAL"))
  .match(/`([^`]+)`/)[1];
const login = await fetch(base + "/api/v2/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username: "NamNH1", password }),
});
assert.equal(login.status, 200);
const token = (await login.json()).accessToken;
const get = async (path) => {
  const r = await fetch(base + "/api/v2/" + path, {
    headers: { Authorization: `Bearer ${token}` },
  });
  assert.equal(r.status, 200);
  return r.json();
};
const users = (await get("users")).map((r) => r.fields),
  target = users.find((u) => u.username === "HaLT1");
const detail = await get(`users/${target.userId}`);
const results = [],
  errors = [];
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const context = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
});
await context.addInitScript(
  (token) => sessionStorage.setItem("hotelops.compact.session", token),
  token,
);
const page = await context.newPage();
page.on("pageerror", (e) => errors.push(e.message));
async function check(name, fn) {
  try {
    await fn();
    results.push({ name, status: "PASS" });
    console.log("PASS " + name);
  } catch (e) {
    results.push({ name, status: "FAIL", error: e.message });
    console.error("FAIL " + name + ": " + e.message);
  }
}
async function open() {
  await page.goto(base + "/#users");
  // A navigation to the identical hash can retain the current open dialog.
  await page.reload();
  await page
    .getByRole("row")
    .filter({ hasText: target.username })
    .getByRole("button", { name: "Quản lý", exact: true })
    .click();
  await expect(page.locator(".account-detail-grid")).toBeVisible();
}
const dialog = page.getByRole("dialog");
try {
  await check("Real API validates inputs and rejects unconfigured email without changing the account", async () => {
    const before = await get(`users/${target.userId}`);
    assert.equal(before.emailConfigured, false, "Do not run this check against configured outbound mail");
    const headers = { Authorization: `Bearer ${token}`, "Content-Type": "application/json" };
    const invalid = await fetch(`${base}/api/v2/users/${target.userId}`, { method: "PUT", headers, body: JSON.stringify({ rowVersion: before.user.fields.rowVersion, fullName: target.fullName, email: "invalid-email", phone: null }) });
    assert.equal(invalid.status, 400);
    const reset = await fetch(`${base}/api/v2/users/${target.userId}/reset-password`, { method: "POST", headers, body: JSON.stringify({ rowVersion: before.user.fields.rowVersion, adminPassword: password, reason: "Verify unavailable mail without changing account" }) });
    assert.equal(reset.status, 503); assert.equal((await reset.json()).title, "EMAIL_NOT_CONFIGURED");
    const after = await get(`users/${target.userId}`);
    assert.equal(after.user.fields.rowVersion, before.user.fields.rowVersion);
    assert.equal((await fetch(`${base}/api/v2/users/${target.userId}`)).status, 401);
  });
  await check(
    "Real account details, current roles and memberships are visible",
    async () => {
      assert(
        !(
          "passwordHash" in detail.user.fields ||
          "securityStamp" in detail.user.fields
        ),
      );
      await open();
      await expect(dialog).toContainText(target.email);
      await expect(
        page.locator('[aria-label="Vai trò đã cấp"] .account-grant'),
      ).toHaveCount(detail.roles.length);
      await expect(
        page.locator('[aria-label="Phạm vi đã cấp"] .account-grant'),
      ).toHaveCount(detail.memberships.length);
      await expect(
        dialog.getByRole("button", {
          name: "Chỉnh sửa thông tin",
          exact: true,
        }),
      ).toBeVisible();
      await expect(
        dialog.getByRole("button", { name: "Đổi khách sạn", exact: true }),
      ).toHaveCount(0);
    },
  );
  await check(
    "Gmail form is available without server SMTP and requires sender credentials",
    async () => {
      assert.equal(detail.emailConfigured, false);
      await dialog
        .getByRole("button", { name: "Đặt lại mật khẩu", exact: true })
        .click();
      await expect(dialog.getByLabel("Email người nhận", { exact: true })).toHaveValue(target.email);
      await expect(dialog.getByLabel("Gmail của Admin", { exact: true })).toBeVisible();
      await expect(dialog.getByLabel("Mật khẩu ứng dụng Gmail", { exact: true })).toHaveAttribute("required", "");
      await expect(
        dialog.getByRole("button", {
          name: "Xác nhận và gửi email",
          exact: true,
        }),
      ).toBeEnabled();
      await dialog.getByRole("button", { name: "Hủy", exact: true }).click();
    },
  );
  await check(
    "Profile edit uses PUT, rowVersion and only editable fields; duplicate email is explained",
    async () => {
      let writes = 0;
      await page.route(`**/api/v2/users/${target.userId}`, async (route) => {
        if (route.request().method() !== "PUT") return route.continue();
        const body = route.request().postDataJSON();
        writes++;
        assert.deepEqual(Object.keys(body).sort(), [
          "email",
          "fullName",
          "phone",
          "rowVersion",
        ]);
        assert.equal(body.rowVersion, target.rowVersion);
        await route.fulfill({
          status: 409,
          json: { title: "EMAIL_ALREADY_USED" },
        });
      });
      await dialog
        .getByRole("button", { name: "Chỉnh sửa thông tin", exact: true })
        .click();
      await expect(dialog.getByLabel("Họ tên", { exact: true })).toHaveValue(
        target.fullName,
      );
      await dialog
        .getByLabel("Email", { exact: true })
        .fill("duplicate@example.invalid");
      await dialog
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await expect(dialog.getByRole("alert")).toContainText(
        "Email này đã thuộc tài khoản khác",
      );
      assert.equal(writes, 1);
      await page.unroute(`**/api/v2/users/${target.userId}`);
      await dialog.getByRole("button", { name: "Hủy", exact: true }).click();
    },
  );
  await check(
    "Role revoke uses the assignment ID/version and confirmation",
    async () => {
      const grant = detail.roles[0];
      let called = false;
      await page.route(
        `**/api/v2/users/role-assignments/${grant.userRoleId}/revoke`,
        async (route) => {
          assert.equal(route.request().method(), "POST");
          assert.equal(
            route.request().postDataJSON().rowVersion,
            grant.rowVersion,
          );
          called = true;
          await route.fulfill({ json: { revoked: true } });
        },
      );
      await dialog
        .getByRole("button", {
          name: `Thu hồi vai trò ${grant.name}`,
          exact: true,
        })
        .click();
      await expect(dialog).toContainText("Xác nhận thu hồi");
      await dialog
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await expect(dialog.getByRole("status")).toContainText(
        "Đã cập nhật tài khoản",
      );
      assert(called);
    },
  );
  await check(
    "Reset error preserves the dialog; success shows recipient without a password",
    async () => {
      await page.route(`**/api/v2/users/${target.userId}`, (route) =>
        route.fulfill({ json: { ...detail, emailConfigured: true } }),
      );
      await open();
      let attempts = 0;
      await page.route(
        `**/api/v2/users/${target.userId}/reset-password`,
        async (route) => {
          const body = route.request().postDataJSON();
          assert.equal(body.rowVersion, target.rowVersion);
          assert.equal(body.adminPassword, "local-test-only");
          assert.equal(body.gmailAddress, "sender@example.invalid");
          assert.equal(body.gmailAppPassword, "abcdefghijklmnop");
          assert(body.reason.length >= 10);
          attempts++;
          await route.fulfill(
            attempts === 1
              ? { status: 502, json: { title: "PASSWORD_EMAIL_FAILED" } }
              : {
                  json: {
                    sent: true,
                    recipient: target.email,
                    expiresAt: new Date(Date.now() + 86400000).toISOString(),
                    requiresPasswordChange: true,
                  },
                },
          );
        },
      );
      await dialog
        .getByRole("button", { name: "Đặt lại mật khẩu", exact: true })
        .click();
      await dialog.getByLabel("Gmail của Admin", { exact: true }).fill("sender@example.invalid");
      await dialog.getByLabel("Mật khẩu ứng dụng Gmail", { exact: true }).fill("abcdefghijklmnop");
      await dialog
        .getByLabel("Mật khẩu HotelOps của bạn", { exact: true })
        .fill("local-test-only");
      await dialog
        .getByRole("button", { name: "Xác nhận và gửi email", exact: true })
        .click();
      await expect(dialog.getByRole("alert")).toContainText(
        "Mật khẩu cũ vẫn được giữ",
      );
      await expect(
        dialog.getByLabel("Mật khẩu HotelOps của bạn", { exact: true }),
      ).toHaveValue("");
      await expect(dialog.getByLabel("Mật khẩu ứng dụng Gmail", { exact: true })).toHaveValue("");
      await dialog.getByLabel("Mật khẩu ứng dụng Gmail", { exact: true }).fill("abcdefghijklmnop");
      await dialog
        .getByLabel("Mật khẩu HotelOps của bạn", { exact: true })
        .fill("local-test-only");
      await dialog
        .getByRole("button", { name: "Xác nhận và gửi email", exact: true })
        .click();
      await expect(dialog.getByRole("status")).toContainText(
        `Đã gửi mật khẩu tạm đến ${target.email}`,
      );
      assert.equal(attempts, 2);
      await expect(dialog.locator('input[type="password"]')).toHaveCount(0);
    },
  );
  await check("Management dialog fits desktop and mobile", async () => {
    for (const width of [1440, 375]) {
      await page.setViewportSize({ width, height: 900 });
      const box = await dialog.boundingBox();
      assert(box.x >= 0 && box.x + box.width <= width + 1);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      );
      await page.screenshot({
        path: `${out}/manage-${width}.png`,
        fullPage: true,
        animations: "disabled",
      });
    }
  });
  await check("No browser exceptions", async () =>
    assert.deepEqual(errors, []),
  );
} finally {
  await browser.close();
  fs.writeFileSync(out + "/results.json", JSON.stringify(results, null, 2));
}
if (results.some((r) => r.status === "FAIL")) process.exitCode = 1;
