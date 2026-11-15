import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
const origin="https://localhost:7142",root=`/api/internal-requests/${fixture.request}`,checks=[];
let browser,stage="destination manager login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  async function login(user){
    const context=await browser.newContext(),page=await context.newPage();page.setDefaultTimeout(15000);
    await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
    await page.getByLabel("Username or email").fill(user.username);
    await page.getByLabel("Password",{exact:true}).fill(user.password);user.password="";
    await page.getByRole("button",{name:"Sign in",exact:true}).click();
    await page.getByRole("region",{name:"Request decision",exact:true}).getByRole("button",{name:"Reload decision",exact:true}).waitFor();
    return {context,page};
  }
  const api=(page,path)=>page.evaluate(async p=>{const r=await fetch(p);return{status:r.status,body:await r.json()};},path);
  const post=(page,path,version,body)=>page.evaluate(async({p,v,b})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    return(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${v}"`},body:JSON.stringify(b)})).status;
  },{p:path,v:version,b:body});
  async function notification(page,type){
    for(let i=0;i<40;i++){
      const result=await api(page,`/api/notifications?type=${type}&pageSize=100`);assert.equal(result.status,200);
      if(result.body.items.length===1){
        const source=await api(page,`/api/notifications/${result.body.items[0].id}/source`);
        assert.equal(source.status,200);assert.equal(source.body.path,`/internal-requests?requestId=${fixture.request}`);
        return result.body.items[0];
      }
      await page.waitForTimeout(250);
    }
    throw new Error("Expected scheduled notification unavailable");
  }
  const manager=await login(fixture.manager),page=manager.page;
  const decision=page.getByRole("region",{name:"Request decision",exact:true});
  await decision.getByRole("button",{name:"Accept request",exact:true}).waitFor();
  await page.getByLabel("Target department",{exact:true}).waitFor();
  stage="routed notification and pending assignment guard";
  await notification(page,"IR_ROUTED");
  let loaded=(await api(page,root+"/assignment")).body;
  assert.equal(loaded.status,"PENDING_ACCEPTANCE");
  assert.equal(await post(page,root+"/assignment",loaded.version,{handlerId:fixture.manager.id,note:null}),409);
  checks.push("Destination manager receives hosted-worker Route notice and resolves authorized source; assignment requires acceptance");
  stage="destination manager accepts";
  await decision.getByRole("button",{name:"Accept request",exact:true}).click();
  await decision.getByRole("status").filter({hasText:"MSG08: Request accepted"}).waitFor();
  const assignment=page.getByRole("region",{name:"Assign request handler",exact:true});
  await assignment.getByLabel("Request handler",{exact:true}).waitFor();
  loaded=(await api(page,root+"/assignment")).body;
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.manager.id).openRequests,0);
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.handler.id).openRequests,0);
  stage="first handler assignment";
  await assignment.getByLabel("Request handler",{exact:true}).selectOption(String(fixture.manager.id));
  await assignment.getByRole("button",{name:"Assign request handler",exact:true}).click();
  await assignment.getByRole("status").filter({hasText:"MSG08: Handler assigned"}).waitFor();
  await decision.getByText("Status: IN_PROGRESS",{exact:true}).waitFor();
  await notification(page,"IR_ASSIGNED");
  await assignment.getByRole("button",{name:"Reload handlers",exact:true}).click();
  await assignment.getByLabel("Request handler",{exact:true}).waitFor();
  loaded=(await api(page,root+"/assignment")).body;
  assert.equal(loaded.handlerId,fixture.manager.id);
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.manager.id).openRequests,1);
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.handler.id).openRequests,0);
  stage="reassign to another handler";
  await assignment.getByLabel("Request handler",{exact:true}).selectOption(String(fixture.handler.id));
  await assignment.getByLabel("Assignment note",{exact:true}).fill("Synthetic responsibility handoff");
  await assignment.getByRole("button",{name:"Assign request handler",exact:true}).click();
  await assignment.getByRole("status").filter({hasText:"MSG08: Handler assigned"}).waitFor();
  assert.equal(await post(page,root+"/assignment",loaded.version,{handlerId:fixture.manager.id,note:null}),412);
  await assignment.getByRole("button",{name:"Reload handlers",exact:true}).click();
  await assignment.getByLabel("Request handler",{exact:true}).waitFor();
  loaded=(await api(page,root+"/assignment")).body;
  assert.equal(loaded.status,"IN_PROGRESS");assert.equal(loaded.handlerId,fixture.handler.id);
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.manager.id).openRequests,0);
  assert.equal(loaded.handlers.find(h=>h.userId===fixture.handler.id).openRequests,1);
  assert.equal(await post(page,root+"/route",loaded.version,{departmentId:fixture.originDepartment,reason:"Cannot route after acceptance"}),409);
  checks.push("New manager Accepts, Assigns and Reassigns in canonical UI; immediate In Progress, current handler/workloads and stale-version/lifecycle guards verified");
  stage="rendered lifecycle history";
  await page.getByRole("button",{name:"Refresh history",exact:true}).click();
  const timeline=page.getByRole("region",{name:"Request timeline",exact:true});
  for(const type of ["ROUTE","ACCEPT","ASSIGN","REASSIGN"])
    await timeline.getByRole("listitem").filter({hasText:new RegExp(`\\b${type}:`)}).waitFor();
  await timeline.getByRole("listitem").filter({hasText:`Handler ${fixture.manager.id};`}).filter({hasText:"REASSIGNED"}).waitFor();
  await timeline.getByRole("listitem").filter({hasText:`Handler ${fixture.handler.id};`}).filter({hasText:"ACTIVE"}).waitFor();
  await assignment.getByLabel("Request handler",{exact:true}).waitFor();
  checks.push("Canonical history renders all four lifecycle events and ended/current assignment provenance after refresh");
  stage="manager logout";
  await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");await manager.context.close();
  stage="handler receives handoff";
  const handler=await login(fixture.handler);
  await notification(handler.page,"IR_ASSIGNED");
  const view=(await api(handler.page,root+"/decision")).body;assert.equal(view.canDecide,false);
  assert.equal((await api(handler.page,root+"/assignment")).status,403);
  assert.equal(await post(handler.page,root+"/assignment",view.version,{handlerId:fixture.manager.id,note:null}),403);
  checks.push("Current handler receives hosted-worker assignment notice and authorized source; IR_VIEW and DEPT_MANAGER grant without mapping cannot reassign");
  await handler.page.goto(origin+"/profile");await handler.page.getByRole("button",{name:"Logout",exact:true}).click();await handler.page.waitForURL(origin+"/login");await handler.context.close();
  console.log(JSON.stringify({success:true,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,errorType:error?.name,checks}));process.exitCode=1;}
finally{await browser?.close();}
