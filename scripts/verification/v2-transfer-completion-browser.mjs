import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyTransferCompletionBrowser(fixture,documentId,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const errors=[],checks=[];let page;
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.otherManager.token);
  page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  const loaded=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/documents/${documentId}`&&r.request().method()==='GET');
  await page.goto(`http://127.0.0.1:5174/login#documents?transfer=1&id=${documentId}`);
  assert.equal((await loaded).status(),200);
  const progress=page.getByRole('region',{name:'Đối soát điều chuyển'});
  await expect(progress.locator('tbody tr')).toHaveCount(1);
  assert.deepEqual((await progress.locator('tbody td').allTextContents()).slice(1),['2','2','2','2','0']);
  assert.equal(await progress.locator('input,select,textarea').count(),0);
  await page.screenshot({path:directory+'/transfer-completed-desktop.png',fullPage:false});
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  assert.match(await progress.locator('tbody td').nth(1).evaluate(cell=>getComputedStyle(cell,'::before').content),/Đề nghị/);
  await page.screenshot({path:directory+'/transfer-completed-mobile.png',fullPage:false});
  assert.deepEqual(errors,[]);
  checks.push({name:'Destination OM sees the same real shortage transfer fully received: 2/2/2/2/0, readonly on desktop and mobile',status:'PASS'});
 }catch(error){checks.push({name:'Completed transfer UI',status:'FAIL',error:error.message});if(page)await page.screenshot({path:directory+'/transfer-completion-failure.png'}).catch(()=>{});throw error;}
 finally{
  try{await browser.close();}finally{await writeFile(directory+'/transfer-completion-browser-results.json',JSON.stringify({at:new Date().toISOString(),documentId,checks,errors,limits:'Read-only UI of the same actual API shortage/advice transfer. SignalR verified separately by business-realtime probe before shipment.'},null,2));}
 }
}
