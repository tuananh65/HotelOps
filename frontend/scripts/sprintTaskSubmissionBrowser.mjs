import { chromium } from "@playwright/test";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";let browser,stage="launch";const checks=[],requests=[];
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const context=await browser.newContext();const page=await context.newPage();page.setDefaultTimeout(15000);
  page.on("response",r=>{const path=new URL(r.url()).pathname;if(/\/(submit|submission|source)$/.test(path))requests.push({path,method:r.request().method(),status:r.status()});});
  stage="canonical login";await page.goto("https://localhost:7142/"+(fixture.mode==="review"?"notifications":"operational-tasks"));
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL(fixture.mode==="review"?"**/notifications":"**/operational-tasks");checks.push(stage);
  if(fixture.mode==="submit"){
    stage="open active task";await page.getByLabel("Branch ID").fill(String(fixture.branch));await page.getByRole("button",{name:"Load tasks",exact:true}).click();
    await page.getByRole("button",{name:fixture.code+": Synthetic task 0",exact:true}).click();await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();checks.push(stage);
    stage="missing required result is actionable";await page.getByRole("button",{name:"Complete task",exact:true}).click();await page.getByRole("button",{name:"Submit completion",exact:true}).click();
    await page.getByRole("alert").filter({hasText:"MSG03: Record results for required criteria: Required task result."}).waitFor();checks.push(stage);
    stage="enter result handover and evidence";await page.getByLabel("Criterion result: Required task result",{exact:true}).selectOption("PASS");
    await page.getByLabel("Criterion result: Optional task result",{exact:true}).selectOption("FAIL");
    await page.getByLabel("Result note",{exact:true}).fill("Browser completion result\nVerified operational output");
    await page.getByLabel("Handover note",{exact:true}).fill("Browser handover\nReady for Department Head review");
    await page.getByLabel("Evidence files (up to 5, PNG/JPEG/PDF, 10 MB each)",{exact:true}).setInputFiles({name:"submission.pdf",mimeType:"application/pdf",buffer:Buffer.from("%PDF-1.4\nSynthetic task submission evidence\n%%EOF")});checks.push(stage);
    stage="submit and lock pending state";await page.getByRole("button",{name:"Submit completion",exact:true}).click();
    await page.getByRole("status").filter({hasText:"MSG09: Task submitted for verification. Working interval closed."}).waitFor();
    await page.getByText("Task execution state: PENDING_VERIFICATION",{exact:true}).waitFor();
    if(await page.getByRole("button",{name:"Save progress",exact:true}).isEnabled()||await page.getByRole("button",{name:"Pause task",exact:true}).count())throw new Error("Pending task remains editable");checks.push(stage);
    stage="reload preserves snapshot and notes";await page.getByRole("button",{name:"Reload submitted completion",exact:true}).click();
    await page.getByText("Browser completion result\nVerified operational output",{exact:true}).waitFor();await page.getByText("Browser handover\nReady for Department Head review",{exact:true}).waitFor();
    await page.getByRole("button",{name:"Reload progress (discard edits)",exact:true}).click();await page.getByText("Task execution state: PENDING_VERIFICATION",{exact:true}).waitFor();checks.push(stage);
  }else{
    stage="inbox opens canonical task snapshot";await page.getByRole("button",{name:"Open task",exact:true}).first().click();await page.waitForURL(`**/operational-tasks?branchId=${fixture.branch}&taskId=${fixture.task}`);
    await page.getByText("Browser completion result\nVerified operational output",{exact:true}).waitFor();await page.getByText("Browser handover\nReady for Department Head review",{exact:true}).waitFor();checks.push(stage);
    stage="manager private evidence download";const region=page.getByRole("region",{name:"Submitted completion"});
    const downloadPromise=page.waitForEvent("download");await region.getByRole("link",{name:"submission.pdf",exact:true}).click();const download=await downloadPromise;
    if(download.suggestedFilename()!=="submission.pdf"||await download.failure())throw new Error("Private manager download failed");checks.push(stage);
    stage="reload retains manager read access";await page.reload();await page.getByText("Browser handover\nReady for Department Head review",{exact:true}).waitFor();checks.push(stage);
  }
  stage="logout";await page.goto("https://localhost:7142/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL("**/login");checks.push(stage);
  console.log(JSON.stringify({success:true,mode:fixture.mode,stage,checks,requests}));
}catch(error){console.log(JSON.stringify({success:false,mode:fixture.mode,stage,checks,requests,error:String(error.message).slice(0,800)}));process.exitCode=1;}finally{await browser?.close();}
