import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {mkdir} from 'node:fs/promises';
import {verifyRefillBrowser} from './v2-refill-browser.mjs';

// Optional continuation after the original twenty checks. All new business state
// is created through the API; Inspect is the runner's read-only ledger observer.
export async function verifyRefillContinuation({fixture,test,ok,request,inspect,nextAssignmentId,refillBatchId,directory}) {
 const fields=x=>x.fields??x,actor=alias=>fixture.actors.find(a=>a.alias===alias);
 const fill=fixture.items.find(i=>i.code.endsWith('-fill')),towel=fixture.items.find(i=>i.code.endsWith('-towel'));
 const room=fixture.rooms.find(r=>r.code==='TEST103');assert.ok(room);
 const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
 const allocations=role=>ok(role,`/supplies/allocations?branchId=${fixture.branch}`);
 const originalAllocations=await allocations('hk');
 let assignmentId,allocationId,cleaning;
 const currentAllocation=async()=> (await allocations('peer')).find(a=>a.assignmentId===assignmentId);
 const balance=async()=>{const a=await currentAllocation();assert.ok(a);const b=a.items.find(i=>i.itemId===fill.itemId);assert.ok(b);return b;};
 const detail=id=>ok('peer',`/work/${id}`);
 async function action(role,id,command){
  const d=await ok(role,`/work/${id}`);
  return ok(role,`/work/${id}/actions/${command}`,'POST',{rowVersion:fields(d.work).rowVersion,operationKey:randomUUID()});
 }
 await test('refill continuation: OM assigns the next shift through API and a second cleaner receives ten units',async()=>{
  const before=await inspect();assert.equal(before.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,34);
  assert.equal(before.held.reduce((n,h)=>n+h.quantity,0),0);
  const week=await ok('manager',`/workday?branchId=${fixture.branch}&date=${date}`);
  const next=week.assignments.find(a=>a.assignmentId===nextAssignmentId);assert.ok(next);
  const shift=week.shifts.find(s=>s.shiftId===next.shiftId);assert.ok(shift);
  const candidates=await ok('manager',`/workday/candidates/batch?shiftId=${shift.shiftId}&dates=${date}`);
  const candidate=candidates.users.find(u=>u.userId===actor('peer').user);assert.ok(candidate,'Second cleaner must be eligible for the next shift');
  const assigned=fields(await ok('manager','/workday/assignments','POST',{shiftId:shift.shiftId,userId:candidate.userId,workDate:date,rowVersion:shift.rowVersion,operationKey:randomUUID(),hoursAcknowledgement:candidate.hoursToken}));
  assignmentId=assigned.assignmentId;
  await ok('peer',`/workday/assignments/${assignmentId}/start`,'POST',{rowVersion:assigned.rowVersion,operationKey:randomUUID()});
  const a=await currentAllocation();allocationId=a.allocationId;assert.equal(a.status,'OPEN');
  assert.equal((await balance()).initialIssued,10);assert.equal((await balance()).remaining,10);
  const state=await inspect();assert.equal(state.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,24);
  assert.equal(state.held.reduce((n,h)=>n+h.quantity,0),10);
  assert.equal(state.batches.find(b=>b.batchId===refillBatchId).warehouseQuantity,0);
  assert.equal(state.held.filter(h=>h.batchId===refillBatchId).reduce((n,h)=>n+h.quantity,0),5,'Initial issuance uses the earlier-expiry returned batch first');
 });
 await test('refill continuation: ten units cannot reserve twelve; browser HK request, HKS approval and SK issue supply the missing two',async()=>{
  const receptionWeek=await ok('reception',`/workday?branchId=${fixture.branch}&date=${date}`);
  const reception=receptionWeek.assignments.find(a=>a.assignmentId===actor('reception').assignment);assert.ok(reception);assert.equal(reception.status,'PLANNED');
  await ok('reception',`/workday/assignments/${reception.assignmentId}/start`,'POST',{rowVersion:reception.rowVersion,operationKey:randomUUID()});
  const originalRoom=(await ok('reception',`/rooms?branchId=${fixture.branch}`)).find(r=>r.roomId===room.roomId);assert.equal(originalRoom.readiness,'READY');
  for(const command of ['reserve','check-in','check-out']){
   const fresh=(await ok('reception',`/rooms?branchId=${fixture.branch}`)).find(r=>r.roomId===room.roomId);
   await ok('reception',`/rooms/${room.roomId}/actions/${command}`,'POST',{rowVersion:fresh.rowVersion,operationKey:randomUUID()});
  }
  const fresh=(await ok('supervisor',`/work/cleaning-rooms?branchId=${fixture.branch}`)).items.find(r=>r.roomId===room.roomId);assert.ok(fresh);
  const body={branchId:fixture.branch,kind:'CLEANING',title:'TEST continue cleaning after refill',roomId:room.roomId,roomRowVersion:fresh.rowVersion,assigneeUserId:actor('peer').user,durationMinutes:30,checklistTemplateId:fixture.checklist};
  const ledger=await inspect(),before=await currentAllocation();
  const denied=await request('supervisor','/work','POST',body);assert.equal(denied.status,409);assert.equal(denied.data.code??denied.data.title,'STAFF_SUPPLIES_INSUFFICIENT');
  assert.deepEqual(await inspect(),ledger);assert.deepEqual(await currentAllocation(),before);
  const queue=await ok('supervisor',`/work/cleaning-queue?branchId=${fixture.branch}&queue=all`);assert.ok(!queue.items.some(w=>w.title===body.title));
  const browserFixture={...fixture,actors:fixture.actors.map(a=>a.alias==='hk'?{...actor('peer'),alias:'hk',assignment:assignmentId}:a)};
  const output=directory+'/refill-continuation';await mkdir(output,{recursive:true});
  const refillId=await verifyRefillBrowser(browserFixture,allocationId,fill.itemId,output);
  const refill=await ok('peer',`/requests/${refillId}`);assert.equal(refill.request.status,'FULFILLED');
  assert.equal(refill.items.find(i=>i.itemId===fill.itemId).approvedQuantity,2);
  assert.equal((await balance()).additionalIssued,2);assert.equal((await balance()).remaining,12);
  const after=await inspect();assert.equal(after.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,22);assert.equal(after.held.reduce((n,h)=>n+h.quantity,0),12);
  assert.equal(after.held.filter(h=>h.batchId!==refillBatchId).reduce((n,h)=>n+h.quantity,0),7);
  cleaning=fields(await ok('supervisor','/work','POST',body));assert.equal((await balance()).reserved,12);
 });
 await test('refill continuation: cleaner consumes twelve actual replenishments including the new two, retries once and passes inspection',async()=>{
  await action('peer',cleaning.workId,'accept');await action('peer',cleaning.workId,'start');
  const lines=await ok('peer',`/supplies/room-assessments/${cleaning.workId}`);assert.equal(lines.length,1);assert.equal(lines[0].requiredQuantity,12);
  await ok('peer',`/supplies/room-assessments/${cleaning.workId}`,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:0,usableQuantity:0,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:12}))});
  assert.equal((await balance()).used,0);assert.equal((await balance()).reserved,12);
  for(const item of (await detail(cleaning.workId)).checklist.map(fields))await ok('peer',`/work/${cleaning.workId}/checklist/${item.workChecklistItemId}`,'PUT',{rowVersion:item.rowVersion,outcome:'PASS'});
  const body={rowVersion:fields((await detail(cleaning.workId)).work).rowVersion,operationKey:randomUUID()};
  await ok('peer',`/work/${cleaning.workId}/actions/complete`,'POST',body);
  const b=await balance();assert.equal(b.used,12);assert.equal(b.remaining,0);assert.equal(b.reserved,0);assert.equal(b.initialIssued+b.additionalIssued,b.used);
  const ledger=await inspect(),history=(await detail(cleaning.workId)).history;
  assert.equal(ledger.held.reduce((n,h)=>n+h.quantity,0),0);assert.equal(ledger.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,22,'Consumption must not issue warehouse stock a second time');
  await ok('peer',`/work/${cleaning.workId}/actions/complete`,'POST',body);assert.deepEqual(await inspect(),ledger);assert.deepEqual((await detail(cleaning.workId)).history,history);
  const d=await ok('supervisor',`/work/${cleaning.workId}`);
  await ok('supervisor',`/work/${cleaning.workId}/inspection-result`,'POST',{rowVersion:fields(d.work).rowVersion,operationKey:randomUUID(),result:'PASS'});
  assert.equal((await ok('manager',`/rooms?branchId=${fixture.branch}`)).find(r=>r.roomId===room.roomId).readiness,'READY');
 });
 await test('refill continuation: settlement preserves thirty-five received equals twenty-two in warehouse plus thirteen used',async()=>{
  const week=await ok('peer',`/workday?branchId=${fixture.branch}&date=${date}`),row=week.assignments.find(a=>a.assignmentId===assignmentId);assert.ok(row);
  const body={rowVersion:row.rowVersion,operationKey:randomUUID()};
  await ok('peer',`/workday/assignments/${assignmentId}/end`,'POST',body);
  assert.equal((await currentAllocation()).status,'CLOSED');assert.equal((await balance()).returned,0);
  const ledger=await inspect();assert.equal(ledger.stock.find(i=>i.itemId===fill.itemId).warehouseQuantity,22);assert.equal(ledger.held.reduce((n,h)=>n+h.quantity,0),0);
  assert.equal(ledger.batches.filter(b=>b.itemId===fill.itemId).reduce((n,b)=>n+b.warehouseQuantity,0),22);
  const usedBefore=originalAllocations.flatMap(a=>a.items).filter(i=>i.itemId===fill.itemId).reduce((n,i)=>n+i.used,0);assert.equal(usedBefore,1);
  assert.equal(22+usedBefore+(await balance()).used,35);
  assert.deepEqual(await allocations('hk'),originalAllocations,'Continuation must preserve the original twenty-check allocation history');
  const reusable=await ok('store',`/reusable-inventory?branchId=${fixture.branch}`);assert.equal(reusable.totals.find(t=>t.itemId===towel.itemId).owned,30);
  assert.equal(reusable.positions.filter(p=>p.itemId===towel.itemId&&p.warehouseId===fixture.warehouse&&p.status==='READY').reduce((n,p)=>n+p.quantity,0),30);
  await ok('peer',`/workday/assignments/${assignmentId}/end`,'POST',body);assert.deepEqual(await inspect(),ledger);
 });
}
