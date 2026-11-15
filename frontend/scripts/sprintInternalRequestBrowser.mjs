import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser, stage = "login";
const checks = [];
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(20000);
  const origin = "https://localhost:7142";
  await page.goto(origin + "/internal-requests");
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/internal-requests");
  stage = "admission";
  if (fixture.mode === "denied") {
    await page.getByRole("alert").filter({ hasText: "do not have access" }).waitFor();
    assert.equal(await page.getByRole("region", { name: "Request list", exact: true }).count(), 0);
    await page.goto(origin + "/dashboard");
    assert.equal(await page.evaluate(async () => (await fetch("/api/internal-requests?pageSize=1")).status), 403);
    assert.equal(await page.getByRole("link", { name: "Internal requests", exact: true }).count(), 0);
    checks.push("revoked role hides history and navigation");
  } else {
    const list = page.getByRole("region", { name: "Request list", exact: true });
    await list.waitFor();
    assert.equal(await list.locator("tbody tr").count(), fixture.mode === "related" ? 2 : 25);
    if (fixture.mode === "manager") {
      await page.getByRole("button", { name: "Next page", exact: true }).click();
      await page.getByText("Page 2", { exact: true }).waitFor();
      await list.waitFor(); assert.equal(await list.locator("tbody tr").count(), 1);
      await page.getByRole("button", { name: "Previous page", exact: true }).click();
      await list.waitFor(); assert.equal(await list.locator("tbody tr").count(), 25);
      checks.push("branch authorization and SQL pagination render 25 plus 1");
    }
    stage = "search";
    await page.getByLabel("Search requests").fill("Synthetic history 1");
    await list.waitFor();
    const row = list.locator("tr").filter({ has: page.getByRole("cell", { name: "Synthetic history 1", exact: true }) });
    await row.getByRole("button").click();
    const timeline = page.getByRole("region", { name: "Request timeline", exact: true });
    await timeline.waitFor();
    await page.getByRole("heading", { name: `Request ${fixture.branch}`, exact: true }).waitFor();
    assert.equal(await timeline.getByText(/Handler \d+; assigned by/).count(), 1);
    checks.push("authorized current assignment rendered from persisted request timeline");
    stage = "empty search";
    await page.getByRole("button", { name: "Back to requests", exact: true }).click();
    await page.getByLabel("Search requests").fill("no-synthetic-match");
    await page.getByText("No requests match your current access and filters.").waitFor();
    stage = "cross-branch source";
    const inaccessible = await page.evaluate(async id => (await fetch(`/api/internal-requests/${id}/timeline`)).status, fixture.department);
    assert.equal(inaccessible, 404);
    checks.push("empty search and inaccessible request remain safe");
    stage = "navigation";
    await page.goto(origin + "/dashboard");
    await page.getByRole("link", { name: "Internal requests", exact: true }).waitFor();
  }
  stage = "logout";
  assert.equal(await page.evaluate(async () => {
    const csrf = await (await fetch("/api/auth/csrf")).json();
    return (await fetch("/api/auth/logout", { method: "POST", headers: { "X-CSRF-TOKEN": csrf.csrfToken } })).status;
  }), 204);
  await page.goto(origin + "/internal-requests"); await page.waitForURL("**/login?*");
  assert.equal(await page.getByRole("region", { name: "Request list" }).count(), 0);
  checks.push("logout clears access and protected route returns to login");
  console.log(JSON.stringify({ success: true, mode: fixture.mode, checks }));
} catch (error) {
  console.log(JSON.stringify({ success: false, stage, errorType: error?.name, checks })); process.exitCode = 1;
} finally { await browser?.close(); }
