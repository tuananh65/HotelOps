import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const directory='artifacts/restructure-v2';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const results=[];
try{
 for(const [name,width,height] of [['desktop',1440,900],['laptop',1140,720],['mobile',390,844],['small-mobile',320,640]]){
  const context=await browser.newContext({viewport:{width,height}});
  const page=await context.newPage(),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/#');
  await expect(page.getByRole('heading',{name:'Đăng nhập',exact:true})).toBeVisible({timeout:30000});
  await expect(page.locator('input[name="username"]')).toHaveAttribute('placeholder','Nhập tên đăng nhập');
  await expect(page.getByLabel('Tên đăng nhập',{exact:true})).toBeVisible();
  await expect(page.getByText(/mã nhân viên/i)).toHaveCount(0);
  const geometry=await page.locator('.login-input-wrap').evaluateAll(wrappers=>wrappers.map(wrapper=>{
   const input=wrapper.querySelector('input'),icon=wrapper.querySelector('svg');
   const i=input.getBoundingClientRect(),s=icon.getBoundingClientRect(),style=getComputedStyle(input);
   const toggle=wrapper.querySelector('button')?.getBoundingClientRect();
   return {height:i.height,gap:i.left+parseFloat(style.paddingLeft)-s.right,iconCenterDelta:Math.abs((s.top+s.bottom-i.top-i.bottom)/2),rightPadding:parseFloat(style.paddingRight),toggleWidth:toggle?.width};
  }));
  assert.equal(geometry.length,2);
  for(const field of geometry){assert.ok(field.height>=48);assert.ok(field.gap>=10);assert.ok(field.iconCenterDelta<1);if(field.toggleWidth)assert.ok(field.rightPadding>=field.toggleWidth+4);}
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  const card=await page.locator('.login-refreshed').boundingBox();
  assert.ok(card.width<=460&&card.x>=0&&card.x+card.width<=width);
  if(height>=720)assert.ok(card.y>=0&&card.y+card.height<=height);
  const password=page.locator('input[name="password"]');
  await password.fill('Layout-only example');
  await page.getByRole('button',{name:'Hiện mật khẩu',exact:true}).click();
  await expect(password).toHaveAttribute('type','text');
  await page.getByRole('button',{name:'Ẩn mật khẩu',exact:true}).click();
  await expect(password).toHaveAttribute('type','password');
  await password.fill('');
  await page.screenshot({path:`${directory}/login-layout-${name}.png`,fullPage:true});
  await page.getByRole('button',{name:'Quên mật khẩu?',exact:true}).click();
  const recovery=page.getByRole('dialog',{name:'Quên mật khẩu',exact:true});
  await expect(recovery.getByLabel('Tên đăng nhập',{exact:true})).toBeVisible();
  await expect(recovery.getByText(/mã nhân viên/i)).toHaveCount(0);
  const send=recovery.getByRole('button',{name:'Gửi liên kết đặt lại',exact:true});
  await expect(send).toBeDisabled();
  const popup=await recovery.boundingBox(),submitBox=await send.boundingBox(),cancelBox=await recovery.getByRole('button',{name:'Hủy',exact:true}).boundingBox();
  assert.ok(popup.width<=420&&popup.height<400&&popup.x>=0&&popup.x+popup.width<=width);
  assert.ok(Math.abs(submitBox.y-cancelBox.y)<1&&submitBox.height>=40&&cancelBox.height>=40);
  assert.ok(cancelBox.x+cancelBox.width<=submitBox.x);
  await page.screenshot({path:`${directory}/recovery-layout-${name}.png`,fullPage:true});
  await recovery.getByRole('button',{name:'Hủy',exact:true}).click();
  await expect(recovery).toHaveCount(0);
  assert.deepEqual(errors,[]);results.push({viewport:name,width,height,geometry,status:'PASS'});
  await context.close();
 }
 await writeFile(directory+'/login-layout-browser.json',JSON.stringify({status:'PASS',checks:results,limits:'Anonymous layout and password visibility only; no login/reset requests or database writes.'},null,2));
 console.log(JSON.stringify({passed:results.length,failed:0}));
}finally{await browser.close();}
