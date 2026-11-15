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
import React from'react';import{flushSync}from'react-dom';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{UserDeactivationCandidate}from'./src/users/UserDeactivationCandidate';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>{if(r.network)throw Error('SECRET-ERROR');const response=new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});if(r.unreadable)response.json=async()=>{throw Error('SECRET-ERROR')};return response;};
window.fetch=async(url,init)=>{calls.push({url,method:init.method});let r;const target=Number(url.split('/')[3]),record={...${JSON.stringify(account)},id:target};if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url.endsWith('/profile'))r=s.profile??{body:record,headers:{ETag:${JSON.stringify(tag)}}};else if(url.includes('/effective-access'))r={body:${JSON.stringify(access)}};else if(url.startsWith('/api/users/')&&url.split('/').length===4)r={body:record};else if(url==='/api/auth/csrf')r=s.csrf??{body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else r=s.mutation??{status:200,body:{userId:target,accountStatus:"INACTIVE"}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
let store=createAuthStore(createAuthApi(window.fetch)),visible=true,target=42,provider=0;const root=createRoot(document.getElementById('root'));function render(){const app=visible?<UserDeactivationCandidate userId={target} dispatchEnabled={s.enabled===true}/>:null;flushSync(()=>root.render(<AuthProvider key={provider} store={store} fallback={app}>{app}</AuthProvider>));}render();window.fixture={calls,revalidate:()=>store.revalidate(),scenario:s,finish:(k,r)=>waiting.get(k)(make(r)),mount:v=>{visible=v;render()},target:id=>{target=id;render()},replace:auth=>{s.auth={body:auth};store=createAuthStore(createAuthApi(window.fetch));provider++;render()}};
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

test("unknown outcome cannot dispatch a second PATCH after remount", () =>
  run({ enabled: true, mutation: { status: 503, body: {} } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivate" }).click();
    await p.getByText("Outcome unknown.", { exact: false }).waitFor();
    await p.evaluate(() => window.fixture.mount(false));
    await p.evaluate(() => window.fixture.mount(true));
    // Exercise any incorrectly restored editor, then check actual dispatch count.
    await p.waitForFunction(
      () =>
        document.querySelector("textarea") ||
        document.body.textContent.includes("Outcome unknown."),
    );
    if (await p.getByLabel("Reason", { exact: true }).count()) {
      await review(p);
      await p.getByRole("button", { name: "Confirm deactivate" }).click();
      await p.getByText("Outcome unknown.", { exact: false }).waitFor();
    }
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PATCH").length,
      ),
      1,
    );
  }));
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
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "PATCH"),
    );
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

const patchCount = (p) =>
  p.evaluate(
    () => window.fixture.calls.filter((c) => c.method === "PATCH").length,
  );
const unknownText = (p) => p.getByText("Outcome unknown.", { exact: false });
async function confirm(p) {
  await review(p);
  await p.getByRole("button", { name: "Confirm deactivate" }).click();
}
for (const fault of [
  { name: "503", response: { status: 503, body: { detail: "SECRET-ERROR" } } },
  { name: "network", response: { network: true } },
  { name: "unreadable success", response: { status: 200, unreadable: true } },
]) {
  for (const replacement of ["revalidation", "provider", "target"]) {
    test(`unknown ${fault.name} remains locked across ${replacement}`, () =>
      run({ enabled: true, mutation: fault.response }, async (p) => {
        await confirm(p);
        await unknownText(p).waitFor();
        const reads = await p.evaluate(
          () =>
            window.fixture.calls.filter((c) => c.url.endsWith("/profile"))
              .length,
        );
        if (replacement === "revalidation") {
          // A newer profile and ETag still do not reconcile the prior operation.
          await p.evaluate(
            ({ account, next }) => {
              window.fixture.scenario.profile = {
                body: account,
                headers: { ETag: next },
              };
              return window.fixture.revalidate();
            },
            { account, next },
          );
        } else if (replacement === "provider") {
          await p.evaluate((s) => window.fixture.replace(s), session);
        } else {
          await p.evaluate(() => window.fixture.target(43));
          await p.getByLabel("Reason", { exact: true }).waitFor();
          await p.evaluate(() => window.fixture.target(42));
        }
        await unknownText(p).waitFor();
        assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
        assert.equal(await patchCount(p), 1);
        if (replacement !== "target")
          assert.equal(
            await p.evaluate(
              () =>
                window.fixture.calls.filter((c) => c.url.endsWith("/profile"))
                  .length,
            ),
            reads,
          );
      }));
  }
}
for (const replacement of ["remount", "provider", "revalidation"])
  test(`pending PATCH survives ${replacement}; late success cannot unlock`, () =>
    run({ enabled: true, mutation: { deferred: "patch" } }, async (p) => {
      await confirm(p);
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.method === "PATCH"),
      );
      if (replacement === "remount") {
        await p.evaluate(() => window.fixture.mount(false));
        await p.evaluate(() => window.fixture.mount(true));
      } else if (replacement === "provider") {
        await p.evaluate((s) => window.fixture.replace(s), session);
      } else await p.evaluate(() => window.fixture.revalidate());
      await p.getByText("Deactivation pending...").waitFor();
      assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
      await p.evaluate(() =>
        window.fixture.finish("patch", {
          status: 200,
          body: { userId: 42, accountStatus: "INACTIVE" },
        }),
      );
      await unknownText(p).waitFor();
      assert.equal(await patchCount(p), 1);
      assert.equal(
        await p.getByText("Account deactivated.", { exact: false }).count(),
        0,
      );
    }));

