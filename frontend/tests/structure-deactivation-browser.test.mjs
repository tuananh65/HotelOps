import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const compiled = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React,{useState}from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{StructureDeactivationPanel}from'./src/structure-deactivation/StructureDeactivationPanel';
const s=window.scenario,calls=[],waiters=[];const reply=(body,status=200)=>Response.json(body,{status,headers:{ETag:'"'+'A'.repeat(64)+'"'}});window.fetch=async(u,o)=>{calls.push({url:u,method:o.method,body:o.body,headers:Object.fromEntries(new Headers(o.headers))});if(u==='/api/auth/session'&&s.anonymous)return reply({status:401},401);if(u==='/api/auth/session')return reply({user:{id:'7',username:'tester',name:'Tester'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:s.restricted??false,csrfToken:'token'});if(u==='/api/auth/csrf')return reply({headerName:'X-CSRF-TOKEN',csrfToken:'token'});const b=Number(u.split('/')[3]);const body={kind:'floor',id:11,branchId:b,code:'F'+b,name:'Protected '+b,description:null,floorId:null,floorNumber:2,isActive:true};if(o.method==='GET'){if(s.late&&b===42)return new Promise(resolve=>waiters.push(()=>resolve(reply(body))));return reply(body);}if(s.network)throw Error('SECRET');if(s.waitPost)return new Promise(resolve=>waiters.push(()=>resolve(reply({status:503,title:'STRUCTURE_WRITER_COORDINATION_REQUIRED'},503))));return reply({status:s.status??409,title:s.status===412?'STALE_STRUCTURE_VERSION':s.status===503?'STRUCTURE_WRITER_COORDINATION_REQUIRED':'STRUCTURE_DEPENDENCIES_EXIST',detail:'SECRET',dependencies:[{kind:'ACTIVE_ROOM',count:2,ids:[3,4]}]},s.status??409);};const store=createAuthStore(createAuthApi(window.fetch));function App(){const[b,set]=useState(42);return <><button onClick={()=>set(43)}>Change branch</button><StructureDeactivationPanel kind="floor" branchId={b} id={11} dispatchEnabled={s.enabled??false}/></>;}createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={<p>Session unavailable</p>}><App/></AuthProvider>);window.fixture={calls,finish:()=>waiters.splice(0).forEach(f=>f()),replace:()=>store.revalidate()};`,
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
  const p = await browser.newPage();
  p.setDefaultTimeout(5000);
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
    await p.addScriptTag({ content: compiled.outputFiles[0].text });
    await fn(p);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
async function review(p) {
  await p.getByText("Protected 42", { exact: true }).waitFor();
  await p.getByLabel("Reason", { exact: true }).fill("Retire unused floor");
  await p.getByRole("button", { name: "Review", exact: true }).click();
}
test("default OFF permits review but no dispatch", () =>
  run({}, async (p) => {
    await review(p);
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm deactivation" })
        .isDisabled(),
      true,
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST" || c.url.endsWith("/csrf"),
      ).length,
      0,
    );
  }));
for (const status of [409, 412, 503])
  test("safe distinct error " + status, () =>
    run({ enabled: true, status }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm deactivation" }).click();
      await p.getByRole("alert").waitFor();
      const text = await p.getByRole("alert").innerText();
      assert.match(
        text,
        status === 409
          ? /Dependencies/
          : status === 412
            ? /Version changed/
            : /Writer coordination/,
      );
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter(
          (c) => c.method === "POST",
        ).length,
        1,
      );
    }),
  );
test("reason validation precedes review", () =>
  run({}, async (p) => {
    await p.getByText("Protected 42", { exact: true }).waitFor();
    await p.getByLabel("Reason", { exact: true }).fill("short");
    await p.getByRole("button", { name: "Review", exact: true }).click();
    await p.getByText(/Enter a reason/).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm deactivation" }).count(),
      0,
    );
  }));
test("double confirm exactly one POST and unknown locks replay", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await review(p);
    await p
      .getByRole("button", { name: "Confirm deactivation" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
    await p.evaluate(() => window.fixture.finish());
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Refresh record" }).isDisabled(),
      true,
    );
  }));
test("branch change ignores late previous record", () =>
  run({ late: true }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.includes("/42/floors")),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Protected 43", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText("Protected 42", { exact: true }).count(), 0);
  }));
test("restricted session sends no protected GET", () =>
  run({ restricted: true }, async (p) => {
    await p
      .getByText("A confirmed unrestricted session is required.")
      .waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter((c) =>
        c.url.includes("/floors/"),
      ).length,
      0,
    );
  }));
test("session replacement clears pending review", () =>
  run({}, async (p) => {
    await review(p);
    await p.evaluate(() => window.fixture.replace());
    await p.getByText("Protected 42", { exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm deactivation" }).count(),
      0,
    );
  }));
test("anonymous session sends no record request", () =>
  run({ anonymous: true }, async (p) => {
    await p
      .getByText("A confirmed unrestricted session is required.")
      .waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter((c) =>
        c.url.includes("/floors/"),
      ).length,
      0,
    );
  }));
test("branch change during POST hides old result", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivation" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Protected 43", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(await p.getByText("Protected 42", { exact: true }).count(), 0);
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
  }));
