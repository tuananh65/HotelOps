import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {copyFile} from 'node:fs/promises';
import {inspectRepairBrowser} from './v2-maintenance-workflow-browser.mjs';

// Called by the owned synthetic maintenance fixture. All scenario writes use APIs.
export async function verifyMaintenanceBlockers({fixture,test,ok,request,act,checklist,issueAct,detail}) {
 const fields=x=>x.fields??x,room=fixture.rooms[0],actor=a=>fixture.actors.find(x=>x.alias===a);
 const reason='Synthetic TEST independent room blockers',issues=[];
 let cleaning,type,turnover;
 const current=async()=>{
  const value=(await ok('reception','/rooms?branchId='+fixture.branch)).find(r=>r.roomId===room.roomId);
  assert.ok(value);return value;
 };
 const roomAction=async command=>ok('reception',`/rooms/${room.roomId}/actions/${command}`,'POST',{
  rowVersion:(await current()).rowVersion,operationKey:randomUUID()
 });
 const blocks=async()=> (await ok('manager',`/rooms/${room.roomId}/history`)).blocks.map(fields);
 async function repair(issue){
  await issueAct('supervisor',issue.workId,'approve',{note:reason});
  await issueAct('lead',issue.workId,'create-repair',{externalRepair:false,userId:actor('technician').user,checklistTemplateId:fixture.repairChecklist});
  const d=await detail(issue.workId);assert.equal(d.maintenance.length,1);
  const id=d.maintenance[0].workId;
  await act('technician',id,'accept');await act('technician',id,'start');
  await checklist('technician',id);await act('technician',id,'complete',{reason});
  await issueAct('lead',issue.workId,'report-remedy',{note:reason});
  const reviewed=await detail(issue.workId);
  assert.equal(reviewed.stage,'ROOM_ISSUE_INSPECTION_REQUESTED');
  assert.equal(reviewed.maintenance[0].technicalReviewed,true);
 }
 async function pass(issue,label){
  await inspectRepairBrowser(fixture,issue.workId,true);
  await copyFile('artifacts/restructure-v2/maintenance-hks-pass.png',`artifacts/restructure-v2/blockers-${label}-hks-pass.png`);
  const d=await detail(issue.workId);
  assert.equal(fields(d.work).status,'CLOSED');assert.equal(d.maintenance[0].status,'CLOSED');
 }
 await test('E2E37: actual turnover cleaning has two separate asset incidents and active blockers',async()=>{
  const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  const week=await ok('reception','/workday?branchId='+fixture.branch+'&date='+date);
  const shift=week.assignments.find(a=>a.assignmentId===actor('reception').assignment);assert.ok(shift);
  if(shift.status==='PLANNED')await ok('reception',`/workday/assignments/${shift.assignmentId}/start`,'POST',{rowVersion:shift.rowVersion,operationKey:randomUUID()});
  else assert.equal(shift.status,'WORKING');
  const before=await current();assert.equal(before.readiness,'READY');
  await roomAction('reserve');await roomAction('check-in');
  assert.equal((await current()).isOccupied,true);
  await roomAction('check-out');
  const fresh=await current();turnover=fresh.turnoverNo;
  assert.equal(turnover,before.turnoverNo+1);assert.equal(fresh.readiness,'PENDING_CLEANING');assert.equal(fresh.isOccupied,false);
  type=(await ok('manager','/work/room-issue-types?branchId='+fixture.branch)).find(t=>t.kind==='BROKEN'&&t.isActive);
  assert.ok(type);
  const first=fixture.assets.find(a=>a.roomId===room.roomId);assert.ok(first);
  assert.ok(fixture.prefix,'New asset code must belong to fixture cleanup prefix');
  const second=fields(await ok('manager','/catalog/assets?branchId='+fixture.branch,'POST',{fields:{assetCode:fixture.prefix+'-blocker-two',name:'TEST second independent damaged asset',roomId:room.roomId,conditionCode:'GOOD'}}));
  await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,assetId:second.assetId,supplyGroup:'C',initialQuantity:0,minimumStock:0,perGuestQuantity:0,isActive:true});
  cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST E2E37 two independent room blockers',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  assert.equal(cleaning.turnoverNo,turnover);
  await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
  const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
  await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:l.requiredQuantity,usableQuantity:l.requiredQuantity,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
  for(const asset of [first,second]){
   const objects=await ok('hk',`/work/room-issue-objects?branchId=${fixture.branch}&roomId=${room.roomId}&workId=${cleaning.workId}`);
   assert.ok(objects.assets.some(a=>a.assetId===asset.assetId));
   const issue=fields(await ok('hk','/work/room-issues','POST',{branchId:fixture.branch,roomId:room.roomId,typeId:type.referenceId,assetId:asset.assetId,cleaningWorkId:cleaning.workId,roomRowVersion:objects.rowVersion,operationKey:randomUUID(),impact:'READINESS'}));
   const d=await detail(issue.workId);assert.equal(d.info.cleaningWorkId,cleaning.workId);assert.equal(issue.turnoverNo,turnover);
   issues.push(issue);
  }
  assert.notEqual(issues[0].workId,issues[1].workId);assert.notEqual(issues[0].assetId,issues[1].assetId);
  const active=(await blocks()).filter(b=>b.releasedAt===null);
  for(const issue of issues)assert.equal(active.filter(b=>b.sourceWorkId===issue.workId).length,1);
  await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');
  assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'PENDING_INSPECTION');
  assert.equal((await current()).readiness,'BLOCKED');
 });
 await test('E2E37: first repaired incident passes HKS UI but second blocker prevents Ready and admission',async()=>{
  await repair(issues[0]);await pass(issues[0],'first');
  const remaining=await detail(issues[1].workId);assert.equal(remaining.stage,'ROOM_ISSUE_REPORTED');assert.equal(remaining.maintenance.length,0);
  const history=await blocks();
  assert.ok(history.find(b=>b.sourceWorkId===issues[0].workId).releasedAt);
  assert.equal(history.find(b=>b.sourceWorkId===issues[1].workId).releasedAt,null);
  const before=await current();assert.equal(before.readiness,'BLOCKED');assert.equal(before.isOccupied,false);assert.equal(before.turnoverNo,turnover);
  assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'PENDING_INSPECTION');
  const denied=await request('reception',`/rooms/${room.roomId}/actions/reserve`,'POST',{rowVersion:before.rowVersion,operationKey:randomUUID()});
  assert.equal(denied.status,409);assert.match(JSON.stringify(denied.data),/ROOM_NOT_READY/);assert.deepEqual(await current(),before);
 });
 await test('E2E37: second repair and HKS UI PASS finish same cleaning and release Ready',async()=>{
  await repair(issues[1]);await pass(issues[1],'second');
  const after=await current();assert.equal(after.readiness,'READY');assert.equal(after.operationalStatus,'READY');assert.equal(after.turnoverNo,turnover);assert.equal(after.isOccupied,false);
  assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'COMPLETED');
  assert.equal((await blocks()).filter(b=>b.releasedAt===null).length,0);
 });
 await test('blockers: actual occupied room retains occupancy and explicit SAFETY hold after repair PASS',async()=>{
  assert.ok(type.impacts.some(i=>i.code==='SAFETY'&&i.isActive));
  await roomAction('reserve');await roomAction('check-in');
  const before=await current();assert.equal(before.isOccupied,true);assert.equal(before.operationalStatus,'OCCUPIED');
  const issue=fields(await ok('reception','/work/room-issues','POST',{branchId:fixture.branch,roomId:room.roomId,typeId:type.referenceId,assetId:issues[0].assetId,roomRowVersion:before.rowVersion,operationKey:randomUUID(),impact:'SAFETY'}));
  await repair(issue);await pass(issue,'safety-occupied');
  const after=await current();assert.equal(after.isOccupied,true);assert.equal(after.operationalStatus,'OCCUPIED');assert.notEqual(after.readiness,'READY');assert.equal(after.turnoverNo,turnover);
  const hold=(await blocks()).find(b=>b.sourceWorkId===issue.workId);assert.ok(hold);assert.equal(hold.releasedAt,null);
 });
}
