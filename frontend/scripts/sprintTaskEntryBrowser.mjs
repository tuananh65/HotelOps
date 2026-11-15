import { chromium } from "@playwright/test";
let input = ""; for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser, stage = "launch"; const checks = [], requests = [];
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const context = await browser.newContext(); const page = await context.newPage(); page.setDefaultTimeout(15000);
  page.on("response", r => { if (/\/(accept|start|progress|pause|resume)$/.test(new URL(r.url()).pathname)) requests.push({ action: new URL(r.url()).pathname.split("/").pop(), method: r.request().method(), status: r.status() }); });
  stage = "canonical login"; await page.goto("https://localhost:7142/operational-tasks");
  await page.getByLabel("Username or email").fill(fixture.username); await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click(); await page.waitForURL("**/operational-tasks"); checks.push(stage);
  stage = "read assigned task"; await page.getByLabel("Branch ID").fill(String(fixture.branch)); await page.getByRole("button", { name: "Load tasks", exact: true }).click();
  await page.getByRole("button", { name: fixture.code + ": Synthetic task 0", exact: true }).click();
  await page.getByText("Task execution state: ASSIGNED", { exact: true }).waitFor();
  if (await page.getByRole("button", { name: "Save progress", exact: true }).isEnabled()) throw new Error("Assigned progress editable"); checks.push(stage);
  stage = "accept task"; await page.getByRole("button", { name: "Accept task", exact: true }).click(); await page.getByRole("status").filter({ hasText: /^Task accepted\.$/ }).waitFor();
  await page.getByText("Task execution state: ACCEPTED", { exact: true }).waitFor(); checks.push(stage);
  stage = "reload accepted state"; await page.getByRole("button", { name: "Reload progress (discard edits)", exact: true }).click(); await page.getByText("Task execution state: ACCEPTED", { exact: true }).waitFor(); checks.push(stage);
  stage = "start task"; await page.getByRole("button", { name: "Start task", exact: true }).click(); await page.getByText("Task started. Working time is now being recorded.", { exact: true }).waitFor();
  await page.getByText("Task execution state: IN_PROGRESS", { exact: true }).waitFor(); checks.push(stage);
  stage = "save real progress"; await page.getByLabel("Progress (%)", { exact: true }).fill("40"); await page.getByLabel("Progress note", { exact: true }).fill("Synthetic accepted and started task");
  await page.getByRole("button", { name: "Save progress", exact: true }).click(); await page.getByText("Progress saved. Reaching 100% does not complete this task.", { exact: true }).waitFor(); checks.push(stage);
  stage = "pause with approved catalog"; await page.getByRole("button", { name: "Load pause reasons", exact: true }).click();
  await page.getByRole("option", { name: "Other", exact: true }).waitFor({ state: "attached" }); await page.getByLabel("Pause reason", { exact: true }).selectOption({ label: "Other" });
  await page.getByLabel("Pause note", { exact: true }).fill("Synthetic lifecycle pause"); await page.getByRole("button", { name: "Pause task", exact: true }).click();
  await page.getByText("Task paused. Working interval closed.", { exact: true }).waitFor(); checks.push(stage);
  stage = "resume without new reason"; await page.getByRole("button", { name: "Resume task", exact: true }).click(); await page.getByText("Task resumed. Working time is now being recorded.", { exact: true }).waitFor(); checks.push(stage);
  stage = "reload and persisted history"; await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await page.getByText("Task execution state: IN_PROGRESS", { exact: true }).waitFor();
  for (const text of ["Task accepted.", "Task started.", "Synthetic accepted and started task", "Task paused: Other. Synthetic lifecycle pause", "Task resumed."]) await page.getByText(text, { exact: true }).first().waitFor();
  if (await page.getByLabel("Progress (%)", { exact: true }).inputValue() !== "40") throw new Error("Progress not persisted"); checks.push(stage);
  stage = "logout protects task route"; await page.goto("https://localhost:7142/profile"); await page.getByRole("button", { name: "Logout", exact: true }).click(); await page.waitForURL("**/login");
  await page.goto("https://localhost:7142/operational-tasks"); await page.waitForURL("**/login?**"); checks.push(stage);
  console.log(JSON.stringify({ success: true, stage, checks, requests }));
} catch (error) { console.log(JSON.stringify({ success: false, stage, checks, requests, error: String(error.message).slice(0, 600) })); process.exitCode = 1; }
finally { await browser?.close(); }
