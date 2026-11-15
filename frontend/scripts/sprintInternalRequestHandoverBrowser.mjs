import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,page,stage="start";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142",path=`/api/internal-requests/${fixture.request}/handover`;
  async function login(username){await page.goto(origin+`/internal-requests?requestId=${fixture.request}`);await page.getByLabel("Username or email").fill(username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);await page.getByRole("button",{name:"Sign in",exact:true}).click();}
  async function logout(){await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");}
  const submitted=[];
  for(const acknowledge of [false,true]){
    stage="sender submit";await login(fixture.username);
    const panel=page.getByRole("region",{name:"Request handover",exact:true});
    stage="sender select receiver";
    await panel.getByLabel("New handler",{exact:true}).selectOption(String(fixture.receiver));
    stage="sender fill summary";await panel.getByLabel("Status summary",{exact:true}).fill("Synthetic current status summary");await panel.getByLabel("Handover notes",{exact:true}).fill("Synthetic handover instructions");
    const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===path);
    stage="sender submit POST";await panel.getByRole("button",{name:"Submit handover",exact:true}).evaluate(b=>{b.click();b.click();});const save=await response;assert.equal(save.status(),200);const pending=await save.json();submitted.push(pending.handoverId);
    await panel.getByRole("status").filter({hasText:"PENDING_ACKNOWLEDGMENT"}).waitFor();await logout();
    stage=acknowledge?"receiver acknowledge":"receiver reject";await login(fixture.receiverName);
    const receiverPanel=page.getByRole("region",{name:"Request handover",exact:true});
    if(!acknowledge){await receiverPanel.getByRole("button",{name:"Reject handover",exact:true}).click();await receiverPanel.getByRole("status").filter({hasText:"rejection reason"}).waitFor();await receiverPanel.getByLabel("Handover rejection reason",{exact:true}).fill("Synthetic cannot take this request");}
    const decision=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===path+"/decision");
    await receiverPanel.getByRole("button",{name:acknowledge?"Acknowledge handover":"Reject handover",exact:true}).click();const result=await decision;assert.equal(result.status(),200);assert.equal((await result.json()).state,acknowledge?"ACKNOWLEDGED":"REJECTED");
    await receiverPanel.getByRole("status").filter({hasText:"MSG08"}).waitFor();await logout();
  }
  console.log(JSON.stringify({success:true,submitted,checks:["Sender submits; designated receiver rejects with reason; sender resubmits; designated receiver acknowledges; all through canonical UI and CSRF/ETag HTTPS API"]}));
}catch{
  let diagnostic;
  try{diagnostic=await page.evaluate(async id=>{const r=await fetch(`/api/internal-requests/${id}/handover`);const v=await r.json();return{status:r.status,keys:Object.keys(v),canSubmit:v.canSubmit,canDecide:v.canDecide,receivers:v.receivers?.length,handoverId:v.handoverId,state:v.state,version:v.version,summary:v.summary,note:v.note};},fixture.request);}catch{}
  console.log(JSON.stringify({success:false,stage,diagnostic}));process.exitCode=1;
}finally{await browser?.close();}
