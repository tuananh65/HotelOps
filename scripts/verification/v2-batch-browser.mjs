import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
export async function verifyBatchBrowser(fixture,batch,entries,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.store.token);
  const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#stock');await page.getByRole('button',{name:'Lô và hạn sử dụng',exact:true}).click();
  await page.getByRole('textbox',{name:'Tìm vật tư, mã lô hoặc kho',exact:true}).fill(batch.lotNumber);
  const list=page.getByRole('heading',{name:'Lô vật tư và hạn sử dụng',exact:true}).locator('..');
  await expect(list.locator('tbody tr')).toHaveCount(1);await expect(list.locator('tbody tr')).toContainText(batch.lotNumber);
  await list.getByRole('button',{name:'Lịch sử lô',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Lịch sử lô '+batch.lotNumber,exact:true});
  await expect(dialog.locator('tbody tr')).toHaveCount(Math.min(25,entries.length));
  const displayed=await dialog.locator('tbody tr').evaluateAll(rows=>rows.map(r=>r.querySelector('td[data-label="Số lượng"]').textContent));
  assert.deepEqual(displayed,entries.slice(0,25).map(e=>String(e.quantity)));
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  await expect(dialog.getByRole('button',{name:'Tiếp',exact:true})).toBeDisabled();
  await page.screenshot({path:directory+'/batch-history-mobile.png',fullPage:false});
  await dialog.getByRole('button',{name:'Đóng',exact:true}).click();
  await page.getByRole('textbox',{name:'Tìm vật tư, mã lô hoặc kho',exact:true}).fill(fixture.prefix+'-NOT-FOUND');
  await expect(page.getByText('Không tìm thấy vật tư phù hợp.',{exact:true})).toBeVisible();
  assert.deepEqual(errors,[]);
  await writeFile(directory+'/batch-browser-results.json',JSON.stringify({status:'PASS',checks:['Server search isolates the actual lot','History matches real ledger order and quantities','390px dialog has no horizontal overflow','No-match search shows empty state'],limits:'TEST sessions; history has fewer than 25 entries. API probe checks page boundaries separately.'},null,2));
 }finally{await browser.close();}
}
