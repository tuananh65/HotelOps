import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {verifyMissingReportQuantity,verifyRemediationQuantity} from './v2-remediation-quantity-checks.mjs';
import {verifyPartialMaterialRequeue} from './v2-material-requeue-probe.mjs';

export async function verifyMaintenanceQuantity({fixture,test,ok,request,act,checklist,issueAct,detail,issueBody,snapshot}) {
 const fields=x=>x.fields??x,room=fixture.rooms[0],fill=fixture.items.find(x=>x.code.endsWith('-fill'));
 const actor=a=>fixture.actors.find(x=>x.alias===a);
 let cleaning,issue,type,firstMaterial;
 const reason='Synthetic TEST actual shortage verification';
 async function startShift(alias){
  assert.ok(actor(alias),'Synthetic fixture must contain '+alias);
  const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  const week=await ok(alias,'/workday?branchId='+fixture.branch+'&date='+date);
  const row=week.assignments.find(x=>x.assignmentId===actor(alias).assignment);assert.ok(row);
  if(row.status==='PLANNED')await ok(alias,'/workday/assignments/'+row.assignmentId+'/start','POST',{rowVersion:row.rowVersion,operationKey:randomUUID()});
  else assert.equal(row.status,'WORKING');
 }
 async function newTurnover(target){
  const rooms=await ok('reception','/rooms?branchId='+fixture.branch);
  const current=rooms.find(r=>r.roomId===target.roomId);assert.ok(current);assert.equal(current.readiness,'READY');
  for(const command of ['reserve','check-in','check-out']){
   const fresh=(await ok('reception','/rooms?branchId='+fixture.branch)).find(r=>r.roomId===target.roomId);
   await ok('reception','/rooms/'+target.roomId+'/actions/'+command,'POST',{rowVersion:fresh.rowVersion,operationKey:randomUUID()});
  }
  const after=(await ok('reception','/rooms?branchId='+fixture.branch)).find(r=>r.roomId===target.roomId);
  assert.equal(after.readiness,'PENDING_CLEANING');assert.equal(after.turnoverNo,current.turnoverNo+1);
 }
 async function postIssue(documentId,quantity){
  const source=await ok('store','/documents/'+documentId);
  const line=source.lines.map(fields).find(x=>x.itemId===fill.itemId);assert.ok(line);
  const draft=fields(await ok('store','/documents/'+documentId+'/derive','POST',{rowVersion:fields(source.document).rowVersion,operationKey:randomUUID(),kind:'ISSUE',warehouseId:fixture.warehouse,reason,lines:[{lineId:line.lineId,quantity}]}));
  await ok('store','/documents/'+draft.documentId+'/actions/post','POST',{rowVersion:draft.rowVersion,operationKey:randomUUID(),reason});
  return draft;
 }
 async function verifyCreator(documentId,quantity){
  const document=await ok('store','/documents/'+documentId),header=fields(document.document);
  assert.equal(header.createdBy,actor('hk').user);assert.equal(header.approvedBy,actor('supervisor').user);
  assert.equal(header.status,'APPROVED');assert.equal(fields(document.lines[0]).requestedQty,quantity);assert.equal(fields(document.lines[0]).approvedQty,quantity);
 }
 await test('quantity: assessed shortage one rejects report 100 atomically and accepts one',async()=>{
  await startShift('reception');await startShift('store');await newTurnover(room);
  const configuration=(await ok('manager','/supplies/configuration?branchId='+fixture.branch)).find(x=>x.itemId===fill.itemId);
  assert.ok(configuration?.rowVersion,'Updating the existing configuration requires its current version');
  await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,itemId:fill.itemId,supplyGroup:'A',warehouseId:fixture.warehouse,unitId:fixture.unit,initialQuantity:10,minimumStock:0,perGuestQuantity:1,isActive:true,rowVersion:configuration.rowVersion});
  cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST missing quantity',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
  const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
  await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:3,usableQuantity:3,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
  type=await ok('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'MISSING',name:'TEST missing consumable',isActive:true});
  const objects=await ok('hk','/work/room-issue-objects?branchId='+fixture.branch+'&roomId='+room.roomId+'&workId='+cleaning.workId);
  assert.ok(objects.items.some(x=>x.itemId===fill.itemId),'Assessed missing consumable must be available as a report object');
  issue=fields(await verifyMissingReportQuantity({send:body=>request('hk','/work/room-issues','POST',body),snapshot,body:{branchId:fixture.branch,roomId:room.roomId,typeId:type.referenceId,itemId:fill.itemId,cleaningWorkId:cleaning.workId,roomRowVersion:objects.rowVersion}}));
  assert.equal((await detail(issue.workId)).reporterUserId,actor('hk').user);
  await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');
  await issueAct('supervisor',issue.workId,'approve',{note:'TEST physically confirmed one missing'});
 });
 await test('quantity: remediation 100 rolls back; one preserves original creator and HKS approver',async()=>{
  await verifyRemediationQuantity({send:body=>request('supervisor','/work/room-issues/'+issue.workId+'/request-material','POST',body),snapshot,body:await issueBody(issue.workId,{itemId:fill.itemId,unitId:fixture.unit,warehouseId:fixture.warehouse,note:'TEST replenish exactly one'})});
  const d=await detail(issue.workId);assert.equal(d.materials.length,1);
  firstMaterial=d.materials[0].documentId;await verifyCreator(firstMaterial,1);
 });
 await test('quantity: actual delivery and one HKS inspection finish the first cleaning',async()=>{
  await postIssue(firstMaterial,1);
  assert.equal((await detail(issue.workId)).stage,'ROOM_ISSUE_INSPECTION_REQUESTED','Full delivery must queue inspection automatically without an extra relay');
  await issueAct('supervisor',issue.workId,'inspection-pass');
  assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'COMPLETED');
 });
 let partialIssue,partialMaterial;
 await test('quantity: shortage two permits request two; warehouse issues one and closes remainder',async()=>{
  const target=fixture.rooms[1];await newTurnover(target);
  cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST partial material shortage',roomId:target.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
  const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
  await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:2,usableQuantity:2,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
  const objects=await ok('hk','/work/room-issue-objects?branchId='+fixture.branch+'&roomId='+target.roomId+'&workId='+cleaning.workId);
  assert.ok(objects.items.some(x=>x.itemId===fill.itemId),'Missing report must retain the assessed-object guard');
  partialIssue=fields(await ok('hk','/work/room-issues','POST',{branchId:fixture.branch,roomId:target.roomId,typeId:type.referenceId,itemId:fill.itemId,cleaningWorkId:cleaning.workId,roomRowVersion:objects.rowVersion,quantity:2,operationKey:randomUUID()}));
  await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');
  await issueAct('supervisor',partialIssue.workId,'approve',{note:reason});
  await issueAct('supervisor',partialIssue.workId,'request-material',{itemId:fill.itemId,unitId:fixture.unit,warehouseId:fixture.warehouse,quantity:2,note:reason});
  partialMaterial=(await detail(partialIssue.workId)).materials[0].documentId;await verifyCreator(partialMaterial,2);
  await postIssue(partialMaterial,1);
  const source=await ok('store','/documents/'+partialMaterial);assert.equal(fields(source.document).status,'PARTIALLY_ISSUED');
  await ok('store','/documents/'+partialMaterial+'/actions/close','POST',{rowVersion:fields(source.document).rowVersion,operationKey:randomUUID(),reason:'Synthetic TEST only one of two physically supplied; remainder awaiting requeue'});
 });
 await test('quantity: closed partial delivery rejects two atomically, requeues only one and retries exactly once',async()=>{
  const after=await verifyPartialMaterialRequeue(request,{incidentId:partialIssue.workId,documentId:partialMaterial,itemId:fill.itemId,unitId:fixture.unit,warehouseId:fixture.warehouse,snapshot});
  const replacement=after.materials.find(x=>x.documentId!==partialMaterial);assert.ok(replacement);
  await verifyCreator(replacement.documentId,1);
 });
}
