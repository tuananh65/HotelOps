import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
let input=""; for await(const chunk of process.stdin) input+=chunk;
const fixture=JSON.parse(input); input="";
let browser,stage="launch"; const checks=[];
try {
  browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
  const context=await browser.newContext(), observer=await browser.newContext();
  const page=await context.newPage(); page.setDefaultTimeout(15000);
  const origin="https://localhost:7142";
  // Requests run in Edge with normal certificate validation and the UI cookie jar.
  async function client(context) {
    const transport=await context.newPage(); await transport.goto(origin+"/login");
    const send=async(method,url,options={})=>{
      const result=await transport.evaluate(async({method,url,options})=>{
        const response=await fetch(url,{method,credentials:"same-origin",headers:{"Content-Type":"application/json",...options.headers},...(method==="POST"?{body:JSON.stringify(options.data)}:{})});
        return {status:response.status,text:await response.text()};
      },{method,url,options});
      return {status:()=>result.status,json:async()=>JSON.parse(result.text)};
    };
    return {get:url=>send("GET",url),post:(url,options)=>send("POST",url,options)};
  }
  const mainApi=await client(context), observerApi=await client(observer);
  async function api(request,path,body,csrf=true) {
    const token=await (await request.get(origin+"/api/auth/csrf")).json();
    return request.post(origin+path,{data:body,headers:csrf?{"X-CSRF-TOKEN":token.csrfToken}:{}});
  }
  stage="parallel temporary session";
  assert.equal((await api(observerApi,"/api/auth/login",{username:fixture.username,password:fixture.password})).status(),200);
  stage="temporary login";
  await page.goto(origin+"/login");
  await page.getByLabel("Username or email").fill(fixture.email);
  await page.getByLabel("Password",{exact:true}).fill(fixture.password);
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  await page.waitForURL(origin+"/change-password");
  const before=await context.cookies();
  assert.ok(before.some(c=>c.httpOnly&&c.secure));
  assert.equal((await mainApi.get(origin+"/api/users")).status(),403);
  const request={currentPassword:fixture.password,newPassword:fixture.newPassword,confirmNewPassword:fixture.newPassword};
  assert.equal((await api(mainApi,"/api/auth/change-password",request,false)).status(),400);
  for(const data of [{...request,newPassword:"weak",confirmNewPassword:"weak"},{...request,confirmNewPassword:"mismatch"},{...request,newPassword:fixture.password,confirmNewPassword:fixture.password}])
    assert.equal((await api(mainApi,"/api/auth/change-password",data)).status(),400);
  checks.push("temporary email login restricts business access; CSRF, weak, mismatched and unchanged passwords rejected");
  async function fill(current,next) {
    await page.getByLabel("Current password",{exact:true}).fill(current);
    await page.getByLabel("New password",{exact:true}).fill(next);
    await page.getByLabel("Confirm new password",{exact:true}).fill(next);
    await page.getByRole("button",{name:"Change password",exact:true}).click();
  }
  stage="unmet password policy feedback";
  await fill(fixture.password,"weak");
  await page.getByRole("alert").filter({hasText:"Add an uppercase letter. Add a digit. Add a special character."}).waitFor();
  await fill(fixture.password,fixture.password);
  await page.getByRole("alert").filter({hasText:"Choose a password different from your current password."}).waitFor();
  checks.push("UI lists unmet password requirements and same-password rejection");
  stage="wrong current password";
  await fill("wrong-current-value",fixture.newPassword);
  await page.getByRole("alert").filter({hasText:"current password is incorrect"}).waitFor();
  stage="temporary promotion";
  await fill(fixture.password,fixture.newPassword);
  await page.waitForURL(origin+"/profile");
  await page.getByRole("status").filter({hasText:"Password changed successfully"}).waitFor();
  assert.equal((await (await mainApi.get(origin+"/api/auth/session")).json()).mustChangePassword,false);
  assert.equal((await observerApi.get(origin+"/api/auth/session")).status(),401);
  const replay=await browser.newContext();await replay.addCookies(before);
  const replayApi=await client(replay); assert.equal((await replayApi.get(origin+"/api/auth/session")).status(),401); await replay.close();
  checks.push("canonical forced change promotes session, confirms success and revokes both old own and other sessions");
  stage="ordinary password change";
  await page.getByRole("link",{name:"Change password",exact:true}).click();
  await fill(fixture.newPassword,fixture.finalPassword);
  await page.waitForURL(origin+"/profile");
  await page.getByRole("status").filter({hasText:"Password changed successfully"}).waitFor();
  stage="public logout";
  await page.getByRole("button",{name:"Logout",exact:true}).click();
  await page.waitForURL(origin+"/login");
  assert.equal((await mainApi.get(origin+"/api/auth/session")).status(),401);
  assert.equal((await api(mainApi,"/api/auth/logout",{})).status(),204);
  checks.push("ordinary change and Logout work through UI; ended session denied; repeated logout succeeds");
  stage="new credential effective";
  assert.equal((await api(mainApi,"/api/auth/login",{username:fixture.username,password:fixture.newPassword})).status(),401);
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password",{exact:true}).fill(fixture.finalPassword);
  await page.getByRole("button",{name:"Sign in",exact:true}).click();
  await page.waitForURL(origin+"/dashboard");
  await page.goto(origin+"/profile"); await page.getByRole("button",{name:"Logout",exact:true}).click();await page.waitForURL(origin+"/login");
  checks.push("old credential rejected; final credential logs in and public Logout ends it");
  console.log(JSON.stringify({success:true,checks}));
} catch(error) {console.log(JSON.stringify({success:false,stage,errorType:error?.name,checks}));process.exitCode=1;}
finally {await browser?.close();}
