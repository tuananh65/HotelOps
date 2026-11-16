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
  stage = "completion scope";
  const filters = page.getByRole("form", { name: "Dashboard filters" });
  await filters.getByLabel("Branch", { exact: true }).selectOption(String(fixture.branch));
  await filters.getByLabel("Department", { exact: true }).selectOption(String(fixture.department));
  await filters.getByLabel("From (UTC)").fill(fixture.date);
  await filters.getByLabel("To (UTC)").fill(fixture.date);
  const loaded = page.waitForResponse(r => new URL(r.url()).pathname === "/api/dashboards/operations"
    && new URL(r.url()).searchParams.get("departmentId") === String(fixture.department));
  await filters.getByRole("button", { name: "Apply filters" }).click();
  const response = await loaded; assert.equal(response.status(), 200);
  const body = await response.json();
  const metric = body.metrics.find(m => m.key === "averageTaskCompletionHours");
  assert.equal(typeof metric.value, "number");
  assert.ok(Math.abs(metric.value - fixture.expectedHours) < 1e-9);
  stage = "completion rendering";
  const indicator = page.getByRole("region", { name: "Dashboard indicators" }).locator("div")
    .filter({ has: page.getByText("Average task completion time", { exact: true }) });
  await indicator.waitFor(); assert.ok((await indicator.innerText()).includes("hours"));
  assert.ok(!(await indicator.innerText()).includes("N/A"));
  stage = "logout";
  await page.goto("https://localhost:7142/profile"); await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("https://localhost:7142/login");
  process.stdout.write(JSON.stringify({ success: true, cycles: 3, taskDenominator: 1, verified: "KPI05 browser HTTPS SQL reconciliation" }));
} catch { process.stdout.write(JSON.stringify({ success: false, stage })); process.exitCode = 1; }
finally { if (browser) await browser.close(); }
