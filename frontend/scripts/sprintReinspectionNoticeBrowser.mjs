import { chromium } from "@playwright/test";
let input="";for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input="";let browser,stage="login";
try {
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(30000);
  const origin="https://localhost:7143";
  await page.goto(origin+"/notifications");await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL(origin+"/notifications");
  stage="assigned inspection inbox";
  const item=page.getByRole("listitem").filter({has:page.getByRole("button",{name:`Mark notification ${fixture.notification} as read`,exact:true})});
  await item.getByRole("button",{name:"Open inspection",exact:true}).click();
  await page.waitForURL(`${origin}/reinspections/${fixture.branch}/${fixture.inspection}/execution`);
  await page.getByRole("heading",{name:"Reinspect room",exact:true}).waitFor();
  stage="authorized source reload";
  const path=`/api/branches/${fixture.branch}/reinspections/${fixture.inspection}/execution`;
  const before=await page.evaluate(async path=>{const r=await fetch(path,{credentials:"include"});return{status:r.status,tag:r.headers.get("ETag"),body:await r.json()};},path);
  if(before.status!==200||before.body.id!==fixture.inspection||before.body.state!==fixture.state||!before.tag||!before.body.defects.length)throw Error("Assignment source mismatch");
  await page.reload();await page.getByRole("heading",{name:"Reinspect room",exact:true}).waitFor();
  const after=await page.evaluate(async path=>{const r=await fetch(path,{credentials:"include"});return{status:r.status,tag:r.headers.get("ETag")};},path);
  if(after.status!==200||after.tag!==before.tag)throw Error("Inbox source reload changed inspection");
  process.stdout.write(JSON.stringify({success:true,state:fixture.state,inbox:true,source:true,reload:true}));
} catch(error) {process.stdout.write(JSON.stringify({success:false,stage,diagnostic:String(error.message).split("\n")[0]}));process.exitCode=1;}
finally {await browser?.close();}
