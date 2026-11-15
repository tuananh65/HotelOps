import { chromium } from "@playwright/test";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";
let browser,stage="launch";const checks=[],requests=[];
try{
 browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});const context=await browser.newContext();const page=await context.newPage();page.setDefaultTimeout(15000);
 page.on("response",r=>{const path=new URL(r.url()).pathname;if(/\/(verification|start|submit|source)$/.test(path))requests.push({path,method:r.request().method(),status:r.status()});});
 stage="login";await page.goto("https://localhost:7142/notifications");await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
 await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL("**/notifications");checks.push(stage);
 if(["resubmit","closed"].includes(fixture.mode)){
  stage="open assignee notification";await page.getByRole("button",{name:"Open task",exact:true}).first().click();await page.waitForURL(`**/operational-tasks?branchId=${fixture.branch}&taskId=${fixture.task}`);checks.push(stage);
 }else await page.goto(`https://localhost:7142/operational-tasks?branchId=${fixture.branch}&taskId=${fixture.task}`);
 const panel=page.getByRole("region",{name:"Task verification"});
 if(fixture.mode==="resubmit"){
  stage="Start returned task";await page.getByText("Task execution state: ACCEPTED",{exact:true}).waitFor();await page.getByRole("button",{name:"Start task",exact:true}).click();await page.getByText("Task execution state: IN_PROGRESS",{exact:true}).waitFor();checks.push(stage);
  stage="resubmit corrected work";await page.getByRole("button",{name:"Complete task",exact:true}).click();await page.getByLabel("Result note",{exact:true}).fill(fixture.note);await page.getByLabel("Handover note",{exact:true}).fill("Corrected after Department Head return");await page.getByRole("button",{name:"Submit completion",exact:true}).click();await page.getByRole("status").filter({hasText:"MSG09: Task submitted"}).waitFor();checks.push(stage);
 }else if(fixture.mode==="closed"){
  stage="Closed result remains readable by executor";await panel.getByText("Workflow: CLOSED",{exact:true}).waitFor();await panel.getByText(fixture.note,{exact:true}).waitFor();if(await page.getByRole("button",{name:"Start task",exact:true}).count()||await panel.getByRole("button",{name:"Accept completion",exact:true}).count())throw Error("Closed task still actionable");checks.push(stage);
  stage="closed private evidence";const download=page.waitForEvent("download");await page.getByRole("region",{name:"Submitted completion"}).getByRole("link",{name:"submission.pdf",exact:true}).click();if(await (await download).failure())throw Error("Closed evidence download failed");checks.push(stage);
 }else{
  stage="review result and actual effort";await panel.getByText("Workflow: PENDING_VERIFICATION",{exact:true}).waitFor();await panel.getByText(/Actual effort: \d+ seconds/).waitFor();checks.push(stage);
  stage="validate decision note";await panel.getByRole("button",{name:fixture.mode==="ACCEPT"?"Accept completion":"Return for more work",exact:true}).click();await panel.getByRole("alert").waitFor();checks.push(stage);
  stage="commit "+fixture.mode;await panel.getByLabel("Verification note or return reason",{exact:true}).fill(fixture.note);await panel.getByRole("button",{name:fixture.mode==="ACCEPT"?"Accept completion":"Return for more work",exact:true}).click();await panel.getByRole("status").filter({hasText:fixture.mode==="ACCEPT"?"MSG08: Task closed.":"MSG20: Task returned"}).waitFor();await panel.getByText("Workflow: "+(fixture.mode==="ACCEPT"?"CLOSED":"ACCEPTED"),{exact:true}).waitFor();checks.push(stage);
 }
 stage="reload authoritative outcome";await page.reload();await panel.getByText("Workflow: "+(fixture.mode==="ACCEPT"||fixture.mode==="closed"?"CLOSED":fixture.mode==="RETURN"?"ACCEPTED":"PENDING_VERIFICATION"),{exact:true}).waitFor();checks.push(stage);
 stage="logout";await page.goto("https://localhost:7142/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL("**/login");checks.push(stage);
 console.log(JSON.stringify({success:true,mode:fixture.mode,stage,checks,requests}));
}catch(error){console.log(JSON.stringify({success:false,mode:fixture.mode,stage,checks,requests,error:String(error.message).slice(0,700)}));process.exitCode=1;}finally{await browser?.close();}
