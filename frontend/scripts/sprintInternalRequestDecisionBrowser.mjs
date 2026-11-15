import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="login";const checks=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);
  const origin="https://localhost:7142",path=`/api/internal-requests/${fixture.request}/decision`;
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request decision",exact:true});
  await panel.getByRole("button",{name:"Accept request",exact:true}).waitFor();
  const snapshot=await page.evaluate(async path=>(await fetch(path)).json(),path);
  assert.equal(snapshot.requestId,fixture.request);assert.equal(snapshot.canDecide,true);
  stage="public authorization guards";
  const guards=await page.evaluate(async({path,version})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    const post=async(headers,body)=> (await fetch(path,{method:"POST",headers:{"Content-Type":"application/json",...headers},body:JSON.stringify(body)})).status;
    const body={accept:true,reason:null,deadline:null};
    return [await post({"If-Match":`"${version}"`},body),await post({"X-CSRF-TOKEN":csrf.csrfToken},body),
      await post({"X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${version}"`},{accept:false,reason:"  ",deadline:null})];
  },{path,version:snapshot.version});
  assert.deepEqual(guards,[400,428,400]);checks.push("public API rejects missing CSRF, missing version and blank rejection reason");
  stage="canonical decision";
  if(fixture.accept){
    await panel.getByLabel("Decision reason",{exact:true}).fill("Synthetic deadline adjustment");
    await panel.getByLabel("Adjusted deadline (UTC)",{exact:true}).fill(new Date(Date.now()+172800000).toISOString().slice(0,16));
    await panel.getByRole("button",{name:"Accept request",exact:true}).click();
    await panel.getByRole("status").filter({hasText:"MSG08: Request accepted."}).waitFor();
  }else{
    await panel.getByRole("button",{name:"Reject request",exact:true}).click();
    await panel.getByRole("status").filter({hasText:"A reason is required"}).waitFor();
    await panel.getByLabel("Decision reason",{exact:true}).fill("Synthetic unable to serve");
    await panel.getByRole("button",{name:"Reject request",exact:true}).click();
    await panel.getByRole("status").filter({hasText:"MSG19: Request rejected."}).waitFor();
  }
  const stale=await page.evaluate(async({path,version})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    return(await fetch(path,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${version}"`},body:JSON.stringify({accept:true,reason:null,deadline:null})})).status;
  },{path,version:snapshot.version});assert.equal(stale,412);
  await panel.getByRole("button",{name:"Reload decision",exact:true}).click();
  await panel.getByText(`Status: ${fixture.accept?"ACCEPTED":"REJECTED"}`,{exact:true}).waitFor();
  assert.equal(await panel.getByRole("button",{name:"Accept request",exact:true}).count(),0);
  checks.push(`${fixture.accept?"Accept with adjusted deadline":"Reject with required reason"} persisted; stale retry denied; reload shows committed state`);
  if(fixture.assign){
    stage="assign accepted request";
    const assignment=page.getByRole("region",{name:"Assign request handler",exact:true});
    const select=assignment.getByLabel("Request handler",{exact:true});await select.waitFor();
    const option=await select.locator('option:not([value=""])').first().getAttribute("value");assert.ok(option);
    const assignmentPath=`/api/internal-requests/${fixture.request}/assignment`;
    const loaded=await page.evaluate(async p=>(await fetch(p)).json(),assignmentPath);
    const guards=await page.evaluate(async({p,v,h})=>{
      const csrf=await(await fetch("/api/auth/csrf")).json();
      const post=async headers=>(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json",...headers},body:JSON.stringify({handlerId:Number(h),note:null})})).status;
      return [await post({"If-Match":`"${v}"`}),await post({"X-CSRF-TOKEN":csrf.csrfToken})];
    },{p:assignmentPath,v:loaded.version,h:option});assert.deepEqual(guards,[400,428]);
    await select.selectOption(option);
    await assignment.getByRole("button",{name:"Assign request handler",exact:true}).click();
    await assignment.getByRole("status").filter({hasText:"MSG08: Handler assigned"}).waitFor();
    const staleAssignment=await page.evaluate(async({p,v,h})=>{
      const csrf=await(await fetch("/api/auth/csrf")).json();
      return(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${v}"`},body:JSON.stringify({handlerId:Number(h),note:null})})).status;
    },{p:assignmentPath,v:loaded.version,h:option});assert.equal(staleAssignment,412);
    await panel.getByRole("button",{name:"Reload decision",exact:true}).click();
    await panel.getByText("Status: IN_PROGRESS",{exact:true}).waitFor();
    checks.push("Accept offers handler assignment; UI assignment commits In Progress; CSRF/version guards and stale retry enforced");
  }
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,errorType:error?.name,checks}));process.exitCode=1;}
finally{await browser?.close();}
