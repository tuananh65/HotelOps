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
    if (path === "/api/internal-requests/1/escalation") {
      if (!state.escalationAllowed) return json({},403);
      if (request.method() === "POST") {
        state.escalationPosts=(state.escalationPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        assert.deepEqual(request.postDataJSON(),{reason:"Required escalation reason",priorityId:7,deadline:null});
        if(state.hold) await new Promise(resolve=>{state.release=resolve;});
        return json({},412);
      }
      return json({requestId:1,version:"AAAAAAAAAAA=",timingStatus:"AT_RISK",canEscalate:true,hasOpen:false,
        createdAt:"2026-11-15T00:00:00Z",deadline:"2026-11-16T00:00:00Z",priorityId:null,priorities:[{id:7,name:"High"}]});
    }
    if(path === "/api/internal-requests/1/completion") {
      if(request.method()==="POST") {
        state.completionPosts=(state.completionPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        assert.deepEqual(request.postDataJSON(),{result:"Finished work"});return json({},412);
      }
      return json({requestId:1,status:"IN_PROGRESS",version:"AAAAAAAAAAA=",canComplete:state.completionAllowed??false,blocker:state.completionBlocker??null,
        visibleBlockers:state.visibleCompletionBlockers,hasUndisclosedBlockers:state.hiddenCompletionBlockers});
    }
    if (path === "/api/internal-requests/1/handover" || path === "/api/internal-requests/1/handover/decision") {
      if(request.method()==="POST") {
        state.handoverPosts=(state.handoverPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');
        assert.equal(request.headers()["x-csrf-token"],"test");
        state.handoverBody=request.postDataJSON();
        return json({requestId:1,handoverId:8,state:"PENDING_ACKNOWLEDGMENT",version:"AAAAAAAAAAE=",message:"MSG08"},state.handoverPostStatus??412);
      }
      return json({requestId:1,version:"AAAAAAAAAAA=",handoverId:state.handoverReceiver?8:null,
        state:state.handoverReceiver?"PENDING_ACKNOWLEDGMENT":null,receiverId:state.handoverReceiver?42:null,
        summary:state.handoverReceiver?"PRIVATE HANDOVER":null,note:null,
        canSubmit:state.handoverSender??false,canDecide:state.handoverReceiver??false,
        receivers:state.handoverSender?[{id:43,name:"Eligible receiver"}]:[]},state.handoverReadStatus??200);
    }
    if (path === "/api/internal-requests/1/withdrawal") {
      if(request.method()==="POST") {
        state.withdrawalPosts=(state.withdrawalPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        assert.deepEqual(request.postDataJSON(),{reason:"No longer needed"});
        if(state.hold) await new Promise(resolve=>{state.release=resolve;});
        return json({requestId:1,status:"CANCELLED",version:"AAAAAAAAAAE=",message:"MSG08"},state.withdrawalStatus??200);
      }
      return json({requestId:1,status:"PENDING_ACCEPTANCE",version:"AAAAAAAAAAA=",canWithdraw:state.canWithdraw??false});
    }
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
      const older = new URL(request.url()).searchParams.get("evidenceBefore");
      if (older) { state.olderCursor = older; state.olderReads = (state.olderReads ?? 0) + 1; }
      const evidence = state.evidence ? { evidenceCount: 2, evidence: [{ id: older ? 1 : 2, name: older ? "older.png" : "latest.png", mimeType: "image/png", size: 8, downloadPath: `/api/internal-requests/1/progress/evidence/${older ? 1 : 2}` }] } : {};
      return json({requestId:1,status:"IN_PROGRESS",version:older && state.changedEvidence ? "AAAAAAAAAAE=" : "AAAAAAAAAAA=",percent:25,note:"PRIVATE PROGRESS",result:"PRIVATE RESULT",deadline:null,canUpdate:state.progressAllowed??false,canChangeDeadline:false,...evidence},older ? (state.olderStatus??200) : (state.progressStatus??200));
    }
    if (path === "/api/internal-requests/1/linked-incidents") {
      if(request.method()==="POST") {
        state.incidentPosts=(state.incidentPosts??0)+1;
        assert.equal(request.headers()["if-match"],'"AAAAAAAAAAA="');assert.equal(request.headers()["x-csrf-token"],"test");
        assert.deepEqual(request.postDataJSON(),{incidentId:7,mandatory:true});
        return json({},412);
      }
      return json({requestId:1,branchId:1,version:"AAAAAAAAAAA=",canLink:true,page:1,total:1,
        candidates:new URL(request.url()).searchParams.get("candidates")==="true",
        items:[{incidentId:7,code:"INC7",title:"PRIVATE INCIDENT",state:"OPEN",mandatory:true,path:"/operations/incidents/7"}]});
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
        items:[{taskId:7,code:"TASK7",title:"PRIVATE LINKED TASK",state:"IN_PROGRESS",mandatory:state.legacyTask ? null : true,path:"/operational-tasks?branchId=1&taskId=7"}]},state.linkStatus??200);
    }
    if (path === "/api/internal-requests/options") return json({ statuses: ["SUBMITTED"], types: [], priorities: [], departments: [] }, state.history);
    if (path === "/api/internal-requests/1/timeline") return json({requestId:1,events:[],eventTotalCount:0,assignments:[],assignmentTotalCount:0,feedback:null},state.history);
    if (path === "/api/internal-requests") return json({ items: [{ id: 1, number: "IR1", branchId: 1, receivingDepartmentId: null, subject: "PRIVATE REQUEST <script>", status: "SUBMITTED", deadline: null, overdue: false, currentAssigneeId: null, ...(state.timing ?? {}) }], totalCount: 1, page: 1, pageSize: 25, snapshotUtc: "2026-11-16T00:00:00Z" }, state.history);
    return json({}, 403);
  });
  try {
    await page.goto("https://history.test/internal-requests"); await page.addScriptTag({ content: compiled.outputFiles[0].text });
    await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
    await run(page, state);
  } finally { await page.close(); }
}
test("time window shows server risk and refresh removes it after deadline change or revoked read", async () => fixture(async (page, state) => {
  state.timing = { createdAt:"2026-11-15T00:00:00Z", timingStatus:"AT_RISK" };
  await page.getByRole("button",{name:"Refresh history",exact:true}).click();
  await page.getByRole("cell",{name:"At risk (80% or more elapsed)",exact:true}).waitFor();
  state.timing.timingStatus = "ON_TIME";
  await page.getByRole("button",{name:"Refresh history",exact:true}).click();
  await page.getByRole("cell",{name:"Under 80% elapsed",exact:true}).waitFor();
  assert.equal(await page.getByText("At risk (80% or more elapsed)",{exact:true}).count(),0);
  state.history = 403;
  await page.getByRole("button",{name:"Refresh history",exact:true}).click();
  await page.getByRole("alert").filter({hasText:"do not have access"}).waitFor();
  assert.equal(await page.getByText("Under 80% elapsed",{exact:true}).count(),0);
}));
test("refresh denial removes previously visible history", async () => fixture(async (page, state) => {
  state.history = 403;
  await page.getByRole("button", { name: "Refresh history", exact: true }).click();
  await page.getByRole("alert").filter({ hasText: "do not have access" }).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", { exact: true }).count(), 0);
}));

