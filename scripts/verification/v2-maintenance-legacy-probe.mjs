import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {verifyLegacyIncidentRecovery,verifyRecoveredCleaningCompletion} from './v2-incident-recovery-probe.mjs';
import {verifyLegacyIssueSearchBrowser} from './v2-maintenance-workflow-browser.mjs';

// Historical SQL fixture is confined to the owned TEST branch; recovery uses HTTP.
export async function verifyMaintenanceLegacy({fixture,test,ok,request,act,checklist,issueAct,detail,seedLegacy}) {
 const fields=x=>x.fields??x;
 // The external-repair room is Ready after the base scenario and still contains
 // its original asset. Start a new turnover instead of exceeding the cleaner's
 // snapshotted ten-unit allocation with the separate twelve-person stress room.
 const room=fixture.rooms[1],asset=fixture.assets.find(x=>x.roomId===room.roomId);
 const hk=fixture.actors.find(x=>x.alias==='hk');
 const technician=fixture.actors.find(x=>x.alias==='technician');
 const title='TEST legacy recover '+randomUUID();
 const prefix='TEST paging '+randomUUID();
 let cleaning,incident;
 await test('legacy: preserve current cleaning ancestry while recovering an original incident',async()=>{
  const reception=fixture.actors.find(x=>x.alias==='reception');assert.ok(reception);
  const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  const week=await ok('reception','/workday?branchId='+fixture.branch+'&date='+date);
  const shift=week.assignments.find(x=>x.assignmentId===reception.assignment);assert.ok(shift);
  if(shift.status==='PLANNED')await ok('reception','/workday/assignments/'+shift.assignmentId+'/start','POST',{rowVersion:shift.rowVersion,operationKey:randomUUID()});
  else assert.equal(shift.status,'WORKING');
  for(const command of ['reserve','check-in','check-out']){
   const fresh=(await ok('reception','/rooms?branchId='+fixture.branch)).find(r=>r.roomId===room.roomId);
   await ok('reception','/rooms/'+room.roomId+'/actions/'+command,'POST',{rowVersion:fresh.rowVersion,operationKey:randomUUID()});
  }
  cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST legacy cleaning',roomId:room.roomId,assigneeUserId:hk.user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
  await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
  const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
  await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:4,usableQuantity:4,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
  assert.equal(typeof seedLegacy,'function','Guarded historical fixture callback required');
  incident=await seedLegacy({cleaningWorkId:cleaning.workId,roomId:room.roomId,assetId:asset.assetId,title,paging:prefix});
  assert.equal(incident.noise.length,26);
  const original=await detail(incident.workId);
  assert.equal(original.legacy,true);assert.equal(fields(original.work).status,'OPEN');
  assert.equal(fields(original.work).createdBy,hk.user);assert.equal(fields(original.work).title,title);
  assert.equal(fields(original.work).parentWorkId,null,'INCIDENT must satisfy the parent-kind constraint');
  const originalWork=await ok('supervisor','/work/'+incident.workId);
  const sources=originalWork.links.map(fields).filter(l=>l.relation==='SOURCE');
  assert.equal(sources.length,1,'Historical fixture must record exactly one cleaning context');
  assert.equal(sources[0].sourceWorkId,incident.workId);
  assert.equal(sources[0].targetWorkId,cleaning.workId);
  await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');
  assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'PENDING_INSPECTION');
 });
 await test('legacy: paged and unpaged lists retain old incidents beyond the first 25 rows',async()=>{
  const path='/work/room-issues?branchId='+fixture.branch;
  const all=await ok('supervisor',path);assert.ok(Array.isArray(all));assert.ok(all.length>25);
  const p1=await ok('supervisor',path+'&page=1'),p2=await ok('supervisor',path+'&page=2');
  assert.equal(p1.items.length,25);assert.equal(p1.total,all.length);assert.equal(p2.total,all.length);
  assert.equal(new Set([...p1.items,...p2.items].map(x=>x.workId)).size,all.length);
  assert.ok(!p1.items.some(x=>x.workId===incident.workId));assert.ok(p2.items.some(x=>x.workId===incident.workId));
  assert.equal((await ok('supervisor',path+'&page=100')).items.length,0);
  for(const page of [0,-1,1000001])assert.equal((await request('supervisor',path+'&page='+page)).status,400);
  const search=await ok('supervisor',path+'&page=1&search='+encodeURIComponent(title));
  assert.equal(search.total,1);assert.equal(search.items[0].workId,incident.workId);assert.equal(search.items[0].legacy,true);
  const byRoom=await ok('supervisor',path+'&page=1&search='+encodeURIComponent(room.code));
  assert.ok(byRoom.total>=27);assert.ok(byRoom.items.every(x=>x.roomId===room.roomId));
  await verifyLegacyIssueSearchBrowser(fixture,title,incident.workId);
  // Historical pagination noise is terminal from setup; it must not require
  // elevated cancellation rights or mask the original incident repair outcome.
  const noise=all.filter(x=>x.title.startsWith(prefix));
  assert.equal(noise.length,26);
  assert.ok(noise.every(x=>x.status==='CANCELLED'));
 });
 await test('legacy: recovery validates roles and preserves history without releasing the room',async()=>{
  await verifyLegacyIncidentRecovery((alias,...args)=>request(alias==='om'?'manager':alias,...args),{incidentId:incident.workId,assetId:asset.assetId,branchId:fixture.branch,cleaningWorkId:cleaning.workId});
 });
 await test('legacy: repaired original incident needs only one HKS PASS to complete current cleaning',async()=>{
  await issueAct('lead',incident.workId,'create-repair',{externalRepair:false,userId:technician.user,checklistTemplateId:fixture.repairChecklist});
  const repair=(await detail(incident.workId)).maintenance[0];
  await act('technician',repair.workId,'accept');await act('technician',repair.workId,'start');
  await checklist('technician',repair.workId);await act('technician',repair.workId,'complete',{reason:'Synthetic repair verified'});
  await issueAct('lead',incident.workId,'report-remedy',{note:'Synthetic technical review'});
  await issueAct('supervisor',incident.workId,'inspection-pass');
  await verifyRecoveredCleaningCompletion(request,{incidentId:incident.workId,cleaningWorkId:cleaning.workId,currentCleaningWorkId:cleaning.workId,currentCycle:true});
  const rooms=await ok('manager','/rooms?branchId='+fixture.branch);assert.equal(rooms.find(r=>r.roomId===room.roomId).readiness,'READY');
 });
}
