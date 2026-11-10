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
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{DepartmentDeactivationCandidate}from'./src/hotel/DepartmentDeactivationCandidate';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>new Response(JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});
window.fetch=async(url,init)=>{calls.push({url,method:init.method,body:init.body});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url.includes('/departments/')&&init.method==='GET')r={body:{id:2,branchId:s.branch??1,code:'HK',name:'Scoped department',description:null,managerUserId:null,isActive:!s.inactive},headers:{ETag:${JSON.stringify(tag)}}};else if(url.startsWith('/api/users?'))r=s.users??{body:{items:[],page:1,pageSize:20,totalCount:0}};else if(url==='/api/auth/csrf')r={body:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC-CSRF'}};else r=s.mutation??{body:{id:2,branchId:s.branch??1,code:'HK',name:'Scoped department',description:null,managerUserId:null,isActive:false}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));let changeBranch;function App(){const[branch,setBranch]=React.useState(s.branch??1);changeBranch=setBranch;return <DepartmentDeactivationCandidate branchId={branch} departmentId={2} dispatchEnabled={s.enabled===true}/>;}const app=<App/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,changeBranch:v=>changeBranch(v),revalidate:()=>store.revalidate(),scenario:s,finish:(k,r)=>waiting.get(k)(make(r))};
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
const item = {
  id: 42,
  employeeCode: "E42",
  username: "staff",
  fullName: "Visible employee",
  email: "staff@example.test",
  primaryBranch: { id: 1, code: "B1", name: "Branch" },
  department: { id: 2, code: "HK", name: "Housekeeping" },
  roles: [],
  accountStatus: "ACTIVE",
};
const list = (items) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
});
async function review(p) {
  await p
    .getByLabel("Reason", { exact: true })
    .fill("Valid department closure reason");
  await p
    .getByRole("button", { name: "Review deactivation", exact: true })
    .click();
}
test("default OFF and scoped empty is not clearance", () =>
  run({}, async (p) => {
    await review(p);
    assert.ok(
      await p
        .getByRole("button", { name: "Confirm department deactivation" })
        .isDisabled(),
    );
    assert.match(
      await p.locator("body").innerText(),
      /not complete dependency clearance/,
    );
    assert.equal(
      await p.evaluate(
        () =>
          window.fixture.calls.filter(
            (c) => c.method === "POST" || c.url === "/api/auth/csrf",
          ).length,
      ),
      0,
    );
  }));
test("observed ACTIVE account blocks confirmation and scopes query", () =>
  run({ enabled: true, users: { body: list([item]) } }, async (p) => {
    await p.getByText("Visible employee", { exact: true }).waitFor();
    await p
      .getByLabel("Reason", { exact: true })
      .fill("Valid department closure reason");
    assert.ok(
      await p.getByRole("button", { name: "Review deactivation" }).isDisabled(),
    );
    const url = await p.evaluate(
      () =>
        window.fixture.calls.find((c) => c.url.startsWith("/api/users?")).url,
    );
    const q = new URL(url, "https://offline.test").searchParams;
    for (const [k, v] of Object.entries({
      branchId: "1",
      departmentId: "2",
      accountStatus: "ACTIVE",
      page: "1",
      pageSize: "20",
    }))
      assert.equal(q.get(k), v);
  }));
test("Users 403 preserves Department without implying zero", () =>
  run({ users: { status: 403, body: {} } }, async (p) => {
    await p.getByText(/Account review denied/).waitFor();
    assert.equal(
      await p.getByRole("heading", { name: "Scoped department" }).count(),
      1,
    );
    assert.equal(await p.getByText(/Server-visible total/).count(), 0);
  }));
test("Users 401 clears snapshot with explicit recovery", () =>
  run({ users: { status: 401, body: {} } }, async (p) => {
    await p.getByRole("button", { name: "Check session" }).waitFor();
    assert.equal(
      await p.getByRole("heading", { name: "Scoped department" }).count(),
      0,
    );
    assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
  }));
