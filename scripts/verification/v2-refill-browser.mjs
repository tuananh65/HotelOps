import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyRefillBrowser(fixture,allocationId,itemId,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page,requestId;const checks=[];
 try{
  for(const role of ['hk','supervisor','store']){
   const context=await browser.newContext({viewport:{width:1440,height:1000}});
   await context.route('**/api/**',async route=>{
    const url=new URL(route.request().url());
    if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
    url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
    try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
   });
   await context.routeWebSocket('**/*',socket=>socket.close());
   await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias===role).token);
   page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.goto('http://127.0.0.1:5174/login#requests'+(requestId?'?id='+requestId:''));
   if(role==='hk'){
    await page.getByRole('button',{name:'Tạo yêu cầu',exact:true}).click({timeout:30000});
    const dialog=page.getByRole('dialog',{name:'Tạo yêu cầu nội bộ',exact:true});
    await dialog.getByRole('combobox',{name:/^Vật tư của ca/}).selectOption(String(allocationId));
    await dialog.locator(`input[name="qty-${itemId}"]`).fill('2');
    await dialog.getByLabel('Lý do yêu cầu',{exact:true}).fill('Synthetic TEST refill through browser');
    const created=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/requests/refills'&&r.request().method()==='POST');
    await dialog.getByRole('button',{name:'Gửi yêu cầu',exact:true}).click();
    const response=await created;assert.equal(response.status(),200);const data=await response.json();
    requestId=data.request.workId;assert.equal(data.request.status,'PENDING_SUPERVISOR');assert.equal(data.items[0].requestedQuantity,2);
    await expect(dialog).toHaveCount(0);
    checks.push('HK creates refill through UI');
   }else{
    const command=role==='supervisor'?'approve':'issue',title=role==='supervisor'?'Xác nhận yêu cầu':'Xác nhận cấp phát';
    await page.getByRole('button',{name:title,exact:true}).click({timeout:30000});
    const dialog=page.getByRole('dialog',{name:title,exact:true});
    if(role==='supervisor')await expect(dialog.locator(`input[name="qty-${itemId}"]`)).toHaveValue('2');
    const acted=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/requests/${requestId}/actions/${command}`&&r.request().method()==='POST');
    await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
    const response=await acted;assert.equal(response.status(),200);const data=await response.json();
    assert.equal(data.request.status,role==='supervisor'?'PENDING_STOREKEEPER':'FULFILLED');
    await expect(dialog).toHaveCount(0);
    checks.push(role+' '+command+' through UI');
   }
   await page.screenshot({path:directory+`/refill-${role}-desktop.png`,fullPage:true});
   await page.setViewportSize({width:390,height:844});
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false,role+' mobile overflow');
   assert.deepEqual(errors,[]);
   await context.close();page=null;
  }
  await writeFile(directory+'/refill-browser-results.json',JSON.stringify({status:'PASS',checks,limits:'Real isolated TEST API; preexisting synthetic authenticated sessions, no password login or SignalR transport.'},null,2));
  return requestId;
 }catch(error){if(page)await page.screenshot({path:directory+'/refill-browser-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
