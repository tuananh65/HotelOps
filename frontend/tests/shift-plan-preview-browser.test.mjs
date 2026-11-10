import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { existsSync } from "node:fs";
const stamp = "2026-11-10T00:00:00Z",
  tag = '"' + "A".repeat(64) + '"',
  next = '"' + "B".repeat(64) + '"';
const session = {
  user: { id: "7", username: "actor", name: "Actor" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "SYNTHETIC-CSRF",
};
const result = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{ShiftPlanPreviewPanel}from'./src/workday/ShiftPlanPreviewPanel';
const s=window.scenario,calls=[],waiting=new Map();window.fetch=async(url,init)=>{calls.push({url,method:init.method});return Response.json(s.auth?.body??${JSON.stringify(session)},{status:s.auth?.status??200});};
const preview=async(drafts,signal)=>{calls.push({drafts,aborted:false});const c=calls.at(-1);signal.addEventListener('abort',()=>c.aborted=true);if(s.throw)throw Error('SECRET-ERROR');if(s.deferred)return new Promise(resolve=>waiting.set(s.deferred,resolve));return s.preview??{rows:drafts.map((_,index)=>({index,startUtc:'2028-02-29T16:00:00Z',endUtc:'2028-03-01T00:00:00Z',issues:[]})),requiresServerRevalidation:true};};
const store=createAuthStore(createAuthApi(window.fetch));const app=<ShiftPlanPreviewPanel adapter={s.enabled?preview:undefined}/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,scenario:s,revalidate:()=>store.revalidate(),finish:(k,v)=>waiting.get(k)(v)};
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  const executablePath = [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function run(s, fn) {
  const p = await browser.newPage(),
    errors = [];
  p.setDefaultTimeout(5000);
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : r.abort(),
  );
  try {
    await p.goto("https://offline.test");
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: result.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(
      !(await p.locator("body").innerText()).includes("SYNTHETIC-CSRF"),
    );
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET-ERROR"));
  } finally {
    await p.close();
  }
}
async function add(p) {
  for (const [k, v] of Object.entries({
    employeeId: "7",
    branchId: "1",
    departmentId: "2",
    templateId: "3",
    shiftDate: "2028-02-29",
  }))
    await p.getByLabel(k, { exact: true }).fill(v);
  await p.getByRole("button", { name: "Add draft", exact: true }).click();
}
const clear = {
  rows: [
    {
      index: 0,
      startUtc: "2028-02-29T16:00:00Z",
      endUtc: "2028-03-01T00:00:00Z",
      issues: [],
    },
  ],
  requiresServerRevalidation: true,
};
test("default unavailable never fetches worktime or calls adapter", () =>
  run({}, async (p) => {
    await add(p);
    assert.ok(
      await p.getByRole("button", { name: "Preview assignments" }).isDisabled(),
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.drafts).length,
      ),
      0,
    );
    assert.equal(
      await p.getByRole("button", { name: /save|start|end workday/i }).count(),
      0,
    );
  }));
test("multi-row preview uses exact immutable drafts and server intervals", () =>
  run({ enabled: true }, async (p) => {
    await add(p);
    await p.getByLabel("employeeId", { exact: true }).fill("8");
    await p.getByRole("button", { name: "Add draft", exact: true }).click();
    await p.getByRole("button", { name: "Preview assignments" }).click();
    await p.getByRole("region", { name: "Preview results" }).waitFor();
    const calls = await p.evaluate(() =>
      window.fixture.calls.filter((c) => c.drafts),
    );
    assert.equal(calls.length, 1);
    assert.equal(calls[0].drafts.length, 2);
    assert.equal(calls[0].drafts[0].employeeId, 7);
    assert.equal(calls[0].drafts[1].employeeId, 8);
    assert.match(
      await p.locator("body").innerText(),
      /Server revalidation required/,
    );
  }));
