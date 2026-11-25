import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function inventoryIssueBrowser(fixture,document,expectedStatus,directory){
 const name=expectedStatus===200?'fifo-issue':'expired-issue-denied';
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.store.token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const loaded=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/documents/${document.documentId}`&&r.status()===200);
  await page.goto(`http://127.0.0.1:5174/login#transactions?kind=ISSUE&id=${document.documentId}`);
  await loaded;
  await page.getByRole('button',{name:'Xác nhận ghi sổ',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Xác nhận ghi sổ',exact:true});
  await dialog.locator('textarea[name="reason"]').fill('Synthetic TEST inventory browser verification');
  const posted=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/documents/${document.documentId}/actions/post`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await posted,data=await response.json();assert.equal(response.status(),expectedStatus);
  if(expectedStatus===409){
   assert.equal(data.title,'BATCH_AVAILABLE_INSUFFICIENT');
   await expect(page.getByText('Không đủ vật tư thuộc các lô còn hạn và được phép cấp.',{exact:true})).toBeVisible();
   await expect(dialog).toBeVisible();
  }else{assert.ok((data.fields??data).postedAt);await expect(dialog).toHaveCount(0);}
  await page.screenshot({path:`${directory}/${name}-desktop.png`,fullPage:true});
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:`${directory}/${name}-mobile.png`,fullPage:true});
  assert.deepEqual(errors,[]);
  await writeFile(`${directory}/${name}-browser.json`,JSON.stringify({status:'PASS',documentId:document.documentId,httpStatus:response.status(),checks:expectedStatus===200?['Storekeeper posts actual issue through confirmation popup']:['Actual posting denied with BATCH_AVAILABLE_INSUFFICIENT','Human-readable eligible-lot error displayed','Dialog remains open for correction'],limits:'Parent runner verifies FIFO batch quantities or unchanged SQL ledger/header/allocation. Existing TEST session; no password login or SignalR.'},null,2));
  return {status:response.status(),data};
 }catch(error){if(page)await page.screenshot({path:`${directory}/${name}-failure.png`,fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
