import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(`${origin}/operations/incidents?branchId=${fixture.branch}`);await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="scoped incident list";
  await page.getByText(fixture.title,{exact:true}).waitFor();assert.equal(await page.getByText(fixture.hidden,{exact:true}).count(),0);
  stage="incident detail";await page.locator(`a[href="/operations/incidents/${fixture.incident}"]`).click();
  await page.getByText(fixture.title,{exact:true}).waitFor();await page.getByText("Private incident ASSIGNED",{exact:true}).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Maintenance Staff sees scoped incident list and follows assigned incident detail; unrelated incident absent"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
