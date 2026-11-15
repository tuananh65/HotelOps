import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(origin+"/notifications");await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="open creation notification";
  const row=page.getByRole("listitem").filter({has:page.getByLabel(`Select notification ${fixture.notice}`,{exact:true})});
  await row.getByRole("button",{name:"Open request",exact:true}).click();
  await page.waitForURL(origin+`/internal-requests?requestId=${fixture.request}`);stage="accept created request";
  await page.getByLabel("Decision reason",{exact:true}).fill("Synthetic accepted service deadline");
  await page.getByLabel("Adjusted deadline (UTC)",{exact:true}).fill(new Date(Date.now()+172800000).toISOString().slice(0,16));
  const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===`/api/internal-requests/${fixture.request}/decision`);
  await page.getByRole("button",{name:"Accept request",exact:true}).click();assert.equal((await response).status(),200);
  await page.getByText("Status: ACCEPTED",{exact:true}).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["In-app creation notification opens authorized source; mapped receiving manager accepts with deadline; status ACCEPTED"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
