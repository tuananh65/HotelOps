import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = ""; for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser, stage = "login"; const checks = [];
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(15000);
  const origin = "https://localhost:7142", path = `/api/internal-requests/${fixture.request}/linked-tasks`;
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const panel = page.getByRole("region", { name: "Linked operational tasks", exact: true });
  await panel.getByRole("button", { name: "Link existing task", exact: true }).waitFor();
  const before = await page.evaluate(async p => (await fetch(p)).json(), path);
  stage = "public link guards";
  const guards = await page.evaluate(async ({ p, v, task }) => {
    const csrf = await (await fetch("/api/auth/csrf")).json();
    const post = async (headers, mandatory) => (await fetch(p, { method: "POST", headers: { "Content-Type": "application/json", ...headers }, body: JSON.stringify({ taskId: task, mandatory }) })).status;
    return [await post({ "If-Match": `"${v}"` }, true), await post({ "X-CSRF-TOKEN": csrf.csrfToken }, true),
      await post({ "If-Match": `"${v}"`, "X-CSRF-TOKEN": csrf.csrfToken }, undefined)];
  }, { p: path, v: before.version, task: fixture.task }); assert.deepEqual(guards, [400, 428, 400]);
  checks.push("Public link requires CSRF, strong ETag and explicit mandatory metadata");
  stage = "canonical link existing";
  await panel.getByRole("button", { name: "Link existing task", exact: true }).click();
  await panel.getByLabel("Open task", { exact: true }).selectOption(String(fixture.task));
  await panel.getByRole("button", { name: "Save task link", exact: true }).click();
  await panel.getByRole("status").filter({ hasText: "Select a task and whether" }).waitFor();
  await panel.getByLabel("Completion requirement", { exact: true }).selectOption("required");
  await panel.getByRole("button", { name: "Save task link", exact: true }).click();
  await panel.getByRole("status").filter({ hasText: "MSG08: Task linked" }).waitFor();
  await panel.getByRole("button", { name: "View linked tasks", exact: true }).click();
  await panel.getByRole("link").filter({ hasText: "Synthetic linked work 2" }).waitFor();
  const after = await page.evaluate(async p => (await fetch(p)).json(), path);
  assert.equal(after.total, 3); assert.equal(after.items.find(t => t.taskId === fixture.task).mandatory, true);
  assert.notEqual(after.version, before.version);
  const stale = await page.evaluate(async ({ p, v, task }) => {
    const csrf = await (await fetch("/api/auth/csrf")).json();
    return (await fetch(p, { method: "POST", headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": `"${v}"` }, body: JSON.stringify({ taskId: task, mandatory: false }) })).status;
  }, { p: path, v: before.version, task: fixture.task }); assert.equal(stale, 412);
  checks.push("UI selects an existing open task, requires explicit completion choice, saves and reloads link; stale retry denies");
  stage = "logout"; await page.goto(origin + "/profile"); await page.getByRole("button", { name: "Logout", exact: true }).click(); await page.waitForURL(origin + "/login");
  console.log(JSON.stringify({ success: true, stage: "complete", checks }));
} catch { console.log(JSON.stringify({ success: false, stage, checks })); process.exitCode = 1; }
finally { await browser?.close(); }
