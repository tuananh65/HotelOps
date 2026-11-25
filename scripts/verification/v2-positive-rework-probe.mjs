import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';

const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const fields=value=>value.fields??value;

async function browserSession(fixture,role,directory,run){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
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
  await run(page);assert.deepEqual(errors,[]);
 }catch(error){if(page)await page.screenshot({path:`${directory}/positive-rework-${role}-failure.png`,fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}

// Called only after the SAME cleaning has consumed one item, before its final PASS.
// This probe intentionally performs no assessment, refill, or inventory mutation.
export async function verifyPositiveRework({fixture,work,fill,ok,allocation,inspect,directory}){
 const before=await inspect(),balance=await allocation();
 const item=balance.items.find(row=>row.itemId===fill.itemId);assert.ok(item);
 assert.equal(item.used,1);assert.equal(item.reserved,0);
 const assessment=await ok('hk',`/supplies/room-assessments/${work.workId}`);
 assert.equal(assessment.filter(row=>row.itemId===fill.itemId).reduce((sum,row)=>sum+row.replenishedQuantity,0),1);
 assert.equal(fields((await ok('hk',`/work/${work.workId}`)).work).status,'PENDING_INSPECTION');
 const note='Dust';
 await browserSession(fixture,'supervisor',directory,async page=>{
  await page.goto(`http://127.0.0.1:5174/login#work?kind=CLEANING&id=${work.workId}`);
  await page.getByRole('button',{name:'Xác nhận hoàn thành',exact:true}).click({timeout:30000});
  const dialog=page.getByRole('dialog',{name:'Ghi kết quả kiểm tra phòng',exact:true});
  await dialog.getByRole('combobox',{name:'Kết quả',exact:true}).selectOption('FAIL');
  await dialog.locator('textarea[name="note"]').fill(note);
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${work.workId}/inspection-result`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Lưu kết quả',exact:true}).click();
  assert.equal((await saved).status(),200);await expect(dialog).toHaveCount(0);
  await page.screenshot({path:`${directory}/positive-rework-inspection-failed.png`,fullPage:true});
 });
 assert.equal(fields((await ok('supervisor',`/work/${work.workId}`)).work).status,'REWORK_REQUIRED');
 const rework=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'REWORK',title:'TEST positive consumption reclean',roomId:work.roomId,parentWorkId:work.workId,assigneeUserId:fixture.actors.find(a=>a.alias==='hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
 assert.equal(rework.parentWorkId,work.workId);assert.equal(rework.roomId,work.roomId);
 for(const command of ['accept','start']){
  const current=fields((await ok('hk',`/work/${rework.workId}`)).work);
  await ok('hk',`/work/${rework.workId}/actions/${command}`,'POST',{rowVersion:current.rowVersion,operationKey:randomUUID()});
 }
 let completionBody;
 await browserSession(fixture,'hk',directory,async page=>{
  await page.goto(`http://127.0.0.1:5174/login#cleaning?kind=REWORK&id=${rework.workId}`);
  const items=page.locator('.check-item select');await expect(items.first()).toBeEnabled({timeout:30000});
  const count=await items.count();assert.ok(count>0);
  for(let index=0;index<count;index++){
   const saved=page.waitForResponse(r=>new URL(r.url()).pathname.startsWith(`/api/v2/work/${rework.workId}/checklist/`)&&r.request().method()==='PUT');
   await items.nth(index).selectOption('PASS');assert.equal((await saved).status(),200);await expect(items.nth(index)).toHaveValue('PASS');
  }
  await page.getByRole('button',{name:'Gửi hoàn thành',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Gửi hoàn thành',exact:true});
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${rework.workId}/actions/complete`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);completionBody=response.request().postDataJSON();
  await expect(dialog).toHaveCount(0);
  await page.screenshot({path:`${directory}/positive-rework-completed.png`,fullPage:true});
 });
 const detail=await ok('hk',`/work/${rework.workId}`);
 assert.equal(fields(detail.work).status,'PENDING_REINSPECTION');
 assert.deepEqual(await inspect(),before,'Rework must not append supply ledger/documents or change held/warehouse stock');
 assert.deepEqual(await allocation(),balance,'Rework must retain used=1 and all original supply quantities');
 assert.deepEqual(await ok('hk',`/supplies/room-assessments/${work.workId}`),assessment);
 await ok('hk',`/work/${rework.workId}/actions/complete`,'POST',completionBody);
 assert.deepEqual((await ok('hk',`/work/${rework.workId}`)).history,detail.history);
 assert.deepEqual(await inspect(),before,'Completion retry must not create a second ledger entry');
 assert.deepEqual(await allocation(),balance);
 const room=(await ok('manager',`/rooms?branchId=${fixture.branch}`)).find(row=>row.roomId===work.roomId);
 assert.ok(room);assert.equal(room.operationalStatus,'DIRTY');assert.notEqual(room.readiness,'READY');
 await writeFile(`${directory}/positive-rework-evidence.json`,JSON.stringify({cleaningWorkId:work.workId,reworkWorkId:rework.workId,roomId:work.roomId,itemId:fill.itemId,initialReplenished:1,usedBefore:item.used,usedAfter:1,additionalReplenishment:0,inspectionFailureNote:note,ledgerUnchanged:true,completionRetryUnchanged:true,operationalStatus:room.operationalStatus,finalPass:'Performed by the following existing first-cleaning inspection test'},null,2));
}
