import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="login";
try {
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);
  const origin="https://localhost:7142",path=`/api/internal-requests/${fixture.request}/progress`;
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  const panel=page.getByRole("region",{name:"Request progress",exact:true});
  await panel.getByLabel("Progress percent",{exact:true}).waitFor();
  assert.equal(await panel.getByLabel("New deadline (UTC)",{exact:true}).count(),0);
  stage="handler cannot change deadline";
  const denial=await page.evaluate(async p=>{
    const before=await(await fetch(p)).json();const csrf=await(await fetch("/api/auth/csrf")).json();
    const response=await fetch(p,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${before.version}"`},
      body:JSON.stringify({percent:75,note:"Forbidden deadline",deadline:new Date(Date.now()+96*3600000).toISOString(),deadlineReason:"Not authorized"})});
    const after=await(await fetch(p)).json();return {status:response.status,before,after};
  },path);
  assert.equal(denial.status,403);assert.equal(denial.before.version,denial.after.version);assert.equal(denial.before.deadline,denial.after.deadline);
  stage="handler saves result";
  await panel.getByLabel("Progress percent",{exact:true}).fill("75");
  await panel.getByLabel("Progress note",{exact:true}).fill("Synthetic handler progress");
  await panel.getByLabel("Result note",{exact:true}).fill("Synthetic handler result");
  await panel.getByRole("button",{name:"Save progress",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"MSG08: Progress saved"}).waitFor();
  await panel.getByRole("button",{name:"Reload progress",exact:true}).click();
  await panel.getByText("Saved progress: 75%",{exact:true}).waitFor();
  const saved=await page.evaluate(async p=>(await(await fetch(p)).json()),path);
  assert.equal(saved.result,"Synthetic handler result");assert.equal(saved.status,"IN_PROGRESS");assert.equal(saved.deadline,denial.before.deadline);
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["HK_STAFF current handler updates and reloads result without manager mapping","Deadline input absent; forged deadline rejected403 without version or deadline change"]}));
} catch {console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}
finally {await browser?.close();}
