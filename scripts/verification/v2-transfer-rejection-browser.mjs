import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

// Separate request, no shipment: this probe never changes the caller's stock.
export async function verifyTransferRejection({fixture,request,snapshot,directory}){
 const fields=x=>x.fields??x,checks=[];
 const report={at:new Date().toISOString(),checks,limits:'Synthetic TEST request created/submitted through HTTP; source OM rejects through Edge. Existing sessions; no password login or SignalR.'};
 const beforeStock=await snapshot();let browser,page;
 async function ok(role,path,method='GET',body){const result=await request(role,path,method,body);assert.equal(result.status,200,path+': '+JSON.stringify(result.data));return result.data;}
 try{
  const draft=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason:'Synthetic TEST request for reasoned refusal'}));
  report.documentId=draft.documentId;
  const added=await ok('otherManager',`/documents/${draft.documentId}/lines`,'POST',{rowVersion:draft.rowVersion,itemId:fixture.items.fefo,unitId:fixture.unit,quantity:1});
  const pending=fields(await ok('otherManager',`/documents/${draft.documentId}/actions/submit`,'POST',{rowVersion:fields(added.document).rowVersion,operationKey:randomUUID()}));
  const route=`/documents/${draft.documentId}/actions/reject`;
  const before=await ok('manager',`/documents/${draft.documentId}`);
  const missing=await request('manager',route,'POST',{rowVersion:pending.rowVersion,operationKey:randomUUID(),reason:''});
  assert.ok([400,409].includes(missing.status));assert.match(JSON.stringify(missing.data),/REASON/);
  assert.deepEqual(await ok('manager',`/documents/${draft.documentId}`),before);
  assert.deepEqual(await snapshot(),beforeStock);checks.push('Empty rejection reason is denied without document/history/stock changes');
  browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.manager.token);
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));
  const loaded=page.waitForResponse(response=>new URL(response.url()).pathname===`/api/v2/documents/${draft.documentId}`&&response.status()===200);
  await page.goto(`http://127.0.0.1:5174/login#documents?transfer=1&id=${draft.documentId}`);await loaded;
  await page.getByRole('button',{name:'Từ chối',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Từ chối',exact:true});
  const note=dialog.locator('textarea[name="reason"]');await expect(note).toHaveAttribute('required','');
  assert.equal(await note.evaluate(element=>element.validity.valueMissing),true);
  const reason='Synthetic TEST source branch needs this stock';await note.fill(reason);
  const committed=page.waitForResponse(response=>new URL(response.url()).pathname==='/api/v2'+route&&response.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await committed;assert.equal(response.status(),200);assert.equal(fields(await response.json()).status,'REJECTED');
  await expect(dialog).toHaveCount(0);await expect(page.getByText(reason,{exact:true}).first()).toBeVisible();
  const rejected=await ok('manager',`/documents/${draft.documentId}`);
  assert.equal(fields(rejected.document).status,'REJECTED');
  assert.ok(rejected.events.map(fields).some(event=>event.eventType==='REJECT'&&event.reason===reason&&event.actorUserId===fixture.sessions.manager.user));
  assert.deepEqual(await snapshot(),beforeStock);checks.push('Source OM popup rejection stores exact reason/actor/history without inventory changes');
  const replay=await request('manager',route,'POST',response.request().postDataJSON());assert.equal(replay.status,409);
  assert.deepEqual(await ok('manager',`/documents/${draft.documentId}`),rejected);assert.deepEqual(await snapshot(),beforeStock);
  checks.push('Exact replay cannot duplicate rejection history or change stock');
  await page.screenshot({path:directory+'/transfer-rejection-desktop.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:directory+'/transfer-rejection-mobile.png',fullPage:true});assert.deepEqual(errors,[]);
  report.status='PASS';return report;
 }catch(error){report.status='FAIL';report.error=String(error.message).replace(/[a-f\d]{64}/gi,'[redacted]');if(page)await page.screenshot({path:directory+'/transfer-rejection-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser?.close();await writeFile(directory+'/transfer-rejection-results.json',JSON.stringify(report,null,2));}
}
