import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "@playwright/test";
import { build } from "esbuild";
const compiled = await build({ stdin: { resolveDir: process.cwd(), loader: "tsx", contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {AuthRouteHost} from './src/auth/AuthRouteHost';
const store=createAuthStore(); window.historyStore=store; const host=<AuthRouteHost/>;
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);`
}, bundle: true, outfile: ".test-output/ir-history-browser.js", write: false, format: "iife", jsx: "automatic", define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" } });
let browser;
before(async () => { browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true }); });
after(async () => browser?.close());
async function fixture(run) {
  const page = await browser.newPage();
  const state = { session: 200, history: 200 };
  await page.route("**/*", async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    const json = (data, status=200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(data) });
    if (request.resourceType() === "document") return route.fulfill({ contentType: "text/html", body: '<div id="root"></div>' });
    if (path === "/api/auth/session") return json({ user: { id: "42", username: "staff", name: "Synthetic" }, expiresAt: "2026-11-17T00:00:00Z", absoluteExpiresAt: "2026-11-17T12:00:00Z", mustChangePassword: false, csrfToken: "test" }, state.session);
    if (path === "/api/auth/csrf") return json({ csrfToken: "test" });
    if (path === "/api/internal-requests") return json({ items: [{ id: 1, number: "IR1", branchId: 1, receivingDepartmentId: null, subject: "PRIVATE REQUEST <script>", status: "SUBMITTED", deadline: null, overdue: false, currentAssigneeId: null }], totalCount: 1, page: 1, pageSize: 25, snapshotUtc: "2026-11-16T00:00:00Z" }, state.history);
    return json({}, 403);
  });
  try {
    await page.goto("https://history.test/internal-requests"); await page.addScriptTag({ content: compiled.outputFiles[0].text });
    await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
    await run(page, state);
  } finally { await page.close(); }
}
test("refresh denial removes previously visible history", async () => fixture(async (page, state) => {
  state.history = 403;
  await page.getByRole("button", { name: "Refresh history", exact: true }).click();
  await page.getByRole("alert").filter({ hasText: "do not have access" }).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", { exact: true }).count(), 0);
}));
test("session revalidation removes request history before login", async () => fixture(async (page, state) => {
  state.session = 401;
  await page.evaluate(() => window.historyStore.revalidate());
  await page.getByRole("button", { name: "Sign in", exact: true }).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", { exact: true }).count(), 0);
}));
