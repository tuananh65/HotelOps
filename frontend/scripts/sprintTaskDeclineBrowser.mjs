import { chromium } from "@playwright/test";
let input = ""; for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser, stage = "launch"; const checks = [], requests = [];
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const context = await browser.newContext(); const page = await context.newPage(); page.setDefaultTimeout(15000);
  page.on("response", r => { const p = new URL(r.url()).pathname; if (p.includes("operational-tasks") || /notifications.*source/.test(p)) requests.push({ path: p, method: r.request().method(), status: r.status() }); });
  stage = "canonical login"; await page.goto("https://localhost:7142/operational-tasks");
  await page.getByLabel("Username or email").fill(fixture.username); await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click(); await page.waitForURL("**/operational-tasks"); checks.push(stage);
  stage = "scoped workflow list"; await page.getByLabel("Branch ID").fill(String(fixture.branch));
  await page.getByLabel("Workflow status", { exact: true }).selectOption(fixture.mode === "decline" ? "ASSIGNED" : "PENDING_ASSIGNMENT");
  await page.getByRole("button", { name: "Load tasks", exact: true }).click();
  await page.getByRole("button", { name: fixture.code + ": Synthetic decline task", exact: true }).click(); checks.push(stage);
  if (fixture.mode === "decline") {
    stage = "assignment details and disabled blank decline";
    await page.getByText("Task execution state: ASSIGNED", { exact: true }).waitFor();
    await page.getByText("Review assignment before responding", { exact: true }).waitFor();
    await page.getByText(/Priority: .*; deadline:/).waitFor();
    if (await page.getByRole("button", { name: "Decline task", exact: true }).isEnabled()) throw new Error("Blank decline enabled");
    await page.getByLabel("Decline reason", { exact: true }).fill("   ");
    if (await page.getByRole("button", { name: "Decline task", exact: true }).isEnabled()) throw new Error("Whitespace decline enabled"); checks.push(stage);
    stage = "committed decline and clear own task"; await page.getByLabel("Decline reason", { exact: true }).fill("Synthetic browser assignment unavailable");
    await page.getByRole("button", { name: "Decline task", exact: true }).click();
    await page.getByText("MSG19: Task declined and returned to Pending Assignment.", { exact: true }).waitFor();
    await page.getByText("0 tasks", { exact: true }).waitFor();
    if (await page.getByRole("button", { name: "Decline task", exact: true }).count()) throw new Error("Declined task editor retained"); checks.push(stage);
    stage = "reload cannot recover lost own access"; await page.reload();
    await page.getByLabel("Branch ID").fill(String(fixture.branch)); await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === `/api/branches/${fixture.branch}/operational-tasks` && r.status() === 200), page.getByRole("button", { name: "Load tasks", exact: true }).click()]);
    await page.getByText(/ tasks$/, { exact: false }).first().waitFor();
    if (await page.getByRole("button", { name: fixture.code + ": Synthetic decline task", exact: true }).count()) throw new Error("Former assignee can still see task"); checks.push(stage);
  } else {
    stage = "manager sees pending assignment and decline history";
    await page.getByText("Task declined: Synthetic browser assignment unavailable", { exact: true }).waitFor();
    await page.getByText(new RegExp(fixture.code + " — PENDING_ASSIGNMENT")).waitFor(); checks.push(stage);
    stage = "inbox opens authorized declined task"; await page.goto("https://localhost:7142/notifications");
    await page.getByRole("button", { name: "Open task", exact: true }).first().click();
    await page.waitForURL(`**/operational-tasks?branchId=${fixture.branch}&taskId=${fixture.task}`);
    await page.getByText("Task declined: Synthetic browser assignment unavailable", { exact: true }).waitFor(); checks.push(stage);
  }
  if (fixture.mode === "decline" && requests.filter(r => r.path.endsWith("/decline") && r.status === 200).length !== 1) throw new Error("Expected one successful browser Decline");
  stage = "logout revokes browser session"; await page.goto("https://localhost:7142/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click(); await page.waitForURL("**/login"); checks.push(stage);
  console.log(JSON.stringify({ success: true, mode: fixture.mode, stage, checks, requests }));
} catch (error) { console.log(JSON.stringify({ success: false, mode: fixture.mode, stage, checks, requests, error: String(error.message).slice(0, 600) })); process.exitCode = 1; }
finally { await browser?.close(); }
