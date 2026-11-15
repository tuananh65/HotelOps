import { chromium, expect } from "@playwright/test";
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
  page = await (await browser.newContext()).newPage();
  page.setDefaultTimeout(15000);
  page.on("response", (r) => {
    const path = new URL(r.url()).pathname;
    if (path.includes("task-workload"))
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
    await page
      .getByRole("button", {
        name: "Review workload and time logs",
        exact: true,
      })
      .click();
  }
  async function logout() {
    await page.goto("https://localhost:7142/profile");
    await page.getByRole("button", { name: "Logout", exact: true }).click();
    await page.waitForURL("**/login");
  }
  stage = "manager workload preserves two executors across three cycles";
  await login(f.manager);
  const workload = page.getByRole("region", {
    name: "Task workload",
    exact: true,
  });
  async function apply() {
    const response = page.waitForResponse(
      (r) =>
        new URL(r.url()).pathname ===
          `/api/branches/${f.branch}/task-workload` &&
        r.request().method() === "GET",
    );
    await workload
      .getByRole("button", { name: "Apply workload filters", exact: true })
      .click();
    await (await response).finished();
    await workload
      .getByText("Loading workload…", { exact: true })
      .waitFor({ state: "hidden" });
  }
  const employees = workload.getByRole("table", {
    name: "Employee workload",
    exact: true,
  });
  await employees.waitFor();
  if ((await employees.locator("tbody tr").count()) !== 2)
    throw Error("Expected both historical executors");
  checks.push(stage);
  stage = "employee drill-down separates task intervals from shift time";
  await employees.getByRole("button").first().click();
  const detail = workload.getByRole("region", {
    name: "Employee time log",
    exact: true,
  });
  await detail.waitFor();
  await detail
    .getByRole("table", { name: "Task work intervals", exact: true })
    .waitFor();
  await detail
    .getByRole("table", { name: "Separate workday time", exact: true })
    .waitFor();
  if (
    (await detail
      .getByRole("table", { name: "Task work intervals", exact: true })
      .locator("tbody tr")
      .count()) < 1
  )
    throw Error("Missing persisted time intervals");
  checks.push(stage);
  stage = "Closed filter returns accepted-cycle workload";
  await detail
    .getByRole("button", { name: "Back to employee workload", exact: true })
    .click();
  await employees.waitFor();
  await workload
    .getByLabel("Workload status", { exact: true })
    .selectOption("CLOSED");
  await apply();
  await expect(employees.locator("tbody tr")).toHaveCount(2);
  checks.push(stage);
  stage = "overdue excludes Closed and shows MSG10";
  await workload.getByLabel("Overdue only", { exact: true }).check();
  await apply();
  await workload
    .getByText("MSG10: No workload matches these filters.", { exact: true })
    .waitFor();
  checks.push(stage);
  stage = "clearing filters restores employee results";
  await workload.getByLabel("Overdue only", { exact: true }).uncheck();
  await workload
    .getByLabel("Workload status", { exact: true })
    .selectOption("");
  await apply();
  await employees.getByRole("button").first().waitFor();
  checks.push(stage);
  await logout();
  f.password = "";
  console.log(JSON.stringify({ success: true, stage, checks, requests }));
} catch (error) {
  console.log(
    JSON.stringify({
      success: false,
      stage,
      checks,
      requests,
      error: String(error.message).slice(0, 500),
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
