import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
let input="";for await(const chunk of process.stdin)input+=chunk;const fixture=JSON.parse(input);input="";
let browser,stage="login";
try{
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(15000);const origin="https://localhost:7142";
  await page.goto(origin+"/notifications");
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();stage="hosted handover notification";
  const notices=page.getByRole("listitem").filter({hasText:"Request handover"});
  for(let attempt=0;attempt<15&&await notices.count()===0;attempt++){await page.getByRole("button",{name:"Refresh",exact:true}).click();await page.waitForTimeout(1000);}
  const source=page.waitForResponse(r=>r.request().method()==="GET"&&/\/api\/notifications\/\d+\/source$/.test(new URL(r.url()).pathname));
  await notices.first().getByRole("button",{name:"Open request",exact:true}).click();assert.equal((await source).status(),200);
  stage="authorized source";await page.waitForURL(origin+`/internal-requests?requestId=${fixture.request}`);
  await page.getByRole("region",{name:"Request handover",exact:true}).waitFor();
  stage="logout";await page.goto(origin+"/profile");await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  console.log(JSON.stringify({success:true,checks:["Hosted handover notification visible; canonical Open request resolves HTTPS source200; current handover panel loads; logout"]}));
}catch{console.log(JSON.stringify({success:false,stage}));process.exitCode=1;}finally{await browser?.close();}
