import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser, page;
let stage = "login route",
  loginStatus = null;
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  page = await browser.newPage();
  page.setDefaultTimeout(45000);
  const origin = "https://localhost:7142";
  await page.goto(origin + "/notifications");
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  stage = "assignee login";
  const login = page.waitForResponse((r) =>
    r.url().endsWith("/api/auth/login"),
  );
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  loginStatus = (await login).status();
  await page.waitForURL(origin + "/notifications");
  stage = "delivered inbox and reload";
  await page
    .getByRole("heading", { name: "Cleaning rework assigned", exact: true })
    .waitFor();
  if (
    (await page
      .getByRole("button", { name: "Open rework", exact: true })
      .count()) !== 1
  )
    throw Error("Assignee inbox is duplicated or absent");
  await page.reload();
  await page
    .getByRole("heading", { name: "Cleaning rework assigned", exact: true })
    .waitFor();
  const source = page.waitForResponse((r) =>
    r.url().endsWith(`/api/notifications/${fixture.notification}/source`),
  );
  const detail = page.waitForResponse((r) =>
    r
      .url()
      .endsWith(
        `/api/branches/${fixture.branch}/reworks/${fixture.rework}/execution`,
      ),
  );
  await page.getByRole("button", { name: "Open rework", exact: true }).click();
  stage = "source route";
  if ((await source).status() !== 200)
    throw Error("Current assignee cannot resolve source");
  await page.waitForURL(
    origin + `/cleaning/reworks/${fixture.rework}?branchId=${fixture.branch}`,
  );
  // This trial host uses an administrator connection. It MUST NOT bypass proof DML-denial guards.
  if ((await detail).status() !== 503)
    throw Error("Privileged runtime failed to remain closed");
  await page
    .getByRole("alert")
    .filter({ hasText: "Rework is unavailable" })
    .waitFor();
  if (
    (await page
      .getByText("Synthetic rejection defect", { exact: true })
      .count()) !== 0
  )
    throw Error("Untrusted runtime disclosed source details");
  process.stdout.write(
    JSON.stringify({
      success: true,
      deliveredInbox: true,
      reloadPersistence: true,
      sourceResolution: true,
      privilegedDetailFailClosed: true,
    }),
  );
} catch (error) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      loginStatus,
      path: page ? new URL(page.url()).pathname : null,
      diagnostic: String(error.message).split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
