import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
const checks = [];
let browser,
  stage = "launch";
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const context = await browser.newContext();
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const origin = "https://localhost:7142";
  stage = "login to notification center";
  await page.goto(origin + "/notifications");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/notifications");
  await page
    .getByRole("heading", { name: "Notification center", exact: true })
    .waitFor();
  const filters = fixture.filters;
  async function apply() {
    await page
      .getByRole("button", { name: "Apply filters", exact: true })
      .click();
  }
  async function matching(count) {
    await page.waitForFunction(
      (n) => document.body.innerText.includes(`Matching: ${n}.`),
      count,
    );
  }
  async function rowIds() {
    return page
      .getByRole("checkbox")
      .evaluateAll((boxes) =>
        boxes.map((box) =>
          Number(
            box.parentElement.textContent.match(/Select notification (\d+)/)[1],
          ),
        ),
      );
  }
  function equal(actual, expected) {
    if (JSON.stringify(actual) !== JSON.stringify(expected))
      throw Error("row ordering");
  }
  stage = "combined type period priority search unread filters";
  for (const [label, value] of Object.entries({
    search: filters.prefix,
    type: "TASK_ASSIGNED",
    priority: "NORMAL",
    from: filters.date,
    to: filters.date,
  }))
    await page.getByLabel(label, { exact: true }).fill(value);
  await page.getByLabel("Read status", { exact: true }).selectOption("true");
  await apply();
  await matching(26);
  equal(await rowIds(), filters.expected.slice(0, 25));
  checks.push(stage);
  stage = "pagination preserves filters and stable ties";
  await page.getByRole("checkbox").first().check();
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page
    .getByLabel(`Select notification ${filters.expected[25]}`, { exact: true })
    .waitFor();
  equal(await rowIds(), filters.expected.slice(25));
  if (
    !(await page
      .getByRole("button", { name: "Mark selected as read", exact: true })
      .isDisabled())
  )
    throw Error("selection leaked between pages");
  if (
    !(await page
      .getByRole("button", { name: "Next", exact: true })
      .isDisabled())
  )
    throw Error("last page allows next");
  checks.push(stage);
  stage = "read last unread page returns to remaining matches";
  await page
    .getByRole("button", {
      name: `Mark notification ${filters.expected[25]} as read`,
      exact: true,
    })
    .click();
  await matching(25);
  equal(await rowIds(), filters.expected.slice(0, 25));
  if (
    !(await page
      .getByRole("button", { name: "Previous", exact: true })
      .isDisabled())
  )
    throw Error("read did not reset page");
  if (
    !(
      await page.getByLabel("Applied notification filters").innerText()
    ).includes("unread: true")
  )
    throw Error("mutation discarded filters");
  checks.push(stage);
  stage = "read status filter shows persisted selected row";
  await page.getByLabel("Read status", { exact: true }).selectOption("false");
  await apply();
  await matching(1);
  equal(await rowIds(), filters.expected.slice(25));
  checks.push(stage);
  stage = "finish filtered batch and recover empty list";
  await page.getByLabel("Read status", { exact: true }).selectOption("true");
  await apply();
  await matching(25);
  for (const box of await page.getByRole("checkbox").all()) await box.check();
  await page
    .getByRole("button", { name: "Mark selected as read", exact: true })
    .click();
  await matching(0);
  await page
    .getByText(
      "No matching notifications (MSG10). Change or clear the applied filters.",
      { exact: true },
    )
    .waitFor();
  await page.getByLabel("Read status", { exact: true }).selectOption("false");
  await apply();
  await matching(26);
  equal(await rowIds(), filters.expected.slice(0, 25));
  checks.push(stage);
  stage = "logout revokes browser session";
  await page.getByRole("link", { name: "Profile", exact: true }).click();
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login**");
  checks.push(stage);
  process.stdout.write(
    JSON.stringify({
      success: true,
      checks,
      transport: "Browser HTTPS SQL; no mocks; TLS validated",
    }),
  );
} catch {
  process.stdout.write(
    JSON.stringify({ success: false, checks, failureStage: stage }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
