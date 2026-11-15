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
  const page = await browser.newPage();
  // Allow the API's unchanged 30-second SQL timeout to return an actual HTTP result.
  page.setDefaultTimeout(45000);
  const origin = "https://localhost:7142",
    path = `/inspections/${fixture.branch}/${fixture.inspection}/execution`;
  const api = `/api/branches/${fixture.branch}/inspections/${fixture.inspection}`;
  async function request(url, method = "GET", headers = {}) {
    return page.evaluate(
      async ({ url, method, headers }) => {
        const r = await fetch(url, {
          method,
          headers: { "Content-Type": "application/json", ...headers },
          body:
            method === "POST"
              ? JSON.stringify({
                  reason: "Synthetic rejection",
                  confirmed: true,
                })
              : undefined,
          credentials: "include",
          cache: "no-store",
        });
        return {
          status: r.status,
          etag: r.headers.get("ETag"),
          text: await r.text(),
        };
      },
      { url, method, headers },
    );
  }
  stage = "login and canonical rejection admission";
  await page.goto(origin + path);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + path);
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  await page.getByText(/MSG03: A mandatory criterion failed/).waitFor();
  if (
    !(await page
      .getByRole("button", { name: "Approve inspection", exact: true })
      .isDisabled())
  )
    throw Error("Mandatory FAIL allowed approval");
  checks.push(stage);
  const before = await request(api + "/execution");
  const csrf = JSON.parse((await request("/api/auth/csrf")).text).csrfToken;
  for (const [label, headers, expected] of [
    ["CSRF required", { "If-Match": before.etag }, 400],
    ["ETag required", { "X-CSRF-TOKEN": csrf }, 428],
    [
      "stale ETag rejected",
      { "X-CSRF-TOKEN": csrf, "If-Match": '"0000000000000000"' },
      412,
    ],
  ]) {
    stage = label;
    const result = await request(api + "/reject", "POST", headers);
    if (
      result.status !== expected ||
      (await request(api + "/execution")).etag !== before.etag
    )
      throw Error(label + ": " + result.status);
    checks.push({ label, status: result.status, unchanged: true });
  }
  stage = "reject via canonical UI";
  let body, acknowledgement;
  if (fixture.approvalRace) {
    stage = "concurrent approval with one ETag";
    const raced = await Promise.all(
      [1, 2].map(() =>
        request(api + "/reject", "POST", {
          "X-CSRF-TOKEN": csrf,
          "If-Match": before.etag,
        }),
      ),
    );
    const statuses = raced.map((r) => r.status).sort((a, b) => a - b);
    if (JSON.stringify(statuses) !== "[200,412]")
      throw Error("Approval race: " + JSON.stringify(statuses));
    const winner = raced.find((r) => r.status === 200);
    body = JSON.parse(winner.text);
    acknowledgement = winner.status;
    checks.push({ label: stage, statuses });
    await page.reload();
  } else {
    const response = page.waitForResponse(
      (r) =>
        r.url() === origin + api + "/reject" && r.request().method() === "POST",
    );
    await page
      .getByLabel("Reason", { exact: true })
      .fill("Synthetic rejection");
    await page
      .getByLabel("Confirm inspection failure and require cleaning rework")
      .check();
    await page
      .getByRole("button", { name: "Reject inspection", exact: true })
      .click();
    const ack = await response;
    body = await ack.json();
    acknowledgement = ack.status();
  }
  if (
    acknowledgement !== 200 ||
    body.state !== "FAILED" ||
    body.effectiveReadiness !==
      (fixture.blocked ? "BLOCKED" : "REWORK_REQUIRED") ||
    body.messageCode !== (fixture.blocked ? "MSG17" : null) ||
    !["PENDING_DELIVERY"].includes(body.notification)
  )
    throw Error("Rejection response mismatch: " + JSON.stringify(body));
  await page.getByText(/^State: FAILED\./).waitFor();
  checks.push({
    label: stage,
    state: body.state,
    readiness: body.effectiveReadiness,
    message: body.messageCode,
    notification: body.notification,
  });
  stage = "duplicate rejection denied";
  const duplicate = await request(api + "/reject", "POST", {
    "X-CSRF-TOKEN": csrf,
    "If-Match": before.etag,
  });
  if (duplicate.status !== 412)
    throw Error("Duplicate status " + duplicate.status);
  checks.push(stage);
  stage = "reload persisted rejection and effective readiness";
  const expectedReadiness = (
    fixture.concurrentBlock ? !fixture.blocked : fixture.blocked
  )
    ? "BLOCKED"
    : "REWORK_REQUIRED";
  if (fixture.concurrentBlock) {
    const deadline = Date.now() + 10000;
    while (Date.now() < deadline) {
      const latest = await request(api + "/execution");
      if (
        latest.status === 200 &&
        JSON.parse(latest.text).effectiveReadiness === expectedReadiness
      )
        break;
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  }
  await page.reload();
  await page.getByText(/^State: FAILED\./).waitFor();
  await page
    .getByText(`Effective room readiness: ${expectedReadiness}.`, {
      exact: true,
    })
    .waitFor();
  checks.push(stage);
  process.stdout.write(JSON.stringify({ success: true, checks }));
} catch (error) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      checks,
      diagnostic: String(error.message).split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
