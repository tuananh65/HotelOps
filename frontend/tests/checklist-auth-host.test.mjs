import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { createServer } from "node:http";

const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React from 'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';
import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{SharedAuthHost}from'./src/shared-host/SharedAuthHost';
const session=()=>({user:{id:'7',name:'Trial',username:'trial'},expiresAt:'2030-01-01T00:00:00Z',absoluteExpiresAt:'2030-01-02T00:00:00Z',mustChangePassword:window.initialStatus==='RESTRICTED'});
window.calls=[];window.pending=[];window.reads=0;window.branchReads={};
const store=createAuthStore({getSession:async()=>{window.reads++;if(window.deferSession)await new Promise(r=>window.finishSession=r);return ['ANONYMOUS','EXPIRED_INVALID'].includes(window.initialStatus)?{ok:false,error:{kind:'http',status:401}}:{ok:true,data:session()};},logout:async()=>({ok:true,status:204,data:undefined})});
window.fetch=async(url,init={})=>{window.calls.push({url:String(url),method:init.method||'GET'});const match=String(url).match(/branches\\/(\\d+)\\/inspection-checklists/);if(!match)return new Response('{}',{status:503});const branch=Number(match[1]);
window.branchReads[branch]=(window.branchReads[branch]||0)+1;
if(window.deferBranch===branch||(window.deferCollection===branch&&window.branchReads[branch]>1))await new Promise(resolve=>{const p={branch,resolve,aborted:!!init.signal?.aborted};init.signal?.addEventListener('abort',()=>p.aborted=true);window.pending.push(p)});
const status=window.apiStatus||200;return new Response(JSON.stringify(status===200?{branchId:branch,page:1,pageSize:20,total:1,search:null,isActive:null,status:null,roomTypeId:null,items:[{id:branch,branchId:branch,code:'C'+branch,name:'BRANCH-'+branch,active:true,roomTypeIds:[],versions:[]}]}:{}),{status,headers:{'Content-Type':'application/json'}})};
window.fixture={store,navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))},release(){window.pending.splice(0).forEach(p=>p.resolve())}};
const host=<SharedAuthHost checklistEnabled={window.enabled}/>;
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  loader: { ".css": "empty" },
  define: { "process.env.NODE_ENV": '"production"', "import.meta.env": "{}" },
});
let browser, server, origin;
before(async () => {
  server = createServer((req, res) => {
    res.setHeader("Content-Type", "text/html");
    res.end(
      '<div id="root"></div><script>' +
        bundle.outputFiles[0].text +
        "</script>",
    );
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  origin = "http://127.0.0.1:" + server.address().port;
  browser = await chromium.launch({
    headless: true,
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
});
after(async () => {
  await browser?.close();
  await new Promise((resolve) => server?.close(resolve));
});
async function open(options = {}, path = "/inspections/checklists?branchId=7") {
  const page = await browser.newPage();
  await page.addInitScript(
    (value) =>
      Object.assign(
        window,
        { enabled: true, initialStatus: "AUTHENTICATED" },
        value,
      ),
    options,
  );
  await page.goto(origin + path);
  await page.waitForFunction(() => !!window.fixture);
  return page;
}
test("default feature OFF sends no protected read", async () => {
  const p = await open({ enabled: false });
  await p.getByText("Checklist destination is not activated.").waitFor();
  assert.deepEqual(await p.evaluate(() => window.calls), []);
  await p.close();
});
for (const state of ["ANONYMOUS", "EXPIRED_INVALID", "RESTRICTED"])
  test("guard " + state, async () => {
    const p = await open({ initialStatus: state });
    await p.waitForURL(
      state === "RESTRICTED" ? "**/change-password" : "**/login",
    );
    assert.deepEqual(await p.evaluate(() => window.calls), []);
    await p.close();
  });
for (const query of [
  "",
  "?branchId=0",
  "?branchId=01",
  "?branchId=7&branchId=8",
  "?branchId=7&token=x",
  "?branchId=9007199254740992",
])
  test("invalid context " + query, async () => {
    const p = await open({}, "/inspections/checklists" + query);
    await p.getByText("Invalid checklist branch context.").waitFor();
    assert.deepEqual(await p.evaluate(() => window.calls), []);
    await p.close();
  });
test("direct open and simulated reload require fresh session and server collection admission", async () => {
  const p = await open();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  assert.equal(
    await p
      .getByRole("button", { name: "Tạo checklist", exact: true })
      .isEnabled(),
    true,
  );
  await p.reload();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  assert.equal(await p.evaluate(() => window.reads), 1);
  assert.ok(
    (await p.evaluate(() => window.calls)).every((c) => c.method === "GET"),
  );
  await p.close();
});
for (const status of [401, 403, 503])
  test(
    "admission " + status + " hides controls without automatic recovery loop",
    async () => {
      const p = await open({ apiStatus: status });
      await p.getByText("Checklist access could not be confirmed.").waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Tạo checklist", exact: true })
          .count(),
        0,
      );
      assert.equal(await p.evaluate(() => window.reads), 1);
      assert.equal(await p.evaluate(() => window.calls.length), 1);
      await p.close();
    },
  );
test("branch switch aborts pending admission; late 200 cannot reveal old branch", async () => {
  const p = await open({ deferBranch: 7 });
  await p.waitForFunction(() => window.pending.length === 1);
  await p.evaluate(() =>
    window.fixture.navigate("/inspections/checklists?branchId=8"),
  );
  await p.getByText("BRANCH-8", { exact: true }).waitFor();
  assert.equal(await p.evaluate(() => window.pending[0].aborted), true);
  await p.evaluate(() => window.fixture.release());
  await p.waitForTimeout(50);
  assert.equal(await p.getByText("BRANCH-7", { exact: true }).count(), 0);
  await p.close();
});
test("branch replacement removes visible snapshot and form; Browser Back rechecks context", async () => {
  const p = await open();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  await p.getByRole("button", { name: "Tạo checklist", exact: true }).click();
  await p.evaluate(() => {
    window.deferBranch = 8;
    window.fixture.navigate("/inspections/checklists?branchId=8");
  });
  await p.getByText("Checking checklist access...").waitFor();
  assert.equal(await p.getByText("BRANCH-7", { exact: true }).count(), 0);
  assert.equal(
    await p.getByRole("button", { name: "Tạo checklist", exact: true }).count(),
    0,
  );
  await p.goBack();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  await p.close();
});
test("logout invalidates pending 200 and never restores controls", async () => {
  const p = await open({ deferBranch: 7 });
  await p.waitForFunction(() => window.pending.length === 1);
  await p.evaluate(() => window.fixture.store.logout());
  await p.waitForURL("**/login");
  assert.equal(await p.evaluate(() => window.pending[0].aborted), true);
  await p.evaluate(() => window.fixture.release());
  await p.waitForTimeout(50);
  assert.equal(
    await p.getByRole("button", { name: "Tạo checklist", exact: true }).count(),
    0,
  );
  await p.close();
});
test("same-user revalidation hides snapshot immediately and resets branch admission", async () => {
  const p = await open();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  await p.evaluate(() => {
    window.deferSession = true;
    void window.fixture.store.revalidate();
  });
  await p.getByText("Checking your session...").waitFor();
  assert.equal(await p.getByText("BRANCH-7", { exact: true }).count(), 0);
  await p.evaluate(() => {
    window.deferSession = false;
    window.apiStatus = 403;
    window.finishSession();
  });
  await p.getByText("Checklist access could not be confirmed.").waitFor();
  assert.equal(
    await p.getByRole("button", { name: "Tạo checklist", exact: true }).count(),
    0,
  );
  await p.close();
});
test("checklist is not added to Return URL allowlist", async () => {
  const p = await open({ initialStatus: "ANONYMOUS" });
  await p.waitForURL("**/login");
  assert.equal(new URL(p.url()).search, "");
  await p.close();
});

test("late collection data after successful admission cannot cross a branch switch", async () => {
  const p = await open({ deferCollection: 7 });
  await p.waitForFunction(() => window.pending.length === 1);
  await p.getByRole("button", { name: "Tạo checklist", exact: true }).waitFor();
  await p.evaluate(() =>
    window.fixture.navigate("/inspections/checklists?branchId=8"),
  );
  await p.getByText("BRANCH-8", { exact: true }).waitFor();
  assert.equal(await p.evaluate(() => window.pending[0].aborted), true);
  await p.evaluate(() => window.fixture.release());
  await p.waitForTimeout(50);
  assert.equal(await p.getByText("BRANCH-7", { exact: true }).count(), 0);
  await p.close();
});

test("expired session after visible checklist clears data and blocks old callbacks", async () => {
  const p = await open();
  await p.getByText("BRANCH-7", { exact: true }).waitFor();
  await p.evaluate(() => {
    window.initialStatus = "EXPIRED_INVALID";
    void window.fixture.store.revalidate();
  });
  await p.waitForURL("**/login");
  assert.equal(await p.getByText("BRANCH-7", { exact: true }).count(), 0);
  assert.equal(
    await p.getByRole("button", { name: "Tạo checklist", exact: true }).count(),
    0,
  );
  await p.close();
});
