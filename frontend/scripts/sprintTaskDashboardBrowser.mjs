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
  stage = "logout";
  await page.goto("https://localhost:7142/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("https://localhost:7142/login");
  process.stdout.write(JSON.stringify({ success: true, checks: ["canonical dashboard filters reach HTTPS", "open=4 overdue=2 closedInPeriod=1 rendered", "Paused/Pending Verification/Closed rendered separately", "closed effort 0.67 hours and one open interval rendered separately"] }));
} catch {
  process.stdout.write(JSON.stringify({ success: false, stage })); process.exitCode = 1;
} finally { if (browser) await browser.close(); }
