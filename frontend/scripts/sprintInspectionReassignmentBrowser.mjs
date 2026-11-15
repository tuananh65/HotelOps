import { chromium } from "@playwright/test";
import { randomUUID } from "node:crypto";
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
  const origin = "https://localhost:7143";
  const api = `/api/branches/${fixture.branch}/inspections/${fixture.inspection}`;
  const entry = fixture.notice
    ? "/notifications"
    : fixture.recipientRead
      ? `/inspections/${fixture.branch}/${fixture.inspection}/execution`
      : `/inspections/cleaning-tasks/${fixture.task}/assign?branchId=${fixture.branch}`;
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
  stage = "login with exact return destination";
  await page.goto(origin + entry);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + entry);
  checks.push(stage);
  if (fixture.notice || fixture.recipientRead) {
    stage =
      "new inspector opens reassignment notification and retained results";
    if (fixture.notice) {
      const row = page
        .getByRole("listitem")
        .filter({
          has: page.getByRole("button", {
            name: `Mark notification ${fixture.notice} as read`,
            exact: true,
          }),
        });
      await row
        .getByRole("button", { name: "Open inspection", exact: true })
        .click();
    }
    await page.waitForURL(
      `${origin}/inspections/${fixture.branch}/${fixture.inspection}/execution`,
    );
    await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
    const read = await request(api + "/execution");
    if (read.status !== 200)
      throw Error("New inspector cannot read assigned execution");
    checks.push(stage);
  } else {
    const before = await request(api + "/reassignment-options");
    if (before.status !== 200) throw Error("Options unavailable: " + before.status + " " + before.text.slice(0,500));
    stage = "eligible inspector form: " + before.text;
    await page.getByLabel("New inspector", { exact: true }).waitFor();
    const options = JSON.parse(before.text);
    if (options.state !== fixture.state)
      throw Error("Unexpected source state " + options.state);
    const csrf = JSON.parse((await request("/api/auth/csrf")).text).csrfToken;
    const body = {
      inspectorId: fixture.other,
      cycleETag: options.cycleETag,
      reason: "Synthetic browser handoff",
    };
    const headers = {
      "X-CSRF-TOKEN": csrf,
      "If-Match": before.etag,
      "Idempotency-Key": randomUUID(),
    };
    for (const test of [
      {
        name: "missing CSRF",
        status: 400,
        headers: { "If-Match": before.etag, "Idempotency-Key": randomUUID() },
      },
      {
        name: "missing ETag",
        status: 428,
        headers: { "X-CSRF-TOKEN": csrf, "Idempotency-Key": randomUUID() },
      },
      {
        name: "stale inspection",
        status: 412,
        headers: { ...headers, "If-Match": '"FFFFFFFFFFFFFFFF"' },
      },
      {
        name: "stale cycle",
        status: 412,
        data: { ...body, cycleETag: '"FFFFFFFFFFFFFFFF"' },
      },
      {
        name: "ineligible inspector",
        status: 400,
        data: { ...body, inspectorId: 900000000 },
      },
    ]) {
      stage = test.name;
      const result = await request(api + "/reassign", {
        method: "POST",
        headers: test.headers ?? headers,
        data: test.data ?? body,
      });
      if (result.status !== test.status)
        throw Error(`Expected ${test.status}, got ${result.status}`);
      const after = await request(api + "/reassignment-options");
      if (after.etag !== before.etag || after.text !== before.text)
        throw Error("Rejected change mutated source");
      checks.push({
        name: stage,
        status: result.status,
        sourceUnchanged: true,
      });
    }
    async function change(selected) {
      await page
        .getByLabel("New inspector", { exact: true })
        .selectOption(String(selected));
      await page
        .getByLabel("Change reason", { exact: true })
        .fill("Synthetic browser handoff");
      await page
        .getByRole("button", { name: "Review inspector change", exact: true })
        .click();
      const response = page.waitForResponse(
        (r) =>
          r.url().endsWith(api + "/reassign") &&
          r.request().method() === "POST",
      );
      await page
        .getByRole("button", { name: "Confirm inspector change", exact: true })
        .click();
      const result = await response;
      if (result.status() !== 200)
        throw Error(
          "Change response " +
            result.status() +
            ": " +
            (await result.text()).slice(0, 300),
        );
      await page.getByText(/^MSG08: Inspector changed successfully/).waitFor();
      return result.json();
    }
    stage = "review and commit inspector change preserving source lifecycle";
    const receipt = await change(fixture.other);
    const after = JSON.parse(
      (await request(api + "/reassignment-options")).text,
    );
    if (
      after.inspectorId !== fixture.other ||
      after.state !== options.state ||
      after.versionId !== options.versionId ||
      after.dueAt !== options.dueAt ||
      after.cycleETag !== options.cycleETag ||
      receipt.previousInspectorId !== fixture.actor
    )
      throw Error("Change altered source lifecycle");
    checks.push(stage);
    stage = "previous inspector execution denied immediately";
    if ((await request(api + "/execution")).status !== 403)
      throw Error("Previous inspector retained execution");
    checks.push(stage);
    if (fixture.returnToActor) {
      stage = "explicit refresh and return assignment";
      await page
        .getByRole("button", { name: "Refresh inspector options", exact: true })
        .click();
      await page.getByLabel("New inspector", { exact: true }).waitFor();
      await change(fixture.actor);
      if ((await request(api + "/execution")).status !== 200)
        throw Error("Current inspector cannot read");
      checks.push(stage);
    }
  }
  console.log(JSON.stringify({ success: true, state: fixture.state, checks }));
} catch (error) {
  console.log(
    JSON.stringify({ success: false, stage, checks, failure: error.message }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
