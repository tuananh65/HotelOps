import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

// Only called with the owned isolated TEST administrator session. Never log form secrets.
export async function invitationAdminBrowser(fixture,user,action,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const errors=[];let page;
 try{
  const context=await browser.newContext({viewport:{width:1280,height:900}});
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.token);
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5778';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#users');
  await expect(page.getByRole('heading',{name:'Nhân sự & truy cập',exact:true})).toBeVisible({timeout:30000});
  let result;
  if(action==='create'){
   await page.getByRole('button',{name:'+ Tạo tài khoản',exact:true}).click();
   const dialog=page.getByRole('dialog').filter({has:page.getByRole('heading',{name:'Tạo tài khoản',exact:true})});
   await dialog.locator('[name="username"]').fill(user.username);
   await dialog.locator('[name="fullName"]').fill(user.fullName);
   await dialog.locator('[name="email"]').fill(user.email);
   await dialog.locator('[name="branchId"]').selectOption(String(fixture.branch));
   await dialog.locator('[name="password"]').fill(user.initialPassword);
   const responsePromise=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/users'&&r.request().method()==='POST');
   const refreshedDirectory=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/users'&&r.request().method()==='GET');
   await dialog.getByRole('button',{name:'Tạo tài khoản',exact:true}).click();
   const response=await responsePromise;assert.equal(response.status(),200);
   result=await response.json();user={...user,id:result.fields.userId};
   const directoryResponse=await refreshedDirectory;assert.equal(directoryResponse.status(),200);
   assert.ok((await directoryResponse.json()).some(row=>row.fields.userId===user.id),'Refreshed directory must contain newly created user');
   await expect(dialog).toHaveCount(0);
  }else assert.equal(action,'resend');
  await page.getByPlaceholder('Tên, mã nhân viên, username hoặc email').fill(user.username);
  const row=page.locator('tbody tr').filter({hasText:user.username});
  await expect(row).toHaveCount(1);await row.getByRole('button',{name:'Quản lý',exact:true}).click();
  const invitations=page.locator('section').filter({has:page.getByRole('heading',{name:'Lời mời tài khoản',exact:true})});
  if(action==='create'){
   await expect(invitations).toContainText('Chờ gửi email');
   await expect(invitations).toContainText('BLOCKED_EXTERNAL_CONFIGURATION');
  }else{
   await expect(invitations).toContainText('Đã gửi email');
   const responsePromise=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/invitations/users/${user.id}/resend`&&r.request().method()==='POST');
   await invitations.getByRole('button',{name:'Gửi lại lời mời',exact:true}).click();
   const response=await responsePromise;assert.equal(response.status(),200);result=await response.json();
   await expect(invitations).toContainText('Đã thu hồi');
   await expect(invitations).toContainText(/Chờ gửi email|Đã gửi email/);
  }
  // Account creation form (and its temporary password) has been closed before capture.
  await page.screenshot({path:directory+`/invitation-admin-${action}.png`,fullPage:true});
  assert.deepEqual(errors,[]);
  await writeFile(directory+`/invitation-admin-${action}.json`,JSON.stringify({status:'PASS',action,checks:action==='create'?['Created synthetic account through form','Pending invitation and disabled-email status visible']:['Resent through account management','Revoked invitation and new delivery state visible'],limits:'Owned TEST admin session; local SMTP sink only; SignalR excluded.'},null,2));
  return result;
 }catch(error){
  if(page&&!await page.locator('input[type="password"]').count())await page.screenshot({path:directory+`/invitation-admin-${action}-failure.png`,fullPage:true}).catch(()=>{});
  let safe=String(error.message).split(fixture.token).join('[redacted]');
  if(user.initialPassword)safe=safe.split(user.initialPassword).join('[redacted]');
  throw new Error(safe);
 }finally{await browser.close();}
}
