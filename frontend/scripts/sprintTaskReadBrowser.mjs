import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "launch";
const checks = [];
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  stage = "canonical deep link login";
  await page.goto("https://localhost:7142/operational-tasks");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL("**/operational-tasks");
  checks.push(stage);
  stage = "branch task list";
  await page.getByLabel("Branch ID").fill(String(fixture.branch));
  await page.getByRole("button", { name: "Load tasks", exact: true }).click();
  await page.getByText("2 tasks", { exact: true }).waitFor();
  checks.push(stage);
  stage = "detail and persisted progress";
  await page
    .getByRole("button", {
      name: fixture.code + ": Synthetic task 0",
      exact: true,
    })
    .click();
  await page
    .getByRole("heading", { name: "Progress history", exact: true })
    .waitFor();
  await page.getByText("Latest synthetic update", { exact: true }).waitFor();
  await page.getByText("Earlier synthetic update", { exact: true }).waitFor();
  checks.push(stage);
  stage = "paused task progress save";
  await page.getByText("Task execution state: PAUSED", { exact: true }).waitFor();
  await page.getByLabel("Progress (%)", { exact: true }).fill("75");
  await page.getByLabel("Progress note", { exact: true }).fill("Browser synthetic progress");
  await page.getByRole("button", { name: "Save progress", exact: true }).click();
  await page.getByText("Progress saved. Reaching 100% does not complete this task.", { exact: true }).waitFor();
  await page.getByRole("button", { name: "Reload progress (discard edits)", exact: true }).click();
  await page.getByText("Task execution state: PAUSED", { exact: true }).waitFor();
  if(await page.getByLabel("Progress (%)", { exact: true }).inputValue()!=="75") throw new Error("Saved progress missing after reload");
  checks.push(stage);
  stage = "search and empty result";
  await page
    .getByRole("button", { name: "Back to tasks", exact: true })
    .click();
  await page.getByLabel("Search tasks").fill("no-such-synthetic-task");
  await page.getByRole("button", { name: "Load tasks", exact: true }).click();
  await page.getByText("0 tasks", { exact: true }).waitFor();
  checks.push(stage);
  stage = "wrong branch denial";
  await page.getByLabel("Branch ID").fill(String(fixture.foreign));
  await page.getByRole("button", { name: "Load tasks", exact: true }).click();
  await page
    .getByRole("alert")
    .filter({ hasText: "You do not have access" })
    .waitFor();
  checks.push(stage);
  stage = "logout hides task data";
  await page.goto("https://localhost:7142/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login");
  await page.goto("https://localhost:7142/operational-tasks");
  await page.waitForURL("**/login?**");
  checks.push(stage);
  process.stdout.write(JSON.stringify({ success: true, checks }));
} catch (e) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      checks,
      diagnostic: String(e?.message ?? "Browser failed").split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
