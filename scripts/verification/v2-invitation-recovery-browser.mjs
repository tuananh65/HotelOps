import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function recoveryBrowser(user,newPassword,receive,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let recoveryToken;
 try{
  const context=await browser.newContext({viewport:{width:390,height:844}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   url.hostname='127.0.0.1';url.port='5778';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}
   catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  const page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto('http://127.0.0.1:5174/login');
  const identifier=page.getByLabel('Tên đăng nhập',{exact:true});
  await expect(identifier).toHaveAttribute('placeholder','Nhập tên đăng nhập');
  const password=page.locator('#login-password');await password.fill('Visibility-only!23');
  await expect(password).toHaveAttribute('type','password');
  await page.getByRole('button',{name:'Hiện mật khẩu',exact:true}).click();
  await expect(password).toHaveAttribute('type','text');await expect(password).toHaveValue('Visibility-only!23');
  await page.getByRole('button',{name:'Ẩn mật khẩu',exact:true}).click();
  await expect(password).toHaveAttribute('type','password');await password.clear();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:directory+'/login-refresh-mobile.png',fullPage:true});
  await page.getByRole('button',{name:'Quên mật khẩu?',exact:true}).click();
  const dialog=page.getByRole('dialog');
  await dialog.getByLabel('Tên đăng nhập',{exact:true}).fill(user.username);
  const response=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/invitations/forgot-password'&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Gửi liên kết đặt lại',exact:true}).click();assert.equal((await response).status(),200);
  await expect(dialog.getByRole('status')).toContainText('Nếu tài khoản có email hợp lệ');
  recoveryToken=(await receive()).token;
  await page.goto('http://127.0.0.1:5174/login#setup-password?token='+recoveryToken);
  await page.getByLabel('Mật khẩu mới',{exact:true}).fill(newPassword);
  await page.getByLabel('Nhập lại mật khẩu',{exact:true}).fill(newPassword);
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/invitations/setup-password'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Lưu mật khẩu',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(page.getByRole('link',{name:'Đăng nhập',exact:true})).toBeVisible();
  await page.getByRole('link',{name:'Đăng nhập',exact:true}).click();
  await page.getByLabel('Tên đăng nhập',{exact:true}).fill(user.username);
  await page.locator('#login-password').fill(newPassword);
  const logged=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/auth/login'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  const result=await logged;assert.equal(result.status(),200);assert.equal((await result.json()).requiresPasswordChange,false);
  assert.deepEqual(errors,[]);
 }catch(error){throw new Error(String(error.message).replaceAll(newPassword,'[redacted]').replaceAll(recoveryToken??'never-a-token','[redacted]'));}
 finally{await browser.close();}
}
