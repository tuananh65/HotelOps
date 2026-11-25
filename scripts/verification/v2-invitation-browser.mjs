import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyInvitationBrowser(user,invitationToken,newPassword,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const errors=[];let page;
 try{
  const context=await browser.newContext({viewport:{width:390,height:844}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5778';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  context.on('page',p=>p.on('pageerror',e=>errors.push(e.message)));
  page=await context.newPage();await page.goto('http://127.0.0.1:5174/login');
  await page.getByLabel('Tên đăng nhập',{exact:true}).fill(user.username);
  await page.locator('#login-password').fill(user.initialPassword);
  const loginResponse=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/auth/login'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  const login=await loginResponse;assert.equal(login.status(),200);assert.equal((await login.json()).requiresPasswordChange,true);
  await expect(page.getByRole('heading',{name:'Đổi mật khẩu',exact:true})).toBeVisible();
  const token=await page.evaluate(()=>sessionStorage.getItem('hotelops.compact.session'));assert.ok(token);
  const second=await context.newPage();await second.goto('http://127.0.0.1:5174/login#requests');
  await second.evaluate(t=>sessionStorage.setItem('hotelops.compact.session',t),token);await second.reload();
  await expect(second.getByRole('heading',{name:'Đổi mật khẩu',exact:true})).toBeVisible();
  const denied=await second.evaluate(async()=>{
   const r=await fetch('/api/v2/users/creation-options',{headers:{Authorization:'Bearer '+sessionStorage.getItem('hotelops.compact.session')}});return {status:r.status,body:await r.json()};
  });assert.equal(denied.status,403);assert.equal(denied.body.title,'PASSWORD_CHANGE_REQUIRED');
  await second.screenshot({path:directory+'/invitation-forced-password-mobile.png',fullPage:true});
  page=await context.newPage();await page.goto('http://127.0.0.1:5174/login#setup-password?token='+invitationToken);
  await page.getByLabel('Mật khẩu mới',{exact:true}).fill(newPassword);
  await page.getByLabel('Nhập lại mật khẩu',{exact:true}).fill(newPassword);
  const setupResponse=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/invitations/setup-password'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Lưu mật khẩu',exact:true}).click();
  const response=await setupResponse;assert.equal(response.status(),200);assert.equal((await response.json()).signInRequired,true);
  await expect(page.getByText('Đã thiết lập mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới.')).toBeVisible();
  assert.equal(new URL(page.url()).hash,'#setup-password','Token must be removed from URL after use');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  const revoked=await second.evaluate(async()=>{
   const r=await fetch('/api/v2/auth/me',{headers:{Authorization:'Bearer '+sessionStorage.getItem('hotelops.compact.session')}});return r.status;
  });assert.equal(revoked,401,'Previously authenticated tab must lose authorization');
  await second.locator('#login-password').fill(user.initialPassword);
  await second.getByLabel('Mật khẩu mới',{exact:true}).fill(newPassword);
  const expiredResponse=second.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/auth/password'&&r.request().method()==='POST');
  await second.getByRole('button',{name:'Lưu mật khẩu',exact:true}).click();
  assert.equal((await expiredResponse).status(),401);
  await expect(second.getByRole('heading',{name:'Đăng nhập',exact:true})).toBeVisible();
  assert.equal(await second.evaluate(()=>sessionStorage.getItem('hotelops.compact.session')),null);
  await page.screenshot({path:directory+'/invitation-setup-complete-mobile.png',fullPage:true});
  await page.getByRole('link',{name:'Đăng nhập',exact:true}).click();
  await page.getByLabel('Tên đăng nhập',{exact:true}).fill(user.username);
  await page.locator('#login-password').fill(newPassword);
  const finalLogin=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/auth/login'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  const final=await finalLogin;assert.equal(final.status(),200);assert.equal((await final.json()).requiresPasswordChange,false);
  assert.deepEqual(errors,[]);
  await writeFile(directory+'/invitation-browser-results.json',JSON.stringify({status:'PASS',checks:['Temporary-password login forces change in two tabs','Business API blocked before password change','SMTP setup token changes password through mobile UI','Token removed from URL; old tab receives 401 through UI, clears session and returns to login','New password login succeeds without first-change requirement'],limits:'Synthetic TEST accounts and local SMTP sink only; invitation administrator creation/resend UI and SignalR excluded.'},null,2));
 }catch(error){
  // Do not save a screenshot or URL containing a live setup token or password.
  throw new Error(String(error.message).split(invitationToken).join('[redacted]').split(newPassword).join('[redacted]').split(user.initialPassword).join('[redacted]'));
 }finally{await browser.close();}
}