test("escalation validates reason and stale double click never replays", async () => fixture(async(page,state)=>{
  state.escalationAllowed=true;await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Escalate request",exact:true});
  await panel.getByRole("button",{name:"Escalate request",exact:true}).click();
  await panel.getByText("Enter a reason of at least 10 characters without control characters.",{exact:true}).waitFor();
  assert.equal(state.escalationPosts??0,0);
  await panel.getByLabel("Escalation reason",{exact:true}).fill("Required escalation reason");
  await panel.getByLabel("New priority",{exact:true}).selectOption("7");
  await panel.getByRole("button",{name:"Escalate request",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByText("Request changed. Reload before escalating.",{exact:true}).waitFor();
  assert.equal(state.escalationPosts,1);assert.equal(await panel.getByLabel("Escalation reason",{exact:true}).count(),0);
}));
test("status filter has an accessible name and sends the selected value", async () => fixture(async (page) => {
  const request = page.waitForRequest(r => new URL(r.url()).pathname === "/api/internal-requests" && new URL(r.url()).searchParams.get("status") === "SUBMITTED");
  await page.getByLabel("Status", {exact:true}).selectOption("SUBMITTED");
  await request;
  await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
}));

test("At Risk filter sends server predicate and excludes Overdue selection", async () => fixture(async(page)=>{
  const first=page.waitForRequest(r=>new URL(r.url()).pathname==="/api/internal-requests"&&new URL(r.url()).searchParams.get("atRisk")==="true");
  await page.getByLabel("At risk only",{exact:true}).check();const request=await first;
  assert.equal(request.method(),"GET");assert.equal(new URL(request.url()).searchParams.has("overdue"),false);
  await page.getByRole("cell",{name:"PRIVATE REQUEST <script>",exact:true}).waitFor();
  const second=page.waitForRequest(r=>new URL(r.url()).pathname==="/api/internal-requests"&&new URL(r.url()).searchParams.get("overdue")==="true");
  await page.getByLabel("Overdue only",{exact:true}).check();assert.equal(new URL((await second).url()).searchParams.has("atRisk"),false);
  assert.equal(await page.getByLabel("At risk only",{exact:true}).isChecked(),false);
}));
test("direction filter sends Sent and Received as scoped reads and clears history on denial", async () => fixture(async (page, state) => {
  for (const direction of ["SENT", "RECEIVED"]) {
    const sent = page.waitForRequest(r => new URL(r.url()).pathname === "/api/internal-requests"
      && new URL(r.url()).searchParams.get("direction") === direction);
    await page.getByLabel("Direction", {exact:true}).selectOption(direction);
    const request = await sent;
    assert.equal(request.method(), "GET");
    assert.equal(new URL(request.url()).searchParams.get("page"), "1");
    await page.getByRole("cell", { name: "PRIVATE REQUEST <script>", exact: true }).waitFor();
  }
  state.history = 403;
  await page.getByRole("button", {name:"Refresh history", exact:true}).click();
  await page.getByRole("alert").filter({hasText:"do not have access"}).waitFor();
  assert.equal(await page.getByText("PRIVATE REQUEST <script>", {exact:true}).count(), 0);
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
test("older evidence preserves unsaved progress and disappears after authority loss",async()=>fixture(async(page,state)=>{
  state.progressAllowed=true;state.evidence=true;
  await page.goto("https://history.test/internal-requests?requestId=1");await page.addScriptTag({content:compiled.outputFiles[0].text});
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByLabel("Progress note",{exact:true}).fill("Unsaved work");
  await panel.getByRole("button",{name:"Load older attachments",exact:true}).click();
  await panel.getByRole("link",{name:"older.png",exact:true}).waitFor();
  assert.equal(state.olderCursor,"2");assert.equal(state.olderReads,1);
  assert.equal(await panel.getByLabel("Progress note",{exact:true}).inputValue(),"Unsaved work");
  assert.equal(await panel.getByRole("button",{name:"Load older attachments",exact:true}).count(),0);
  state.progressStatus=403;await panel.getByRole("button",{name:"Reload progress",exact:true}).click();
  await panel.getByText("Progress unavailable. Reload to check current access.",{exact:true}).waitFor();
  assert.equal(await panel.getByRole("link").count(),0);
}));
for (const denied of [true,false]) test(`older evidence rejects ${denied ? "revoked access" : "changed version"} without retaining private content`,async()=>fixture(async(page,state)=>{
  state.evidence=true;state.olderStatus=denied?403:200;state.changedEvidence=!denied;
  await page.goto("https://history.test/internal-requests?requestId=1");await page.addScriptTag({content:compiled.outputFiles[0].text});
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByRole("button",{name:"Load older attachments",exact:true}).click();
  await panel.getByText("Evidence or access changed. Reload progress before continuing.",{exact:true}).waitFor();
  assert.equal(await panel.getByRole("link").count(),0);assert.equal(state.olderReads,1);
}));
for(const status of [200,403,412])test(`progress ${status} uses CSRF/ETag, prevents double submit and requires reload`,async()=>fixture(async(page,state)=>{
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
  await panel.getByRole("status").filter({hasText:status===200?"MSG08: Progress saved":status===403?"MSG02: Current authority":"Request changed"}).waitFor();
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
test("withdrawal requires reason and stale response is not replayed",async()=>fixture(async(page,state)=>{
  state.canWithdraw=true;state.withdrawalStatus=412;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Withdraw request",exact:true});
  await panel.getByRole("button",{name:"Withdraw request",exact:true}).click();
  await panel.getByText("Enter a withdrawal reason.",{exact:true}).waitFor();assert.equal(state.withdrawalPosts??0,0);
  await panel.getByLabel("Withdrawal reason",{exact:true}).fill("  No longer needed  ");
  await panel.getByRole("button",{name:"Withdraw request",exact:true}).click();
  await panel.getByText("Request changed. Reload before withdrawing.",{exact:true}).waitFor();
  assert.equal(state.withdrawalPosts,1);assert.equal(await panel.getByRole("button",{name:"Withdraw request",exact:true}).count(),0);
}));
test("withdrawal double click sends once and loses authority with session",async()=>fixture(async(page,state)=>{
  state.canWithdraw=true;state.hold=true;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Withdraw request",exact:true});
  await panel.getByLabel("Withdrawal reason",{exact:true}).fill("No longer needed");
  const sent=page.waitForRequest(r=>r.method()==="POST"&&new URL(r.url()).pathname.endsWith("/withdrawal"));
  await panel.getByRole("button",{name:"Withdraw request",exact:true}).evaluate(b=>{b.click();b.click();});await sent;
  state.session=401;await page.evaluate(()=>window.historyStore.revalidate());
  await page.getByRole("button",{name:"Sign in",exact:true}).waitFor();state.release?.();
  assert.equal(state.withdrawalPosts,1);assert.equal(await page.getByLabel("Withdrawal reason",{exact:true}).count(),0);
}));

test("incident linking requires explicit mandatory selection and never replays stale submission",async()=>fixture(async(page,state)=>{
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Linked incidents",exact:true});
  await panel.getByRole("button",{name:"Link existing incident",exact:true}).click();
  await panel.getByLabel("Open incident",{exact:true}).selectOption("7");
  await panel.getByRole("button",{name:"Save incident link",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Select an incident and whether"}).waitFor();assert.equal(state.incidentPosts??0,0);
  await panel.getByLabel("Completion requirement",{exact:true}).selectOption("required");
  await panel.getByRole("button",{name:"Save incident link",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByRole("status").filter({hasText:"Request changed"}).waitFor();
  assert.equal(state.incidentPosts,1);assert.equal(await panel.getByLabel("Open incident",{exact:true}).count(),0);
}));

test("handover sender validates input, uses CSRF/ETag and never replays a stale write",async()=>fixture(async(page,state)=>{
  state.handoverSender=true;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request handover",exact:true});
  await panel.getByRole("button",{name:"Submit handover",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Enter the required"}).waitFor();
  assert.equal(state.handoverPosts??0,0);
  await panel.getByLabel("New handler",{exact:true}).selectOption("43");
  await panel.getByLabel("Status summary",{exact:true}).fill("  Pending inspection  ");
  await panel.getByRole("button",{name:"Submit handover",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByText("Request changed. Reload before deciding.",{exact:true}).waitFor();
  assert.equal(state.handoverPosts,1);
  assert.deepEqual(state.handoverBody,{receiverId:43,summary:"Pending inspection",note:null});
  assert.equal(await panel.getByLabel("New handler",{exact:true}).count(),0);
}));

test("handover receiver requires rejection reason and removes receipt after read revocation",async()=>fixture(async(page,state)=>{
  state.handoverReceiver=true;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request handover",exact:true});
  await panel.getByText("PRIVATE HANDOVER",{exact:true}).waitFor();
  await panel.getByRole("button",{name:"Reject handover",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"Enter the required"}).waitFor();
  assert.equal(state.handoverPosts??0,0);
  state.handoverReadStatus=403;
  await panel.getByRole("button",{name:"Reload handover",exact:true}).click();
  await panel.getByText("Handover unavailable. Reload current access and responsibility.",{exact:true}).waitFor();
  assert.equal(await panel.getByText("PRIVATE HANDOVER",{exact:true}).count(),0);
  assert.equal(await panel.getByRole("button",{name:"Acknowledge handover",exact:true}).count(),0);
}));

test("completion lists authorized blockers and keeps undisclosed work blocking",async()=>fixture(async(page,state)=>{
  state.completionBlocker="MSG34";
  state.visibleCompletionBlockers=[{kind:"MATERIAL",id:7,code:"MATERIAL_COMPLETION_UNPROVEN"}];
  state.hiddenCompletionBlockers=true;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Complete request",exact:true});
  await panel.getByText("MATERIAL #7: Material completion evidence is not confirmed.",{exact:true}).waitFor();
  await panel.getByText(/Hidden records still prevent completion/).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Complete request",exact:true}).count(),0);
}));
test("completion refuses contradictory permission and blocker response",async()=>fixture(async(page,state)=>{
  state.completionAllowed=true;
  state.visibleCompletionBlockers=[{kind:"TASK",id:7,code:"MANDATORY_WORK_NOT_CLOSED"}];
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Complete request",exact:true});
  await panel.getByText(/Completion availability could not be loaded/).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Complete request",exact:true}).count(),0);
}));
test("completion requires result and never replays a stale double click",async()=>fixture(async(page,state)=>{
  state.completionAllowed=true;
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Complete request",exact:true});
  await panel.getByRole("button",{name:"Complete request",exact:true}).click();
  await panel.getByText("Enter a completion result.",{exact:true}).waitFor();assert.equal(state.completionPosts??0,0);
  await panel.getByLabel("Completion result",{exact:true}).fill(" Finished work ");
  await panel.getByRole("button",{name:"Complete request",exact:true}).evaluate(b=>{b.click();b.click();});
  await panel.getByText("Request changed. Reload before closing.",{exact:true}).waitFor();
  assert.equal(state.completionPosts,1);assert.equal(await panel.getByLabel("Completion result",{exact:true}).count(),0);
}));

test("completion blockers hide mutation without disclosing target identifiers",async()=>fixture(async(page,state)=>{
  state.completionBlocker="MSG34";
  await page.getByRole("button",{name:"IR1",exact:true}).click();
  const panel=page.getByRole("region",{name:"Complete request",exact:true});
  await panel.getByRole("status").filter({hasText:"MSG34"}).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Complete request",exact:true}).count(),0);
  assert.equal(await panel.getByLabel("Completion result",{exact:true}).count(),0);
}));

 test("historical task requirement is unclassified rather than optional",async()=>fixture(async(page,state)=>{
 state.legacyTask=true; await page.getByRole("button",{name:"IR1",exact:true}).click();
 const panel=page.getByRole("region",{name:"Linked operational tasks",exact:true});
 await panel.getByText(/Completion requirement unclassified/).waitFor();
 assert.equal(await panel.getByText("Optional",{exact:true}).count(),0);
 state.linkStatus=403;await panel.getByRole("button",{name:"Reload linked tasks",exact:true}).click();
 await panel.getByRole("status").filter({hasText:"Linked tasks unavailable"}).waitFor();
 assert.equal(await panel.getByRole("link").count(),0);
}));
