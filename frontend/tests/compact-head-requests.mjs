import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

// Every API request is intercepted; this suite never writes to the database.
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [];
const out='../artifacts/production-261124-dashboard-requests';
fs.mkdirSync(out,{recursive:true});
let debugPage;
const permissions = ["TASK_VIEW", "IR_VIEW", "MR_VIEW"];
try {
  for (const allowed of [permissions, ["MR_VIEW"], ["TASK_VIEW"]]) {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const reads = [];
    await context.addInitScript(() =>
      sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
    );
    await context.route("**/api/v2/**", async (route) => {
      const url = new URL(route.request().url());
      const path = url.pathname.replace("/api/v2/", "");
      assert.equal(route.request().method(), "GET");
      reads.push({ path, search: url.search });
      let json = [];
      if (path === "auth/me")
        json = {
          user: { fields: { userId: 1, fullName: "Trưởng bộ phận" } },
          branches: [{ branchId: 1, name: "Bùi Viện" }],
          roles: [
            { code: "DEPT_MANAGER", name: "Trưởng bộ phận", branchId: 1 },
          ],
          grants: allowed.map((code) => ({
            code,
            branchId: 1,
            dataScope: "BRANCH",
          })),
          memberships: [],
          requiresPasswordChange: false,
        };
      else if (path === "workspace/options")
        json = {
          departments: [],
          rooms: [],
          warehouses: [],
          items: [],
          references: [],
        };
      else if (["work", "documents"].includes(path))
        json = { items: [], total: 0, page: 1, pageSize: 25 };
      else if (path === "work/internal-request-types")
        json = { configured: false, items: [] };
      else if (path === "insights/notifications")
        json = { items: [], total: 0 };
      await route.fulfill({ json });
    });
    const page = await context.newPage();
    debugPage = page;
    page.on("pageerror", (e) => errors.push(e.message));
    await page.goto(`${base}/#requests`);
    const nav = page.getByRole("navigation", { name: "Điều hướng chính" });
    await expect(
      nav.getByRole("button", { name: "Yêu cầu", exact: true }),
    ).toHaveAttribute("aria-current", "page");
    for (const name of ["Công việc", "Yêu cầu nội bộ", "Yêu cầu vật tư"])
      await expect(nav.getByRole("button", { name, exact: true })).toHaveCount(
        0,
      );
    const tabs = page.getByLabel("Loại yêu cầu", { exact: true });
    await expect(tabs.getByRole("link")).toHaveCount(allowed.length);
    for (const [route, name, permission, kind] of [
      ["work", "Công việc", "TASK_VIEW", "OPERATIONAL"],
      ["requests", "Yêu cầu nội bộ", "IR_VIEW", "INTERNAL_REQUEST"],
      ["documents", "Yêu cầu vật tư", "MR_VIEW", "MATERIAL_REQUEST"],
    ]) {
      if (!allowed.includes(permission)) continue;
      await tabs.getByRole("link", { name, exact: true }).click();
      await expect(
        tabs.getByRole("link", { name, exact: true }),
      ).toHaveAttribute("aria-current", "page");
      await expect(
        nav.getByRole("button", { name: "Yêu cầu", exact: true }),
      ).toHaveAttribute("aria-current", "page");
      await expect
        .poll(() =>
          reads.some(
            (r) =>
              r.path === (route === "documents" ? "documents" : "work") &&
              r.search.includes(`kind=${kind}`) &&
              r.search.includes("branchId=1"),
          ),
        )
        .toBe(true);
    }
    if (allowed.length === 3) {
      await page.evaluate(() => {
        location.hash = "work?kind=INTERNAL_REQUEST";
      });
      await expect(
        tabs.getByRole("link", { name: "Yêu cầu nội bộ", exact: true }),
      ).toHaveAttribute("aria-current", "page");
      await page.screenshot({path:`${out}/head-requests-desktop.png`,animations:'disabled'});
      await page.setViewportSize({ width: 375, height: 812 });
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        "No horizontal overflow",
      );
      await page.screenshot({path:`${out}/head-requests-mobile.png`,animations:'disabled'});
    } else {
      assert(
        !reads.some((r) => r.path === "work" && !allowed.includes("TASK_VIEW")),
        "Material-only role must not read work",
      );
      assert(
        !reads.some(
          (r) => r.path === "documents" && !allowed.includes("MR_VIEW"),
        ),
        "Task-only role must not read documents",
      );
    }
    await context.close();
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(`${out}/head-requests-verification.json`,JSON.stringify({passed:true,fixtureOnly:true,permissions,errors},null,2));
  console.log(
    "PASS: grouped Head navigation, legacy routes, permission-filtered tabs, branch scope and mobile overflow",
  );
} catch (error) {
  console.error(errors, debugPage && await debugPage.locator('body').innerText());
  throw error;
} finally {
  await browser.close();
}