test("unknown operation isolates principal and target, restores lock on return", () =>
  run({ enabled: true, mutation: { status: 503, body: {} } }, async (p) => {
    await confirm(p);
    await unknownText(p).waitFor();
    await p.evaluate(() => window.fixture.target(43));
    await confirm(p);
    await unknownText(p).waitFor();
    await p.evaluate(() => window.fixture.target(42));
    await unknownText(p).waitFor();
    await p.evaluate((s) => window.fixture.replace(s), {
      ...session,
      user: { id: "8", username: "other", name: "Other" },
    });
    await confirm(p);
    await unknownText(p).waitFor();
    assert.equal(await patchCount(p), 3);
    await p.evaluate((s) => window.fixture.replace(s), session);
    await unknownText(p).waitFor();
    assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
    assert.equal(await patchCount(p), 3);
  }));

test("old pending response cannot overwrite or recover replacement principal", () =>
  run({ enabled: true, mutation: { deferred: "patch" } }, async (p) => {
    await confirm(p);
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "PATCH"),
    );
    await p.evaluate((s) => window.fixture.replace(s), {
      ...session,
      user: { id: "8", username: "other", name: "Other" },
    });
    await p
      .getByLabel("Reason", { exact: true })
      .fill("Independent new reason");
    const authReads = await p.evaluate(
      () =>
        window.fixture.calls.filter((c) => c.url === "/api/auth/session")
          .length,
    );
    await p.evaluate(() =>
      window.fixture.finish("patch", { status: 401, body: {} }),
    );
    // Returning to the old principal proves completion settled its original reservation.
    await p.evaluate((s) => window.fixture.replace(s), session);
    await unknownText(p).waitFor();
    await p.evaluate((s) => window.fixture.replace(s), {
      ...session,
      user: { id: "8", username: "other", name: "Other" },
    });
    await p.getByLabel("Reason", { exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Check session" }).count(),
      0,
    );
    assert.equal(
      await p.evaluate(
        () =>
          window.fixture.calls.filter((c) => c.url === "/api/auth/session")
            .length,
      ),
      authReads + 2,
    );
    assert.equal(await patchCount(p), 1);
  }));

test("CSRF pending across remount never dispatches stale PATCH and releases only non-dispatch", () =>
  run({ enabled: true, csrf: { deferred: "csrf" } }, async (p) => {
    await confirm(p);
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url === "/api/auth/csrf"),
    );
    await p.evaluate(() => window.fixture.mount(false));
    await p.evaluate(() => window.fixture.mount(true));
    await p.getByText("Deactivation pending...").waitFor();
    await p.evaluate(() =>
      window.fixture.finish("csrf", {
        body: { headerName: "X-CSRF-TOKEN", csrfToken: "SYNTHETIC-CSRF" },
      }),
    );
    await p.getByLabel("Reason", { exact: true }).waitFor();
    assert.equal(await patchCount(p), 0);
    await p.evaluate(() => {
      window.fixture.scenario.csrf = undefined;
    });
    await confirm(p);
    await p
      .getByText("Account deactivated. Server confirmation received.")
      .waitFor();
    assert.equal(await patchCount(p), 1);
  }));

test("definitive 401 keeps explicit session recovery and never replays PATCH", () =>
  run({ enabled: true, mutation: { status: 401, body: {} } }, async (p) => {
    await confirm(p);
    await p.getByRole("button", { name: "Check session" }).click();
    await p.getByLabel("Reason", { exact: true }).waitFor();
    assert.equal(await patchCount(p), 1);
    assert.equal(
      await p.evaluate(
        () =>
          window.fixture.calls.filter((c) => c.url === "/api/auth/session")
            .length,
      ),
      2,
    );
  }));
