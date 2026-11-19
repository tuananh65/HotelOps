import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';import {createAuthStore} from './src/auth/authStore';import {createAuthApi} from './src/auth/authApi';
import {RoomStatusDetails} from './src/screens/rooms/RoomStatusDetails';import {InternalRequestAccessLink,InternalRequestHistoryPage} from './src/internalRequests/HistoryPage';import {InternalRequestIntakeLink} from './src/internalRequests/IntakePage';
import {HomeNavigation} from './src/shared-host/HomeNavigation';import {SearchableSelect} from './src/components/SearchableSelect';
import {detail as fixture} from './tests/room-status-fixtures.mjs';
const s=window.scenario;window.calls=[];window.nav=s.paths;
window.fetch=async(url)=>{window.calls.push(url);
if(url==='/api/auth/session')return Response.json({user:{id:'24',username:'huy.dinh',name:'Huy'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'test'});
if(url==='/api/auth/navigation')return window.nav===null?Response.json({},{status:503}):Response.json({paths:window.nav,selectSearchEnabled:s.selectSearchEnabled});
if(url==='/api/auth/navigation/operation-grants')return Response.json({grants:['CLEANING_VIEW','INCIDENT_VIEW','INCIDENT_CREATE'].map(permission=>({branchId:7,permission,scope:'BRANCH'}))});
if(url.startsWith('/api/internal-requests/1/timeline'))return Response.json({requestId:1,events:[],eventTotalCount:0,assignments:[],assignmentTotalCount:0,feedback:null,...(s.capabilities??{})});
if(s.linkPath && url.startsWith('/api/internal-requests/1/linked-cleaning'))return Response.json({requestId:1,branchId:7,version:'AAAAAAAAAAA=',canLink:false,total:1,page:1,candidates:false,items:[{taskId:12,code:'CLN12',title:'Cleaning source',state:'IN_PROGRESS',mandatory:true,path:s.linkPath}]});
return Response.json({},{status:403});};
const detail={...fixture,currentCycle:{cleaningTaskId:12,cleaningState:'IN_PROGRESS',inspectionId:8,inspectionState:'PASSED',reworkId:9},effectiveBlocks:[{id:2,incidentId:3,blockedAtUtc:'2026-11-10T00:00:00Z'}]};
const store=createAuthStore(createAuthApi(window.fetch));
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store}>{s.selectOnly?<HomeNavigation>{()=> <SearchableSelect aria-label="T?ng"><option value="1">T?ng m?t</option><option value="2">T?ng hai</option></SearchableSelect>}</HomeNavigation>:s.request?<InternalRequestHistoryPage/>:<><InternalRequestAccessLink/><InternalRequestIntakeLink/><RoomStatusDetails detail={detail} branchName="Bùi Viện" canMonitor={s.monitor} close={()=>{}} paginate={()=>{}} sourceLink={()=>null}/></>}</AuthProvider></BrowserRouter>);
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  loader: { ".css": "empty" },
  define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  browser = await chromium.launch({
    headless: true,
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
});
after(async () => browser?.close());
async function run(scenario, check) {
  const page = await browser.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  try {
    await page.route("**/*", (r) =>
      r.request().resourceType() === "document"
        ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
        : r.abort(),
    );
    await page.goto(
      scenario.request
        ? "https://test.local/internal-requests?requestId=1"
        : "https://test.local/rooms/status?branchId=7",
    );
    await page.evaluate((s) => (window.scenario = s), scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    if (scenario.selectOnly) {
      await page.getByRole("combobox",{name:"T?ng"}).waitFor();
    } else if (scenario.request) {
      await page.getByRole("heading", { name: "Lịch sử xử lý" }).waitFor();
    } else {
      await page
        .getByRole("heading", { name: "Chu kỳ vệ sinh hiện tại" })
        .waitFor();
      await page.waitForFunction(() =>
        window.calls.includes("/api/auth/navigation/operation-grants"),
      );
    }
    await page.waitForTimeout(200);
    await check(page);
    assert.deepEqual(errors, []);
  } finally {
    await page.close();
  }
}
for (const capabilities of [
  {},
  { canReopen: false, canEscalate: false },
  { canReopen: true, canEscalate: true },
])
  test(`Request detail requests privileged panels only for server-approved capabilities: ${JSON.stringify(capabilities)}`, () =>
    run(
      { request: true, paths: ["/internal-requests"], capabilities },
      async (p) => {
        const calls = await p.evaluate(() => window.calls);
        assert.equal(
          calls.includes("/api/internal-requests/1/reopen"),
          capabilities.canReopen === true,
        );
        assert.equal(
          calls.includes("/api/internal-requests/1/escalation"),
          capabilities.canEscalate === true,
        );
        assert.equal(
          calls.some((path) => path.includes("/linked-materials")),
          false,
        );
        assert.equal(
          await p
            .getByRole("heading", { name: "Yêu cầu vật tư liên quan" })
            .count(),
          0,
        );
      },
    ));
test("Request detail retains material panel for a user with material navigation access", () =>
  run(
    {
      request: true,
      paths: ["/internal-requests", "/operations/material-requests"],
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.calls.some((path) => path.includes("/linked-materials")),
      );
    },
  ));
