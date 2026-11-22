import fs from "node:fs";
import assert from "node:assert/strict";
import { chromium, expect as baseExpect } from "@playwright/test";
const expect = baseExpect.configure({ timeout: 30000 });
const base = process.env.UX_BASE || "http://127.0.0.1:5186";
const out = "../artifacts/fixed-operational-levels";
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
const headers = {
  Authorization: `Bearer ${token}`,
  "Content-Type": "application/json",
};
const read = async (path) => {
  const response = await fetch(base + "/api/v2/" + path, { headers });
  assert.equal(response.status, 200);
  return response.json();
};
const before = (await read("catalog/references?pageSize=100")).items.map(
  (r) => r.fields,
);
const codes = ["LOW", "NORMAL", "HIGH", "CRITICAL"];
for (const kind of ["PRIORITY", "SEVERITY"]) {
  const levels = before
    .filter((r) => r.kind === kind)
    .sort((a, b) => a.sortOrder - b.sortOrder);
  assert.deepEqual(
    levels.map((r) => r.code),
    codes,
  );
  assert(levels.every((r) => r.isActive));
  for (const submittedKind of [kind, kind.toLowerCase(), kind + " "]) {
    const response = await fetch(base + "/api/v2/catalog/references", {
      method: "POST",
      headers,
      body: JSON.stringify({
        fields: { kind: submittedKind, code: "HIGH", name: "Rejected test" },
      }),
    });
    assert.equal(response.status, 405);
    assert.equal((await response.json()).title, "FIXED_OPERATIONAL_REFERENCE");
  }
  for (const fields of [
    { name: levels[0].name },
    { kind: "ASSET_TYPE" },
    { isActive: false },
  ]) {
    const response = await fetch(
      base + `/api/v2/catalog/references/${levels[0].referenceId}`,
      {
        method: "PUT",
        headers,
        body: JSON.stringify({ rowVersion: levels[0].rowVersion, fields }),
      },
    );
    assert.equal(response.status, 405);
    assert.equal((await response.json()).title, "FIXED_OPERATIONAL_REFERENCE");
  }
}
const profile = await read("auth/me");
const options = await read(
  `workspace/options?branchId=${profile.branches[0].branchId}`,
);
for (const kind of ["PRIORITY", "SEVERITY"])
  assert.deepEqual(
    options.references.filter((r) => r.kind === kind).map((r) => r.code),
    codes,
  );
assert.deepEqual(
  (await read("catalog/references?pageSize=100")).items.map((r) => r.fields),
  before,
);
console.log(
  "PASS: two fixed groups, 8 values, 12 write rejections, sorted operational lookups, unchanged references.",
);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const errors = [],
  requested = [];
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });
  await context.addInitScript(
    (value) => sessionStorage.setItem("hotelops.compact.session", value),
    token,
  );
  const page = await context.newPage();
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) => requested.push(request.url()));
  for (const hash of [
    "settings",
    "settings?resource=references",
    "settings?resource=permissions",
  ]) {
    await page.goto(base + "/#" + hash);
    await expect(page.locator(".roles-table")).toBeVisible();
    await expect(page.getByLabel("Nội dung cấu hình")).toHaveValue("roles");
    await expect(
      page.getByLabel("Nội dung cấu hình").locator("option"),
    ).toHaveCount(3);
    await expect(page.locator('[name="kind"]')).toHaveCount(0);
  }
  assert(!requested.some((url) => /\/catalog\/references/.test(url)));
  for (const width of [1440, 375]) {
    await page.setViewportSize({ width, height: 900 });
    // Let the sidebar's breakpoint transition finish before capturing its final layout.
    await page.waitForTimeout(400);
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
    );
    await page.screenshot({
      path: `${out}/settings-${width}.png`,
      fullPage: true,
      animations: "disabled",
    });
  }
  assert.deepEqual(errors, []);
  fs.writeFileSync(
    `${out}/verification.json`,
    JSON.stringify(
      {
        status: "PASS",
        fixedValues: 8,
        deniedWrites: 12,
        legacyRoutes: 3,
        browserErrors: errors,
      },
      null,
      2,
    ),
  );
  console.log(
    "PASS: operational settings absent, legacy routes show roles, desktop/mobile fit, no browser errors.",
  );
} finally {
  await browser.close();
}
