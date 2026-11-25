import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyExpiredShiftSettlement(fixture){
 const actor=fixture.actors.find(a=>a.alias==='hk');
 const route=`/workday/assignments/${actor.assignment}/end`;
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
  const context=await browser.newContext({viewport:{width:390,height:844}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),actor.token);
  page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto('http://127.0.0.1:5174/login#workday');
  await expect(page.locator('.active-shift')).toContainText('Ca đã kết thúc, chờ đối soát',{timeout:30000});
  await page.getByRole('button',{name:'Đối soát và hoàn trả vật tư',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Đối soát vật tư cuối ca',exact:true});
  await expect(dialog).toContainText('giữ nguyên thời điểm kết thúc');
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2'+route&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);
  const body=response.request().postDataJSON(),result=await response.json();
  await expect(dialog).toHaveCount(0);await expect(page.locator('.active-shift')).toHaveCount(0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  assert.deepEqual(errors,[]);
  await page.screenshot({path:'artifacts/restructure-v2/shift-expired-settlement-mobile.png',fullPage:true});
  return {route,body,result};
 }catch(error){if(page)await page.screenshot({path:'artifacts/restructure-v2/shift-settlement-browser-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
