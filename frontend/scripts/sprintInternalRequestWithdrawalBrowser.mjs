import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="withdraw request";
  await page.getByLabel("Withdrawal reason",{exact:true}).fill("No longer needed");
  const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===`/api/internal-requests/${fixture.request}/withdrawal`);
  await page.getByRole("button",{name:"Withdraw request",exact:true}).click();const saved=await response;assert.equal(saved.status(),200);assert.equal((await saved.json()).status,"CANCELLED");
  stage="reload cancelled history";await page.reload();await page.getByText("Status: CANCELLED",{exact:true}).waitFor();
  await page.getByText(/WITHDRAW: PENDING_ACCEPTANCE/).waitFor();assert.equal(await page.getByRole("button",{name:"Withdraw request",exact:true}).count(),0);
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Requester withdraws Pending Acceptance with reason; refreshed timeline is Cancelled and hides Withdraw"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
