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
  } else if (fixture.mode === "inspection-actors") {
    stage = "inspection actor filters";
    await page.getByLabel("Report type", { exact: true }).selectOption("inspections");
    await page.getByLabel("Branch", { exact: true }).selectOption(String(fixture.branch));
    await page.getByLabel("Housekeeper ID", { exact: true }).fill(String(fixture.housekeeper));
    await page.getByLabel("Inspector ID", { exact: true }).fill(String(fixture.inspector));
    const pending = page.waitForResponse(r => r.url().endsWith("/api/reports/inspections/preview") && r.request().method() === "POST");
    await page.getByRole("button", { name: "Preview", exact: true }).click();
    const response = await pending;
    assert.equal(response.status(), 200);
    const snapshot = await response.json();
    assert.equal(snapshot.totalCount, 3);
    const outcomes = snapshot.sections.find(s => s.key === "inspection_outcomes");
    assert.equal(outcomes.rows.length, 1);
    assert.equal(outcomes.rows[0].result, "FAIL");
    assert.equal(outcomes.rows[0].inspectionCount, 1);
    const reworks = snapshot.sections.find(s => s.key === "cleaning_reworks");
    assert.equal(reworks.rows.length, 1);
    assert.equal(reworks.rows[0].storedStatus, "PENDING");
    assert.equal(reworks.rows[0].assigneeUserId, fixture.inspector);
    assert.equal(reworks.rows[0].completedAt, null);
    assert.equal(snapshot.appliedFilters.housekeeperId, String(fixture.housekeeper));
    assert.equal(snapshot.appliedFilters.inspectorId, String(fixture.inspector));
    await page.getByRole("region", { name: "Report preview", exact: true }).waitFor();
    await page.getByRole("heading", { name: "cleaning_reworks", exact: true }).waitFor();
    await page.getByRole("heading", { name: "inspection_outcomes", exact: true }).waitFor();
    await page.getByRole("cell", { name: "PENDING", exact: true }).waitFor();
    for (const format of ["CSV", "XLSX"]) {
      stage = "actor filtered " + format;
      const download = page.waitForEvent("download");
      await page.getByRole("button", { name: "Export " + format, exact: true }).click();
      const file = await download;
      assert.equal(file.suggestedFilename(), `inspections-${snapshot.snapshotId}.${format.toLowerCase()}`);
      const parts = [];
      for await (const chunk of await file.createReadStream()) parts.push(chunk);
      const bytes = Buffer.concat(parts);
      if (format === "CSV") {
        const csv = bytes.toString("utf8");
        assert.ok(csv.includes("cleaning_reworks") && csv.includes("PENDING"));
        assert.ok(csv.includes("inspection_outcomes") && csv.includes("inspectionCount"));
        assert.ok(csv.includes("housekeeperId") && csv.includes(String(fixture.housekeeper)));
        assert.ok(csv.includes("inspectorId") && csv.includes(String(fixture.inspector)));
        assert.ok(csv.includes(snapshot.snapshotId));
      } else assert.equal(bytes.readUInt32LE(0), 0x04034b50);
      await file.delete();
    }
    exports.push(snapshot.snapshotId);
    await page.getByLabel("Housekeeper ID", { exact: true }).fill(String(fixture.inspector));
    assert.equal(await page.getByRole("region", { name: "Report preview", exact: true }).count(), 0);
    const empty = page.waitForResponse(r => r.url().endsWith("/api/reports/inspections/preview") && r.request().method() === "POST");
    await page.getByRole("button", { name: "Preview", exact: true }).click();
    const noCleaner = await empty;
    assert.equal(noCleaner.status(), 200);
    assert.equal((await noCleaner.json()).totalCount, 0);
    checks.push("official cleaner and inspector intersection, CSV/XLSX downloads, snapshot invalidation and non-cleaner empty result");
  } else if (fixture.mode === "requests" || fixture.mode === "rooms") {
    const kind=fixture.mode;
    stage = kind + " report preview";
    await page.getByLabel("Report type",{exact:true}).selectOption(kind);
    await page.getByLabel("Branch",{exact:true}).selectOption(String(fixture.branch));
    const ready=page.waitForResponse(r=>r.url().endsWith(`/api/reports/${kind}/preview`)&&r.request().method()==="POST");
    await page.getByRole("button",{name:"Preview",exact:true}).click();
    const response=await ready;assert.equal(response.status(),200);const snapshot=await response.json();
    if(kind==="requests"){
      assert.equal(snapshot.totalCount,1);assert.equal(snapshot.rows[0].overdue,true);
      await page.getByRole("columnheader",{name:"overdue",exact:true}).waitFor();
      await page.getByRole("cell",{name:"true",exact:true}).waitFor();
    }else{
      assert.equal(snapshot.rows.length,1);assert.equal(snapshot.rows[0].effectiveReadiness,"BLOCKED");
      assert.equal(snapshot.rows[0].canUse,false);assert.equal(snapshot.rows[0].operationalStatus,"READY");
      await page.getByRole("columnheader",{name:"effectiveReadiness",exact:true}).waitFor();
      await page.getByRole("cell",{name:"BLOCKED",exact:true}).waitFor();
      await page.getByRole("cell",{name:"false",exact:true}).waitFor();
    }
    for(const format of ["CSV","XLSX"]){
      stage=kind+" report "+format;
      const pending=page.waitForEvent("download");await page.getByRole("button",{name:"Export "+format,exact:true}).click();
      const file=await pending;assert.equal(file.suggestedFilename(),`${kind}-${snapshot.snapshotId}.${format.toLowerCase()}`);
      const stream=await file.createReadStream();const parts=[];for await(const chunk of stream)parts.push(chunk);
      const bytes=Buffer.concat(parts);if(format==="CSV"){const csv=bytes.toString("utf8");assert.ok(csv.includes(kind==="requests"?'"overdue"':'"effectiveReadiness"'));assert.ok(csv.includes(kind==="requests"?'"true"':'"BLOCKED"'));if(kind==="rooms"){assert.ok(csv.includes('"canUse"'));assert.ok(csv.includes('"false"'));}}else assert.equal(bytes.readUInt32LE(0),0x04034b50);
      await file.delete();
    }
    exports.push(snapshot.snapshotId);checks.push(kind==="requests"?"RPT07 overdue visible in canonical preview and downloaded CSV/XLSX":"RPT01 effective BLOCKED and canUse=false despite physical READY in preview and CSV/XLSX");
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
