import { chromium } from "@playwright/test";
import { randomUUID } from "node:crypto";
let input="";
for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);
  const origin="https://localhost:7143",entry=`/inspections/cleaning-tasks/${fixture.task}/assign?branchId=${fixture.branch}`;
  const api=`/api/branches/${fixture.branch}/cleaning-tasks/${fixture.task}`;
  await page.goto(origin+entry);await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL(origin+entry);
  async function request(url,init={}){
    return page.evaluate(async({url,init})=>{const r=await fetch(url,{credentials:"include",cache:"no-store",...init});return{status:r.status,etag:r.headers.get("ETag"),body:await r.json()};},{url,init});
  }
  const before=await request(api+"/initial-inspection-options");
  if(before.status!==200)throw Error("Options denied");
  const options=before.body;
  stage=fixture.mode==="checklist"?"MSG03 missing published checklist":"MSG22 cleaning attribution conflict";
  if(fixture.mode==="checklist"){
    await page.getByText("MSG03: No effective published checklist is available for this room type. Publish an eligible checklist before assignment.",{exact:true}).waitFor();
    if(options.canAssign||options.unavailableReason!=="CHECKLIST_UNAVAILABLE"||await page.getByRole("button",{name:"Review assignment",exact:true}).count())throw Error("Unpublished checklist admitted");
  }else{
    await page.getByLabel("Inspector",{exact:true}).waitFor();
    if(options.inspectors.some(x=>x.id===fixture.other))throw Error("Cleaner offered as inspector");
  }
  const csrf=(await request("/api/auth/csrf")).body.csrfToken;
  const rejected=await request(api+"/inspections",{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf,"If-Match":before.etag,"Idempotency-Key":randomUUID()},body:JSON.stringify({
    inspectorId:fixture.other,checklistVersionId:fixture.version,cycleETag:options.cycleETag,dueAt:new Date(Date.now()+3600000).toISOString(),reason:"Synthetic exception verification"
  })});
  if(rejected.status!==400||rejected.body.messageCode!==(fixture.mode==="checklist"?"MSG03":"MSG22")
    ||rejected.body.title!==(fixture.mode==="checklist"?"CHECKLIST_UNAVAILABLE":"INSPECTOR_CLEANER_CONFLICT"))throw Error("Exception response mismatch: "+JSON.stringify(rejected));
  const after=await request(api+"/initial-inspection-options");
  if(after.etag!==before.etag||JSON.stringify(after.body)!==JSON.stringify(before.body))throw Error("Rejected command changed source");
  console.log(JSON.stringify({success:true,mode:fixture.mode,status:rejected.status,messageCode:rejected.body.messageCode,sourceUnchanged:true,uiGuard:true}));
}catch(error){console.log(JSON.stringify({success:false,stage,failure:error.message}));process.exitCode=1;}
finally{await browser?.close();}
