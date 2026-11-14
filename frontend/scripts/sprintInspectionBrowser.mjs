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
  const origin = "https://localhost:7142",
    path = `/inspections/${fixture.branch}/${fixture.inspection}/execution`;
  stage = "anonymous deep link and login";
  await page.goto(origin + path);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + path);
  checks.push(stage);
  stage = "canonical inspection admission";
  await page
    .getByRole("heading", {
      name: `Inspection #${fixture.inspection}`,
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  stage = "start";
  await page
    .getByLabel("Reason", { exact: true })
    .fill("Synthetic inspection verification");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await page.getByText(/^State: INSPECTING\./).waitFor();
  checks.push(stage);
  stage = "save failed criterion";
  await page.getByRole("combobox", { name: /^Result/ }).selectOption("FAIL");
  await page.getByRole("button", { name: "Save results", exact: true }).click();
  await page
    .getByLabel("Failed criterion")
    .locator("option")
    .filter({ hasText: "Synthetic criterion" })
    .waitFor({ state: "attached" });
  checks.push(stage);
  stage = "defect with server catalog and evidence";
  const item = await page
    .getByLabel("Failed criterion")
    .locator("option")
    .evaluateAll((xs) => xs.map((x) => x.value).find((v) => v));
  await page.getByLabel("Failed criterion").selectOption(item);
  await page
    .getByRole("combobox", { name: /^Severity/ })
    .selectOption(String(fixture.severity));
  await page.getByLabel("Description", { exact: true }).fill("Synthetic stain");
  await page.getByLabel("Location", { exact: true }).fill("Synthetic bed");
  await page
    .getByLabel(/Evidence \(JPG/)
    .setInputFiles({
      name: "evidence.png",
      mimeType: "image/png",
      buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    });
  await page
    .getByRole("button", { name: "Record defect", exact: true })
    .click();
  await page.getByText(/Defect #.*Synthetic stain/).waitFor();
  checks.push(stage);
  stage = "finish pending conclusion";
  await page.getByRole("button", { name: /^Finish/ }).click();
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  checks.push(stage);
  stage = "reload persists";
  await page.reload();
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  await page.getByText(/Defect #.*Synthetic stain/).waitFor();
  checks.push(stage);
  stage = "logout";
  await page.goto(origin + "/profile");
  await page.getByRole("button", { name: "Logout", exact: true }).click();
  await page.waitForURL("**/login");
  checks.push(stage);
  process.stdout.write(JSON.stringify({ success: true, checks }));
} catch {
  process.stdout.write(JSON.stringify({ success: false, stage, checks }));
  process.exitCode = 1;
} finally {
  await browser?.close();
}
