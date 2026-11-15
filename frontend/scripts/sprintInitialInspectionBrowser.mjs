import { chromium } from "@playwright/test";
import { randomUUID } from "node:crypto";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "launch",
  inspection = fixture.inspection ?? null;
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
  const origin = "https://localhost:7143";
  const entry = fixture.notice
    ? "/notifications"
    : `/rooms/status?branchId=${fixture.branch}&roomId=${fixture.room}`;
  const api = `/api/branches/${fixture.branch}/cleaning-tasks/${fixture.task}`;
  async function request(url, options = {}) {
    return page.evaluate(
      async ({ url, options }) => {
        const r = await fetch(url, {
          method: options.method ?? "GET",
          credentials: "include",
          cache: "no-store",
          headers: {
            ...(options.data ? { "Content-Type": "application/json" } : {}),
            ...options.headers,
          },
          body: options.data ? JSON.stringify(options.data) : undefined,
        });
        return {
          status: r.status,
          etag: r.headers.get("ETag"),
          text: await r.text(),
        };
      },
      { url, options },
    );
  }
  stage = "anonymous assignment API denied";
  await page.goto(origin + "/login");
  const anonymous = await request(api + "/initial-inspection-options");
  if (anonymous.status !== 401)
    throw Error(
      "Anonymous options status=" +
        anonymous.status +
        " body=" +
        anonymous.text.slice(0, 300),
    );
  checks.push(stage);
  stage = "login return path";
  await page.goto(origin + entry);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + entry);
  checks.push(stage);
  if (!fixture.notice) {
    stage = "room board to pending inspections";
    await page
      .locator(`a[href="/inspections/assignment?branchId=${fixture.branch}&roomId=${fixture.room}"]`)
      .click();
    await page.waitForURL(
      `${origin}/inspections/assignment?branchId=${fixture.branch}&roomId=${fixture.room}`,
    );
    checks.push(stage);
    stage = "pending queue to exact cleaning cycle";
    await page
      .getByRole("heading", { name: "Pending inspections", exact: true })
      .waitFor();
    await page
      .getByRole("link", { name: "Review inspection assignment", exact: true })
      .click();
    await page.waitForURL(
      `${origin}/inspections/cleaning-tasks/${fixture.task}/assign?branchId=${fixture.branch}`,
    );
    await page.getByLabel("Inspector", { exact: true }).waitFor();
    checks.push(stage);
    const initial = await request(api + "/initial-inspection-options");
    if (initial.status !== 200) throw Error("Options unavailable");
    const options = JSON.parse(initial.text);
    const csrf = JSON.parse((await request("/api/auth/csrf")).text).csrfToken;
    const body = {
      inspectorId: fixture.actor,
      checklistVersionId: options.checklistVersionId,
      cycleETag: options.cycleETag,
      dueAt: new Date(Date.now() + 3600000).toISOString(),
      reason: "Synthetic initial inspection assignment",
    };
    const headers = {
      "X-CSRF-TOKEN": csrf,
      "If-Match": initial.etag,
      "Idempotency-Key": randomUUID(),
    };
    for (const test of [
      {
        name: "missing CSRF",
        status: 400,
        headers: { "If-Match": initial.etag, "Idempotency-Key": randomUUID() },
      },
      {
        name: "missing ETag",
        status: 428,
        headers: { "X-CSRF-TOKEN": csrf, "Idempotency-Key": randomUUID() },
      },
      {
        name: "stale task",
        status: 412,
        headers: { ...headers, "If-Match": '"FFFFFFFFFFFFFFFF"' },
      },
      {
        name: "stale cycle",
        status: 412,
        data: { ...body, cycleETag: '"FFFFFFFFFFFFFFFF"' },
      },
      {
        name: "foreign inspector",
        status: 400,
        data: { ...body, inspectorId: 900000000 },
      },
      {
        name: "nonfuture deadline",
        status: 400,
        data: { ...body, dueAt: "2020-01-01T00:00:00Z" },
      },
    ]) {
      stage = test.name;
      const result = await request(api + "/inspections", {
        method: "POST",
        data: test.data ?? body,
        headers: test.headers ?? headers,
      });
      if (result.status !== test.status)
        throw Error(`Expected ${test.status}, got ${result.status}`);
      const after = await request(api + "/initial-inspection-options");
      if (after.etag !== initial.etag || after.text !== initial.text)
        throw Error("Rejected request changed source");
      checks.push({
        name: stage,
        status: result.status,
        sourceUnchanged: true,
      });
    }
    stage = "canonical review and assignment";
    await page
      .getByLabel("Inspector", { exact: true })
      .selectOption(String(fixture.actor));
    await page.getByLabel("Due time", { exact: true }).fill(
      await page.evaluate(() => {
        const d = new Date(Date.now() + 3600000);
        d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
        return d.toISOString().slice(0, 16);
      }),
    );
    await page
      .getByLabel("Assignment reason", { exact: true })
      .fill(body.reason);
    await page
      .getByRole("button", { name: "Review assignment", exact: true })
      .click();
    const response = page.waitForResponse(
      (r) =>
        r.url().endsWith(api + "/inspections") &&
        r.request().method() === "POST",
    );
    await page
      .getByRole("button", { name: "Confirm assignment", exact: true })
      .click();
    const assigned = await response;
    if (assigned.status() !== 201)
      throw Error("Assignment response " + assigned.status());
    inspection = (await assigned.json()).id;
    await page
      .getByText(
        `Inspection assigned successfully. Inspection #${inspection}.`,
        { exact: true },
      )
      .waitFor();
    checks.push(stage);
    stage = "existing assignment refresh does not offer duplicate";
    await page.reload();
    await page
      .getByText(`An inspection is already assigned: #${inspection}.`, {
        exact: true,
      })
      .waitFor();
    if (
      await page
        .getByRole("button", { name: "Review assignment", exact: true })
        .count()
    )
      throw Error("Duplicate form visible");
    checks.push(stage);
  } else {
    stage = "assignment inbox opens exact inspection";
    const row = page.getByRole("listitem").filter({
      has: page.getByRole("button", {
        name: `Mark notification ${fixture.notice} as read`,
        exact: true,
      }),
    });
    await row
      .getByRole("button", { name: "Open inspection", exact: true })
      .click();
    await page.waitForURL(
      `${origin}/inspections/${fixture.branch}/${inspection}/execution`,
    );
    await page.getByText(/^State: ASSIGNED\./).waitFor();
    checks.push(stage);
    stage = "assigned checklist execution";
    await page
      .getByLabel("Reason", { exact: true })
      .fill("Synthetic inspection execution");
    await page
      .getByRole("button", { name: "Start Inspection", exact: true })
      .click();
    await page.getByText(/^State: INSPECTING\./).waitFor();
    await page.getByRole("combobox", { name: /^Result/ }).selectOption("PASS");
    const saved = page.waitForResponse(
      (r) =>
        r.url().endsWith(`/inspections/${inspection}/results`) &&
        r.request().method() === "PUT",
    );
    await page
      .getByRole("button", { name: "Save results", exact: true })
      .click();
    if ((await saved).status() !== 200) throw Error("Saving results failed");
    await page
      .getByRole("button", { name: "Finish → PENDING_CONCLUSION", exact: true })
      .click();
    await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
    await page.reload();
    await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
    checks.push(stage);
  }
  console.log(JSON.stringify({ success: true, inspection, checks }));
} catch (error) {
  console.log(
    JSON.stringify({ success: false, stage, checks, failure: error.message }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
