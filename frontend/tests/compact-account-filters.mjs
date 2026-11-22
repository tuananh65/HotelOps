// Live API/browser checks; back up SQL before running (login writes a session).
import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect as baseExpect } from "@playwright/test";
const expect = baseExpect.configure({ timeout: 30000 });
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/account-filters";
fs.mkdirSync(out, { recursive: true });
const approval = fs.readFileSync(process.env.STAFFING_APPROVAL_FILE, "utf8");
const password = approval
  .slice(approval.indexOf("SHARED TEST CREDENTIAL"))
  .match(/`([^`]+)`/)[1];
const results = [],
  errors = [];
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
async function login(username) {
  const r = await fetch(base + "/api/v2/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });
  assert.equal(r.status, 200);
  return (await r.json()).accessToken;
}
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
try {
  const token = await login("NamNH1");
  const response = await fetch(base + "/api/v2/users", {
    headers: { Authorization: `Bearer ${token}` },
  });
  assert.equal(response.status, 200);
  const resources = await response.json(),
    users = resources.map((r) => r.fields);
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(
    (token) => sessionStorage.setItem("hotelops.compact.session", token),
    token,
  );
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto(base + "/#users");
  await expect(page.locator("tbody tr")).toHaveCount(
    Math.min(10, users.length),
  );
  const filterButton = page.getByRole("button", { name: /^Bộ lọc/ });
  const branch = page.getByRole("combobox", {
    name: "Chi nhánh",
    exact: true,
  });
  const department = page.getByRole("combobox", {
    name: "Bộ phận",
    exact: true,
  });
  const role = page.getByRole("combobox", { name: "Vai trò", exact: true });
  async function assertRows(expected) {
    const names = [];
    const pager = page.getByRole("navigation", {
      name: "Phân trang tài khoản",
    });
    while (true) {
      names.push(
        ...(await page
          .locator("tbody tr td:nth-child(2)")
          .evaluateAll((cells) =>
            cells.map((c) => c.childNodes[0].textContent.trim()),
          )),
      );
      if (
        await pager
          .getByRole("button", { name: "Tiếp", exact: true })
          .isDisabled()
      )
        break;
      await pager.getByRole("button", { name: "Tiếp", exact: true }).click();
    }
    while (
      await pager
        .getByRole("button", { name: "Trước", exact: true })
        .isEnabled()
    )
      await pager.getByRole("button", { name: "Trước", exact: true }).click();
    assert.deepEqual(
      names,
      expected.map((u) => u.username),
    );
    await expect(
      page.locator(".account-filter-summary [role=status]"),
    ).toHaveText(`${expected.length} / ${users.length} tài khoản`);
  }
  await check(
    "API directory matches independent SQL employment/role snapshot and contains no credentials",
    async () => {
      const sql = JSON.parse(
        fs
          .readFileSync(out + "/sql-directory.json", "utf8")
          .replace(/^\uFEFF/, ""),
      );
      assert.equal(users.length, sql.length);
      const sort = (rows) => rows.map((r) => JSON.stringify(r)).sort();
      for (const u of users) {
        const expected = sql.find((x) => x.userId === u.userId);
        assert(expected);
        assert(Array.isArray(u.employments) && Array.isArray(u.roles));
        // FOR JSON omits JSON_QUERY(NULL) for accounts with no matching rows.
        assert.deepEqual(sort(u.employments), sort(expected.employments || []));
        assert.deepEqual(sort(u.roles), sort(expected.roles || []));
        assert(!("passwordHash" in u || "securityStamp" in u));
      }
    },
  );
  await check(
    "Create is beside filters; status selector removed; keyboard toggle works",
    async () => {
      await expect(page.getByLabel("Trạng thái", { exact: true })).toHaveCount(
        0,
      );
      await expect(page.locator(".page-heading button")).toHaveCount(0);
      const create = page
        .locator(".account-filter-actions")
        .getByRole("button", { name: "+ Tạo tài khoản", exact: true });
      const a = await filterButton.boundingBox(),
        b = await create.boundingBox();
      assert(Math.abs(a.y - b.y) < 2 && b.x > a.x + a.width);
      await filterButton.focus();
      await page.keyboard.press("Enter");
      await expect(filterButton).toHaveAttribute("aria-expanded", "true");
      await create.click();
      await expect(page.getByRole("dialog")).toBeVisible();
      await page.keyboard.press("Escape");
      await expect(page.getByRole("dialog")).toHaveCount(0);
      await expect(filterButton).toHaveAttribute("aria-expanded", "true");
    },
  );
  await check(
    "Every accessible branch, employment department and role yields matching accounts",
    async () => {
      for (const id of [
        ...new Set(users.flatMap((u) => u.branches.map((m) => m.branchId))),
      ]) {
        await branch.selectOption(String(id));
        await assertRows(
          users.filter((u) => u.branches.some((m) => m.branchId === id)),
        );
        const deptIds = [
          ...new Set(
            users.flatMap((u) =>
              u.employments
                .filter((m) => m.branchId === id && m.departmentId)
                .map((m) => m.departmentId),
            ),
          ),
        ];
        await expect(department.locator("option")).toHaveCount(
          deptIds.length + 1,
        );
        for (const dept of deptIds) {
          await department.selectOption(String(dept));
          await assertRows(
            users.filter((u) =>
              u.employments.some(
                (m) => m.branchId === id && m.departmentId === dept,
              ),
            ),
          );
        }
      }
      await branch.selectOption("");
      await expect(department).toHaveValue("");
      for (const id of [
        ...new Set(users.flatMap((u) => u.roles.map((r) => r.roleId))),
      ]) {
        await role.selectOption(String(id));
        await assertRows(
          users.filter((u) => u.roles.some((r) => r.roleId === id)),
        );
      }
      await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
      await assertRows(users);
    },
  );
  await check(
    "Combined branch/department/role and search; collapse preserves filters; reset restores list",
    async () => {
      const sample = users.find((u) =>
        u.employments.some(
          (m) =>
            m.departmentId && u.roles.some((r) => r.branchId === m.branchId),
        ),
      );
      const m = sample.employments.find(
        (m) =>
          m.departmentId && sample.roles.some((r) => r.branchId === m.branchId),
      );
      const r = sample.roles.find((r) => r.branchId === m.branchId);
      await branch.selectOption(String(m.branchId));
      await department.selectOption(String(m.departmentId));
      await role.selectOption(String(r.roleId));
      await assertRows(
        users.filter(
          (u) =>
            u.employments.some(
              (e) =>
                e.branchId === m.branchId && e.departmentId === m.departmentId,
            ) &&
            u.roles.some(
              (x) =>
                x.roleId === r.roleId &&
                (x.branchId === null || x.branchId === m.branchId),
            ),
        ),
      );
      await page
        .getByLabel("Tìm tài khoản")
        .fill(sample.username.toUpperCase());
      await assertRows([sample]);
      await filterButton.click();
      await expect(filterButton).toHaveText("Bộ lọc (3)");
      await assertRows([sample]);
      await page.getByLabel("Tìm tài khoản").fill("not-an-account-zz");
      await expect(page.locator("tbody")).toContainText(
        "Không tìm thấy tài khoản",
      );
      await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
      await assertRows(users);
    },
  );
  await check(
    "375/768/1440 layouts do not overflow with filters open or closed",
    async () => {
      for (const width of [375, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        for (const open of [false, true]) {
          if (
            ((await filterButton.getAttribute("aria-expanded")) === "true") !==
            open
          )
            await filterButton.click();
          assert(
            await page.evaluate(
              () => document.documentElement.scrollWidth <= innerWidth + 1,
            ),
          );
          await expect(
            page.locator(".account-filter-actions .primary"),
          ).toBeInViewport();
          await page.screenshot({
            path: `${out}/accounts-${width}-${open ? "open" : "closed"}.png`,
            fullPage: true,
            animations: "disabled",
          });
        }
      }
    },
  );
  await check(
    "Unauthorized and staff access to enriched directory remain rejected",
    async () => {
      assert.equal((await fetch(base + "/api/v2/users")).status, 401);
      const staff = await login("AnhPN1");
      assert.equal(
        (
          await fetch(base + "/api/v2/users", {
            headers: { Authorization: `Bearer ${staff}` },
          })
        ).status,
        403,
      );
    },
  );
  await check(
    "Multi-branch fixture cannot combine employment in one branch with a role in another",
    async () => {
      const a = {
        branchId: 9101,
        branchName: "Khách sạn A",
        departmentId: 9201,
        departmentName: "Buồng phòng",
      };
      const b = {
        branchId: 9102,
        branchName: "Khách sạn B",
        departmentId: 9202,
        departmentName: "Buồng phòng",
      };
      const fixture = [
        {
          username: "cross-branch",
          employments: [a, b],
          roles: [{ roleId: 9301, name: "Vai trò thử", branchId: b.branchId }],
        },
        {
          username: "global",
          employments: [a],
          roles: [{ roleId: 9301, name: "Vai trò thử", branchId: null }],
        },
        { username: "unassigned", employments: [], roles: [] },
      ].map((u, i) => ({
        type: "User",
        fields: {
          ...users[0],
          ...u,
          branches: u.employments,
          userId: 9401 + i,
          fullName: u.username,
        },
      }));
      await page.route("**/api/v2/users", (route) =>
        route.fulfill({ json: fixture }),
      );
      await page.reload();
      await expect(page.locator("tbody tr")).toHaveCount(3);
      await filterButton.click();
      await branch.selectOption("9101");
      await department.selectOption("9201");
      await role.selectOption("9301");
      await expect(page.locator("tbody tr")).toHaveCount(1);
      await expect(page.locator("tbody")).toContainText("global");
      await branch.selectOption("9102");
      await expect(department).toHaveValue("");
      await expect(page.locator("tbody tr")).toHaveCount(1);
      await expect(page.locator("tbody")).toContainText("cross-branch");
      await page.getByRole("button", { name: "Xóa lọc", exact: true }).click();
      await expect(page.locator("tbody tr")).toHaveCount(3);
      await page.unroute("**/api/v2/users");
    },
  );
  await check("No browser exceptions", async () =>
    assert.deepEqual(errors, []),
  );
} finally {
  await browser.close();
  fs.writeFileSync(out + "/results.json", JSON.stringify(results, null, 2));
}
if (results.some((r) => r.status === "FAIL")) process.exitCode = 1;