test("Reception room detail hides monitor, inspection and incident queue links despite raw branch grants", () =>
  run({ paths: [], monitor: false }, async (p) => {
    const links = await p
      .locator("a[href]")
      .evaluateAll((xs) => xs.map((x) => x.getAttribute("href")));
    assert.deepEqual(links, []);
    assert.equal(
      (await p.evaluate(() => window.calls)).some((x) =>
        x.startsWith("/api/internal-requests"),
      ),
      false,
    );
  }));

for (const [path, allowed] of [
  ["/cleaning/tasks/12?branchId=7&execution=1", true],
  ["/cleaning/tasks/12?branchId=7&monitor=1&kind=cleaning", true],
  ["/cleaning/tasks/12?branchId=8&monitor=1&kind=cleaning", false],
])
  test(`Linked cleaning accepts only the authorized branch and destination: ${path}`, () =>
    run(
      { request: true, paths: ["/internal-requests"], linkPath: path },
      async (p) => {
        await p
          .locator("summary")
          .filter({ hasText: "Công việc liên quan" })
          .click();
        const link = p.getByRole("link", { name: "CLN12: Cleaning source" });
        if (allowed) {
          await link.waitFor();
          assert.equal(await link.getAttribute("href"), path);
        } else {
          assert.equal(await link.count(), 0);
        }
      },
    ));
test("Approved reporting is visible and disappears if navigation refresh is unavailable", () =>
  run({ paths: ["/incidents/new"], monitor: false }, async (p) => {
    await p.getByRole("link", { name: "Báo sự cố cho phòng này" }).waitFor();
    await p.evaluate(() => {
      window.nav = null;
      window.dispatchEvent(new Event("focus"));
    });
    await p
      .getByRole("link", { name: "Báo sự cố cho phòng này" })
      .waitFor({ state: "hidden" });
  }));
test("Monitor links retain exact branch and rework monitor context for authorized supervisor", () =>
  run({ paths: [], monitor: true }, async (p) => {
    const links = await p
      .locator("a[href]")
      .evaluateAll((xs) => xs.map((x) => x.getAttribute("href")));
    assert.ok(
      links.includes("/cleaning/tasks/12?branchId=7&monitor=1&kind=cleaning"),
    );
    assert.ok(
      links.includes("/cleaning/tasks/9?branchId=7&monitor=1&kind=rework"),
    );
    assert.ok(!links.some((x) => x.includes("inspection-history")));
  }));

for (const selectSearchEnabled of [false,true]) test('Selection search follows server preference: '+selectSearchEnabled,()=>run({selectOnly:true,selectSearchEnabled,paths:['/rooms/status']},async p=>{
  assert.equal(await p.locator('.ds-select-search').count(),selectSearchEnabled?1:0);
  assert.equal(await p.locator('select option').count(),2);
  await p.getByRole('combobox',{name:'T?ng'}).selectOption('2');
  assert.equal(await p.getByRole('combobox',{name:'T?ng'}).inputValue(),'2');
}));
