import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="login";const checks=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);
  const origin="https://localhost:7142",path=`/api/internal-requests/${fixture.request}/progress`;
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByLabel("Progress percent",{exact:true}).waitFor();
  const snapshot=await page.evaluate(async p=>(await fetch(p)).json(),path);
  stage="progress security guards";
  const guards=await page.evaluate(async({p,v})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    const post=async(headers,percent)=>(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json",...headers},body:JSON.stringify({percent,note:null,result:null,deadline:null,deadlineReason:null})})).status;
    return [await post({"If-Match":`"${v}"`},50),await post({"X-CSRF-TOKEN":csrf.csrfToken},50),await post({"If-Match":`"${v}"`,"X-CSRF-TOKEN":csrf.csrfToken},101),
      await post({"If-Match":`"${v}"`,"X-CSRF-TOKEN":csrf.csrfToken},undefined)];
  },{p:path,v:snapshot.version});assert.deepEqual(guards,[400,428,400,400]);
  checks.push("Public progress enforces CSRF, loaded ETag and range validation");
  stage="canonical progress";
  for(const percent of [50,100]){
    stage=`fill percent ${percent}`;
    await panel.getByLabel("Progress percent",{exact:true}).fill(String(percent));
    stage=`fill note ${percent}`;
    await panel.getByLabel("Progress note",{exact:true}).fill("Synthetic progress note");
    stage=`fill result ${percent}`;
    await panel.getByLabel("Result note",{exact:true}).fill("Synthetic result ready for review");
    if(percent===50) await panel.getByLabel("Progress attachments",{exact:true}).setInputFiles({name:"synthetic-proof.png",mimeType:"image/png",
      buffer:Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=","base64")});
    stage=`save ${percent}`;
    await panel.getByRole("button",{name:"Save progress",exact:true}).click();
    stage=`confirm ${percent}`;
    await panel.getByRole("status").filter({hasText:"MSG08: Progress saved"}).waitFor();
    stage=`reload ${percent}`;
    await panel.getByRole("button",{name:"Reload progress",exact:true}).click();
    await panel.getByText(`Saved progress: ${percent}%`,{exact:true}).waitFor();
    await panel.getByLabel("Progress percent",{exact:true}).waitFor();
  }
  const view=await page.evaluate(async p=>(await fetch(p)).json(),path);
  assert.equal(view.status,"IN_PROGRESS");assert.equal(view.percent,100);assert.equal(view.result,"Synthetic result ready for review");
  stage="private evidence download";
  assert.equal(view.evidenceCount,1);assert.equal(view.evidence.length,1);
  await panel.getByRole("link",{name:"synthetic-proof.png",exact:true}).waitFor();
  const file=await page.evaluate(async({p,id})=>{
    const response=await fetch(p);const bytes=new Uint8Array(await response.arrayBuffer());
    return {status:response.status,length:bytes.length,prefix:Array.from(bytes.slice(0,8)),mime:response.headers.get("content-type"),
      disposition:response.headers.get("content-disposition"),nosniff:response.headers.get("x-content-type-options"),
      wrongRequest:(await fetch(`/api/internal-requests/9223372036854775807/progress/evidence/${id}`)).status};
  },{p:view.evidence[0].downloadPath,id:view.evidence[0].id});
  assert.equal(file.status,200);assert.equal(file.length,view.evidence[0].size);assert.deepEqual(file.prefix,[137,80,78,71,13,10,26,10]);
  assert.equal(file.mime,"image/png");assert.equal(file.nosniff,"nosniff");assert.match(file.disposition,/attachment/);assert.equal(file.wrongRequest,404);
  checks.push("Canonical upload persists event-linked private attachment; authorized download matches bytes and wrong source denies");
  const stale=await page.evaluate(async({p,v})=>{
    const csrf=await(await fetch("/api/auth/csrf")).json();
    return(await fetch(p,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${v}"`},body:JSON.stringify({percent:10})})).status;
  },{p:path,v:snapshot.version});assert.equal(stale,412);
  checks.push("Canonical UI saves 50 then 100 percent, reloads persisted result, stays In Progress and rejects stale overwrite");
  stage="scheduled readiness notice";
  let notices;
  for(let attempt=0;attempt<50;attempt++){
    notices=await page.evaluate(async()=>(await fetch("/api/notifications?type=IR_READY&pageSize=100")).json());
    if(notices.items?.length===2)break;await page.waitForTimeout(200);
  }
  assert.equal(notices.items.length,2);
  await page.waitForTimeout(1500);
  const proof=await page.evaluate(async id=>({count:(await(await fetch("/api/notifications?type=IR_READY&pageSize=100")).json()).items.length,
    source:await(await fetch(`/api/notifications/${id}/source`)).json()}),notices.items[0].id);
  assert.equal(proof.count,2);assert.equal(proof.source.path,`/internal-requests?requestId=${fixture.request}`);
  checks.push("Hosted worker delivers both readiness transitions once and requester opens canonical authorized source");
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,errorType:error?.name,checks}));process.exitCode=1;}
finally{await browser?.close();}
