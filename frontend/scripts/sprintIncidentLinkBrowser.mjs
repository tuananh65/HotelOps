import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="direction filters";
  await page.getByRole("button",{name:"Back to requests",exact:true}).click();
  for(const direction of ["SENT","RECEIVED"]){const response=page.waitForResponse(r=>new URL(r.url()).pathname==="/api/internal-requests"&&new URL(r.url()).searchParams.get("direction")===direction);await page.getByLabel("Direction",{exact:true}).selectOption(direction);const reply=await response;assert.equal(reply.status(),200);assert.deepEqual((await reply.json()).items.map(r=>r.id),[fixture.request]);}
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);stage="choose incident";
  const panel=page.getByRole("region",{name:"Linked incidents",exact:true});
  stage="legacy incident";await panel.getByText(/Completion requirement unclassified/).waitFor();
  await panel.getByRole("button",{name:"Link existing incident",exact:true}).click();
  await panel.getByLabel("Open incident",{exact:true}).selectOption(String(fixture.incident));
  await panel.getByLabel("Completion requirement",{exact:true}).selectOption("required");
  const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===`/api/internal-requests/${fixture.request}/linked-incidents`);
  await panel.getByRole("button",{name:"Save incident link",exact:true}).click();assert.equal((await response).status(),200);
  stage="saved link";await panel.getByRole("button",{name:"View linked incidents",exact:true}).click();await panel.getByText(/Required for completion/).waitFor();
  await panel.locator(`a[href="/operations/incidents/${fixture.incident}"]`).click();stage="detail";await page.getByText("Private incident OWN",{exact:true}).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Canonical Sent and Received select current authorized request", "Link existing incident with explicit mandatory flag; saved link opens scoped incident detail"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
