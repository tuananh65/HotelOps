// Live read-only directory/UI checks; login requires a verified DB backup first.
import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect } from "@playwright/test";

const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/branch-access-filter";
fs.mkdirSync(out, { recursive: true });
const approval = fs.readFileSync(process.env.STAFFING_APPROVAL_FILE, "utf8");
const password = approval
  .slice(approval.indexOf("SHARED TEST CREDENTIAL"))
  .match(/`([^`]+)`/)[1];
const login = await fetch(`${base}/api/v2/auth/login`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username: "NamNH1", password }),
});
assert.equal(login.status, 200);
const token = (await login.json()).accessToken;
const response = await fetch(`${base}/api/v2/users`, {
  headers: { Authorization: `Bearer ${token}` },
});
assert.equal(response.status, 200);
const users = (await response.json()).map((r) => r.fields);
const user = users.find((u) => u.username === "PhuongDH1");
assert(user, "Target account absent from directory");
assert.equal(user.employments.length, 0, "Keep access-only membership intact");
const branch = user.branches?.find((b) => b.branchName.includes("Bùi Viện"));
assert(
  branch,
  "Directory must include active branch access, not only employment",
);
const role = user.roles.find(
  (r) => r.branchId === branch.branchId && r.name === "Quản lý vận hành",
);
assert(role);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [];
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(
    (token) => sessionStorage.setItem("hotelops.compact.session", token),
    token,
  );
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(`${base}/#users`);
  await page.getByRole("button", { name: /^Bộ lọc/ }).click();
  await page
    .getByRole("combobox", { name: "Chi nhánh", exact: true })
    .selectOption(String(branch.branchId));
  await page
    .getByRole("combobox", { name: "Vai trò", exact: true })
    .selectOption(String(role.roleId));
  await expect(page.locator("tbody")).toContainText("PhuongDH1");
  const expected = users.filter(
    (u) =>
      u.branches.some((b) => b.branchId === branch.branchId) &&
      u.roles.some(
        (r) =>
          r.roleId === role.roleId &&
          (r.branchId === null || r.branchId === branch.branchId),
      ),
  );
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(
    expected.length,
  );
  await page.getByLabel("Tìm tài khoản").fill("PhuongDH1");
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(1);
  await expect(page.locator("tbody")).toContainText("Đặng Hoài Phương");
  await page.screenshot({ path: `${out}/phuong-filtered.png`, fullPage: true });
  const department = page.getByRole("combobox", {
    name: "Bộ phận",
    exact: true,
  });
  const deptId = await department
    .locator("option")
    .nth(1)
    .getAttribute("value");
  assert(deptId, "Need an actual department for the negative check");
  await department.selectOption(deptId);
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(0);
  await department.selectOption("");
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(1);
  const other = await page
    .getByRole("combobox", { name: "Chi nhánh", exact: true })
    .locator("option")
    .evaluateAll(
      (options, id) => options.find((o) => o.value && o.value !== id)?.value,
      String(branch.branchId),
    );
  assert(other);
  await page
    .getByRole("combobox", { name: "Chi nhánh", exact: true })
    .selectOption(other);
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(0);
  await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
  await expect(page.locator("tbody tr:has(td:nth-child(2))")).toHaveCount(
    Math.min(10, users.length),
  );
  await expect(page.locator(".account-filter-summary")).toContainText(
    `${users.length} / ${users.length} tài khoản`,
  );
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        passed: true,
        username: user.username,
        userId: user.userId,
        branch,
        role,
        employmentCount: user.employments.length,
        combinedMatchCount: expected.length,
        totalUsers: users.length,
        noFalseDepartmentOrBranchMatch: true,
        browserErrors: errors,
      },
      null,
      2,
    ),
  );
  console.log(
    `PASS PhuongDH1 appears for Bui Vien + Operation Manager; ${expected.length} combined matches; department/other-branch exclusions and reset verified`,
  );
} finally {
  await browser.close();
}
