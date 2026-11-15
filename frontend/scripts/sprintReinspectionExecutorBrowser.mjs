import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input); input = "";
let browser;
try {
  browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
  const page = await browser.newPage(); page.setDefaultTimeout(30000);
  const origin = "https://localhost:7143";
  const source = `/cleaning/reworks/${fixture.rework}?branchId=${fixture.branch}`;
  await page.goto(origin + (fixture.notice ? "/notifications" : source));
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password); fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  if (fixture.notice) {
    await page.waitForURL(origin + "/notifications");
    const title = page.getByRole("heading", { name: "Reinspection passed", exact: true });
    await title.waitFor();
    if (await title.count() !== 1) throw Error("Duplicate terminal notice");
    const mark = page.getByRole("button", { name: `Mark notification ${fixture.notice} as read`, exact: true });
    await mark.click(); await page.getByText("Marked as read. Source records are unchanged.", { exact: true }).waitFor();
    await page.reload(); await title.waitFor();
    if (!await mark.isDisabled()) throw Error("Read acknowledgement not persisted");
    await page.getByRole("listitem").filter({ has: title }).getByRole("button", { name: "Open rework", exact: true }).click();
  }
  await page.waitForURL(origin + source);
  const outcome = fixture.state === "COMPLETED" ? "passed" : "failed";
  await page.getByText(`Reinspection #${fixture.inspection}: ${outcome}.`, { exact: true }).waitFor();
  await page.getByText(`Current room readiness: ${fixture.readiness}.`, { exact: true }).waitFor();
  await page.reload();
  await page.getByText(`Reinspection #${fixture.inspection}: ${outcome}.`, { exact: true }).waitFor();
  if (await page.getByRole("button", { name: /Accept rework|Start rework|Submit rework|Conclude/ }).count()) throw Error("Terminal executor has mutation controls");
  const photos = await page.getByRole("link", { name: /View (defect|remediation) photo/ }).evaluateAll(links => links.map(a => a.getAttribute("href")));
  if (!photos.length) throw Error("Terminal executor evidence missing");
  const checks = await page.evaluate(async ({ fixture, photos }) => {
    const proof = await fetch(`/api/branches/${fixture.branch}/reworks/${fixture.rework}/execution`, { credentials: "include" });
    const data = await proof.json();
    const inspection = await fetch(`/api/branches/${fixture.branch}/reinspections/${fixture.inspection}/execution`, { credentials: "include" });
    const evidence = [];
    for (const file of photos) {
      const response = await fetch(file, { credentials: "include" });
      evidence.push(response.status === 200 && response.headers.get("X-Content-Type-Options") === "nosniff" && (await response.arrayBuffer()).byteLength > 0);
    }
    return { status: proof.status, tag: proof.headers.get("ETag") === data.sourceETag, state: data.state,
      inspectionDenied: [403, 404].includes(inspection.status), evidence: evidence.every(Boolean) };
  }, { fixture, photos });
  if (checks.status !== 200 || !checks.tag || checks.state !== fixture.state || !checks.inspectionDenied || !checks.evidence)
    throw Error("Executor HTTPS proof failed: " + JSON.stringify(checks));
  if (fixture.nextRework) {
    await page.goto(origin + "/notifications");
    const row = page.getByRole("listitem").filter({ has: page.getByRole("button", { name: `Mark notification ${fixture.nextNotice} as read`, exact: true }) });
    await row.getByRole("heading", { name: "Cleaning rework assigned", exact: true }).waitFor();
    await row.getByRole("button", { name: "Open rework", exact: true }).click();
    await page.waitForURL(origin + `/cleaning/reworks/${fixture.nextRework}?branchId=${fixture.branch}`);
    await page.getByText(`Rework #${fixture.nextRework}: pending acceptance.`, { exact: true }).waitFor();
    await page.reload();
    await page.getByText(`Rework #${fixture.nextRework}: pending acceptance.`, { exact: true }).waitFor();
    const next = await page.evaluate(async f => {
      const response = await fetch(`/api/branches/${f.branch}/reworks/${f.nextRework}/execution`, { credentials: "include" });
      const data = await response.json();
      return response.status === 200 && data.inspectionId === f.inspection && data.state === "PENDING_ACCEPTANCE" &&
        data.sourceAuthority === "FE05_COMMITTED_V1" && data.sourceETag === response.headers.get("ETag") && data.defects.length > 0;
    }, fixture);
    if (!next) throw Error("Next-round executor source proof failed");
  }
  process.stdout.write(JSON.stringify({ success: true, terminalExecutor: true, reload: true, ownEvidence: true,
    inspectionDenied: true, inboxSource: !!fixture.notice, readAcknowledgement: !!fixture.notice }));
} catch (error) {
  process.stdout.write(JSON.stringify({ success: false, diagnostic: String(error.message).split("\n")[0] })); process.exitCode = 1;
} finally { await browser?.close(); }