test("invalid date never invokes adapter", () =>
  run({ enabled: true }, async (p) => {
    await add(p);
    await p.getByLabel("shiftDate", { exact: true }).fill("2027-02-29");
    await p.getByRole("button", { name: "Add draft", exact: true }).click();
    await p.getByText(/real calendar date/).waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.drafts).length,
      ),
      0,
    );
  }));
test("existing and proposed overlaps rendered without save authority", () =>
  run(
    {
      enabled: true,
      preview: { ...clear, rows: [{ ...clear.rows[0], issues: [7, 8] }] },
    },
    async (p) => {
      await add(p);
      await p.getByRole("button", { name: "Preview assignments" }).click();
      await p
        .getByText("Existing assignment overlap", { exact: true })
        .waitFor();
      await p
        .getByText("Proposed assignment overlap", { exact: true })
        .waitFor();
    },
  ));
for (const scenario of [
  { throw: true },
  { preview: { detail: "SECRET-ERROR" } },
  { preview: { ...clear, requiresServerRevalidation: false } },
])
  test("safe unavailable without retry " + JSON.stringify(scenario), () =>
    run({ enabled: true, ...scenario }, async (p) => {
      await add(p);
      await p.getByRole("button", { name: "Preview assignments" }).click();
      await p.getByText(/Preview unavailable/).waitFor();
      assert.equal(
        await p.evaluate(
          () => window.fixture.calls.filter((c) => c.drafts).length,
        ),
        1,
      );
    }),
  );
test("double click cannot queue previews and edit aborts stale result", () =>
  run({ enabled: true, deferred: "old" }, async (p) => {
    await add(p);
    await p
      .getByRole("button", { name: "Preview assignments" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.getByText("Loading preview...").waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.drafts).length,
      ),
      1,
    );
    await p.getByLabel("branchId", { exact: true }).fill("2");
    await p.evaluate((v) => window.fixture.finish("old", v), clear);
    assert.equal(
      await p.getByRole("region", { name: "Preview results" }).count(),
      0,
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.find((c) => c.drafts).aborted,
      ),
      true,
    );
  }));
test("remove draft invalidates prior preview", () =>
  run({ enabled: true }, async (p) => {
    await add(p);
    await p.getByRole("button", { name: "Preview assignments" }).click();
    await p.getByRole("region", { name: "Preview results" }).waitFor();
    await p.getByRole("button", { name: "Remove draft 1" }).click();
    assert.equal(
      await p.getByRole("region", { name: "Preview results" }).count(),
      0,
    );
    assert.ok(
      await p.getByRole("button", { name: "Preview assignments" }).isDisabled(),
    );
  }));
for (const auth of [
  { status: 401, body: {} },
  { body: { ...session, mustChangePassword: true } },
])
  test("anonymous/restricted no draft or preview " + auth.status, () =>
    run({ enabled: true, auth }, async (p) => {
      await p.getByText("Confirmed unrestricted session required.").waitFor();
      assert.equal(
        await p.getByLabel("employeeId", { exact: true }).count(),
        0,
      );
      assert.equal(
        await p.evaluate(
          () => window.fixture.calls.filter((c) => c.drafts).length,
        ),
        0,
      );
    }),
  );
test("identity replacement removes drafts and ignores late preview", () =>
  run({ enabled: true, deferred: "old" }, async (p) => {
    await add(p);
    await p.getByRole("button", { name: "Preview assignments" }).click();
    await p.getByText("Loading preview...").waitFor();
    await p.evaluate(
      async (s) => {
        window.fixture.scenario.auth = { body: s };
        await window.fixture.revalidate();
      },
      { ...session, user: { ...session.user, id: "8" } },
    );
    await p.getByLabel("employeeId", { exact: true }).waitFor();
    await p.evaluate((v) => window.fixture.finish("old", v), clear);
    assert.equal(
      await p.getByRole("region", { name: "Preview results" }).count(),
      0,
    );
    assert.ok(
      await p.getByRole("button", { name: "Preview assignments" }).isDisabled(),
    );
  }));
