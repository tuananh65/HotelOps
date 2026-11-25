import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';

const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

// Caller owns TEST fixture, active STOREKEEPER workday, API5779 and cleanup.
// inspect() must be the inventory fixture SQL Inspect (stock/batches/positions),
// not a request-history snapshot: creating a request legitimately adds history.
export async function verifyReplenishmentScope({fixture,request,inspect,directory}){
 const checks=[],report={at:new Date().toISOString(),checks,status:'RUNNING',limits:'Four branch-scoped OM/Storekeeper actors. TEST HTTP, stock SQL and Edge. No password login, global-role matrix or SignalR assertion.'};
 const item=fixture.items.fifo;
 assert.ok(item&&fixture.sessions.store&&fixture.sessions.manager&&fixture.sessions.otherStore&&fixture.sessions.otherManager);
 let browser,page;
 const errors=[];
 async function ok(actor,path,method='GET',body){const r=await request(actor,path,method,body);assert.equal(r.status,200,path+': '+JSON.stringify(r.data));return r.data;}
 const detail=id=>ok('store',`/requests/${id}`);
 const body=d=>({rowVersion:d.request.rowVersion,operationKey:randomUUID()});
 async function forbidden(actor,path,method='GET',data){const result=await request(actor,path,method,data);assert.equal(result.status,403,`${actor} ${path}: ${JSON.stringify(result.data)}`);}
 async function open(actor,id){
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions[actor].token);
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
  const loaded=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/requests/${id}`&&r.status()===200);
  await page.goto(`http://127.0.0.1:5174/login#requests?id=${id}`);await loaded;
  return context;
 }
 async function action(id,command,label,quantity,reason){
  await page.getByRole('button',{name:label,exact:true}).click();
  const dialog=page.getByRole('dialog',{name:label,exact:true});await expect(dialog).toBeVisible();
  if(quantity!==undefined)await dialog.locator(`input[name="qty-${item}"]`).fill(String(quantity));
  if(command==='reject'){
   const note=dialog.locator('textarea[name="reason"]');await expect(note).toHaveAttribute('required','');
   assert.equal(await note.evaluate(e=>e.validity.valueMissing),true);await note.fill(reason);
  }
  const path=`/api/v2/requests/${id}/${command==='receive'?'receive':`actions/${command}`}`;
  const response=page.waitForResponse(r=>new URL(r.url()).pathname===path&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const result=await response;assert.equal(result.status(),200);await expect(dialog).toHaveCount(0);
  const data=await result.json();
  await expect(page.getByRole('dialog',{name:data.request.title,exact:true}).getByText(command==='reject'?'Đã từ chối':data.request.status==='FULFILLED'?'Đã cấp phát':'Chờ nhận hàng thực tế',{exact:true}).first()).toBeVisible();
  return {data,payload:result.request().postDataJSON()};
 }
 try{
  const baseline=await inspect();
  const create={branchId:fixture.branch,warehouseId:fixture.warehouse,items:[{itemId:item,quantity:3}],reason:'Synthetic TEST replenishment scope',operationKey:randomUUID()};
  for(const actor of ['manager','otherStore','otherManager'])await forbidden(actor,'/requests/replenishments','POST',{...create,operationKey:randomUUID()});
  assert.deepEqual(await inspect(),baseline);checks.push('OM and foreign branch cannot create warehouse replenishment; stock unchanged');
  let d=await ok('store','/requests/replenishments','POST',create);const id=d.request.workId;report.workId=id;
  assert.equal(d.request.status,'PENDING_OPERATIONS_MANAGER');assert.equal(d.request.createdBy,fixture.sessions.store.user);
  assert.deepEqual(await inspect(),baseline);
  for(const actor of ['otherManager','otherStore'])await forbidden(actor,`/requests/${id}`);
  await forbidden('store',`/requests/${id}/actions/approve`,'POST',body(d));
  await forbidden('otherManager',`/requests/${id}/actions/approve`,'POST',body(d));
  assert.deepEqual(await detail(id),d);assert.deepEqual(await inspect(),baseline);
  checks.push('Creator retained; wrong-role and foreign approval/read denied without history or stock changes');
  browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  let context=await open('manager',id);
  d=(await action(id,'approve','Xác nhận yêu cầu',2)).data;
  assert.equal(d.request.status,'PENDING_RECEIPT');assert.equal(d.items[0].approvedQuantity,2);
  assert.deepEqual(await inspect(),baseline);checks.push('Same-branch OM approves two of three through popup; approval adds no stock');
  await page.screenshot({path:directory+'/replenishment-approved-desktop.png',fullPage:true});await context.close();
  d=await detail(id); // Compare denial snapshots using the same caller's action projection.
  const receive={...body(d),items:[{itemId:item,quantity:1}],supplierName:'Synthetic TEST supplier'};
  await forbidden('manager',`/requests/${id}/receive`,'POST',receive);
  await forbidden('otherStore',`/requests/${id}/receive`,'POST',receive);
  const over=await request('store',`/requests/${id}/receive`,'POST',{...receive,operationKey:randomUUID(),items:[{itemId:item,quantity:3}]});
  assert.equal(over.status,409);assert.deepEqual(await detail(id),d);assert.deepEqual(await inspect(),baseline);
  checks.push('OM/foreign receipt denied; receipt above approved balance rolls back');
  context=await open('store',id);
  const first=await action(id,'receive','Ghi nhận hàng thực nhận',1);d=first.data;
  assert.equal(d.request.status,'PENDING_RECEIPT');assert.equal(d.items[0].receivedQuantity,1);
  const firstStock=await inspect();const initial=baseline.items.find(x=>x.item_id===item),one=firstStock.items.find(x=>x.item_id===item);
  assert.equal(Number(one.ledger)-Number(initial.ledger),1);assert.equal(Number(one.batches)-Number(initial.batches),1);
  const replay=await ok('store',`/requests/${id}/receive`,'POST',first.payload);assert.deepEqual(replay,d);assert.deepEqual(await inspect(),firstStock);
  checks.push('Actual one-unit receipt through popup posts one ledger/batch unit; exact retry adds nothing');
  d=(await action(id,'receive','Ghi nhận hàng thực nhận',1)).data;
  assert.equal(d.request.status,'FULFILLED');assert.equal(d.items[0].receivedQuantity,2);
  const completeStock=await inspect(),two=completeStock.items.find(x=>x.item_id===item);
  assert.equal(Number(two.ledger)-Number(initial.ledger),2);assert.equal(Number(two.batches)-Number(initial.batches),2);
  await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:directory+'/replenishment-received-mobile.png',fullPage:true});await context.close();
  checks.push('Second actual receipt completes request at exactly two units; mobile detail has no horizontal overflow');
  d=await ok('store','/requests/replenishments','POST',{...create,operationKey:randomUUID()});const rejectedId=d.request.workId;
  const empty=await request('manager',`/requests/${rejectedId}/actions/reject`,'POST',{...body(d),reason:''});assert.ok([400,409].includes(empty.status));
  assert.deepEqual(await detail(rejectedId),d);assert.deepEqual(await inspect(),completeStock);
  context=await open('manager',rejectedId);const reason='Synthetic TEST replenishment no longer needed';
  d=(await action(rejectedId,'reject','Từ chối',undefined,reason)).data;
  assert.equal(d.request.status,'REJECTED');assert.ok(d.history.some(h=>h.action==='reject'&&h.reason===reason&&h.actorUserId===fixture.sessions.manager.user));
  assert.deepEqual(await inspect(),completeStock);
  await page.screenshot({path:directory+'/replenishment-rejected-desktop.png',fullPage:true});await context.close();
  checks.push('Rejection requires reason; same-branch OM popup records actor/reason/history without stock mutation');
  assert.deepEqual(errors,[]);report.stock={initial:initial.ledger,received:two.ledger,delta:2};report.status='PASS';return report;
 }catch(error){report.status='FAIL';report.error=String(error.message).replace(/[a-f\d]{64}/gi,'[redacted]');if(page&&!page.isClosed())await page.screenshot({path:directory+'/replenishment-scope-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser?.close();await writeFile(directory+'/replenishment-scope-results.json',JSON.stringify(report,null,2));}
}
