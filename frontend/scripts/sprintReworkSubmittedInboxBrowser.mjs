import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser;
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(30000);
  const origin = "https://localhost:7143";
  await page.goto(origin + "/notifications"); await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/notifications");
  const title = page.getByRole("heading", { name: "Cleaning rework submitted", exact: true });
  await title.waitFor();
  if (await title.count() !== 1) throw Error("Duplicate supervisor submission notice");
  await page.reload(); await title.waitFor();
  const status = await page.evaluate(async id => (await fetch(`/api/notifications/${id}/source`, { credentials: "include" })).status, fixture.notification);
  if (status !== 404) throw Error("Uncomposed supervisor source must remain closed");
  process.stdout.write(JSON.stringify({ success: true, supervisorInbox: true, reload: true, sourceFailClosed: true }));
} catch (error) {
  process.stdout.write(JSON.stringify({ success: false, diagnostic: String(error.message).split("\n")[0] })); process.exitCode = 1;
} finally { await browser?.close(); }
