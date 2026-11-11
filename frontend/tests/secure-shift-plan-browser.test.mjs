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
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{SecureShiftPlanWorkspace}from'./src/workday/SecureShiftPlanWorkspace';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>Response.json(r.body??{}, {status:r.status??200});
window.fetch=async(url,init)=>{calls.push({url,...init});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url==='/api/auth/csrf')r=s.csrf??{body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else if(url.endsWith('/preview'))r=s.previewReply??{body:{rows:JSON.parse(init.body).drafts.map((_,index)=>({index,startUtc:'2028-02-29T08:00:00Z',endUtc:'2028-02-29T16:00:00Z',issues:[]})),requiresServerRevalidation:true}};else r=s.saveReply??{status:201,body:{createdShifts:1,createdAssignments:1,notificationCount:1}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));const app=<SecureShiftPlanWorkspace saveEnabled={s.saveEnabled===true}/>;const root=createRoot(document.getElementById('root'));root.render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,scenario:s,revalidate:()=>store.revalidate(),finish:(k,v)=>waiting.get(k)(make(v)),unmount:()=>root.unmount()};`,
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
async function preview(p) {
  await add(p);
  await p.getByRole("button", { name: "Preview assignments" }).click();
  await p.getByRole("region", { name: "Preview results" }).waitFor();
}
const posts = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "POST"));
test("connected scoped preview renders and Save is absent by default", () =>
  run({}, async (p) => {
    await preview(p);
    const calls = await posts(p);
    assert.equal(calls.length, 1);
    assert.ok(calls[0].url.endsWith("/preview"));
    assert.equal(
      await p.getByRole("button", { name: "Review save" }).count(),
      0,
    );
  }));
test("Save review/confirm double click has one POST and staged receipt", () =>
  run({ saveEnabled: true }, async (p) => {
    await preview(p);
    await p.getByRole("button", { name: "Review save" }).click();
    await p.getByRole("button", { name: "Confirm save plan" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p
      .getByText("Shift plan saved; employee notifications staged.")
      .waitFor();
    assert.equal((await posts(p)).length, 2);
    assert.equal(
      await p.getByRole("button", { name: "Confirm save plan" }).count(),
      0,
    );
  }));
for (const status of [401, 403, 503])
  test("preview " + status + " removes results and offers no Save", () =>
    run(
      {
        saveEnabled: true,
        previewReply: { status, body: { detail: "SECRET-ERROR" } },
      },
      async (p) => {
        await add(p);
        await p.getByRole("button", { name: "Preview assignments" }).click();
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Preview results" }).count(),
          0,
        );
        assert.equal(
          await p.getByRole("button", { name: "Review save" }).count(),
          0,
        );
        if (status === 401) {
          await p.getByRole("button", { name: "Check session" }).waitFor();
          assert.equal(
            await p
              .getByRole("button", { name: "Preview assignments" })
              .isDisabled(),
            true,
          );
        }
      },
    ),
  );
test("Save unavailable never claims failure certainty or retries", () =>
  run(
    {
      saveEnabled: true,
      saveReply: { status: 503, body: { detail: "SECRET-ERROR" } },
    },
    async (p) => {
      await preview(p);
      await p.getByRole("button", { name: "Review save" }).click();
      await p.getByRole("button", { name: "Confirm save plan" }).click();
      await p
        .getByText(
          "Save outcome unknown. Do not replay; verify with an authorized owner.",
        )
        .waitFor();
      assert.equal((await posts(p)).length, 2);
      assert.equal(
        await p.getByRole("region", { name: "Preview results" }).count(),
        0,
      );
    },
  ));
test("late Save after session replacement cannot publish receipt", () =>
  run({ saveEnabled: true, saveReply: { deferred: "save" } }, async (p) => {
    await preview(p);
    await p.getByRole("button", { name: "Review save" }).click();
    await p.getByRole("button", { name: "Confirm save plan" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some(
        (c) => c.method === "POST" && c.url.endsWith("/shift-plans"),
      ),
    );
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401 };
      void window.fixture.revalidate();
    });
    await p.getByText("Confirmed unrestricted session required.").waitFor();
    await p.evaluate(() =>
      window.fixture.finish("save", {
        status: 201,
        body: { createdShifts: 1, createdAssignments: 1, notificationCount: 1 },
      }),
    );
    assert.equal(
      await p
        .getByText("Shift plan saved; employee notifications staged.")
        .count(),
      0,
    );
  }));
