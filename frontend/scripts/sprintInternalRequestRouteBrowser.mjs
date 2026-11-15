import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="login";const checks=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);
  const origin="https://localhost:7142",path=`/api/internal-requests/${fixture.request}/route`;
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  const panel=page.getByRole("region",{name:"Route request",exact:true});
  await panel.getByLabel("Target department",{exact:true}).selectOption(String(fixture.target));
  const snapshot=await page.evaluate(async p=>(await fetch(p)).json(),path);
  stage="route security guards";
  const guards=await page.evaluate(async({p,v,target})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    const post=async(headers,reason)=>(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json",...headers},body:JSON.stringify({departmentId:target,reason})})).status;
    return [await post({"If-Match":`"${v}"`},"Synthetic route reason"),await post({"X-CSRF-TOKEN":csrf.csrfToken},"Synthetic route reason"),
      await post({"X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${v}"`},"short")];
  },{p:path,v:snapshot.version,target:fixture.target});assert.deepEqual(guards,[400,428,400]);
  checks.push("Route API enforces CSRF, loaded version and GBR012 reason");
  stage="canonical route";
  await panel.getByRole("button",{name:"Route request",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"at least 10 characters"}).waitFor();
  await panel.getByLabel("Routing reason",{exact:true}).fill("Synthetic route to responsible department");
  await panel.getByRole("button",{name:"Route request",exact:true}).click();
  await page.getByRole("status").filter({hasText:"MSG08: Request routed"}).waitFor();
  const decision=page.getByRole("region",{name:"Request decision",exact:true});
  assert.equal(await decision.getByRole("button",{name:"Accept request",exact:true}).count(),0);
  await decision.getByRole("button",{name:"Reload decision",exact:true}).click();
  await decision.getByText("Status: PENDING_ACCEPTANCE",{exact:true}).waitFor();
  assert.equal(await decision.getByRole("button",{name:"Accept request",exact:true}).count(),0);
  const postRoute=await page.evaluate(async({id,path})=>{
    const view=await(await fetch(`/api/internal-requests/${id}/decision`)).json();
    const csrf=await(await fetch("/api/auth/csrf")).json();
    const denied=await fetch(`/api/internal-requests/${id}/decision`,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${view.version}"`},body:JSON.stringify({accept:true,reason:null,deadline:null})});
    return [view.canDecide,denied.status,(await fetch(path)).status];
  },{id:fixture.request,path});assert.deepEqual(postRoute,[false,403,403]);
  checks.push("Route remains Pending Acceptance; old manager loses UI and API processing authority");
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,errorType:error?.name,checks}));process.exitCode=1;}
finally{await browser?.close();}
