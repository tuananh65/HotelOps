import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
export async function verifyAdvisoryBrowser(fixture,directory,positiveTransfer=null){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const checks=[];
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions[positiveTransfer?'otherManager':'manager'].token);
  const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#stock');await page.getByRole('button',{name:'Dự báo và đề xuất',exact:true}).click({timeout:30000});
  for(const kind of positiveTransfer?['TRANSFER']:['DEMAND','TRANSFER']){
   await page.getByRole('combobox',{name:/^Nội dung/}).selectOption(kind);
   const analyzed=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/advisory'&&r.request().method()==='POST');
   await page.getByRole('button',{name:'Xem phân tích',exact:true}).click();
   const response=await analyzed;assert.equal(response.status(),200);const result=await response.json();
   assert.equal(result.method,'STATISTICAL_RULES');assert.equal(result.externalStatus,'BLOCKED_EXTERNAL_CONFIGURATION');assert.equal(result.requiresHumanDecision,true);
   await expect(page.getByRole('heading',{name:'Thống kê và quy tắc nghiệp vụ — không phải kết quả AI',exact:true})).toBeVisible();
   await expect(page.getByText('AI bên ngoài chưa khả dụng: Chưa bật hoặc chưa đủ cấu hình.',{exact:true})).toBeVisible();
   if(kind==='DEMAND'){
    const estimate=result.evidence.find(r=>r.itemId===fixture.items.fifo);assert.ok(estimate);assert.equal(estimate.usedInLast30Days,0);assert.equal(estimate.expectedDemand,0);assert.equal(estimate.available,3);
   }
   if(positiveTransfer){
    const suggestion=result.evidence.find(r=>r.itemId===positiveTransfer.itemId&&r.sourceWarehouseId===fixture.warehouse&&r.destinationWarehouseId===fixture.otherWarehouse);
    assert.ok(suggestion);assert.equal(suggestion.quantity,positiveTransfer.quantity);
    const row=page.locator('tbody tr').filter({hasText:suggestion.item});await expect(row).toHaveCount(1);
    await expect(row.locator('td').nth(2)).toHaveText(String(positiveTransfer.quantity));
   }
   await page.screenshot({path:directory+`/advisory-${kind.toLowerCase()}${positiveTransfer?'-positive':''}-desktop.png`,fullPage:false});checks.push(kind+' displayed as statistical/rules fallback requiring human decision');
  }
  assert.deepEqual(errors,[]);
  await writeFile(directory+`/advisory${positiveTransfer?'-positive-transfer':''}-browser-results.json`,JSON.stringify({status:'PASS',checks,positiveTransfer,limits:'TEST statistical/rules fallback only; no external provider calls or incident UI coverage.'},null,2));
 }finally{await browser.close();}
}
