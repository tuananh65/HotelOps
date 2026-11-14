import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
const checks = [];
let stage = "launch",
  browser;
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
  stage = "anonymous activation redirects to login";
  await page.goto(`${origin}/users/${fixture.targetId}/activate`);
  await page.waitForURL("**/login");
  checks.push(stage);
  stage = "real administrator login";
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/dashboard");
  checks.push(stage);
  stage = "canonical account activation navigation";
  await page.goto(`${origin}/users/${fixture.targetId}`);
  await page
    .getByRole("link", { name: "Activate account", exact: true })
    .click();
  await page
    .getByText("Full name: Synthetic activation target", { exact: true })
    .waitFor();
  checks.push(stage);
  stage = "explicit review and confirmed activation";
  await page
    .getByLabel("Reason", { exact: true })
    .fill("Synthetic activation verification");
  await page
    .getByRole("button", { name: "Review activation", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Confirm activation", exact: true })
    .click();
  await page
    .getByText(
      `Activation confirmed by the server. Account ID: ${fixture.targetId}. accountStatus: ACTIVE.`,
      { exact: true },
    )
    .waitFor();
  if (
    await page
      .getByRole("button", { name: "Confirm activation", exact: true })
      .count()
  )
    throw Error("repeat confirmation");
  checks.push(stage);
  stage = "cookie and storage boundaries";
  if (
    !(await context.cookies()).some(
      (c) => c.name.startsWith("__Host-HotelOps") && c.httpOnly && c.secure,
    )
  )
    throw Error("cookie flags");
  if (await page.evaluate(() => localStorage.length + sessionStorage.length))
    throw Error("persistent state");
  checks.push(stage);
  stage = "logout and protected activation recovery";
  await page.getByRole("link", { name: "Profile", exact: true }).click();
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login**");
  await page.goto(`${origin}/users/${fixture.targetId}/activate`);
  await page.waitForURL("**/login");
  checks.push(stage);
  process.stdout.write(JSON.stringify({ success: true, checks }));
} catch {
  process.stdout.write(
    JSON.stringify({ success: false, failedStage: stage, checks }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
