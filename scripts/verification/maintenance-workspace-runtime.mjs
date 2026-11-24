import assert from 'node:assert/strict';
import fs from 'node:fs';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const fixture=read('artifacts/p0-workflow/private/runtime-fixture.json');
const runtime=read('artifacts/warehouse-validation/process.json');
assert.equal(runtime.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(runtime.port,5775);
assert(read(`artifacts/workday/backup-${runtime.database}.json`).verified);
const branch=fixture.actors['DVCT.maintenance'].branchId;
const dir='artifacts/maintenance-workspace';fs.mkdirSync(dir,{recursive:true});
const checkpoint=dir+'/runtime-checkpoint.json';const state=fs.existsSync(checkpoint)?read(checkpoint):{checks:[]};
const save=()=>fs.writeFileSync(checkpoint,JSON.stringify(state,null,2));
const key=()=>crypto.randomUUID(),note='TEST xác minh tiếp nhận bảo trì theo ca làm việc';
async function api(who,path,body){const response=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method:body?'POST':'GET',headers:{'Content-Type':'application/json',Authorization:`Bearer ${fixture.actors[who.includes('.')?who:'DVCT.'+who].token}`},body:body?JSON.stringify(body):undefined});return {status:response.status,data:await response.json()};}
const ok=r=>{assert.equal(r.status,200,JSON.stringify(r));return r.data;};
const issue=async()=>ok(await api('maintenance',`work/room-issues/${state.id}`));
const act=async(who,command)=>{const d=ok(await api(who,`work/room-issues/${state.id}`));return api(who,`work/room-issues/${state.id}/${command}`,{rowVersion:d.work.fields.rowVersion,roomRowVersion:d.room.rowVersion,operationKey:key(),note});};
const inbox=async queue=>ok(await api('maintenance',`work/maintenance-inbox?branchId=${branch}&queue=${queue}`));
if(!state.id){
 const rooms=ok(await api('reception',`rooms?branchId=${branch}`));const room=rooms.find(r=>r.code==='P0_101');assert(room);
 const objects=ok(await api('reception',`work/room-issue-objects?branchId=${branch}&roomId=${room.roomId}`));
 const types=ok(await api('reception',`work/room-issue-types?branchId=${branch}`));assert(objects.assets.length);
 state.id=ok(await api('reception','work/room-issues',{branchId:branch,roomId:room.roomId,typeId:types.find(t=>t.kind==='BROKEN').referenceId,assetId:objects.assets[0].assetId,quantity:1,impact:'READINESS',roomRowVersion:objects.rowVersion,operationKey:key(),note})).fields.workId;save();
}
let d=ok(await api('supervisor',`work/room-issues/${state.id}`));
if(d.stage==='ROOM_ISSUE_REPORTED')ok(await act('supervisor','approve'));
d=ok(await api('supervisor',`work/room-issues/${state.id}`));
if(d.stage==='ROOM_ISSUE_APPROVED')ok(await act('supervisor','send-repair'));
if(!state.shiftChecked){
 const flow=ok(await api('maintenance',`requests/${state.id}`));assert.equal(flow.actionBlockedReason,'ACTIVE_SHIFT_REQUIRED');assert(!flow.actions.includes('claim'));
 const denied=await act('maintenance','create-repair');assert.equal(denied.status,403);assert.equal(denied.data.title,'ACTIVE_SHIFT_REQUIRED');
 assert((await inbox('waiting')).items.some(r=>r.workId===state.id));
 const week=ok(await api('maintenance',`workday?branchId=${branch}&date=2026-11-24`));
 const assignment=week.assignments.find(a=>a.assignmentId===read(dir+'/fixture.json').assignmentId);assert.equal(assignment.status,'PLANNED');
 ok(await api('maintenance',`workday/assignments/${assignment.assignmentId}/start`,{rowVersion:assignment.rowVersion,operationKey:key()}));
 const active=ok(await api('maintenance',`requests/${state.id}`));assert.equal(active.actionBlockedReason,null);assert(active.actions.includes('claim'));
 state.shiftChecked=true;state.checks.push('Outside shift: explicit reason and API 403; actual Start Shift enables acceptance');save();
}
d=await issue();if(d.stage==='ROOM_ISSUE_REPAIR_REQUESTED'){
 const payload={rowVersion:d.work.fields.rowVersion,roomRowVersion:d.room.rowVersion,operationKey:key(),note};
 ok(await api('maintenance',`work/room-issues/${state.id}/create-repair`,payload));
 const retry=await api('maintenance',`work/room-issues/${state.id}/create-repair`,payload);assert([200,409].includes(retry.status));
}
d=await issue();assert.equal(d.maintenance.length,1);state.childId=d.maintenance[0].workId;
if(d.stage==='ROOM_ISSUE_REPAIR_CREATED'){
 const all=await inbox('all');assert.equal(all.items.filter(r=>r.workId===state.id).length,1);assert(!all.items.some(r=>r.workId===state.childId));
 assert((await inbox('doing')).items.some(r=>r.workId===state.id));
 if(d.maintenance[0].status==='PENDING')ok(await act('maintenance','start-repair'));
 ok(await act('maintenance','report-remedy'));
}
d=await issue();if(d.stage==='ROOM_ISSUE_INSPECTION_REQUESTED'){
 assert((await inbox('inspection')).items.some(r=>r.workId===state.id));
 ok(await act('supervisor','inspection-pass'));
}
assert((await inbox('done')).items.some(r=>r.workId===state.id));assert(!(await inbox('all')).items.some(r=>r.workId===state.id));
const other=fixture.actors['DVBV.maintenance'].branchId;
assert.equal((await api('maintenance',`work/maintenance-inbox?branchId=${other}`)).status,403);
assert.equal((await api('hk',`work/maintenance-inbox?branchId=${branch}`)).status,403);
assert.equal((await api('maintenance',`work/maintenance-inbox?branchId=${branch}&queue=bogus`)).status,400);
const empty=ok(await api('maintenance',`work/maintenance-inbox?branchId=${branch}&queue=done&search=NOT-A-REAL-WORK-7842`));assert.equal(empty.total,0);
const page=ok(await api('maintenance',`work/maintenance-inbox?branchId=${branch}&queue=done&page=999`));assert.equal(page.items.length,0);
for(const code of ['DVBV','DVCT']){
 const b=fixture.actors[code+'.supervisor'].branchId;
 const all=ok(await api(code+'.supervisor',`work/cleaning-queue?branchId=${b}&queue=all`));
 const rooms=ok(await api(code+'.supervisor',`work/cleaning-queue?branchId=${b}&queue=rooms`));
 assert.equal(all.total,all.counts.reduce((n,c)=>n+c.count,0));assert.equal(rooms.total,all.counts.find(c=>c.status==='PENDING_CLEANING').count);
 assert(rooms.items.every(r=>r.isRoom&&r.rowVersion&&r.workId===null));for(const room of rooms.items)assert(all.items.some(r=>r.isRoom&&r.roomId===room.roomId));
}
state.checks.push('Acceptance retry creates only one repair; unified inbox deduplicates child; doing → inspection → history verified','Cross-branch/role denied; invalid queue, search, pagination verified','Cleaning All includes rooms needing cleaning on both branches');save();
fs.writeFileSync(dir+'/runtime.json',JSON.stringify(state,null,2));console.log(JSON.stringify(state));
