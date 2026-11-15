import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(origin+`/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="required result";
  const panel=page.getByRole("region",{name:"Complete request",exact:true});
  await panel.getByRole("button",{name:"Complete request",exact:true}).click();await panel.getByText("Enter a completion result.",{exact:true}).waitFor();
  await panel.getByLabel("Completion result",{exact:true}).fill("Synthetic browser completion result");stage="close";
  const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===`/api/internal-requests/${fixture.request}/completion`);
  await panel.getByRole("button",{name:"Complete request",exact:true}).evaluate(b=>{b.click();b.click();});
  const saved=await response;assert.equal(saved.status(),200);assert.equal((await saved.json()).status,"CLOSED");
  await page.getByText("MSG08: Request closed.",{exact:true}).waitFor();
  await page.getByText("Synthetic browser completion result",{exact:true}).waitFor();stage="worker notification";
  await page.goto(origin+"/notifications");
  let notice=page.getByRole("listitem").filter({hasText:"Internal request closed"});
  for(let attempt=0;attempt<15&&await notice.count()===0;attempt++){await page.getByRole("button",{name:"Refresh",exact:true}).click();await page.waitForTimeout(1000);}
  await notice.getByRole("button",{name:"Open request",exact:true}).click();stage="open closure source";
  await page.waitForURL(origin+`/internal-requests?requestId=${fixture.request}`);
  await page.getByText("Synthetic browser completion result",{exact:true}).waitFor();
  assert.equal(await page.getByRole("button",{name:"Complete request",exact:true}).count(),0);
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Required result; double-click Close sends one mutation; MSG08 and persisted result; hosted worker notification opens current authorized closure source"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
