import { chromium } from "@playwright/test";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";
let browser,stage="launch";const checks=[];const requests=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const context=await browser.newContext();const page=await context.newPage();page.setDefaultTimeout(15000);page.on("response",r=>{if(/\/(pause|resume)$/.test(new URL(r.url()).pathname))requests.push({action:new URL(r.url()).pathname.split("/").pop(),status:r.status()});});
  stage="canonical login";await page.goto("https://localhost:7142/operational-tasks");
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL("**/operational-tasks");checks.push(stage);
  stage="open paused task";await page.getByLabel("Branch ID").fill(String(fixture.branch));await page.getByRole("button",{name:"Load tasks",exact:true}).click();
  await page.getByRole("button",{name:fixture.code+": Synthetic task 0",exact:true}).click();if(fixture.pause){
    stage="pause using backend reason catalog";
    await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();
    stage="load pause catalog";await page.getByRole("button",{name:"Load pause reasons",exact:true}).click();
    stage="select Other reason";await page.getByRole("option",{name:"Other",exact:true}).waitFor({state:"attached"});await page.getByLabel("Pause reason",{exact:true}).selectOption({label:"Other"});
    stage="enter pause note";await page.getByLabel("Pause note",{exact:true}).fill("Synthetic browser pause note");
    stage="submit pause";await page.getByRole("button",{name:"Pause task",exact:true}).click();
    await page.getByText("Task paused. Working interval closed.",{exact:true}).waitFor();
  }
  await page.getByText("Task execution state: PAUSED",{exact:true}).waitFor();checks.push(stage);
  stage="resume opens working interval";await page.getByRole("button",{name:"Resume task",exact:true}).click();await page.getByText("Task resumed. Working time is now being recorded.",{exact:true}).waitFor();
  await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();checks.push(stage);
  stage="reload preserves resumed state";await page.getByRole("button",{name:"Reload progress (discard edits)",exact:true}).click();await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();
  if(await page.getByRole("button",{name:"Resume task",exact:true}).count())throw new Error("Resume still exposed");checks.push(stage);
  if(fixture.pause){
    for(let cycle=0;cycle<3;cycle++){
      stage="repeated pause/resume cycle "+cycle;
      await page.getByRole("button",{name:"Load pause reasons",exact:true}).click();
      await page.getByRole("option",{name:"Other",exact:true}).waitFor({state:"attached"});
      await page.getByLabel("Pause reason",{exact:true}).selectOption({label:"Other"});
      await page.getByLabel("Pause note",{exact:true}).fill("Synthetic repeated pause "+cycle);
      await page.getByRole("button",{name:"Pause task",exact:true}).click();
      await page.getByText("Task paused. Working interval closed.",{exact:true}).waitFor();
      await page.getByRole("button",{name:"Resume task",exact:true}).click();
      await page.getByText("Task resumed. Working time is now being recorded.",{exact:true}).waitFor();checks.push(stage);
    }
    stage="timeline preserves pause reason and resume";
    await page.getByRole("button",{name:"Refresh",exact:true}).click();
    await page.getByText("Task paused: Other. Synthetic browser pause note",{exact:true}).waitFor();
    await page.getByText("Task resumed.",{exact:true}).first().waitFor();checks.push(stage);
  }
  stage="logout hides task route";await page.goto("https://localhost:7142/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL("**/login");
  await page.goto("https://localhost:7142/operational-tasks");await page.waitForURL("**/login?**");checks.push(stage);
  console.log(JSON.stringify({success:true,stage,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,checks,requests,error:String(error.message).slice(0,600)}));process.exitCode=1;}finally{await browser?.close();}
