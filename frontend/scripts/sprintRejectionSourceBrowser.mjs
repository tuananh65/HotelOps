import { chromium } from "@playwright/test";
let input=""; for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input); input="";
let browser,stage="login";
try {
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const page=await browser.newPage();page.setDefaultTimeout(30000);
  const origin="https://localhost:7142",destination=`/inspection-history/${fixture.inspection}?branchId=${fixture.branch}`;
  await page.goto(origin+"/notifications");await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);await page.getByLabel("Password",{exact:true}).fill(fixture.password);fixture.password="";
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await page.waitForURL(origin+"/notifications");
  await page.getByRole("button",{name:"Open inspection",exact:true}).waitFor();
  stage="resolve current notification";
  const source=await page.evaluate(async id=>{const r=await fetch(`/api/notifications/${id}/source`,{credentials:"include"});return{status:r.status,body:await r.json()};},fixture.notice);
  if(fixture.denied){
    if(source.status!==404)throw Error("Revoked notification source allowed");
    await page.getByRole("button",{name:"Open inspection",exact:true}).click();
    await page.getByText("Source is unavailable or access has changed.",{exact:true}).waitFor();
    const status=await page.evaluate(async ({inspection,branch})=>(await fetch(`/api/inspection-history/${inspection}?branchId=${branch}`,{credentials:"include"})).status,fixture);
    if(status!==403)throw Error("Revoked history API allowed: "+status);
  }else{
    if(source.status!==200||source.body.path!==destination)throw Error("Noncanonical source");
    await page.getByRole("button",{name:"Open inspection",exact:true}).click();await page.waitForURL(origin+destination);
    stage="history content reload";
    await page.getByRole("heading",{name:"D3-EVIDENCE",exact:true}).waitFor();await page.reload();
    await page.getByRole("heading",{name:"D3-EVIDENCE",exact:true}).waitFor();
    if(await page.getByRole("button",{name:/Approve|Reject|Confirm conclusion|Start cleaning/}).count())throw Error("Read-only history exposes mutation");
    const checked=await page.evaluate(async ({inspection,branch,notice})=>{
      const path=`/api/inspection-history/${inspection}?branchId=${branch}`,r=await fetch(path,{credentials:"include"}),d=await r.json();
      if(r.status!==200||d.inspection.id!==inspection||d.inspection.branchId!==branch||d.inspection.result!=="FAIL"||!d.defects.length||!d.evidence.length)throw Error("History source mismatch");
      for(const file of d.evidence){const photo=await fetch(`/api/inspection-history/${inspection}/evidence/${file.id}?branchId=${branch}`,{credentials:"include"});if(photo.status!==200||photo.headers.get("X-Content-Type-Options")!=="nosniff"||!(await photo.arrayBuffer()).byteLength)throw Error("Private photo unavailable");}
      const foreign=await fetch(`/api/inspection-history/${inspection}/evidence/9223372036854775807?branchId=${branch}`,{credentials:"include"});
      if(foreign.status!==404)throw Error("Foreign evidence allowed");
      const missing=await fetch(`/api/notifications/9223372036854775807/source`,{credentials:"include"});if(missing.status!==404)throw Error("Unowned notification source allowed");
      return{photo:d.evidence[0].id};
    },fixture);
    const anonymous=await browser.newContext();
    try {const guest=await anonymous.newPage();await guest.goto(origin+"/login");const status=await guest.evaluate(async path=>(await fetch(path,{credentials:"omit"})).status,`/api/inspection-history/${fixture.inspection}/evidence/${checked.photo}?branchId=${fixture.branch}`);if(status!==401)throw Error("Anonymous evidence allowed");}finally{await anonymous.close();}
  }
  process.stdout.write(JSON.stringify({success:true,denied:fixture.denied,historyReload:!fixture.denied,privateEvidence:!fixture.denied}));
}catch(error){process.stdout.write(JSON.stringify({success:false,stage,diagnostic:String(error.message).split("\n")[0]}));process.exitCode=1;}
finally{await browser?.close();}
