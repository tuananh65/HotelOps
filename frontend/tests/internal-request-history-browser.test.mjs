import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "@playwright/test";
import { build } from "esbuild";
const compiled = await build({ stdin: { resolveDir: process.cwd(), loader: "tsx", contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {AuthRouteHost} from './src/auth/AuthRouteHost';
const store=createAuthStore(); window.historyStore=store; const host=<AuthRouteHost/>;
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);`
}, bundle: true, outfile: ".test-output/ir-history-browser.js", write: false, format: "iife", jsx: "automatic", define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" } });
let browser;
before(async () => { browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true }); });
after(async () => browser?.close());
async function fixture(run) {
  const page = await browser.newPage();
  page.setDefaultTimeout(10000);
  const state = { session: 200, history: 200, decision: 200, canDecide:true, decisionPosts:0, postStatus:200, hold:false, release:null, assignmentPosts:0, assignmentStatus:200, routePosts:0, routeStatus:200 };
  await page.route("**/*", async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    const json = (data, status=200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(data) });
    if (request.resourceType() === "document") return route.fulfill({ contentType: "text/html", body: '<div id="root"></div>' });
    if (path === "/api/auth/session") return json({ user: { id: "42", username: "staff", name: "Synthetic" }, expiresAt: "2026-11-17T00:00:00Z", absoluteExpiresAt: "2026-11-17T12:00:00Z", mustChangePassword: false, csrfToken: "test" }, state.session);
    if (path === "/api/auth/csrf") return json({ csrfToken: "test" });
    if (path === "/api/internal-requests/1/decision") {
      if(request.method()==="POST") {
        state.decisionPosts++;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');
        assert.equal(request.headers()["x-csrf-token"],"test");
        if(state.hold) await new Promise(resolve=>{state.release=resolve;});
        return json({requestId:1,status:"ACCEPTED",version:"AAAAAAAAAAE=",message:"MSG08"},state.postStatus);
      }
      return json({requestId:1,subject:"PRIVATE DECISION",description:"PRIVATE DESCRIPTION",status:"PENDING_ACCEPTANCE",
        submittedAt:"2026-11-16T00:00:00Z",deadline:null,ageSeconds:60,version:"AAAAAAAAAAA=",canDecide:state.canDecide},state.decision);
    }
    if (path === "/api/internal-requests/1/route") {
      if(request.method()==="POST") {
        state.routePosts++;assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        return json({requestId:1,departmentId:5,eventId:1,version:"AAAAAAAAAAE="},state.routeStatus);
      }
      return json({requestId:1,status:"PENDING_ACCEPTANCE",version:"AAAAAAAAAAA=",departments:[{departmentId:5,name:"Receiving team"}]});
    }
    if (path === "/api/internal-requests/1/assignment") {
      if(request.method()==="POST") {
        state.assignmentPosts++; assert.equal(request.headers()["if-match"],'"AAAAAAAAAAE="');
        assert.equal(request.headers()["x-csrf-token"],"test");
        return json({requestId:1,status:"IN_PROGRESS",version:"AAAAAAAAAAI="},state.assignmentStatus);
      }
      return json({requestId:1,status:"ACCEPTED",version:"AAAAAAAAAAE=",handlerId:null,handlers:[{userId:42,name:"Eligible Handler",openRequests:2}]});
    }
    if(path === "/api/internal-requests/1/progress") {
      if(request.method()==="POST") {
        state.progressPosts=(state.progressPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        if(state.expectProgressFile){const body=request.postDataJSON();assert.equal(body.files.length,1);assert.equal(body.files[0].name,"proof.png");assert.equal(body.files[0].mimeType,"image/png");assert.equal(Buffer.from(body.files[0].base64,"base64").length,8);}
        return json({requestId:1,status:"IN_PROGRESS",version:"AAAAAAAAAAE="},state.progressPostStatus??200);
      }
      return json({requestId:1,status:"IN_PROGRESS",version:"AAAAAAAAAAA=",percent:25,note:"PRIVATE PROGRESS",result:"PRIVATE RESULT",deadline:null,canUpdate:state.progressAllowed??false,canChangeDeadline:false},state.progressStatus??200);
    }
    if (path === "/api/internal-requests/1/linked-tasks") {
      if(request.method()==="POST") {
        state.linkPosts=(state.linkPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        assert.deepEqual(request.postDataJSON(),{taskId:7,mandatory:true});
        return json({requestId:1,taskId:7,mandatory:true,version:"AAAAAAAAAAE="},state.linkPostStatus??200);
      }
      return json({requestId:1,branchId:1,version:"AAAAAAAAAAA=",canLink:state.linkAllowed??false,page:1,total:1,
        candidates:new URL(request.url()).searchParams.get("candidates")==="true",
        items:[{taskId:7,code:"TASK7",title:"PRIVATE LINKED TASK",state:"IN_PROGRESS",mandatory:true,path:"/operational-tasks?branchId=1&taskId=7"}]},state.linkStatus??200);
    }
    if (path === "/api/internal-requests/options") return json({ statuses: ["SUBMITTED"], types: [], priorities: [], departments: [] }, state.history);
    if (path === "/api/internal-requests/1/timeline") return json({requestId:1,events:[],eventTotalCount:0,assignments:[],assignmentTotalCount:0,feedback:null},state.history);
    if (path === "/api/internal-requests") return json({ items: [{ id: 1, number: "IR1", branchId: 1, receivingDepartmentId: null, subject: "PRIVATE REQUEST <script>", status: "SUBMITTED", deadline: null, overdue: false, currentAssigneeId: null }], totalCount: 1, page: 1, pageSize: 25, snapshotUtc: "2026-11-16T00:00:00Z" }, state.history);
    return json({}, 403);
  });
  try {
    await page.goto("https://history.test/internal-requests"); await page.addScriptTag({ content: compiled.outputFiles[0].text });
    await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
    await run(page, state);
  } finally { await page.close(); }
}
test("refresh denial removes previously visible history", async () => fixture(async (page, state) => {
  state.history = 403;
  await page.getByRole("button", { name: "Refresh history", exact: true }).click();
  await page.getByRole("alert").filter({ hasText: "do not have access" }).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", { exact: true }).count(), 0);
}));
test("status filter has an accessible name and sends the selected value", async () => fixture(async (page) => {
  const request = page.waitForRequest(r => new URL(r.url()).pathname === "/api/internal-requests" && new URL(r.url()).searchParams.get("status") === "SUBMITTED");
  await page.getByLabel("Status", {exact:true}).selectOption("SUBMITTED");
  await request;
  await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
}));
test("session revalidation removes request history before login", async () => fixture(async (page, state) => {
  state.session = 401;
  await page.evaluate(() => window.historyStore.revalidate());
  await page.getByRole("button", { name: "Sign in", exact: true }).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", { exact: true }).count(), 0);
}));
test("request notification deep link loads only the selected authorized timeline and back clears it", async () => fixture(async (page,state) => {
  await page.goto("https://history.test/internal-requests?requestId=1");
  await page.addScriptTag({content:compiled.outputFiles[0].text});
  await page.getByRole("heading",{name:"Request 1",exact:true}).waitFor();
  await page.getByRole("button",{name:"Back to requests",exact:true}).click();
  await page.getByRole("cell",{name:"PRIVATE REQUEST <script>",exact:true}).waitFor();
  assert.equal(new URL(page.url()).search,"");
  state.history=404;
  await page.goto("https://history.test/internal-requests?requestId=1");
  await page.addScriptTag({content:compiled.outputFiles[0].text});
  await page.getByRole("alert").filter({hasText:"Request unavailable"}).waitFor();
  assert.equal(await page.getByRole("region",{name:"Request timeline",exact:true}).count(),0);
}));
test("decision stale response requires reload without replay or retained details",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request decision",exact:true});
  await panel.getByRole("button",{name:"Accept request",exact:true}).waitFor();state.postStatus=412;
  await panel.getByRole("button",{name:"Accept request",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Request changed"}).waitFor();
  assert.equal(state.decisionPosts,1);assert.equal(await panel.getByText("PRIVATE DESCRIPTION",{exact:true}).count(),0);
  assert.equal(await panel.getByRole("button",{name:"Accept request",exact:true}).count(),0);
}));
test("decision double click sends once and session loss hides details while request is pending",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request decision",exact:true});
  const button=panel.getByRole("button",{name:"Accept request",exact:true});await button.waitFor();state.hold=true;
  const sent=page.waitForRequest(r=>r.method()==="POST"&&new URL(r.url()).pathname.endsWith("/decision"));
  await button.evaluate(b=>{b.click();b.click();});
  await sent;
  await page.waitForTimeout(100);assert.equal(state.decisionPosts,1);assert.equal(await button.isDisabled(),true);
  state.session=401;await page.evaluate(()=>window.historyStore.revalidate());
  await page.getByRole("button",{name:"Sign in",exact:true}).waitFor();state.release?.();
  assert.equal(await page.getByText("PRIVATE DESCRIPTION",{exact:true}).count(),0);
}));
test("read access without processing authority exposes no decision action",async()=>fixture(async(page,state)=>{
  state.canDecide=false;await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request decision",exact:true});
  await panel.getByText("PRIVATE DESCRIPTION",{exact:true}).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Accept request",exact:true}).count(),0);
  assert.equal(await panel.getByRole("button",{name:"Reject request",exact:true}).count(),0);
}));

test("accept offers assignment; stale assign clears form without replay",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  await page.getByRole("button",{name:"Accept request",exact:true}).click();
  const panel=page.getByRole("region",{name:"Assign request handler",exact:true});
  await panel.getByLabel("Request handler",{exact:true}).selectOption("42");state.assignmentStatus=412;
  await panel.getByRole("button",{name:"Assign request handler",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Request changed"}).waitFor();
  assert.equal(state.assignmentPosts,1);assert.equal(await panel.getByLabel("Request handler",{exact:true}).count(),0);
}));

test("accepted request assignment succeeds with ETag and CSRF then requires fresh load",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  await page.getByRole("button",{name:"Accept request",exact:true}).click();
  const panel=page.getByRole("region",{name:"Assign request handler",exact:true});
  await panel.getByLabel("Request handler",{exact:true}).selectOption("42");
  await panel.getByRole("button",{name:"Assign request handler",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByRole("status").filter({hasText:"MSG08: Handler assigned"}).waitFor();
  await page.getByText("Status: IN_PROGRESS",{exact:true}).waitFor();
  assert.equal(state.assignmentPosts,1);assert.equal(await panel.getByLabel("Request handler",{exact:true}).count(),0);
}));

test("route requires reason then clears old decision authority after committed response",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Route request",exact:true});
  await panel.getByLabel("Target department",{exact:true}).selectOption("5");
  await panel.getByRole("button",{name:"Route request",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"at least 10 characters"}).waitFor();assert.equal(state.routePosts,0);
  await panel.getByLabel("Routing reason",{exact:true}).fill("Route to responsible team");
  await panel.getByRole("button",{name:"Route request",exact:true}).evaluate(b=>{b.click();b.click();});
  await page.getByRole("status").filter({hasText:"MSG08: Request routed"}).waitFor();
  assert.equal(state.routePosts,1);assert.equal(await page.getByRole("button",{name:"Accept request",exact:true}).count(),0);
}));

test("stale route response hides target options and never replays mutation",async()=>fixture(async(page,state)=>{
  state.routeStatus=412;await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Route request",exact:true});
  await panel.getByLabel("Target department",{exact:true}).selectOption("5");
  await panel.getByLabel("Routing reason",{exact:true}).fill("Route to responsible team");
  await panel.getByRole("button",{name:"Route request",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Request changed"}).waitFor();
  assert.equal(state.routePosts,1);assert.equal(await panel.getByLabel("Target department",{exact:true}).count(),0);
}));

test("progress read does not grant mutation and reload denial removes private snapshot", async()=>fixture(async(page,state)=>{
  await page.goto("https://history.test/internal-requests?requestId=1");await page.addScriptTag({content:compiled.outputFiles[0].text});
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByText("PRIVATE RESULT",{exact:true}).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Save progress",exact:true}).count(),0);
  state.progressStatus=403;await panel.getByRole("button",{name:"Reload progress",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Progress unavailable"}).waitFor();
  assert.equal(await panel.getByText("PRIVATE RESULT",{exact:true}).count(),0);
}));
for(const status of [200,412])test(`progress ${status} uses CSRF/ETag, prevents double submit and requires reload`,async()=>fixture(async(page,state)=>{
  state.progressAllowed=true;state.progressPostStatus=status;
  await page.goto("https://history.test/internal-requests?requestId=1");await page.addScriptTag({content:compiled.outputFiles[0].text});
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByLabel("Progress percent",{exact:true}).fill("100");
  await panel.getByLabel("Progress note",{exact:true}).fill("Updated note");
  await panel.getByLabel("Result note",{exact:true}).fill("Updated result");
  state.expectProgressFile=true;
  await panel.getByLabel("Progress attachments",{exact:true}).setInputFiles({name:"proof.png",mimeType:"image/png",buffer:Buffer.from([137,80,78,71,13,10,26,10])});
  assert.equal(await panel.getByLabel("New deadline (UTC)",{exact:true}).count(),0);
  await panel.getByRole("button",{name:"Save progress",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByRole("status").filter({hasText:status===200?"MSG08: Progress saved":"Request changed"}).waitFor();
  assert.equal(state.progressPosts,1);assert.equal(await panel.getByLabel("Progress percent",{exact:true}).count(),0);
}));

test("linked task read grants no mutation and revoked read clears target metadata",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Linked operational tasks",exact:true});
  await panel.getByRole("link",{name:"TASK7: PRIVATE LINKED TASK",exact:true}).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Link existing task",exact:true}).count(),0);
  state.linkStatus=404;await panel.getByRole("button",{name:"Reload linked tasks",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Linked tasks unavailable"}).waitFor();
  assert.equal(await panel.getByRole("link").count(),0);
}));
for(const status of [200,412])test(`link ${status} requires explicit mandatory flag, sends once and clears stale options`,async()=>fixture(async(page,state)=>{
  state.linkAllowed=true;state.linkPostStatus=status;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Linked operational tasks",exact:true});
  await panel.getByRole("button",{name:"Link existing task",exact:true}).click();
  await panel.getByLabel("Open task",{exact:true}).selectOption("7");
  await panel.getByRole("button",{name:"Save task link",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Select a task and whether"}).waitFor();assert.equal(state.linkPosts??0,0);
  await panel.getByLabel("Completion requirement",{exact:true}).selectOption("required");
  await panel.getByRole("button",{name:"Save task link",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByRole("status").filter({hasText:status===200?"MSG08: Task linked":"Request changed"}).waitFor();
  assert.equal(state.linkPosts,1);assert.equal(await panel.getByLabel("Open task",{exact:true}).count(),0);
}));
