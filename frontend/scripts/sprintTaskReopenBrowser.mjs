import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const f = JSON.parse(input);
input = "";
let browser,
  page,
  stage = "launch";
const checks = [],
  requests = [];
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const context = await browser.newContext();
  page = await context.newPage();
  page.setDefaultTimeout(15000);
  page.on("response", (r) => {
    const path = new URL(r.url()).pathname;
    if (path.includes("/operational-tasks"))
      requests.push({ path, method: r.request().method(), status: r.status() });
  });
  async function login(user) {
    await page.goto("https://localhost:7142/operational-tasks");
    await page.getByLabel("Username or email").fill(user);
    await page.getByLabel("Password", { exact: true }).fill(f.password);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await page.waitForURL("**/operational-tasks");
    await page.goto(
      `https://localhost:7142/operational-tasks?branchId=${f.branch}&taskId=${f.task}`,
    );
  }
  async function logout() {
    await page.goto("https://localhost:7142/profile");
    await page.getByRole("button", { name: "Logout", exact: true }).click();
    await page.waitForURL("**/login");
  }
  stage = "manager reviews Closed result and previous cycle history";
  await login(f.manager);
  const reopen = page.getByRole("region", { name: "Task reopening" });
  await page
    .getByRole("region", { name: "Submitted completion", exact: true })
    .getByText(f.previousResultNote, { exact: true })
    .waitFor();
  await page
    .getByRole("region", {
      name: `Previous completion ${f.oldSubmission}`,
      exact: true,
    })
    .getByText("Operational result verified in browser", { exact: true })
    .waitFor();
  checks.push(stage);
  stage = "manager reopens to a different eligible executor";
  await reopen
    .getByLabel("Reopening executor", { exact: true })
    .selectOption(String(f.assignee));
  await reopen
    .getByLabel("Reopening reason", { exact: true })
    .fill("Further issue found after independent verification");
  await reopen
    .getByLabel("Remediation instructions", { exact: true })
    .fill("Repeat the inspection and correct the remaining issue");
  await reopen
    .getByRole("button", { name: "Reopen task", exact: true })
    .click();
  await page
    .getByText(
      "MSG08: Task reopened. The executor must accept the new cycle.",
      { exact: true },
    )
    .waitFor();
  await page.reload();
  await reopen
    .getByText(
      "Remediation instructions: Repeat the inspection and correct the remaining issue",
      { exact: true },
    )
    .waitFor();
  checks.push(stage);
  await logout();
  stage = "new executor sees remediation and must Accept again";
  await login(f.executor);
  await page
    .getByText("Task execution state: ASSIGNED", { exact: true })
    .waitFor();
  await reopen
    .getByText(
      "Remediation instructions: Repeat the inspection and correct the remaining issue",
      { exact: true },
    )
    .waitFor();
  if (
    await page.getByRole("button", { name: "Start task", exact: true }).count()
  )
    throw Error("Reopen bypasses acceptance");
  await page.getByRole("button", { name: "Accept task", exact: true }).click();
  await page
    .getByText("Task execution state: ACCEPTED", { exact: true })
    .waitFor();
  checks.push(stage);
  stage = "new cycle starts with cleared criterion and records new work";
  await page.getByRole("button", { name: "Start task", exact: true }).click();
  await page
    .getByText("Task execution state: IN_PROGRESS", { exact: true })
    .waitFor();
  if (
    (await page
      .getByLabel("Criterion result: Public acceptance criterion", {
        exact: true,
      })
      .inputValue()) === "PASS"
  )
    throw Error("Previous cycle criterion leaked into current results");
  await page.getByLabel("Progress (%)", { exact: true }).fill("100");
  await page
    .getByLabel("Criterion result: Public acceptance criterion", {
      exact: true,
    })
    .selectOption("PASS");
  await page
    .getByLabel("Progress note", { exact: true })
    .fill("New executor corrected remaining issue");
  await page
    .getByRole("button", { name: "Save progress", exact: true })
    .click();
  await page
    .getByText("Progress saved. Reaching 100% does not complete this task.", {
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  stage = "new cycle submits and cannot self verify";
  await page
    .getByRole("button", { name: "Complete task", exact: true })
    .click();
  await page
    .getByLabel("Result note", { exact: true })
    .fill("Third processing cycle corrected by new executor");
  await page
    .getByLabel("Handover note", { exact: true })
    .fill("Independent verification requested");
  await page
    .getByRole("button", { name: "Submit completion", exact: true })
    .click();
  await page
    .getByRole("status")
    .filter({ hasText: "MSG09: Task submitted" })
    .waitFor();
  await page.reload();
  const verification = page.getByRole("region", { name: "Task verification" });
  await verification
    .getByText("Workflow: PENDING_VERIFICATION", { exact: true })
    .waitFor();
  if (
    await verification
      .getByRole("button", { name: "Accept completion", exact: true })
      .count()
  )
    throw Error("Current performer can self verify");
  checks.push(stage);
  await logout();
  stage = "prior-cycle performer can independently verify the new cycle";
  await login(f.verifier);
  await verification
    .getByText("Workflow: PENDING_VERIFICATION", { exact: true })
    .waitFor();
  await verification
    .getByLabel("Verification note or return reason", { exact: true })
    .fill("New cycle performed independently and checked");
  await verification
    .getByRole("button", { name: "Accept completion", exact: true })
    .click();
  await verification.getByText("Workflow: CLOSED", { exact: true }).waitFor();
  await page.reload();
  await verification.getByText("Workflow: CLOSED", { exact: true }).waitFor();
  checks.push(stage);
  stage = "prior completion remains readable after third cycle";
  await page
    .getByRole("region", {
      name: `Previous completion ${f.oldSubmission}`,
      exact: true,
    })
    .getByText("Operational result verified in browser", { exact: true })
    .waitFor();
  await page
    .getByRole("region", { name: "Submitted completion", exact: true })
    .getByText("Third processing cycle corrected by new executor", {
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  await logout();
  f.password = "";
  console.log(
    JSON.stringify({ success: true, stage, taskId: f.task, checks, requests }),
  );
} catch (e) {
  console.log(
    JSON.stringify({
      success: false,
      stage,
      taskId: f.task,
      checks,
      requests,
      error: String(e.message).slice(0, 700),
      visibleText: await page
        ?.locator("body")
        .innerText()
        .catch(() => "unavailable"),
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
