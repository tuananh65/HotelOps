import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { existsSync } from "node:fs";
const stamp = "2026-11-10T00:00:00Z",
  tag = '"0011223344556677"',
  next = '"0011223344556678"';
const session = {
  user: { id: "7", username: "actor", name: "Actor" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "SYNTHETIC-CSRF",
};
const account = {
  id: 42,
  employeeCode: "E42",
  username: "staff",
  fullName: "Server employee",
  email: "staff@example.test",
  phone: null,
  accountStatus: "ACTIVE",
  primaryBranch: { id: 1, code: "B1", name: "Branch" },
  department: { id: 2, code: "HK", name: "Housekeeping" },
};
const access = {
  account,
  evaluatedAtUtc: stamp,
  reviewerScope: { isGlobal: true, branchIds: [] },
  assignedRoles: [
    {
      assignmentId: 18,
      roleId: 3,
      code: "HK_STAFF",
      name: "Server housekeeping role",
      branchId: 1,
      assignmentActive: true,
      roleActive: true,
      assignedAtUtc: stamp,
      expiresAtUtc: null,
    },
  ],
  branchMemberships: [],
  configuredGrants: [],
  effectivePermissions: [],
};
const result = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{UserDeactivationCandidate}from'./src/users/UserDeactivationCandidate';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});
window.fetch=async(url,init)=>{calls.push({url,method:init.method});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url.endsWith('/profile'))r=s.profile??{body:${JSON.stringify(account)},headers:{ETag:${JSON.stringify(tag)}}};else if(url.includes('/effective-access'))r={body:${JSON.stringify(access)}};else if(url==="/api/users/42")r={body:${JSON.stringify(account)}};else if(url==='/api/auth/csrf')r={body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else r=s.mutation??{status:200,body:{userId:42,accountStatus:"INACTIVE"}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));const app=<UserDeactivationCandidate userId={42}  dispatchEnabled={s.enabled===true}/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,revalidate:()=>store.revalidate(),scenario:s,finish:(k,r)=>waiting.get(k)(make(r))};
`,
  },
  bundle: true,
  write: false,
  jsx: "automatic",
  format: "iife",
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
async function review(p) {
  await p.getByLabel("Reason", { exact: true }).fill("Valid departure reason");
  await p
    .getByRole("button", { name: "Review deactivation", exact: true })
    .click();
}
test("default OFF reason review cannot dispatch", () =>
  run({}, async (p) => {
    await p.getByLabel("Reason").waitFor();
    assert.ok(
      await p.getByRole("button", { name: "Review deactivation" }).isDisabled(),
    );
    await review(p);
    assert.ok(
      await p.getByRole("button", { name: "Confirm deactivate" }).isDisabled(),
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method !== "GET").length,
      ),
      0,
    );
    assert.equal(
      await p.evaluate(() =>
        window.fixture.calls.some((c) => c.url === "/api/auth/csrf"),
      ),
      false,
    );
  }));
test("synthetic success sends one confirmed attempt and removes controls", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivate" }).click();
    await p
      .getByText("Account deactivated. Server confirmation received.")
      .waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm deactivate" }).count(),
      0,
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PATCH").length,
      ),
      1,
    );
  }));
for (const status of [401, 403, 412, 503])
  test("safe operation error " + status, () =>
    run(
      { enabled: true, mutation: { status, body: { detail: "SECRET-ERROR" } } },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm deactivate" }).click();
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Target account" }).count(),
          0,
        );
        assert.equal(
          await p.getByRole("button", { name: "Confirm deactivate" }).count(),
          0,
        );
        assert.equal(
          await p.evaluate(
            () =>
              window.fixture.calls.filter((c) => c.method === "PATCH").length,
          ),
          1,
        );
        if (status === 401)
          await p.getByRole("button", { name: "Check session" }).waitFor();
      },
    ),
  );
for (const auth of [
  { status: 401, body: {} },
  { body: { ...session, mustChangePassword: true } },
])
  test(
    "anonymous/restricted never reads protected account " + auth.status,
    () =>
      run({ auth, enabled: true }, async (p) => {
        await p
          .getByText(
            "Confirm an unrestricted session before reviewing deactivation.",
          )
          .waitFor();
        assert.equal(await p.evaluate(() => window.fixture.calls.length), 1);
      }),
  );
test("late response after session invalidation cannot restore account or reason", () =>
  run({ enabled: true, mutation: { deferred: "patch" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivate" }).click();
    await p.getByText("Deactivation pending...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      void window.fixture.revalidate();
    });
    await p
      .getByText(
        "Confirm an unrestricted session before reviewing deactivation.",
      )
      .waitFor();
    await p.evaluate(() =>
      window.fixture.finish("patch", {
        status: 200,
        body: { userId: 42, accountStatus: "INACTIVE" },
      }),
    );
    assert.equal(
      await p.getByRole("region", { name: "Target account" }).count(),
      0,
    );
    assert.equal(await p.getByLabel("Reason").count(), 0);
    assert.equal(
      await p.getByText("Account deactivated.", { exact: false }).count(),
      0,
    );
  }));
test("two synchronous confirms produce one PATCH and no queued replay", () =>
  run({ enabled: true, mutation: { deferred: "patch" } }, async (p) => {
    await review(p);
    await p
      .getByRole("button", { name: "Confirm deactivate" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.getByText("Deactivation pending...").waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PATCH").length,
      ),
      1,
    );
    await p.evaluate(() =>
      window.fixture.finish("patch", {
        status: 200,
        body: { userId: 42, accountStatus: "INACTIVE" },
      }),
    );
    await p
      .getByText("Account deactivated. Server confirmation received.")
      .waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PATCH").length,
      ),
      1,
    );
  }));