for (const bad of [
  { ...item, department: { ...item.department, id: 9 } },
  { ...item, accountStatus: "INACTIVE" },
])
  test(
    "reject unrelated or inactive account " +
      bad.accountStatus +
      bad.department.id,
    () =>
      run({ users: { body: list([bad]) } }, async (p) => {
        await p.getByText(/Account coverage unavailable/).waitFor();
        assert.equal(
          await p.getByText("Visible employee", { exact: true }).count(),
          0,
        );
      }),
  );
test("inactive Department has no controls or account request", () =>
  run({ inactive: true }, async (p) => {
    await p.getByText(/already inactive/).waitFor();
    assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
    assert.equal(
      await p.evaluate(
        () =>
          window.fixture.calls.filter((c) => c.url.startsWith("/api/users?"))
            .length,
      ),
      0,
    );
  }));
for (const auth of [
  { status: 401, body: {} },
  { body: { ...session, mustChangePassword: true } },
])
  test("unconfirmed session no protected reads " + auth.status, () =>
    run({ auth }, async (p) => {
      await p.getByText(/Confirm an unrestricted session/).waitFor();
      assert.equal(
        await p.evaluate(
          () =>
            window.fixture.calls.filter((c) => c.url !== "/api/auth/session")
              .length,
        ),
        0,
      );
    }),
  );
test("synthetic confirmed response has one POST", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p
      .getByRole("button", { name: "Confirm department deactivation" })
      .click();
    await p.getByText("Server confirmed department deactivation.").waitFor();
    const posts = await p.evaluate(() =>
      window.fixture.calls.filter((c) => c.method === "POST"),
    );
    assert.equal(posts.length, 1);
    assert.equal(posts[0].url, "/api/branches/1/departments/2/deactivate");
    assert.deepEqual(JSON.parse(posts[0].body), {
      reason: "Valid department closure reason",
      confirmation: true,
    });
    assert.equal(await p.getByLabel("Reason", { exact: true }).count(), 0);
  }));
for (const status of [401, 403, 412, 503])
  test("mutation " + status + " clears snapshot without replay", () =>
    run(
      { enabled: true, mutation: { status, body: { detail: "SECRET-ERROR" } } },
      async (p) => {
        await review(p);
        await p
          .getByRole("button", { name: "Confirm department deactivation" })
          .click();
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByRole("heading", { name: "Scoped department" }).count(),
          0,
        );
        assert.equal(
          await p.evaluate(
            () =>
              window.fixture.calls.filter((c) => c.method === "POST").length,
          ),
          1,
        );
      },
    ),
  );
test("late branch response cannot restore accounts", () =>
  run({ users: { deferred: "old" } }, async (p) => {
    await p.getByText("Loading scoped accounts...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.branch = 3;
      window.fixture.scenario.users = {
        body: { items: [], page: 1, pageSize: 20, totalCount: 0 },
      };
      window.fixture.changeBranch(3);
    });
    await p.getByText(/Branch 3;/).waitFor();
    await p.evaluate(
      (body) => window.fixture.finish("old", { body }),
      list([item]),
    );
    await p.getByText(/No visible active accounts/).waitFor();
    assert.equal(
      await p.getByText("Visible employee", { exact: true }).count(),
      0,
    );
  }));
test("late session response cannot restore accounts", () =>
  run({ users: { deferred: "old" } }, async (p) => {
    await p.getByText("Loading scoped accounts...").waitFor();
    await p.evaluate(async () => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      await window.fixture.revalidate();
    });
    await p.getByText(/Confirm an unrestricted session/).waitFor();
    await p.evaluate(
      (body) => window.fixture.finish("old", { body }),
      list([item]),
    );
    assert.equal(
      await p.getByRole("heading", { name: "Scoped department" }).count(),
      0,
    );
  }));
