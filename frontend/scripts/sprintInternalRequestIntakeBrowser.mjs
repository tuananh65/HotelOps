import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(origin+"/internal-requests/new");await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="create form";
  await page.getByLabel("Branch",{exact:true}).selectOption(String(fixture.branch));
  await page.getByLabel("Request type",{exact:true}).selectOption(String(fixture.material));
  await page.getByRole("alert").filter({hasText:"Create a material request"}).waitFor();assert.equal(await page.getByRole("button",{name:"Submit request",exact:true}).isDisabled(),true);
  await page.getByLabel("Request type",{exact:true}).selectOption(String(fixture.type));
  await page.getByLabel("Receiving department",{exact:true}).selectOption(String(fixture.department));
  await page.getByLabel("Priority",{exact:true}).selectOption(String(fixture.priority));
  await page.getByLabel("Title",{exact:true}).fill("Synthetic browser intake");await page.getByLabel("Description",{exact:true}).fill("Synthetic non-material service request");
  const due=new Date(Date.now()+86400000+7*3600000);
  await page.getByLabel("Desired deadline (UTC+7)",{exact:true}).fill(due.toISOString().slice(0,16));
  await page.getByLabel("Attachments",{exact:true}).setInputFiles({name:"synthetic.pdf",mimeType:"application/pdf",buffer:Buffer.from("%PDF-1.4\nSynthetic intake evidence\n%%EOF")});
  stage="submit";const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname==="/api/internal-requests/intake");
  await page.getByRole("button",{name:"Submit request",exact:true}).evaluate(b=>{b.click();b.click();});const saved=await response;assert.equal(saved.status(),201);const result=await saved.json();
  await page.getByRole("status").filter({hasText:"MSG09: Request created"}).waitFor();
  stage="open created request";await page.getByRole("link",{name:"Open created request",exact:true}).click();
  await page.getByText("Status: PENDING_ACCEPTANCE",{exact:true}).waitFor();await page.getByText(/CREATE:/).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,requestId:result.requestId,eventId:result.eventId,checks:["Material type advises UC091; non-material request and private attachment created through HTTPS; history Pending Acceptance and CREATE"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
