import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { session } from "./workday-fixtures.mjs";
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `import React,{useState}from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{InitialRolePermissionsPanel}from'./src/roles/InitialRolePermissionsPanel';const s=window.scenario,calls=[],grants=new Map();let finish;const permission={permissionId:10,permissionCode:'ROOM_VIEW',permissionName:'View rooms',moduleCode:'ROOM',resourceCode:'room',actionCode:'view',description:null};window.fetch=async(u,o)=>{calls.push({url:u,method:o.method});if(u==='/api/auth/session')return Response.json(s.anonymous?{}:{...${JSON.stringify(session())},mustChangePassword:!!s.restricted},{status:s.anonymous?401:200});if(u==='/api/auth/csrf')return Response.json(s.csrfStatus?{detail:'SECRET'}:{headerName:'X-CSRF-TOKEN',csrfToken:'SYNTHETIC'},{status:s.csrfStatus??200});if(u.startsWith('/api/permissions/catalog'))return Response.json({items:[permission,{...permission,permissionId:11,permissionCode:'USER_MANAGE',permissionName:'Manage users',moduleCode:'ADMIN'}],page:1,pageSize:25,total:2});const roleId=Number(u.split('/')[3]);if(o.method==='GET'&&s.readStatus)return Response.json({detail:'SECRET'},{status:s.readStatus});if(o.method==='POST'){const run=()=>{if(s.network)throw Error('SECRET');if(s.status)return Response.json({detail:'SECRET',traceId:'SECRET'},{status:s.status});const g=JSON.parse(o.body).grants;grants.set(roleId,g);return Response.json(s.malformed?{}:{contractVersion:1,roleId,grants:g},{status:201})};if(s.defer)return new Promise(r=>finish=()=>r(run()));return run();}const g=grants.get(roleId)??[];return Response.json({role:{roleId,roleCode:'CUSTOM_TEST',roleName:'Custom '+roleId,description:null,isSystemRole:!!s.system,isActive:true,assignedUserCount:0,permissionGrantCount:g.length},grants:g.map((v,i)=>({rolePermissionId:60+i,permission,dataScope:v.dataScope}))});};const store=createAuthStore(createAuthApi(window.fetch));function App(){const[id,setId]=useState(7);return <><button onClick={()=>setId(8)}>Change role</button><InitialRolePermissionsPanel roleId={id} dispatchEnabled={s.enabled??false}/></>};const app=<App/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={<p>Checking session</p>}>{app}</AuthProvider>);window.fixture={calls,finish:()=>finish(),replace:()=>store.revalidate()};`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
});
after(async () => browser?.close());
async function run(s, fn) {
  const p = await browser.newPage();
  p.setDefaultTimeout(6000);
  const errors = [];
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
            throw Error("Storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
async function review(p) {
  await p.getByLabel("ROOM_VIEW", { exact: true }).check();
  await p
    .getByRole("button", { name: "Review permissions", exact: true })
    .click();
  await p
    .getByRole("region", { name: "Review permissions", exact: true })
    .waitFor();
}
const posts = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "POST"));
test("disabled capability loads real catalog but cannot dispatch", () =>
  run({}, async (p) => {
    await p
      .getByText("Mutation dispatch OFF pending runtime approval.")
      .waitFor();
    assert.equal(
      await p.getByLabel("ROOM_VIEW", { exact: true }).isDisabled(),
      true,
    );
    assert.equal((await posts(p)).length, 0);
  }));
for (const s of [{ anonymous: true }, { restricted: true }])
  test(
    "unconfirmed session never reads grants or sends mutation " +
      JSON.stringify(s),
    () =>
      run(s, async (p) => {
        await p.getByText(/Checking session|Confirmed unrestricted/).waitFor();
        assert.equal((await posts(p)).length, 0);
        assert.equal(
          (await p.evaluate(() => window.fixture.calls)).some((c) =>
            c.url.startsWith("/api/roles/"),
          ),
          false,
        );
      }),
  );
test("review confirm success refreshes authoritative grants, unsupported admin selection disabled", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    assert.equal((await posts(p)).length, 0);
    assert.equal(
      await p.getByLabel("USER_MANAGE", { exact: true }).isDisabled(),
      true,
    );
    await p.getByRole("button", { name: "Confirm and save" }).click();
    await p.getByText("Existing grants: 1", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
    await p.getByText("ROOM_VIEW: BRANCH", { exact: true }).waitFor();
  }));
test("synchronous double confirmation sends one POST", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.getByText("Existing grants: 1", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
  }));
for (const status of [403, 409])
  test("business rejection " + status + " is sanitized and no retry", () =>
    run({ enabled: true, status }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.getByRole("status").waitFor();
      assert.equal((await posts(p)).length, 1);
      assert.equal(
        await p
          .getByRole("region", { name: "Review permissions", exact: true })
          .count(),
        0,
      );
    }),
  );
for (const s of [{ network: true }, { status: 503 }, { malformed: true }])
  test("unknown outcome locks replay " + JSON.stringify(s), () =>
    run({ enabled: true, ...s }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.getByText(/Outcome unknown/).waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Review permissions", exact: true })
          .isDisabled(),
        true,
      );
      assert.equal((await posts(p)).length, 1);
    }),
  );
test("401 clears role snapshot and changing target cannot bypass recovery", () =>
  run({ enabled: true, status: 401 }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm and save" }).click();
    await p.getByText("Confirmed unrestricted session required.").waitFor();
    await p.getByRole("button", { name: "Change role" }).click();
    assert.equal(await p.getByText("Custom 8", { exact: true }).count(), 0);
    assert.equal((await posts(p)).length, 1);
  }));
for (const operation of ["Change role", "replace"])
  test("late success discarded after " + operation, () =>
    run({ enabled: true, defer: true }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.method === "POST"),
      );
      if (operation === "replace")
        await p.evaluate(() => window.fixture.replace());
      else await p.getByRole("button", { name: "Change role" }).click();
      await p
        .getByText("Custom " + (operation === "replace" ? 7 : 8), {
          exact: true,
        })
        .waitFor();
      await p.evaluate(() => window.fixture.finish());
      assert.equal(
        await p.getByText(/Initial permissions confirmed/).count(),
        0,
      );
      assert.equal((await posts(p)).length, 1);
    }),
  );

for (const csrfStatus of [401, 403])
  test("CSRF bootstrap " + csrfStatus + " never sends POST", () =>
    run({ enabled: true, csrfStatus }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm and save" }).click();
      if (csrfStatus === 401)
        await p.getByText("Confirmed unrestricted session required.").waitFor();
      else await p.getByText("Access denied by server.").waitFor();
      assert.equal((await posts(p)).length, 0);
    }),
  );

for (const readStatus of [403, 503])
  test(
    "read denial/unavailable " + readStatus + " is not empty editable role",
    () =>
      run({ enabled: true, readStatus }, async (p) => {
        await p.getByText("No editable role snapshot available.").waitFor();
        assert.equal(await p.getByRole("checkbox").count(), 0);
        assert.equal(
          await p.getByText("Loading role and catalog...").count(),
          0,
        );
        assert.equal((await posts(p)).length, 0);
      }),
  );
