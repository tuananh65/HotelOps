import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile,mkdir} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const directory='artifacts/local-runtime';await mkdir(directory,{recursive:true});
const report={at:new Date().toISOString(),checks:[],limits:'Anonymous HTTP/HTTPS, native Edge certificate validation, login/recovery UI and API authentication boundaries. No password reset, real email or account password login.'};
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
try{
 for(const [environment,base] of [['Development','http://127.0.0.1:5173'],['Production','https://localhost:5080']]){
  const context=await browser.newContext({viewport:{width:1366,height:900},ignoreHTTPSErrors:false});
  const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
  const response=await page.goto(base+'/login');assert.equal(response.status(),200);
  await expect(page.getByRole('heading',{name:'Đăng nhập',exact:true})).toBeVisible();
  await expect(page.locator('input[name="username"]')).toHaveAttribute('placeholder','Nhập tên đăng nhập');
  await expect(page.getByText(/mã nhân viên/i)).toHaveCount(0);
  const password=page.locator('input[name="password"]');await password.fill('UI-only value');
  await page.getByRole('button',{name:'Hiện mật khẩu',exact:true}).click();await expect(password).toHaveAttribute('type','text');
  await page.getByRole('button',{name:'Ẩn mật khẩu',exact:true}).click();await expect(password).toHaveAttribute('type','password');await password.fill('');
  const fetchPath=(path,method='GET')=>page.evaluate(async({path,method})=>{const r=await fetch(path,{method});return {status:r.status,type:r.headers.get('content-type'),body:await r.text()};},{path,method});
  assert.equal((await fetchPath('/api/v2/auth/me')).status,401);
  assert.equal((await fetchPath('/api/v2/does-not-exist')).status,404);
  assert.equal((await fetchPath('/api/v2/realtime/notifications/negotiate?negotiateVersion=1','POST')).status,401);
  if(environment==='Production'){
   const health=await fetchPath('/health');assert.equal(health.status,200);assert.equal(JSON.parse(health.body).tables,57);
   assert.equal((await fetchPath('/openapi/v1.json')).status,404);
   assert.equal((await fetchPath('/missing-file.js')).status,404);
   assert.equal((await fetchPath('/not-an-api','POST')).status,404);
   assert.equal((await fetchPath('/login','HEAD')).body,'');
   const asset=await page.locator('script[type="module"][src]').first().getAttribute('src');assert.ok(asset.startsWith('/assets/'));
   const js=await fetchPath(asset);assert.equal(js.status,200);assert.match(js.type,/javascript/);
  }
  await page.screenshot({path:`${directory}/${environment.toLowerCase()}-login.png`,fullPage:true});
  await page.setViewportSize({width:390,height:844});
  await page.getByRole('button',{name:'Quên mật khẩu?',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Quên mật khẩu',exact:true});await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button',{name:'Gửi liên kết đặt lại',exact:true})).toBeDisabled();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:`${directory}/${environment.toLowerCase()}-recovery-mobile.png`,fullPage:true});
  await dialog.getByRole('button',{name:'Hủy',exact:true}).click();await expect(dialog).toHaveCount(0);
  assert.deepEqual(errors,[]);
  report.checks.push({environment,url:base,status:'PASS',tlsValidationBypassed:false,apiAnonymous401:true,unknownApi404:true,realtimeAnonymous401:true,loginUi:true,mobileRecovery:true});
  await context.close();
 }
 report.status='PASS';console.log(JSON.stringify(report));
}catch(error){report.status='FAIL';report.error=error.message;throw error;}
finally{await browser.close();await writeFile(directory+'/browser-smoke.json',JSON.stringify(report,null,2));}
