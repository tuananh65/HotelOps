import { chromium } from "@playwright/test";
import { verifyLockedInspectionDefects } from "./inspectionDefectTerminalChecks.mjs";
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
  async function request(
    url,
    method = "GET",
    headers = {},
    body = { reason: "Synthetic rejection", confirmed: true },
  ) {
    return page.evaluate(
      async ({ url, method, headers, body }) => {
        const r = await fetch(url, {
          method,
          headers: { "Content-Type": "application/json", ...headers },
          body: method === "POST" ? JSON.stringify(body) : undefined,
          credentials: "include",
          cache: "no-store",
        });
        return {
          status: r.status,
          etag: r.headers.get("ETag"),
          text: await r.text(),
        };
      },
      { url, method, headers, body },
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
  if (fixture.rework) {
    stage = "Request Rework preselected form and authoritative assignee";
    const form = page.getByRole("region", { name: "Request cleaning rework" });
    const options = JSON.parse((await request(api + "/rework-options")).text);
    if (
      options.assignees.length !== 1 ||
      !options.assignees[0].originalCleaner ||
      options.defects.length !== 1
    )
      throw Error("Rework options mismatch");
    const optionResponse = await request(api + "/rework-options");
    const reworkBody = {
      assigneeId: options.assignees[0].id,
      defectIds: options.defects.map((d) => d.id),
      dueAt: new Date(Date.now() + 3600000).toISOString(),
      reason: "Synthetic rework request",
    };
    const guardHeaders = {
      "X-CSRF-TOKEN": csrf,
      "If-Match": optionResponse.etag,
    };
    for (const [label, headers, body, status] of [
      ["rework CSRF", { "If-Match": optionResponse.etag }, reworkBody, 400],
      ["rework revision", { "X-CSRF-TOKEN": csrf }, reworkBody, 428],
      [
        "rework stale",
        { ...guardHeaders, "If-Match": '"0000000000000000"' },
        reworkBody,
        412,
      ],
      [
        "rework empty defects",
        guardHeaders,
        { ...reworkBody, defectIds: [] },
        400,
      ],
      [
        "rework foreign defect",
        guardHeaders,
        { ...reworkBody, defectIds: [9007199254740991] },
        400,
      ],
      [
        "rework ineligible assignee",
        guardHeaders,
        { ...reworkBody, assigneeId: 9007199254740991 },
        400,
      ],
      [
        "rework past due",
        guardHeaders,
        { ...reworkBody, dueAt: "2000-01-01T00:00:00Z" },
        400,
      ],
    ]) {
      const rejected = await request(api + "/reworks", "POST", headers, body);
      if (rejected.status !== status)
        throw Error(label + ": " + rejected.status);
      const unchanged = JSON.parse(
        (await request(api + "/rework-options")).text,
      );
      if (unchanged.existingReworkId !== null)
        throw Error(label + " created rework");
      checks.push({ label, status });
    }
    await form
      .getByLabel("Housekeeper")
      .selectOption(String(options.assignees[0].id));
    if (!(await form.getByRole("checkbox").isChecked()))
      throw Error("Defect not preselected");
    const due = await page.evaluate(() => {
      const d = new Date(Date.now() + 3600000);
      const pad = (n) => String(n).padStart(2, "0");
      return (
        d.getFullYear() +
        "-" +
        pad(d.getMonth() + 1) +
        "-" +
        pad(d.getDate()) +
        "T" +
        pad(d.getHours()) +
        ":" +
        pad(d.getMinutes())
      );
    });
    await form.getByLabel("Rework due time").fill(due);
    await form.getByLabel("Rework reason").fill("Synthetic rework request");
    const saved = page.waitForResponse(
      (r) =>
        r.url() === origin + api + "/reworks" &&
        r.request().method() === "POST",
    );
    await form.getByRole("button", { name: "Save rework request" }).click();
    const response = await saved;
    const receipt = await response.json();
    if (response.status() !== 201 || receipt.state !== "PENDING_ACCEPTANCE")
      throw Error("Rework save: " + response.status());
    await form.getByText(/is pending acceptance/).waitFor();
    checks.push({
      stage,
      state: receipt.state,
      notification: receipt.notification,
    });
    await form.getByRole("button", { name: "Reload rework options" }).click();
    await form
      .getByText("MSG03: Open rework #" + receipt.id + " already exists.", {
        exact: true,
      })
      .waitFor();
    const duplicate = await request(
      api + "/reworks",
      "POST",
      guardHeaders,
      reworkBody,
    );
    if (duplicate.status !== 409)
      throw Error("Rework duplicate status " + duplicate.status);
    checks.push("Existing open rework shown; duplicate API rejected409");
  }
  stage = "Failed inspection locks defects";
  checks.push(
    await verifyLockedInspectionDefects(
      page,
      api,
      fixture.branch,
      fixture.inspection,
      "FAILED",
    ),
  );
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
