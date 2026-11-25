import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyTransferBrowser(fixture,itemId,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page,root,shipment,receipt;const checks=[];
 const reason='Synthetic TEST transfer: one unit missing, pending reconciliation';
 const fields=x=>x.fields??x;
 async function act(title,path,fill){
  await page.getByRole('button',{name:title,exact:true}).click({timeout:30000});
  const dialog=page.getByRole('dialog',{name:title,exact:true});
  if(fill)await fill(dialog);
  const note=dialog.locator('textarea[name="reason"]');if(await note.count())await note.fill(reason);
  const pending=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2'+path&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await pending;assert.equal(response.status(),200,await response.text());
  const data=await response.json();await expect(dialog).toHaveCount(0);return data;
 }
 try{
  for(const role of ['otherManager','manager','store','otherStore']){
   const context=await browser.newContext({viewport:{width:1440,height:1000}});
   await context.route('**/api/**',async route=>{
    const url=new URL(route.request().url());
    if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
    url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
    try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
   });
   await context.routeWebSocket('**/*',socket=>socket.close());
   await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions[role].token);
   page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
   const selected=role==='otherStore'?shipment:root;
   await page.goto('http://127.0.0.1:5174/login#documents?transfer=1'+(selected?'&id='+selected:''));
   if(role==='otherManager'){
    const created=fields(await act('Đề nghị điều chuyển','/documents',async dialog=>{
     await dialog.locator('select[name="source"]').selectOption(String(fixture.warehouse));
     await dialog.locator('select[name="destination"]').selectOption(String(fixture.otherWarehouse));
    }));root=created.documentId;
    const line=await act('Thêm vật tư',`/documents/${root}/lines`,async dialog=>{
     await dialog.locator('select[name="item"]').selectOption(String(itemId));
     await dialog.locator('input[name="quantity"]').fill('3');
    });assert.equal(fields(line.line).requestedQty,3);
    const submitted=fields(await act('Gửi yêu cầu',`/documents/${root}/actions/submit`));assert.equal(submitted.status,'PENDING_APPROVAL');
    checks.push('Destination OM creates three-unit request and submits through UI');
   }else if(role==='manager'){
    const approved=fields(await act('Duyệt điều chuyển',`/documents/${root}/actions/approve`,dialog=>dialog.locator('input[name^="qty-"]').fill('2')));
    assert.equal(approved.status,'APPROVED');checks.push('Source OM approves two of three requested units through UI');
   }else{
    const receiving=role==='otherStore',source=receiving?shipment:root;
    const created=fields(await act(receiving?'Ghi nhận hàng thực nhận':'Chuẩn bị xuất kho',`/documents/${source}/derive`,dialog=>dialog.locator('input[name^="qty-"]').fill(receiving?'1':'2')));
    if(receiving)receipt=created.documentId;else shipment=created.documentId;
    const posted=fields(await act('Xác nhận ghi sổ',`/documents/${created.documentId}/actions/post`));assert.ok(posted.postedAt);
    checks.push(receiving?'Destination SK records only one unit actually received with discrepancy reason':'Source SK dispatches two approved units through UI');
   }
   await page.screenshot({path:directory+`/transfer-${role}-desktop.png`,fullPage:true});
   await page.setViewportSize({width:390,height:844});
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false,role+' mobile overflow');assert.deepEqual(errors,[]);
   await context.close();page=null;
  }
  const context=await browser.newContext({viewport:{width:390,height:844}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';await route.fulfill({response:await route.fetch({url:url.toString()})});
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.otherManager.token);
  page=await context.newPage();await page.goto(`http://127.0.0.1:5174/login#documents?transfer=1&id=${root}`);
  const progress=page.getByRole('region',{name:'Đối soát điều chuyển'});
  await expect(progress.locator('tbody tr')).toHaveCount(1);
  assert.deepEqual((await progress.locator('tbody td').allTextContents()).slice(1),['3','2','2','1','1']);
  await expect(progress.getByText('Phần chưa nhận giữ chờ đối soát. Hệ thống không tự ghi mất hoặc hoàn về kho nguồn.')).toBeVisible();
  assert.equal(await progress.locator('input,select,textarea').count(),0,'Original and committed quantities must be read-only');
  assert.match(await progress.locator('tbody td').nth(1).evaluate(cell=>getComputedStyle(cell,'::before').content),/Đề nghị/,'Mobile quantities must retain visible labels when headers collapse');
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  await page.screenshot({path:directory+'/transfer-pending-mobile.png',fullPage:false});
  checks.push('Readonly requested/approved/shipped/received/pending values 3/2/2/1/1 on mobile');
  await writeFile(directory+'/transfer-browser-results.json',JSON.stringify({status:'PASS',checks,limits:'Real TEST API, existing scoped sessions; password authentication and SignalR excluded.'},null,2));
  return {root,shipment,receipt};
 }catch(error){if(page)await page.screenshot({path:directory+'/transfer-browser-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
