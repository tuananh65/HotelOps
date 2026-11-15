import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "login";
const checks = [];
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const page = await browser.newPage();
  page.setDefaultTimeout(20000);
  const origin = "https://localhost:7142";
  await page.goto(origin + "/audit");
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + "/audit");
  async function get(path, status) {
    const r = await page.evaluate(async (path) => {
      const response = await fetch(path, {
        cache: "no-store",
        redirect: "error",
      });
      return { status: response.status, body: await response.json() };
    }, path);
    assert.equal(r.status, status);
    assert.ok(!JSON.stringify(r.body).includes("SYNTHETIC_SECRET"));
    return r.body;
  }
  if (fixture.mode === "denied-owner") {
    await page
      .getByRole("alert")
      .filter({ hasText: "do not have access" })
      .waitFor();
    assert.equal(await page.getByRole("table").count(), 0);
    await get("/api/audit-logs/options", 403);
    await get(`/api/audit-logs/${fixture.entry}`, 403);
    checks.push(
      "Owner without Administrator assignment cannot list or inspect audit history",
    );
  } else {
    stage = "filter list";
    await page
      .getByLabel("Entity type", { exact: true })
      .fill(fixture.entityType);
    async function apply() {
      const response = page.waitForResponse(
        (r) =>
          r.url().includes("/api/audit-logs?") &&
          new URL(r.url()).searchParams.get("entityType") ===
            fixture.entityType &&
          r.request().method() === "GET",
      );
      await page
        .getByRole("button", { name: "Apply filters", exact: true })
        .click();
      const r = await response;
      assert.equal(r.status(), 200);
      const data = await r.json();
      await page
        .getByText(`${data.totalCount} entries`, { exact: true })
        .waitFor();
      return data;
    }
    const expected = fixture.mode === "global-admin" ? 29 : 27;
    let list = await apply();
    assert.equal(list.totalCount, expected);
    assert.equal(list.items.length, 25);
    for (let i = 1; i < list.items.length; i++)
      assert.ok(
        list.items[i - 1].occurredAt > list.items[i].occurredAt ||
          (list.items[i - 1].occurredAt === list.items[i].occurredAt &&
            list.items[i - 1].id > list.items[i].id),
      );
    assert.ok(
      list.items.every(
        (r) => !r.changedFields?.some((k) => /password|token/i.test(k)),
      ),
    );
    let pending = page.waitForResponse((r) => r.url().includes("page=2"));
    await page.getByRole("button", { name: "Next", exact: true }).click();
    let second = await (await pending).json();
    assert.equal(second.items.length, expected - 25);
    assert.ok(
      second.items.every((r) => !list.items.some((old) => old.id === r.id)),
    );
    checks.push(
      "newest-first ordering, safe changed-field summaries and stable pagination",
    );
    stage = "record history";
    await page.getByLabel("Record ID", { exact: true }).fill("42");
    await page.getByLabel("Action", { exact: true }).fill("UPDATE");
    await page
      .getByLabel("Actor ID", { exact: true })
      .fill(String(fixture.actor));
    await page
      .getByLabel("Branch ID", { exact: true })
      .fill(String(fixture.branch));
    await page.getByLabel("Search", { exact: true }).fill(fixture.entityType);
    const today = new Date().toISOString().slice(0, 10);
    await page.getByLabel("From (UTC)", { exact: true }).fill(today);
    await page.getByLabel("To (UTC)", { exact: true }).fill(today);
    list = await apply();
    assert.equal(list.totalCount, 26);
    await page
      .getByRole("button", { name: `View entry ${fixture.entry}`, exact: true })
      .click();
    await page
      .getByRole("table", { name: "Field changes", exact: true })
      .waitFor();
    const text = await page
      .getByRole("region", { name: "Audit entry", exact: true })
      .innerText();
    assert.ok(
      text.includes("PENDING") &&
        text.includes("COMPLETED") &&
        text.includes("closedAt") &&
        text.includes("acknowledgedAt") &&
        text.includes("readAt") &&
        text.includes("2026-11-15T17:00:01Z") &&
        text.includes("null"),
    );
    assert.ok(
      !text.includes("SYNTHETIC_SECRET") &&
        !text.includes("password") &&
        !text.includes("token"),
    );
    pending = page.waitForResponse((r) => r.url().includes("/api/audit-logs?"));
    await page
      .getByRole("button", { name: "View this record's history", exact: true })
      .click();
    const historyResponse = await pending;
    const url = new URL(historyResponse.url());
    assert.equal(url.searchParams.get("entityId"), "42");
    assert.equal(url.searchParams.get("branchId"), String(fixture.branch));
    for (const key of ["actorId", "action", "search", "from", "to"])
      assert.equal(url.searchParams.has(key), false);
    assert.equal((await historyResponse.json()).totalCount, 26);
    checks.push(
      "combined actor/date/entity/action/branch/search filters; exact record history removes incidental filters but preserves branch scope; before/after null and secret redaction",
    );
    stage = "creation only and empty filters";
    await page.getByLabel("Record ID", { exact: true }).fill("creation-only");
    list = await apply();
    assert.equal(list.totalCount, 1);
    assert.equal(list.items[0].action, "CREATE");
    await page
      .getByRole("button", {
        name: `View entry ${fixture.creation}`,
        exact: true,
      })
      .click();
    await page
      .getByRole("table", { name: "Field changes", exact: true })
      .waitFor();
    assert.ok(
      (
        await page
          .getByRole("table", { name: "Field changes", exact: true })
          .innerText()
      ).includes("Not recorded or withheld"),
    );
    await page.getByLabel("Record ID", { exact: true }).fill("missing");
    list = await apply();
    assert.equal(list.totalCount, 0);
    await page.getByRole("status").filter({ hasText: "MSG10" }).waitFor();
    assert.ok(
      (await page.getByLabel("Applied audit filters").innerText()).includes(
        "entityId=missing",
      ),
    );
    await page.getByLabel("Record ID", { exact: true }).fill("42");
    list = await apply();
    assert.equal(list.totalCount, 26);
    checks.push(
      "creation-only history, honest absent-before values, empty list with applied filters and recovery",
    );
    stage = "scope and readonly";
    await get(
      `/api/audit-logs/${fixture.foreignEntry}`,
      fixture.mode === "global-admin" ? 200 : 404,
    );
    await get(
      `/api/audit-logs/${fixture.globalEntry}`,
      fixture.mode === "global-admin" ? 200 : 404,
    );
    const methods = await page.evaluate(async (id) => {
      const csrf = await (await fetch("/api/auth/csrf")).json();
      const statuses = [];
      for (const method of ["PATCH", "DELETE"])
        statuses.push(
          (
            await fetch(`/api/audit-logs/${id}`, {
              method,
              headers: {
                "X-CSRF-TOKEN": csrf.csrfToken,
                "Content-Type": "application/json",
              },
              body: "{}",
            })
          ).status,
        );
      return statuses;
    }, fixture.entry);
    // Canonical fallback leaves unmatched API methods at 404; no mutation handler exists.
    assert.deepEqual(methods, [404, 404]);
    checks.push(
      "branch/global history scope and no audit edit/delete endpoint",
    );
  }
  stage = "logout";
  const logout = await page.evaluate(async () => {
    const csrf = await (await fetch("/api/auth/csrf")).json();
    return (
      await fetch("/api/auth/logout", {
        method: "POST",
        headers: { "X-CSRF-TOKEN": csrf.csrfToken },
      })
    ).status;
  });
  assert.equal(logout, 204);
  console.log(JSON.stringify({ success: true, mode: fixture.mode, checks }));
} catch (e) {
  console.log(
    JSON.stringify({ success: false, stage, errorType: e?.name, checks }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
