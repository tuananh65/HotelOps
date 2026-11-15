import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="request cleaning prefill";
  await page.getByRole("link",{name:"Create new cleaning task",exact:true}).click();
  await page.getByLabel("Request completion requirement",{exact:true}).waitFor();
  assert.equal(await page.locator('input[name="instruction"]').inputValue(),fixture.instruction);
  assert.ok(await page.locator('input[name="dueAt"]').inputValue());
  await page.locator('select[name="cleaningTypeId"]').selectOption(String(fixture.cleaningType));
  await page.getByLabel("Request completion requirement",{exact:true}).selectOption("required");
  stage="create linked cleaning";const response=page.waitForResponse(r=>r.request().method()==="POST"&&new URL(r.url()).pathname===`/api/branches/${fixture.branch}/cleaning-tasks`);
  await page.getByRole("button",{name:"Save and Assign",exact:true}).click();const saved=await response;assert.equal(saved.status(),201);const task=(await saved.json()).id;
  await page.waitForURL(`${origin}/cleaning/assignment?branchId=${fixture.branch}&taskId=${task}`);
  stage="reload source link";await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  const panel=page.getByRole("region",{name:"Linked cleaning tasks",exact:true});const link=panel.locator(`a[href^="/cleaning/tasks/${task}?"]`);await link.waitFor();assert.match(await link.locator("..").textContent(),/Required for completion/);
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,task,checks:["Request prefill uses UC028 form; explicit Required; Save and Assign creates cleaning and reloads durable source link"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
