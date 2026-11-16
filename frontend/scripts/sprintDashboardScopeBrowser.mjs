import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const f = JSON.parse(input); input = "";
let browser, stage = "login";
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(15000);
  await page.goto("https://localhost:7142/dashboard");
  await page.getByLabel("Username or email").fill(f.username);
  await page.getByLabel("Password", { exact: true }).fill(f.password); f.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  if (!f.revoked) {
    stage = "long dashboard period";
    const filters = page.getByRole("form", { name: "Dashboard filters" });
    await filters.getByLabel("From (UTC)").fill(f.from);
    await filters.getByLabel("To (UTC)").fill(f.to);
    const response = page.waitForResponse(r => new URL(r.url()).pathname === "/api/dashboards/operations" && new URL(r.url()).searchParams.get("from") === f.from);
    await filters.getByRole("button", { name: "Apply filters" }).click();
    assert.equal((await response).status(), 200);
    const row = page.getByRole("region", { name: "Dashboard indicators" }).locator("div").filter({ has: page.getByText("Tasks closed in period", { exact: true }) });
    await row.getByText("3 count", { exact: true }).waitFor();
    await row.getByRole("link", { name: "Review workload", exact: true }).click();
  } else {
    await page.getByRole("form", { name: "Dashboard filters" }).waitFor();
    const params = new URLSearchParams({ view: "workload", workloadBranches: f.branches.join(","), from: f.from, to: f.to, calendar: "UTC", taskSet: "CLOSED_IN_PERIOD" });
    await page.goto("https://localhost:7142/operational-tasks?" + params);
  }
  stage = "branch populations";
  for (let i = 0; i < f.branches.length; i++) {
    const section = page.getByRole("region", { name: `Workload branch ${f.branches[i]}`, exact: true });
    if (f.revoked && i === 1) {
      await section.getByText("Workload requires an authorized Department Head or Operations Manager.", { exact: true }).waitFor();
      assert.equal(await section.getByRole("table").count(), 0);
    } else {
      const rows = section.getByRole("table", { name: "Matching tasks" }).locator("tbody tr");
      await rows.first().waitFor(); assert.equal(await rows.count(), i === 0 ? 2 : 1);
      assert.equal(await section.getByLabel("Workload from", { exact: true }).inputValue(), f.from);
      assert.equal(await section.getByLabel("Workload to", { exact: true }).inputValue(), f.to);
    }
  }
  stage = "logout";
  await page.goto("https://localhost:7142/profile"); await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("https://localhost:7142/login");
  process.stdout.write(JSON.stringify({ success: true, revoked: f.revoked, verified: "long UTC period and exact multi-branch population; independent authorization" }));
} catch { process.stdout.write(JSON.stringify({ success: false, stage })); process.exitCode = 1; }
finally { if (browser) await browser.close(); }
