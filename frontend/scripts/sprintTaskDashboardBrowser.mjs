import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser, stage = "login";
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(15000);
  await page.goto("https://localhost:7142/dashboard");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  stage = "scope filters";
  const filters = page.getByRole("form", { name: "Dashboard filters" });
  await filters.getByLabel("Branch", { exact: true }).selectOption(String(fixture.branch));
  await filters.getByLabel("Department", { exact: true }).selectOption(String(fixture.department));
  await filters.getByLabel("From (UTC)").fill(fixture.from);
  await filters.getByLabel("To (UTC)").fill(fixture.to);
  const loaded = page.waitForResponse(r => new URL(r.url()).pathname === "/api/dashboards/operations"
    && new URL(r.url()).searchParams.get("departmentId") === String(fixture.department));
  await filters.getByRole("button", { name: "Apply filters" }).click();
  assert.equal((await loaded).status(), 200);
  stage = "canonical indicators";
  const indicators = page.getByRole("region", { name: "Dashboard indicators" });
  await page.getByText(/^MSG29: Incomplete dashboard:/).waitFor();
  await page.getByRole("list", { name: "Unavailable dashboard sources" }).getByText(/KPI-05:/).waitFor();
  for (const [label, count] of [["Open tasks", 4], ["Overdue tasks", 2], ["Tasks closed in period", 1]]) {
    const row = indicators.locator("div").filter({ has: page.getByText(label, { exact: true }) });
    await row.getByText(`${count} count`, { exact: true }).waitFor();
  }
  const distributions = page.getByRole("region", { name: "Status distributions" });
  await indicators.locator("div").filter({ has: page.getByText("Task effort", { exact: true }) })
    .getByText("0.67 hours", { exact: true }).waitFor();
  await indicators.locator("div").filter({ has: page.getByText("Open task intervals", { exact: true }) })
    .getByText("1 count", { exact: true }).waitFor();
  await distributions.getByRole("cell", { name: "PAUSED", exact: true }).waitFor();
  await distributions.getByRole("cell", { name: "PENDING_VERIFICATION", exact: true }).waitFor();
  await distributions.getByRole("cell", { name: "CLOSED", exact: true }).waitFor();
  stage = "UTC workload drill-down";
  const workloadResponse = page.waitForResponse(r => new URL(r.url()).pathname === `/api/branches/${fixture.branch}/task-workload/tasks`);
  await indicators.locator("div").filter({ has: page.getByText("Task effort", { exact: true }) })
    .getByRole("link", { name: "Review workload", exact: true }).click();
  const workload = await workloadResponse;
  assert.equal(workload.status(), 200);
  const query = new URL(workload.url()).searchParams;
  assert.equal(query.get("calendar"), "UTC"); assert.equal(query.get("departmentId"), String(fixture.department));
  assert.equal(query.get("from"), fixture.from); assert.equal(query.get("to"), fixture.to);
  assert.equal(query.get("taskSet"), "EFFORT_IN_PERIOD");
  const effortRows = page.getByRole("table", { name: "Matching tasks" }).locator("tbody tr");
  await effortRows.first().waitFor(); assert.equal(await effortRows.count(), 1);
  assert.equal(await page.getByRole("table", { name: "Employee workload" }).count(), 0);
  assert.equal(await page.getByLabel("Workload department ID").inputValue(), String(fixture.department));
  await page.getByText("Period calendar: UTC.", { exact: true }).waitFor();
  for (const [label, count, taskSet] of [["Open tasks", 4, "OPEN"], ["Overdue tasks", 2, "OVERDUE"], ["Tasks closed in period", 1, "CLOSED_IN_PERIOD"]]) {
    stage = `${taskSet} population drill-down`;
    await page.goBack();
    await page.getByRole("region", { name: "Dashboard indicators" }).waitFor();
    // Dashboard filters are page-local; restore the selected department and UTC dates.
    await filters.getByLabel("Branch", { exact: true }).selectOption(String(fixture.branch));
    await filters.getByLabel("Department", { exact: true }).selectOption(String(fixture.department));
    await filters.getByLabel("From (UTC)").fill(fixture.from);
    await filters.getByLabel("To (UTC)").fill(fixture.to);
    const refreshed = page.waitForResponse(r => new URL(r.url()).pathname === "/api/dashboards/operations"
      && new URL(r.url()).searchParams.get("departmentId") === String(fixture.department));
    await filters.getByRole("button", { name: "Apply filters" }).click();
    assert.equal((await refreshed).status(), 200);
    const populationResponse = page.waitForResponse(r => new URL(r.url()).pathname === `/api/branches/${fixture.branch}/task-workload/tasks`);
    await indicators.locator("div").filter({ has: page.getByText(label, { exact: true }) }).getByRole("link", { name: "Review workload", exact: true }).click();
    const population = await populationResponse;
    assert.equal(population.status(), 200);
    assert.equal(new URL(population.url()).searchParams.get("taskSet"), taskSet);
    const rows = page.getByRole("table", { name: "Matching tasks" }).locator("tbody tr");
    await rows.first().waitFor();
    assert.equal(await rows.count(), count);
    assert.equal(await rows.getByText("No current assignment", { exact: true }).count(), count);
  }
  stage = "logout";
  await page.goto("https://localhost:7142/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("https://localhost:7142/login");
  process.stdout.write(JSON.stringify({ success: true, checks: ["canonical dashboard filters reach HTTPS", "open=4 overdue=2 closedInPeriod=1 rendered", "Paused/Pending Verification/Closed rendered separately", "closed effort 0.67 hours and one open interval rendered separately", "dashboard effort link opens task-only population for the same UTC scope without employee time logs", "all three task-count links reconcile task populations including no current assignment"] }));
} catch {
  process.stdout.write(JSON.stringify({ success: false, stage })); process.exitCode = 1;
} finally { if (browser) await browser.close(); }
