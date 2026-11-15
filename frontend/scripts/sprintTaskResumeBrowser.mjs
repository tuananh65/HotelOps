import { chromium } from "@playwright/test";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="launch";const checks=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const context=await browser.newContext();const page=await context.newPage();page.setDefaultTimeout(15000);
  stage="canonical login";await page.goto("https://localhost:7142/operational-tasks");
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL("**/operational-tasks");checks.push(stage);
  stage="open paused task";await page.getByLabel("Branch ID").fill(String(fixture.branch));await page.getByRole("button",{name:"Load tasks",exact:true}).click();
  await page.getByRole("button",{name:fixture.code+": Synthetic task 0",exact:true}).click();await page.getByText("Task execution state: PAUSED",{exact:true}).waitFor();checks.push(stage);
  stage="resume opens working interval";await page.getByRole("button",{name:"Resume task",exact:true}).click();await page.getByText("Task resumed. Working time is now being recorded.",{exact:true}).waitFor();
  await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();checks.push(stage);
  stage="reload preserves resumed state";await page.getByRole("button",{name:"Reload progress (discard edits)",exact:true}).click();await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();
  if(await page.getByRole("button",{name:"Resume task",exact:true}).count())throw new Error("Resume still exposed");checks.push(stage);
  stage="logout hides task route";await page.goto("https://localhost:7142/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL("**/login");
  await page.goto("https://localhost:7142/operational-tasks");await page.waitForURL("**/login?**");checks.push(stage);
  console.log(JSON.stringify({success:true,stage,checks}));
}catch{console.log(JSON.stringify({success:false,stage,checks}));process.exitCode=1;}finally{await browser?.close();}
