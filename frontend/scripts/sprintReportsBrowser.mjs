import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "login";
const checks = [];
const exports = [];
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const page = await browser.newPage();
  page.setDefaultTimeout(20000);
  const origin = "https://localhost:7142";
  await page.goto(origin + "/reports");
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/reports");
  if (fixture.mode === "denied") {
    await page
      .getByRole("alert")
      .filter({ hasText: "do not have access" })
      .waitFor();
    assert.equal(
      await page.getByRole("button", { name: "Preview", exact: true }).count(),
      0,
    );
    await page.goto(origin + "/dashboard");
    const admission = await page.evaluate(
      async () => (await fetch("/api/reports/options")).status,
    );
    assert.equal(admission, 403);
    assert.equal(
      await page.getByRole("link", { name: "Reports", exact: true }).count(),
      0,
    );
    checks.push(
      "DEPT_MANAGER with REPORT_VIEW cannot open reports or see report navigation",
    );
  } else {
    stage = "select branch";
    await page
      .getByLabel("Branch", { exact: true })
      .selectOption(String(fixture.branch));
    stage = "select department";
    await page
      .getByLabel("Department", { exact: true })
      .selectOption(String(fixture.department));
    stage = "request preview";
    const result = page.waitForResponse(
      (r) =>
        r.url().endsWith("/api/reports/tasks/preview") &&
        r.request().method() === "POST",
    );
    await page.getByRole("button", { name: "Preview", exact: true }).click();
    const response = await result;
    assert.equal(response.status(), 200);
    const snapshot = await response.json();
    stage = "validate snapshot";
    assert.equal(snapshot.totalCount, 26);
    assert.deepEqual(snapshot.filters.branchIds, [fixture.branch]);
    assert.equal(snapshot.filters.departmentId, fixture.department);
    stage = "render preview";
    await page
      .getByRole("region", { name: "Report preview", exact: true })
      .waitFor();
    await page
      .getByText("Partial report — unavailable indicators:", { exact: true })
      .waitFor();
    stage = "pagination";
    assert.equal(await page.locator("tbody tr").count(), 25);
    await page.getByRole("button", { name: "Next page", exact: true }).click();
    assert.equal(await page.locator("tbody tr").count(), 1);
    assert.ok(
      await page
        .getByRole("button", { name: "Next page", exact: true })
        .isDisabled(),
    );
    await page
      .getByRole("button", { name: "Previous page", exact: true })
      .click();
    assert.equal(await page.locator("tbody tr").count(), 25);
    checks.push(
      "26 persisted task rows paginate 25 plus 1 without changing snapshot",
    );
    async function download(expected) {
      stage = "CSV download " + expected.totalCount;
      const pending = page.waitForEvent("download");
      await page
        .getByRole("button", { name: "Export CSV", exact: true })
        .click();
      const file = await pending;
      assert.equal(
        file.suggestedFilename(),
        `tasks-${expected.snapshotId}.csv`,
      );
      const stream = await file.createReadStream();
      const chunks = [];
      for await (const chunk of stream) chunks.push(chunk);
      const csv = Buffer.concat(chunks).toString("utf8");
      assert.ok(csv.includes(expected.snapshotId));
      assert.ok(
        csv.includes(expected.filters.from) &&
          csv.includes(expected.filters.to),
      );
      assert.ok(csv.includes('"progressPercent"'));
      await file.delete();
      stage = "XLSX download " + expected.totalCount;
      const xlsxPending = page.waitForEvent("download");
      await page.getByRole("button", { name: "Export XLSX", exact: true }).click();
      const xlsx = await xlsxPending;
      assert.equal(xlsx.suggestedFilename(), `tasks-${expected.snapshotId}.xlsx`);
      const xlsxStream = await xlsx.createReadStream();
      const parts = [];
      for await (const chunk of xlsxStream) parts.push(chunk);
      const bytes = Buffer.concat(parts);
      assert.equal(bytes.readUInt32LE(0), 0x04034b50);
      assert.ok(bytes.includes(Buffer.from("xl/worksheets/sheet1.xml")));
      await xlsx.delete();
      stage = "export success message";
      exports.push(expected.snapshotId);
      await page
        .getByText("MSG25: Export created; download requested.", {
          exact: true,
        })
        .waitFor();
    }
    await download(snapshot);
    checks.push(
      "real canonical preview and CSV download preserve snapshot, filters and partial indicator disclosure",
    );
    stage = "empty preview";
    await page.getByLabel("From (UTC)", { exact: true }).fill("2020-01-01");
    assert.equal(
      await page
        .getByRole("button", { name: "Export CSV", exact: true })
        .count(),
      0,
    );
    await page.getByLabel("To (UTC)", { exact: true }).fill("2020-01-02");
    const emptyResponse = page.waitForResponse((r) =>
      r.url().endsWith("/api/reports/tasks/preview"),
    );
    await page.getByRole("button", { name: "Preview", exact: true }).click();
    const empty = await (await emptyResponse).json();
    assert.equal(empty.totalCount, 0);
    await page
      .getByText(
        "MSG10: No records match these filters. CSV headers can still be exported.",
        { exact: true },
      )
      .waitFor();
    await download(empty);
    checks.push(
      "editing filters invalidates old preview; empty snapshot remains exportable with headers",
    );
    await page.goto(origin + "/dashboard");
    await page.getByRole("link", { name: "Reports", exact: true }).waitFor();
    checks.push("eligible actor sees report navigation");
  }
  stage = "logout";
  const logout = await page.evaluate(async () => {
    const csrf = await (await fetch("/api/auth/csrf")).json();
    return (
      await fetch("/api/auth/logout", {
        method: "POST",
        headers: { "X-CSRF-TOKEN": csrf.csrfToken },
      })
    ).status;
  });
  assert.equal(logout, 204);
  checks.push("browser session logout persisted via CSRF-protected endpoint");
  console.log(
    JSON.stringify({ success: true, mode: fixture.mode, checks, exports }),
  );
} catch (error) {
  console.log(
    JSON.stringify({ success: false, stage, errorType: error?.name, checks }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
