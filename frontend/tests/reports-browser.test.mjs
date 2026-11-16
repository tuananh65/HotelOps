import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "@playwright/test";
import { existsSync } from "node:fs";
import { build } from "esbuild";
const compiled = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
 import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
 import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore';
 import {AuthRouteHost} from './src/auth/AuthRouteHost';
 const store=createAuthStore(); window.reportTest=store;
 const host=<AuthRouteHost/>;
 createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);
 `,
  },
  bundle: true,
  outfile: ".test-output/reports-browser.js",
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  const executablePath =
    process.env.HOTELOPS_TEST_BROWSER ||
    [
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
      "C:/Program Files/Google/Chrome/Application/chrome.exe",
    ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
const session = {
  user: { id: "42", username: "owner", name: "Synthetic Owner" },
  expiresAt: "2026-11-17T00:00:00Z",
  absoluteExpiresAt: "2026-11-17T12:00:00Z",
  mustChangePassword: false,
  csrfToken: "synthetic-csrf",
};
async function fixture(run) {
  const page = await browser.newPage();
  const state = {
    exportStatus: 200,
    optionsStatus: 200,
    sessionStatus: 200,
    calls: [],
    downloads: 0,
  };
  page.on("download", () => state.downloads++);
  await page.route("**/*", async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    state.calls.push({ path, method: req.method(), body: req.postDataJSON() });
    const json = (body, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (req.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    if (path === "/api/auth/session")
      return json(
        state.sessionStatus === 200
          ? session
          : { status: state.sessionStatus, code: "AUTHENTICATION_REQUIRED" },
        state.sessionStatus,
      );
    if (path === "/api/auth/csrf") return json({ csrfToken: "fresh-csrf" });
    if (path === "/api/reports/options")
      return json(
        {
          branches: [{ id: 1, code: "B", name: "Branch" }],
          departments: [{ id: 7, branchId: 1, name: "Synthetic receiving department" }],
          calendar: "UTC",
          defaultPeriodDays: 30,
          exportRowLimit: 50000,
        },
        state.optionsStatus,
      );
    if (path.endsWith("/preview")) {
      const q = req.postDataJSON();
      return json({
        snapshotId: "b".repeat(32),
        key: path.split("/")[3],
        snapshot: "2026-11-15T00:00:00Z",
        filters: {
          branchIds: [1],
          departmentId: q.departmentId ?? null,
          from: q.from,
          to: q.to,
          calendar: "UTC",
        },
        sections: [
          {
            key: "tasks",
            columns: ["title"],
            rows: [{ title: "PRIVATE REPORT ROW" }],
          },
        ],
        totalCount: 1,
        missingSources: [],
        exportFormats: ["csv"],
        appliedFilters: Object.fromEntries(Object.entries(q).map(([k, v]) => [k, String(v)])),
      });
    }
    if (path.endsWith("/export"))
      return route.fulfill({
        status: state.exportStatus,
        contentType:
          state.exportStatus === 200 ? "text/csv" : "application/json",
        body: state.exportStatus === 200 ? "frozen,csv" : "{}",
      });
    return json({ status: 403, code: "ACCESS_DENIED" }, 403);
  });
  try {
    await page.goto("http://hotelops.test/reports");
    await page.addScriptTag({ content: compiled.outputFiles[0].text });
    await run(page, state);
  } finally {
    await page.close();
  }
}
async function preview(page) {
  await page.getByRole("button", { name: "Preview", exact: true }).click();
  await page.getByText("PRIVATE REPORT ROW", { exact: true }).waitFor();
}

test("inspection actor filters are explicit and editing removes the frozen preview", async () => fixture(async (page, state) => {
  await page.getByLabel("Report type", { exact: true }).selectOption("inspections");
  await page.getByLabel("Housekeeper ID", { exact: true }).fill("42");
  await page.getByLabel("Inspector ID", { exact: true }).fill("43");
  await preview(page);
  const sent = state.calls.findLast(c => c.path.endsWith("/preview"));
  assert.equal(sent.path, "/api/reports/inspections/preview");
  assert.equal(sent.body.housekeeperId, 42); assert.equal(sent.body.inspectorId, 43);
  await page.getByLabel("Housekeeper ID", { exact: true }).fill("44");
  assert.equal(await page.getByText("PRIVATE REPORT ROW", { exact: true }).count(), 0);
  assert.equal(await page.getByRole("button", { name: "Export CSV", exact: true }).count(), 0);
  await page.getByLabel("Report type", { exact: true }).selectOption("tasks");
  await preview(page);
  const task = state.calls.findLast(c => c.path.endsWith("/preview"));
  assert.equal(task.body.housekeeperId, undefined); assert.equal(task.body.inspectorId, undefined);
}));
test("employee and receiving department filters send scoped queries and invalidate snapshots", async () => fixture(async (page, state) => {
  await page.getByLabel("Employee ID", { exact: true }).fill("42");
  await preview(page);
  assert.equal(state.calls.findLast(c => c.path.endsWith("/preview")).body.employeeId, 42);
  await page.getByLabel("Employee ID", { exact: true }).fill("43");
  assert.equal(await page.getByText("PRIVATE REPORT ROW", { exact: true }).count(), 0);
  await page.getByLabel("Report type", { exact: true }).selectOption("requests");
  assert.equal(await page.getByLabel("Employee ID", { exact: true }).count(), 0);
  await page.getByLabel("Department", { exact: true }).selectOption("7");
  await preview(page);
  const sent = state.calls.findLast(c => c.path.endsWith("/preview"));
  assert.equal(sent.path, "/api/reports/requests/preview");
  assert.equal(sent.body.departmentId, 7);
  assert.equal(sent.body.employeeId, undefined);
}));

test("revoked export clears cached report and offers no download", async () =>
  fixture(async (page, state) => {
    await preview(page);
    state.exportStatus = 403;
    await page.getByRole("button", { name: "Export CSV", exact: true }).click();
    await page
      .getByRole("alert")
      .filter({ hasText: "no longer available" })
      .waitFor();
    assert.equal(
      await page.getByText("PRIVATE REPORT ROW", { exact: true }).count(),
      0,
    );
    assert.equal(
      await page
        .getByRole("button", { name: "Export CSV", exact: true })
        .count(),
      0,
    );
    assert.equal(state.downloads, 0);
  }));
test("export failure keeps frozen preview for explicit retry without auto download", async () =>
  fixture(async (page, state) => {
    await preview(page);
    state.exportStatus = 500;
    await page.getByRole("button", { name: "Export CSV", exact: true }).click();
    await page.getByRole("status").filter({ hasText: "MSG27" }).waitFor();
    assert.equal(state.downloads, 0);
    assert.equal(
      state.calls.filter((c) => c.path.endsWith("/export")).length,
      1,
    );
    state.exportStatus = 200;
    const downloaded = page.waitForEvent("download");
    await page.getByRole("button", { name: "Export CSV", exact: true }).click();
    const file = await downloaded;
    await file.delete();
    assert.equal(
      state.calls.filter((c) => c.path.endsWith("/preview")).length,
      1,
    );
    assert.equal(
      new Set(
        state.calls
          .filter((c) => c.path.endsWith("/export"))
          .map((c) => c.path),
      ).size,
      1,
    );
  }));
test("session revalidation removes prior report data before new login", async () =>
  fixture(async (page, state) => {
    await preview(page);
    state.sessionStatus = 401;
    await page.evaluate(() => window.reportTest.revalidate());
    await page.getByRole("button", { name: "Sign in", exact: true }).waitFor();
    assert.equal(
      await page.getByText("PRIVATE REPORT ROW", { exact: true }).count(),
      0,
    );
    assert.equal(state.downloads, 0);
  }));
