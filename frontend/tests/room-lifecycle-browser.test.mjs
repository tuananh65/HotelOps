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
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{RoomLifecycleCandidate}from'./src/hotel/RoomLifecycleCandidate';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});
window.fetch=async(url,init)=>{calls.push({url,method:init.method});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url.includes('/edit?'))r=s.profile??{body:{id:10,branchId:s.branch??1,floorId:2,areaId:null,roomTypeId:3,roomNumber:"101",notes:null,operationalStatus:s.inactive?"OUT_OF_SERVICE":"DIRTY",isActive:!s.inactive},headers:{ETag:${JSON.stringify(tag)}}};else if(url.includes('/effective-access'))r={body:${JSON.stringify(access)}};else if(url==="/api/users/42")r={body:${JSON.stringify(account)}};else if(url==='/api/auth/csrf')r={body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else r=s.mutation??{status:200,body:{id:10,branchId:s.branch??1,floorId:2,areaId:null,roomTypeId:3,roomNumber:"101",notes:null,operationalStatus:s.inactive?"DIRTY":"OUT_OF_SERVICE",isActive:!!s.inactive}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));let changeBranch;function App(){const[branch,setBranch]=React.useState(s.branch??1);changeBranch=setBranch;return <RoomLifecycleCandidate branchId={branch} roomId={10} dispatchEnabled={s.enabled===true}/>;}const app=<App/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,changeBranch:v=>changeBranch(v),revalidate:()=>store.revalidate(),scenario:s,finish:(k,r)=>waiting.get(k)(make(r))};
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
    .getByLabel("Reason", { exact: true })
    .fill("Valid room lifecycle reason");
  await p
    .getByRole("button", { name: "Review room change", exact: true })
    .click();
}
test("default OFF reads room but never bootstraps or posts", () =>
  run({}, async (p) => {
    await review(p);
    assert.ok(
      await p.getByRole("button", { name: "Confirm room change" }).isDisabled(),
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
for (const inactive of [false, true])
  test("synthetic lifecycle confirmation " + inactive, () =>
    run({ enabled: true, inactive }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm room change" }).click();
      await p.getByText("Server confirmed room change.").waitFor();
      const calls = await p.evaluate(() =>
        window.fixture.calls.filter((c) => c.method === "POST"),
      );
      assert.equal(calls.length, 1);
      assert.ok(
        calls[0].url.includes(inactive ? "/reactivate?" : "/deactivate?"),
      );
      assert.equal(
        await p.getByRole("button", { name: "Confirm room change" }).count(),
        0,
      );
    }),
  );
for (const status of [401, 403, 412, 503])
  test("safe room error " + status, () =>
    run(
      { enabled: true, mutation: { status, body: { detail: "SECRET-ERROR" } } },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm room change" }).click();
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Selected room" }).count(),
          0,
        );
        assert.equal(
          await p.getByRole("button", { name: "Confirm room change" }).count(),
          0,
        );
        assert.equal(
          await p.evaluate(
            () =>
              window.fixture.calls.filter((c) => c.method === "POST").length,
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
  test("anonymous/restricted does not read room " + auth.status, () =>
    run({ auth, enabled: true }, async (p) => {
      await p
        .getByText("Confirm an unrestricted session before reviewing the room.")
        .waitFor();
      assert.equal(await p.evaluate(() => window.fixture.calls.length), 1);
    }),
  );
test("late room response after session invalidation cannot restore data", () =>
  run({ profile: { deferred: "read" } }, async (p) => {
    await p.getByText("Loading room...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      void window.fixture.revalidate();
    });
    await p
      .getByText("Confirm an unrestricted session before reviewing the room.")
      .waitFor();
    await p.evaluate(() => window.fixture.finish("read", { body: {} }));
    assert.equal(
      await p.getByRole("region", { name: "Selected room" }).count(),
      0,
    );
  }));
test("double confirmation posts only once", () =>
  run({ enabled: true, mutation: { deferred: "post" } }, async (p) => {
    await review(p);
    await p
      .getByRole("button", { name: "Confirm room change" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.getByText("Room change pending...").waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "POST").length,
      ),
      1,
    );
  }));
test("branch replacement discards old read and shows only new branch", () =>
  run({ profile: { deferred: "old" } }, async (p) => {
    await p.getByText("Loading room...").waitFor();
    await p.evaluate(() => {
      delete window.fixture.scenario.profile;
      window.fixture.scenario.branch = 2;
      window.fixture.changeBranch(2);
    });
    await p.getByText(/Branch 2;/).waitFor();
    await p.evaluate(() =>
      window.fixture.finish("old", {
        body: {
          id: 10,
          branchId: 1,
          floorId: 2,
          areaId: null,
          roomTypeId: 3,
          roomNumber: "OLD-PRIVATE",
          notes: null,
          operationalStatus: "DIRTY",
          isActive: true,
        },
        headers: { ETag: '"' + "A".repeat(64) + '"' },
      }),
    );
    assert.equal(await p.getByText("Room OLD-PRIVATE").count(), 0);
    await p.getByText(/Branch 2;/).waitFor();
  }));
