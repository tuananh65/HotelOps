import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login",diagnostic;
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.on("response",async r=>{if(new URL(r.url()).pathname.endsWith("/own-time-log")){try{diagnostic={status:r.status(),body:await r.json()};}catch{}}});page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(origin+"/my-time-log");await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="own log";
  await page.getByLabel("Branch ID",{exact:true}).fill(String(fixture.branch));await page.getByLabel("Work date",{exact:true}).fill(fixture.date);
  await page.getByRole("button",{name:"Load time log",exact:true}).click();
  const region=page.getByRole("region",{name:"Own daily time log",exact:true});await region.waitFor();
  assert.equal(await region.locator("tbody tr").count(),fixture.intervalCount);await region.getByText(new RegExp(`Page 1 .* ${fixture.intervalCount} intervals`)).waitFor();
  assert.equal(await region.locator("dl dd").first().textContent(),"0h 10m 0s");
  stage="foreign branch clears data";await page.getByLabel("Branch ID",{exact:true}).fill(String(fixture.foreign));
  assert.equal(await region.count(),0);await page.getByRole("button",{name:"Load time log",exact:true}).click();await page.getByRole("alert").filter({hasText:"no longer have access"}).waitFor();assert.equal(await region.count(),0);
  stage="empty date";await page.getByLabel("Branch ID",{exact:true}).fill(String(fixture.branch));await page.getByLabel("Work date",{exact:true}).fill("1900-01-01");await page.getByRole("button",{name:"Load time log",exact:true}).click();await page.getByText("No work intervals for this day.",{exact:true}).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Canonical own log shows expected intervals and pause-excluded total; branch edit immediately clears data; foreign branch403 renders denial; date change shows empty log; logout"]}));
}catch(error){console.log(JSON.stringify({success:false,stage,diagnostic,error:String(error).slice(0,700)}));process.exitCode=1;}finally{await browser?.close();}
