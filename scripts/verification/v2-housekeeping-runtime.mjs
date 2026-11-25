import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {verifyRefillBrowser} from './v2-refill-browser.mjs';
import {verifyRefillRejectionBrowser} from './v2-refill-rejection-browser.mjs';
import {createCleaningAssignmentBrowser,verifyCancelledReservationBrowser} from './v2-assignment-browser.mjs';
import {verifyRequestScopeMatrix} from './v2-request-scope-matrix-probe.mjs';
import {verifyExpiredShiftSettlement} from './v2-shift-settlement-browser.mjs';
import {verifyRefillContinuation} from './v2-refill-continuation-probe.mjs';
import {verifyPositiveRework} from './v2-positive-rework-probe.mjs';
import {verifyCleaningAssessmentBrowser,verifyCleaningCompletionBrowser,verifyPaperInspectionBrowser,verifyShiftBrowser} from './v2-cleaning-browser.mjs';
const dir='artifacts/restructure-v2',base='http://127.0.0.1:5779/api/v2',results=[];
const runId=randomUUID();
let fixture,cleanup,inventoryOwned=false,housekeepingOwned=false;
const scrub=s=>String(s).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
const run=(script,action)=>new Promise((resolve,reject)=>{
 const p=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File',`scripts/verification/${script}.ps1`,'-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});let out='',err='';
 p.stdout.on('data',d=>out+=d);p.stderr.on('data',d=>err+=d);p.on('error',reject);p.on('exit',c=>{if(c)return reject(new Error(scrub(`${script}/${action}: ${err.slice(0,2000)}`)));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});
});
const inventory=a=>run('v2-inventory-runtime-fixture',a),own=a=>run('v2-housekeeping-fixture',a);
const fields=x=>x.fields??x;
const actor=alias=>fixture.actors.find(a=>a.alias===alias);
async function setupOwned(script,metadata,setOwned){
 try{await run(script,'Setup');setOwned(true);}
 catch(error){
  try{const partial=JSON.parse((await readFile(dir+'/'+metadata+'/fixture.json','utf8')).replace(/^\uFEFF/,''));if(partial.runId===runId)setOwned(true);}catch{/* Preserve unowned fixtures. */}
  throw error;
 }
}
async function request(role,route,method='GET',body){
 const r=await fetch(base+route,{method,headers:{Authorization:`Bearer ${actor(role).token}`,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
 let data;try{data=await r.json();}catch{data={};}return {status:r.status,data};
}
async function ok(role,route,method='GET',body){const r=await request(role,route,method,body);assert.equal(r.status,200,`${method} ${route}: ${r.status} ${r.data.code??r.data.title??JSON.stringify(r.data)}`);return r.data;}
async function test(name,action){try{await action();results.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){results.push({name,status:'FAIL',error:scrub(e.message)});throw e;}}
const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
const reason='Synthetic V2 housekeeping verification';
const version=async(role,id)=>fields((await ok(role,`/work/${id}`)).work).rowVersion;
const act=async(role,id,command,extra={})=>ok(role,`/work/${id}/actions/${command}`,'POST',{rowVersion:await version(role,id),operationKey:randomUUID(),...extra});
const shiftCalls=new Map();
async function shift(role,command){const week=await ok(role,`/workday?branchId=${fixture.branch}&date=${date}`);const row=week.assignments.find(a=>a.assignmentId===actor(role).assignment);const route=`/workday/assignments/${row.assignmentId}/${command}`,body={rowVersion:row.rowVersion,operationKey:randomUUID()};shiftCalls.set(`${role}:${command}`,{route,body});return ok(role,route,'POST',body);}
async function repeatShift(role,command){const {route,body}=shiftCalls.get(`${role}:${command}`);return ok(role,route,'POST',body);}
const allocation=async()=> (await ok('hk',`/supplies/allocations?branchId=${fixture.branch}`)).find(a=>a.assignmentId===actor('hk').assignment);
let fill,towel,work,startedShift,refillBatchId,refill;
let expiredShift,nextAssignmentId;
const refillDetail=id=>ok('hk',`/requests/${id}`);
async function refillAction(role,id,command,extra={}){
 const d=await refillDetail(id);
 return ok(role,`/requests/${id}/actions/${command}`,'POST',{rowVersion:d.request.rowVersion,operationKey:randomUUID(),...extra});
}
try{
 await setupOwned('v2-inventory-runtime-fixture','inventory-private',v=>inventoryOwned=v);
 await setupOwned('v2-housekeeping-fixture','housekeeping-private',v=>housekeepingOwned=v);
 fixture=JSON.parse((await readFile(`${dir}/housekeeping-private/fixture.json`,'utf8')).replace(/^\uFEFF/,''));await inventory('Start');
 if(process.env.HOTELOPS_REQUEST_SCOPE_PROBE==='1')await test('eight branch actors cannot read foreign request inbox or types',async()=>{
  const inventoryFixture=JSON.parse((await readFile(`${dir}/inventory-private/fixture.json`,'utf8')).replace(/^\uFEFF/,''));
  await verifyRequestScopeMatrix({fixture,foreignBranch:inventoryFixture.otherBranch,request,inspect:()=>own('Inspect'),directory:dir});
 });
 fill=fixture.items.find(i=>i.code.endsWith('-fill'));towel=fixture.items.find(i=>i.code.endsWith('-towel'));
 await test('OM configures consumable and reusable separately; warehouse posts actual TEST receipt',async()=>{
  for(const item of [fill,towel]){
   await ok('manager','/inventory-lifecycle/profiles','PUT',{branchId:fixture.branch,itemId:item.itemId,classification:item===fill?'CONSUMABLE':'REUSABLE',expiryPolicy:item===fill?'REQUIRED':'NONE',reason});
   await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,itemId:item.itemId,supplyGroup:item===fill?'A':'B',warehouseId:fixture.warehouse,unitId:fixture.unit,initialQuantity:item===fill?10:4,minimumStock:0,perGuestQuantity:item===fill?1:0,isActive:true});
   const d=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST delivery'}));
   const line=await ok('store',`/documents/${d.documentId}/lines`,'POST',{rowVersion:d.rowVersion,itemId:item.itemId,unitId:fixture.unit,quantity:30});
   await ok('store',`/documents/${d.documentId}/actions/post`,'POST',{rowVersion:fields(line.document).rowVersion,operationKey:randomUUID(),reason});
  }
  const batches=await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fill.itemId}`);assert.equal(batches.length,1);
  await ok('store',`/inventory-lifecycle/batches/${batches[0].batchId}`,'PUT',{rowVersion:batches[0].rowVersion,lotNumber:'TEST-HK-LOT',receivedAt:new Date().toISOString(),expiresOn:new Date(Date.now()+60*86400000).toISOString().slice(0,10),status:'READY',reason});
 });
 await test('Start Shift issues consumable once and hands over reusable without consuming ownership',async()=>{
  for(const role of ['manager','store','supervisor'])await shift(role,'start');
  const started=await verifyShiftBrowser(fixture,'start');shiftCalls.set('hk:start',started);startedShift=started.result;const a=await allocation();assert.equal(a.status,'OPEN');assert.equal(a.items.length,1);assert.equal(a.items[0].initialIssued,10);
  const s=await own('Inspect');assert.equal(s.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,20);assert.equal(s.held.reduce((n,h)=>n+h.quantity,0),10);
  const r=await ok('hk',`/reusable-inventory?branchId=${fixture.branch}`);assert.equal(r.positions.filter(p=>p.userId===actor('hk').user&&p.status==='STAFF_HOLDING').reduce((n,p)=>n+p.quantity,0),4);
  await repeatShift('hk','start');assert.deepEqual(await own('Inspect'),s);assert.equal((await allocation()).items[0].initialIssued,10);
  assert.deepEqual(await ok('hk',`/reusable-inventory?branchId=${fixture.branch}`),r);
 });
 await test('assignment reserves maximum capacity before staff room assessment',async()=>{
  const rooms=await ok('supervisor',`/work/cleaning-rooms?branchId=${fixture.branch}`);const room=rooms.items.find(r=>r.roomId===fixture.rooms[0].roomId);assert.ok(room);
  work=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST cleaning full chain',roomId:room.roomId,roomRowVersion:room.rowVersion,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  const a=await allocation();assert.equal(a.items[0].reserved,4);
 });
 await test('second assignment is blocked until first assigned cleaning is accepted',async()=>{
  const r=await request('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST second pending assignment',roomId:fixture.rooms[1].roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist});
  assert.equal(r.status,409);assert.equal(r.data.code??r.data.title,'CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK');assert.equal((await allocation()).items[0].reserved,4);
 });
 await test('assigned staff accepts, starts, assesses actual usable quantity and releases excess reservation',async()=>{
  await verifyCleaningAssessmentBrowser(fixture,work.workId);
  const lines=await ok('hk',`/supplies/room-assessments/${work.workId}`);assert.equal(lines.length,1);assert.equal(lines[0].requiredQuantity,4);
  assert.equal(lines[0].usableQuantity,3);assert.equal(lines[0].replenishedQuantity,1);
  assert.equal((await allocation()).items[0].reserved,1);
 });
 await test('different employee cannot alter assessment or required checklist',async()=>{
  const lines=await ok('hk',`/supplies/room-assessments/${work.workId}`);
  const assessment=await request('peer',`/supplies/room-assessments/${work.workId}`,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:3,usableQuantity:3,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:1}))});assert.equal(assessment.status,403);
  const detail=await ok('hk',`/work/${work.workId}`),item=fields(detail.checklist[0]);
  const result=await request('peer',`/work/${work.workId}/checklist/${item.workChecklistItemId}`,'PUT',{rowVersion:item.rowVersion,outcome:'PASS'});assert.ok([403,404].includes(result.status));
 });
 await test('insufficient available supplies rejects assignment atomically without a new task or reservation',async()=>{
  const room=fixture.rooms.find(r=>r.code==='TEST103'),before=await allocation();
  const denied=process.env.HOTELOPS_ASSIGNMENT_BROWSER==='1'
   ?await createCleaningAssignmentBrowser(fixture,room,'TEST insufficient twelve',409)
   :await request('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST insufficient twelve',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist});
  assert.equal(denied.status,409);assert.equal(denied.data.code??denied.data.title,'STAFF_SUPPLIES_INSUFFICIENT');
  assert.deepEqual(await allocation(),before);
  const queue=await ok('supervisor',`/work/cleaning-queue?branchId=${fixture.branch}&queue=all`);assert.ok(!queue.items.some(x=>x.title==='TEST insufficient twelve'));
 });
 await test('two room reservations share one balance; cancelling one releases only its reservation',async()=>{
  const second=fields(process.env.HOTELOPS_ASSIGNMENT_BROWSER==='1'
   ?(await createCleaningAssignmentBrowser(fixture,fixture.rooms[1],'TEST parallel reservation',200)).data
   :await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST parallel reservation',roomId:fixture.rooms[1].roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  assert.equal((await allocation()).items[0].reserved,5);assert.equal((await allocation()).items[0].used,0);
  await act('supervisor',second.workId,'cancel',{reason:'Synthetic TEST cancel to release reservation'});
  if(process.env.HOTELOPS_ASSIGNMENT_BROWSER==='1')await verifyCancelledReservationBrowser(fixture,second.workId);
  assert.equal((await allocation()).items[0].reserved,1);assert.equal((await allocation()).items[0].used,0);
  const original=await ok('hk',`/supplies/room-assessments/${work.workId}`);assert.equal(original[0].replenishedQuantity,1);
 });
 await test('required execution checklist blocks direct API completion before stock consumption',async()=>{
  const response=await request('hk',`/work/${work.workId}/actions/complete`,'POST',{rowVersion:await version('hk',work.workId),operationKey:randomUUID()});assert.equal(response.status,409);assert.equal((await allocation()).items[0].used,0);
  const detail=await ok('hk',`/work/${work.workId}`);const items=detail.checklist.map(fields);assert.ok(items.length);
 });
 await test('completion consumes exactly one actual refill, inspection passes without note and room becomes Ready',async()=>{
  const body=await verifyCleaningCompletionBrowser(fixture,work.workId),before=await own('Inspect'),beforeDetail=await ok('hk',`/work/${work.workId}`);
  await ok('hk',`/work/${work.workId}/actions/complete`,'POST',body);
  assert.deepEqual(await own('Inspect'),before);assert.deepEqual((await ok('hk',`/work/${work.workId}`)).history,beforeDetail.history);
  const a=await allocation();assert.equal(a.items[0].used,1);assert.equal(a.items[0].reserved,0);
  if(process.env.HOTELOPS_REWORK_POSITIVE_PROBE==='1')await test('same cleaning with positive consumption survives FAIL and rework without double supply use',()=>verifyPositiveRework({fixture,work,fill,ok,allocation,inspect:()=>own('Inspect'),directory:dir}));
  await verifyPaperInspectionBrowser(fixture,work.workId);
  const rooms=await ok('manager',`/rooms?branchId=${fixture.branch}`);assert.equal(rooms.find(r=>r.roomId===work.roomId).readiness,'READY');
 });
 await test('failed inspection and rework preserve usable supplies and never turn the room into maintenance',async()=>{
  const second=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST rework cycle',roomId:fixture.rooms[1].roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  async function execute(id,rework=false){
   await act('hk',id,'accept');await act('hk',id,'start');
   if(!rework){const lines=await ok('hk',`/supplies/room-assessments/${id}`);
   await ok('hk',`/supplies/room-assessments/${id}`,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:4,usableQuantity:4,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});}
   const d=await ok('hk',`/work/${id}`);for(const i of d.checklist.map(fields))await ok('hk',`/work/${id}/checklist/${i.workChecklistItemId}`,'PUT',{rowVersion:i.rowVersion,outcome:'PASS'});
   await act('hk',id,'complete');
  }
  await execute(second.workId);
  await ok('supervisor',`/work/${second.workId}/inspection-result`,'POST',{rowVersion:await version('supervisor',second.workId),operationKey:randomUUID(),result:'FAIL',note:'Dust'});
  const r=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'REWORK',title:'TEST reclean',roomId:second.roomId,parentWorkId:second.workId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  await execute(r.workId,true);assert.equal((await allocation()).items[0].used,1);
  let rooms=await ok('manager',`/rooms?branchId=${fixture.branch}`);assert.equal(rooms.find(x=>x.roomId===second.roomId).operationalStatus,'DIRTY');
  await ok('supervisor',`/work/${second.workId}/inspection-result`,'POST',{rowVersion:await version('supervisor',second.workId),operationKey:randomUUID(),result:'PASS'});
  rooms=await ok('manager',`/rooms?branchId=${fixture.branch}`);assert.equal(rooms.find(x=>x.roomId===second.roomId).readiness,'READY');
 });
 await test('new earlier-expiry delivery is eligible for FEFO refill without changing held supplies',async()=>{
  const d=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST refill delivery'}));
  const line=await ok('store',`/documents/${d.documentId}/lines`,'POST',{rowVersion:d.rowVersion,itemId:fill.itemId,unitId:fixture.unit,quantity:5});
  await ok('store',`/documents/${d.documentId}/actions/post`,'POST',{rowVersion:fields(line.document).rowVersion,operationKey:randomUUID(),reason});
  const batches=await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fill.itemId}`);
  const batch=batches.find(b=>b.status==='QUARANTINED');assert.ok(batch);refillBatchId=batch.batchId;
  await ok('store',`/inventory-lifecycle/batches/${refillBatchId}`,'PUT',{rowVersion:batch.rowVersion,lotNumber:'TEST-HK-REFILL-EARLY',receivedAt:new Date().toISOString(),expiresOn:new Date(Date.now()+10*86400000).toISOString().slice(0,10),status:'READY',reason});
  assert.equal((await allocation()).items[0].remaining,9);
 });
 await test('HK refill request blocks shift end and duplicate requests; only HKS can approve',async()=>{
  const a=await allocation(),body={allocationId:a.allocationId,items:[{itemId:fill.itemId,quantity:5}],reason,operationKey:randomUUID()};
  refill=await ok('hk','/requests/refills','POST',body);assert.equal(refill.request.status,'PENDING_SUPERVISOR');
  assert.equal((await ok('hk','/requests/refills','POST',body)).request.workId,refill.request.workId);
  const duplicate=await request('hk','/requests/refills','POST',{...body,operationKey:randomUUID()});assert.equal(duplicate.status,409);
  const week=await ok('hk',`/workday?branchId=${fixture.branch}&date=${date}`),row=week.assignments.find(x=>x.assignmentId===actor('hk').assignment);
  const end=await request('hk',`/workday/assignments/${row.assignmentId}/end`,'POST',{rowVersion:row.rowVersion,operationKey:randomUUID()});assert.equal(end.status,409);assert.equal((await allocation()).status,'OPEN');
  for(const role of ['hk','manager','store']){
   const denied=await request(role,`/requests/${refill.request.workId}/actions/approve`,'POST',{rowVersion:refill.request.rowVersion,operationKey:randomUUID()});assert.equal(denied.status,403);
  }
  refill=await refillAction('supervisor',refill.request.workId,'approve');assert.equal(refill.request.status,'PENDING_STOREKEEPER');assert.equal(refill.items[0].approvedQuantity,5);
 });
 await test('SK issues approved refill using FEFO; exact retry cannot double issue or lose request history',async()=>{
  const body={rowVersion:refill.request.rowVersion,operationKey:randomUUID()};
  refill=await ok('store',`/requests/${refill.request.workId}/actions/issue`,'POST',body);assert.equal(refill.request.status,'FULFILLED');
  const a=await allocation();assert.equal(a.items[0].additionalIssued,5);assert.equal(a.items[0].remaining,14);assert.equal(a.items[0].used,1);
  const state=await own('Inspect');assert.equal(state.batches.find(b=>b.batchId===refillBatchId).warehouseQuantity,0);
  assert.equal(state.held.filter(h=>h.batchId===refillBatchId).reduce((n,h)=>n+h.quantity,0),5);
  await ok('store',`/requests/${refill.request.workId}/actions/issue`,'POST',body);assert.deepEqual(await own('Inspect'),state);
  assert.deepEqual((await refillDetail(refill.request.workId)).history,refill.history);
  assert.equal((await allocation()).items[0].additionalIssued,5);
  assert.deepEqual(refill.history.map(h=>h.action).sort(),['CREATE_REFILL','approve','issue'].sort());
 });
 await test('insufficient refill rolls back; SK rejection requires reason and preserves inventory',async()=>{
  const a=await allocation(),created=await ok('hk','/requests/refills','POST',{allocationId:a.allocationId,items:[{itemId:fill.itemId,quantity:10000}],reason,operationKey:randomUUID()});
  const approved=await refillAction('supervisor',created.request.workId,'approve'),state=await own('Inspect');
  const denied=await request('store',`/requests/${created.request.workId}/actions/issue`,'POST',{rowVersion:approved.request.rowVersion,operationKey:randomUUID()});assert.equal(denied.status,409);
  assert.deepEqual(await own('Inspect'),state);assert.equal((await refillDetail(created.request.workId)).request.status,'PENDING_STOREKEEPER');
  const noReason=await request('store',`/requests/${created.request.workId}/actions/reject`,'POST',{rowVersion:approved.request.rowVersion,operationKey:randomUUID()});assert.ok([400,409].includes(noReason.status));
  const rejected=process.env.HOTELOPS_REFILL_REJECTION_BROWSER==='1'
   ?await verifyRefillRejectionBrowser(fixture,created.request.workId,'Insufficient TEST warehouse stock',dir)
   :await refillAction('store',created.request.workId,'reject',{reason:'Insufficient TEST warehouse stock'});
  assert.equal(rejected.request.status,'REJECTED');
  assert.ok(rejected.history.some(h=>h.action==='reject'&&h.reason==='Insufficient TEST warehouse stock'));
  assert.deepEqual(await own('Inspect'),state);assert.equal((await allocation()).items[0].remaining,14);
 });
 await test('browser HK creates refill, HKS approves and SK issues actual stock with persisted history',async()=>{
  const id=await verifyRefillBrowser(fixture,(await allocation()).allocationId,fill.itemId,dir);
  const flow=await refillDetail(id);assert.equal(flow.request.status,'FULFILLED');assert.equal(flow.items[0].approvedQuantity,2);
  assert.deepEqual(flow.history.map(h=>h.action).sort(),['CREATE_REFILL','approve','issue'].sort());
  const a=await allocation();assert.equal(a.items[0].additionalIssued,7);assert.equal(a.items[0].remaining,16);assert.equal(a.items[0].used,1);
  const state=await own('Inspect');assert.equal(state.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,18);assert.equal(state.held.reduce((n,h)=>n+h.quantity,0),16);
 });
 await test('background worker expires HK attendance without GET polling and preserves outstanding custody and reservations',async()=>{
  const room=fixture.rooms.find(r=>r.code==='TEST103');
  const pending=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST finish assigned work after attendance expiry',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  await act('hk',pending.workId,'accept');await act('hk',pending.workId,'start');
  const a=await allocation();assert.equal(a.items[0].reserved,12);assert.equal(a.items[0].remaining,16);
  const ledger=await own('Inspect'),reusable=await ok('store',`/reusable-inventory?branchId=${fixture.branch}`);
  const seeded=await own('ExpireActiveShift');nextAssignmentId=seeded.nextAssignmentId;
  await run('v2-shift-worker','Start');
  // SQL-only observation proves the background worker, not Week(), closes attendance.
  const deadline=Date.now()+120000;
  do{expiredShift=(await own('InspectShift')).assignments.find(x=>x.assignmentId===actor('hk').assignment);
   if(expiredShift.status==='ENDED')break;
   await new Promise(resolve=>setTimeout(resolve,1000));
  }while(Date.now()<deadline);
  await run('v2-shift-worker','Stop');
  assert.equal(expiredShift.status,'ENDED');assert.ok(expiredShift.endedAt);assert.equal(expiredShift.autoEndCount,1);assert.equal(expiredShift.manualEndCount,0);
  assert.equal(Date.parse(expiredShift.endedAt+'Z'),Date.parse(seeded.expectedEndedAt+'Z'));
  assert.deepEqual(await own('Inspect'),ledger);assert.deepEqual(await allocation(),a);
  assert.deepEqual(await ok('store',`/reusable-inventory?branchId=${fixture.branch}`),reusable);
  const week=await ok('hk',`/workday?branchId=${fixture.branch}&date=${date}`);
  assert.equal(week.active.status,'ENDED');assert.equal(week.active.canEnd,false);assert.equal(week.active.endBlockedReason,'SHIFT_WORK_OPEN');
  const next=week.assignments.find(x=>x.assignmentId===nextAssignmentId);
  const blocked=await request('hk',`/workday/assignments/${nextAssignmentId}/start`,'POST',{rowVersion:next.rowVersion,operationKey:randomUUID()});
  assert.equal(blocked.status,409);assert.equal(blocked.data.code??blocked.data.title,'PREVIOUS_SHIFT_RECONCILIATION_REQUIRED');
  const lines=await ok('hk',`/supplies/room-assessments/${pending.workId}`);
  await ok('hk',`/supplies/room-assessments/${pending.workId}`,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:12,usableQuantity:12,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
  const detail=await ok('hk',`/work/${pending.workId}`);
  for(const item of detail.checklist.map(fields))await ok('hk',`/work/${pending.workId}/checklist/${item.workChecklistItemId}`,'PUT',{rowVersion:item.rowVersion,outcome:'PASS'});
  await act('hk',pending.workId,'complete');
  await ok('supervisor',`/work/${pending.workId}/inspection-result`,'POST',{rowVersion:await version('supervisor',pending.workId),operationKey:randomUUID(),result:'PASS'});
  assert.equal((await allocation()).items[0].reserved,0);assert.equal((await allocation()).items[0].used,1);
  const repeated=(await own('InspectShift')).assignments.find(x=>x.assignmentId===actor('hk').assignment);
  assert.equal(repeated.autoEndCount,1);assert.equal(repeated.endedAt,expiredShift.endedAt);
 });
 await test('explicit settlement after auto-end returns sixteen and reusable units without rewriting attendance end time',async()=>{
  const ended=await verifyExpiredShiftSettlement(fixture);shiftCalls.set('hk:end',ended);const a=await allocation();assert.equal(a.status,'CLOSED');assert.equal(a.items[0].used,1);assert.equal(a.items[0].returned,16);assert.equal(a.items[0].remaining,0);
  const state=await own('Inspect');assert.equal(state.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,34);assert.equal(state.batches.find(b=>b.itemId===fill.itemId&&b.batchId!==refillBatchId).warehouseQuantity,29);assert.equal(state.batches.find(b=>b.batchId===refillBatchId).warehouseQuantity,5);assert.equal(state.held.reduce((n,h)=>n+h.quantity,0),0);
  const r=await ok('store',`/reusable-inventory?branchId=${fixture.branch}`);assert.equal(r.totals.find(t=>t.itemId===towel.itemId).owned,30);assert.equal(r.positions.filter(p=>p.warehouseId===fixture.warehouse&&p.status==='READY').reduce((n,p)=>n+p.quantity,0),30);
  await repeatShift('hk','end');assert.deepEqual(await own('Inspect'),state);
  assert.deepEqual(await ok('store',`/reusable-inventory?branchId=${fixture.branch}`),r);
  const endedRow=(await own('InspectShift')).assignments.find(x=>x.assignmentId===actor('hk').assignment);
  assert.equal(endedRow.endedAt,expiredShift.endedAt);assert.equal(endedRow.autoEndCount,1);assert.equal(endedRow.manualEndCount,1);
 });
 await test('demand advisory uses actual one-unit consumption, separates seventeen issued and sixteen returned, and leaves ledger unchanged',async()=>{
  const before=await own('Inspect');
  const result=await ok('store','/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:14});
  assert.equal(result.method,'STATISTICAL_RULES');assert.equal(result.externalStatus,'BLOCKED_EXTERNAL_CONFIGURATION');assert.equal(result.requiresHumanDecision,true);
  const estimate=result.evidence.find(e=>e.itemId===fill.itemId&&e.warehouseId===fixture.warehouse);assert.ok(estimate);
  assert.equal(estimate.usedInLast30Days,1);assert.equal(estimate.issuedInLast30Days,17);assert.equal(estimate.returnedInLast30Days,16);
  assert.equal(estimate.expectedDemand,2);assert.equal(estimate.available,34);assert.equal(estimate.observedDays,1);
  assert.match(estimate.limitation,/dưới 7 ngày/);
  assert.equal((await request('hk','/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:14})).status,403);
  assert.deepEqual(await own('Inspect'),before);
 });
 await test('next shift is permitted only after previous custody has been explicitly settled',async()=>{
  const week=await ok('hk',`/workday?branchId=${fixture.branch}&date=${date}`),next=week.assignments.find(x=>x.assignmentId===nextAssignmentId);
  const body={rowVersion:next.rowVersion,operationKey:randomUUID()};
  const started=fields(await ok('hk',`/workday/assignments/${nextAssignmentId}/start`,'POST',body));assert.equal(started.status,'WORKING');
  const all=await ok('hk',`/supplies/allocations?branchId=${fixture.branch}`),a=all.find(x=>x.assignmentId===nextAssignmentId);
  assert.equal(a.status,'OPEN');assert.equal(a.items[0].initialIssued,10);assert.equal(a.items[0].used,0);
  const state=await own('Inspect');await ok('hk',`/workday/assignments/${nextAssignmentId}/start`,'POST',body);assert.deepEqual(await own('Inspect'),state);
  await ok('hk',`/workday/assignments/${nextAssignmentId}/end`,'POST',{rowVersion:started.rowVersion,operationKey:randomUUID()});
  assert.equal((await ok('hk',`/supplies/allocations?branchId=${fixture.branch}`)).find(x=>x.assignmentId===nextAssignmentId).status,'CLOSED');
 });
 if(process.env.HOTELOPS_REFILL_CONTINUATION_PROBE==='1')await verifyRefillContinuation({fixture,test,ok,request,inspect:()=>own('Inspect'),nextAssignmentId,refillBatchId,directory:dir});
}catch(e){if(!results.some(r=>r.status==='FAIL'))results.push({name:'setup',status:'FAIL',error:scrub(e.message)});console.log(scrub(e.message));}
finally{
 const ownedCleanup=[...(housekeepingOwned?[['shift worker stop',()=>run('v2-shift-worker','Stop')]]:[]),...(inventoryOwned?[['stop',()=>inventory('Stop')]]:[]),...(housekeepingOwned?[['housekeeping cleanup',()=>own('Cleanup')]]:[]),...(inventoryOwned?[['inventory cleanup',()=>inventory('Cleanup')]]:[])];
 for(const [name,fn] of ownedCleanup)try{const r=await fn();if(name==='housekeeping cleanup')cleanup=r;}catch(e){results.push({name,status:'FAIL',error:scrub(e.message)});}
 await writeFile(`${dir}/housekeeping-runtime-results.json`,JSON.stringify({database:'HotelOpsCompactWorkdayTestV22611250010',at:new Date().toISOString(),mode:'HTTP + SQL; browser HK start/assessment, HKS inspection, refill create/approve/issue; synthetic branch/catalog/checklist/users, no password authentication or SignalR browser coverage',cleanup,tests:results},null,2));
 console.log(JSON.stringify({passed:results.filter(r=>r.status==='PASS').length,failed:results.filter(r=>r.status==='FAIL').length,cleanup}));if(results.some(r=>r.status==='FAIL'))process.exitCode=1;
}
