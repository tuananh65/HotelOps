import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";let browser,stage="login";
try {
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(`${origin}/internal-requests?requestId=${fixture.request}`);
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  stage="link existing cleaning";const panel=page.getByRole("region",{name:"Linked cleaning tasks",exact:true});
  await panel.getByRole("button",{name:"Link existing cleaning task",exact:true}).click();
  await panel.getByLabel("Open cleaning task",{exact:true}).selectOption(String(fixture.task));
  await panel.getByLabel("Completion requirement",{exact:true}).selectOption("optional");
  await panel.getByRole("button",{name:"Save cleaning link",exact:true}).click();
  await panel.getByRole("status").filter({hasText:"MSG08: Cleaning task linked"}).waitFor();
  await panel.getByRole("button",{name:"View linked tasks",exact:true}).click();
  const link=panel.getByRole("link").filter({hasText:"Synthetic cleaning link 2"});await link.waitFor();
  assert.match(await link.getAttribute("href"),new RegExp(`^/cleaning/tasks/${fixture.task}\\?branchId=[0-9]+&execution=1$`));
  const row=link.locator("..");assert.match(await row.textContent(),/Optional/);
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Select visible open cleaning task, explicitly choose Optional, save and reload durable link with canonical target"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
