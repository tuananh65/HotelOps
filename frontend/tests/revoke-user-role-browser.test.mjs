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
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{RevokeUserRoleCandidate}from'./src/users/RevokeUserRoleCandidate';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});
window.fetch=async(url,init)=>{calls.push({url,method:init.method});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url.endsWith('/profile'))r=s.profile??{body:${JSON.stringify(account)},headers:{ETag:${JSON.stringify(tag)}}};else if(url.includes('/effective-access'))r={body:${JSON.stringify(access)}};else if(url==='/api/auth/csrf')r={body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else r=s.mutation??{status:204};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));const app=<RevokeUserRoleCandidate userId={42} assignmentId={18} dispatchEnabled={s.enabled===true}/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,revalidate:()=>store.revalidate(),scenario:s,finish:(k,r)=>waiting.get(k)(make(r))};
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
  await p
    .getByRole("button", { name: "Review revocation", exact: true })
    .click();
}
test("default OFF reviews real server identity but cannot confirm or bootstrap", () =>
  run({}, async (p) => {
    await review(p);
    assert.ok(
      await p.getByRole("button", { name: "Confirm revoke" }).isDisabled(),
    );
    assert.deepEqual(
      await p.evaluate(() => window.fixture.calls.map((c) => c.method)),
      ["GET", "GET", "GET"],
    );
  }));
test("mock 204 confirms exact assignment once, no replay control", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm revoke" }).click();
    await p
      .getByText(
        "Role revoked. Target sessions must be invalidated by the server.",
      )
      .waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm revoke" }).count(),
      0,
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "DELETE").length,
      ),
      1,
    );
  }));
for (const status of [401, 403, 412, 503])
  test("safe candidate rejection " + status, () =>
    run(
      { enabled: true, mutation: { status, body: { detail: "SECRET-ERROR" } } },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm revoke" }).click();
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Role to revoke" }).count(),
          0,
        );
        assert.equal(
          await p.getByRole("button", { name: "Confirm revoke" }).count(),
          0,
        );
        assert.equal(
          await p.evaluate(
            () =>
              window.fixture.calls.filter((c) => c.method === "DELETE").length,
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
    "anonymous/restricted candidate does not read or mutate " + auth.status,
    () =>
      run({ auth, enabled: true }, async (p) => {
        await p
          .getByText("Confirm an unrestricted session before reviewing a role.")
          .waitFor();
        assert.equal(await p.evaluate(() => window.fixture.calls.length), 1);
      }),
  );
test("late mutation after session invalidation cannot restore protected data", () =>
  run({ enabled: true, mutation: { deferred: "delete" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm revoke" }).click();
    await p.getByText("Revocation pending...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      void window.fixture.revalidate();
    });
    await p
      .getByText("Confirm an unrestricted session before reviewing a role.")
      .waitFor();
    await p.evaluate(() => window.fixture.finish("delete", { status: 204 }));
    assert.equal(
      await p.getByText("Server employee", { exact: false }).count(),
      0,
    );
    assert.equal(
      await p.getByText("Role revoked.", { exact: false }).count(),
      0,
    );
  }));
test("two synchronous confirms dispatch only one DELETE", () =>
  run({ enabled: true, mutation: { deferred: "delete" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm revoke" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.getByText("Revocation pending...").waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "DELETE").length,
      ),
      1,
    );
    await p.evaluate(() => window.fixture.finish("delete", { status: 204 }));
    await p
      .getByText(
        "Role revoked. Target sessions must be invalidated by the server.",
      )
      .waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "DELETE").length,
      ),
      1,
    );
  }));

test("open responsibilities block confirmation without leaking server details", () =>
  run(
    {
      enabled: true,
      mutation: {
        status: 409,
        body: {
          status: 409,
          code: "OPEN_RESPONSIBILITIES",
          title: "OPEN_RESPONSIBILITIES",
          detail: "PRIVATE-DEPENDENCY-FACTS",
        },
        headers: { "Content-Type": "application/problem+json" },
      },
    },
    async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm revoke" }).click();
      await p
        .getByText(
          "Open responsibilities prevent revocation. Arrange an authorized handover before a new review.",
        )
        .waitFor();
      assert.equal(
        await p.getByRole("button", { name: "Confirm revoke" }).count(),
        0,
      );
      assert.equal(await p.getByText("PRIVATE-DEPENDENCY-FACTS").count(), 0);
      assert.equal(
        await p.evaluate(
          () =>
            window.fixture.calls.filter((c) => c.method === "DELETE").length,
        ),
        1,
      );
    },
  ));
