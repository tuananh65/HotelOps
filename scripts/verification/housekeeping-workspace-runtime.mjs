import assert from 'node:assert/strict';
import fs from 'node:fs';
const fixture=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const runtime=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));
assert.equal(runtime.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(runtime.port,5775);
const checks=[];
async function api(actor,path,body){const r=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method:body?'POST':'GET',headers:{'Content-Type':'application/json',Authorization:`Bearer ${fixture.actors[actor].token}`},body:body?JSON.stringify(body):undefined});return {status:r.status,data:await r.json()};}
for(const code of ['DVBV','DVCT']){
 const supervisor=`${code}.supervisor`,staff=`${code}.hk`,branch=fixture.actors[staff].branchId;
 const me=await api(supervisor,'auth/me');assert.equal(me.status,200);assert(!me.data.grants.some(g=>g.code==='ROOM_VIEW'));
 assert.equal((await api(supervisor,`rooms?branchId=${branch}`)).status,403);
 assert.equal((await api(supervisor,`work/cleaning-rooms?branchId=${branch}`)).status,200);
 const all=await api(supervisor,`work/cleaning-queue?branchId=${branch}&queue=all`);assert.equal(all.status,200);
 for(const queue of ['assigned','doing','unassigned','inspect','rework']){
  const r=await api(supervisor,`work/cleaning-queue?branchId=${branch}&queue=${queue}`);assert.equal(r.status,200,JSON.stringify(r));
  const valid={assigned:['ASSIGNED'],doing:['IN_PROGRESS','ON_HOLD'],unassigned:['PENDING_ASSIGNMENT'],inspect:['PENDING_INSPECTION'],rework:['REWORK_REQUIRED']}[queue];
  assert(r.data.items.every(w=>valid.includes(w.status)));assert.equal(r.data.total,all.data.counts.filter(c=>valid.includes(c.status)).reduce((n,c)=>n+c.count,0));
 }
 const current=await api(staff,`work?branchId=${branch}&kind=CLEANING&cleaningCurrent=true&assignedToMe=true`);assert.equal(current.status,200);assert(current.data.items.every(r=>!['COMPLETED','CLOSED','CANCELLED','FAILED'].includes(r.fields.status)));
 for(const w of current.data.items.map(r=>r.fields)){
  const d=await api(staff,`work/${w.workId}`);assert.equal(d.status,200);
  if(w.status!=='IN_PROGRESS')assert.equal(d.data.capabilities.canReportRoomIssue,false);
  for(const a of d.data.assignmentDetails)assert('assignee' in a&&'acceptedAt' in a);
 }
 const week=await api(supervisor,`work/housekeeping-history?branchId=${branch}&period=week&date=2026-11-24`);assert.equal(week.status,200);assert.equal(week.data.date,'2026-11-23');assert.equal(week.data.endDate,'2026-11-29');
 let total=0;for(let i=23;i<=29;i++){const day=await api(supervisor,`work/housekeeping-history?branchId=${branch}&date=2026-11-${i}`);assert.equal(day.status,200);total+=day.data.total;}assert.equal(total,week.data.total);
 const own=await api(staff,`work/housekeeping-history?branchId=${branch}&period=week&date=2026-11-24&mine=false`);assert.equal(own.status,200);
 const filtered=await api(supervisor,`work/housekeeping-history?branchId=${branch}&period=week&date=2026-11-24&status=COMPLETED&search=TEST&sort=oldest`);assert.equal(filtered.status,200);assert(filtered.data.items.every(w=>w.status==='COMPLETED'));assert(filtered.data.items.every((w,i,rows)=>!i||Date.parse(rows[i-1].completedAt)<=Date.parse(w.completedAt)));
 for(const w of own.data.items){const d=await api(staff,`work/${w.workId}`);assert.equal(d.status,200);assert(d.data.events.some(e=>e.fields.eventType==='COMPLETE'&&e.fields.actorUserId===fixture.actors[staff].userId));}
 const other=fixture.actors[`${code==='DVBV'?'DVCT':'DVBV'}.hk`].branchId;
 assert.equal((await api(staff,`work/housekeeping-history?branchId=${other}&period=week`)).status,403);
 assert.equal((await api(supervisor,`work/housekeeping-history?branchId=${branch}&period=month`)).status,400);
 const rooms=await api(`${code}.om`,`rooms?branchId=${branch}`);assert.equal(rooms.status,200);
 if(rooms.data.length){const room=rooms.data[0];assert.equal((await api(supervisor,`work/room-issue-objects?branchId=${branch}&roomId=${room.roomId}`)).status,403);const report=await api(supervisor,'work/room-issues',{branchId:branch,roomId:room.roomId,roomRowVersion:room.rowVersion,typeId:1,assetId:1,quantity:1,impact:'READINESS',operationKey:crypto.randomUUID()});assert.equal(report.status,403);assert.equal(report.data.title,'ROOM_REPORT_EXECUTION_REQUIRED');}
 checks.push({branch:code,queues:all.data.counts,current:current.data.total,historyWeek:week.data.total,ownHistory:own.data.total,authorization:true});
}
fs.mkdirSync('artifacts/housekeeping-workspace',{recursive:true});fs.writeFileSync('artifacts/housekeeping-workspace/runtime.json',JSON.stringify({checks},null,2));console.log(JSON.stringify({checks}));
