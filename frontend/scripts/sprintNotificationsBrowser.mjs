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
  stage = "anonymous protected deep link";
  await page.goto(origin + "/notifications");
  await page.waitForURL("**/login**");
  checks.push(stage);
  stage = "real login";
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/notifications");
  checks.push(stage);
  const signedCookies = await context.cookies();
  stage = "recipient inbox";
  await page.getByText(fixture.title, { exact: true }).waitFor();
  await page
    .getByRole("heading", { name: "Notification center", exact: true })
    .waitFor();
  if ((await page.locator("body").innerText()).includes(fixture.foreignTitle))
    throw Error("foreign");
  checks.push(stage);
  stage = "select multiple and mark read";
  const boxes = page.getByRole("checkbox");
  if ((await boxes.count()) !== 2) throw Error("recipient selection count");
  await boxes.nth(0).check();
  await boxes.nth(1).check();
  await page
    .getByRole("button", { name: "Mark selected as read", exact: true })
    .click();
  await page.waitForFunction(() =>
    document.body.innerText.includes("Unread: 0"),
  );
  checks.push(stage);
  stage = "reload persisted read";
  await page.reload();
  await page.getByText(fixture.title, { exact: true }).waitFor();
  if (
    !(await page
      .getByRole("button", {
        name: `Mark notification ${fixture.id} as read`,
        exact: true,
      })
      .isDisabled())
  )
    throw Error("read not persisted");
  checks.push(stage);
  stage = "selection cleared and read controls disabled";
  if (
    !(await page
      .getByRole("button", { name: "Mark selected as read", exact: true })
      .isDisabled()) ||
    !(await page
      .getByRole("button", { name: "Mark all as read", exact: true })
      .isDisabled())
  )
    throw Error("read controls");
  checks.push(stage);
  stage = "cookie and storage boundaries";
  const cookies = await context.cookies();
  if (
    !cookies.some(
      (c) => c.name.startsWith("__Host-HotelOps") && c.httpOnly && c.secure,
    )
  )
    throw Error("cookie flags");
  if (
    (await page.evaluate(() => localStorage.length + sessionStorage.length)) !==
    0
  )
    throw Error("persistent browser state");
  checks.push(stage);
  stage = "API and missing assets are not SPA fallbacks";
  for (const path of [
    "/api/no-such-notification-route",
    "/assets/missing.js",
  ]) {
    const response = await page.evaluate(async (path) => {
      const r = await fetch(path);
      return { status: r.status, type: r.headers.get("content-type") ?? "" };
    }, path);
    if (response.status !== 404 || response.type.includes("text/html"))
      throw Error("fallback");
  }
  checks.push(stage);
  stage = "logout";
  await page.getByRole("link", { name: "Profile", exact: true }).click();
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login**");
  await page.goto(origin + "/notifications");
  await page.waitForURL("**/login**");
  checks.push(stage);
  stage = "revoked-cookie shell recovery";
  await context.addCookies(signedCookies);
  await page.goto(origin + "/notifications");
  await page.waitForURL("**/login**");
  await page.getByRole("button", { name: "Sign in", exact: true }).waitFor();
  checks.push(stage);
  process.stdout.write(
    JSON.stringify({
      success: true,
      checks,
      transport:
        "Real browser HTTPS API; no route interception or fetch mocks; TLS validation enabled",
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
