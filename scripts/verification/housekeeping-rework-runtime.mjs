import assert from 'node:assert/strict';
import fs from 'node:fs';
const f=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const runtime=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));
const backup=JSON.parse(fs.readFileSync('artifacts/workday/backup-HotelOpsCompactWorkdayTestWarehouse261124.json','utf8').replace(/^\uFEFF/,''));
assert.equal(runtime.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(runtime.port,5775);assert(backup.verified);
async function api(actor,path,body){const r=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method:body?'POST':'GET',headers:{'Content-Type':'application/json',Authorization:`Bearer ${f.actors[actor].token}`},body:body?JSON.stringify(body):undefined});return {status:r.status,data:await r.json()};}
const checks=[];
for(const code of ['DVBV','DVCT']){
 const supervisor=`${code}.supervisor`,staff=`${code}.hk`,branch=f.actors[staff].branchId;
 const rooms=await api(supervisor,`work/cleaning-rooms?branchId=${branch}`);assert.equal(rooms.status,200);
 const room=rooms.data.items[0];assert(room,'TEST needs a room requiring cleaning');
 const body={branchId:branch,kind:'CLEANING',title:`TEST workspace ${code}`,roomId:room.roomId,roomRowVersion:room.rowVersion,assigneeUserId:f.actors[staff].userId,durationMinutes:15};
 const bad=await api(supervisor,'work',body);assert.equal(bad.status,400,JSON.stringify(bad));assert.equal(bad.data.title,'CLEANING_DURATION_REQUIRED');
 let rootId,reworkId;
 const detail=async(id,actor=supervisor)=>{const r=await api(actor,`work/${id}`);assert.equal(r.status,200,JSON.stringify(r));return r.data;};
 const act=async(id,command,actor=staff)=>{const d=await detail(id,actor);const r=await api(actor,`work/${id}/actions/${command}`,{rowVersion:d.work.fields.rowVersion,operationKey:crypto.randomUUID(),reason:'Kiểm thử workspace vệ sinh'});assert.equal(r.status,200,JSON.stringify(r));return r.data;};
 try{
  const created=await api(supervisor,'work',{...body,durationMinutes:20});assert.equal(created.status,200,JSON.stringify(created));rootId=created.data.fields.workId;
  assert.equal(created.data.fields.status,'ASSIGNED');
  const assigned=await api(supervisor,`work/cleaning-queue?branchId=${branch}&queue=assigned`);assert(assigned.data.items.some(w=>w.workId===rootId));
  const doing=await api(supervisor,`work/cleaning-queue?branchId=${branch}&queue=doing`);assert(!doing.data.items.some(w=>w.workId===rootId));
  const before=await detail(rootId,staff);assert.equal(before.capabilities.canReportRoomIssue,false);
  assert.equal((await api(staff,`work/room-issue-objects?branchId=${branch}&roomId=${room.roomId}&workId=${rootId}`)).status,403);
  await act(rootId,'start');const started=await detail(rootId,staff);assert.equal(started.capabilities.canReportRoomIssue,true);assert(started.assignmentDetails[0].acceptedAt);assert(started.assignmentDetails[0].assignee);
  assert.equal((await api(staff,`work/room-issue-objects?branchId=${branch}&roomId=${room.roomId}&workId=${rootId}`)).status,200);
  await act(rootId,'complete');assert.equal((await detail(rootId,staff)).capabilities.canReportRoomIssue,false);
  const review=await detail(rootId);assert.equal(review.capabilities.canReportRoomIssue,true);
  const failed=await api(supervisor,`work/${rootId}/inspection-result`,{rowVersion:review.work.fields.rowVersion,operationKey:crypto.randomUUID(),result:'FAIL',note:'Kiểm thử yêu cầu dọn lại phòng'});assert.equal(failed.status,200,JSON.stringify(failed));
  const rework={branchId:branch,kind:'REWORK',title:`TEST dọn lại ${code}`,roomId:room.roomId,parentWorkId:rootId,assigneeUserId:f.actors[staff].userId,durationMinutes:25};
  const invalid=await api(supervisor,'work',{...rework,assigneeUserId:f.actors[code==='DVBV'?'DVCT.hk':'DVBV.hk'].userId});assert.notEqual(invalid.status,200);
  const result=await api(supervisor,'work',rework);assert.equal(result.status,200,JSON.stringify(result));reworkId=result.data.fields.workId;assert.equal(result.data.fields.status,'PENDING_ACCEPTANCE');
  const d=await detail(reworkId);assert.equal(d.assignments.length,1);assert.equal(d.assignments[0].fields.assigneeUserId,f.actors[staff].userId);
  assert.equal(Math.round((Date.parse(d.work.fields.dueAt)-Date.parse(d.assignmentDetails[0].assignedAt))/60000),25);
  const duplicate=await api(supervisor,'work',rework);assert.equal(duplicate.status,409);
  await act(reworkId,'start');await act(reworkId,'complete');
  const ready=await detail(rootId);assert.equal(ready.capabilities.canReportRoomIssue,true);
  const passed=await api(supervisor,`work/${rootId}/inspection-result`,{rowVersion:ready.work.fields.rowVersion,operationKey:crypto.randomUUID(),result:'PASS',resolveFindings:true,note:'Đã kiểm tra và khắc phục toàn bộ lỗi thử'});assert.equal(passed.status,200,JSON.stringify(passed));
  assert.equal((await detail(rootId)).work.fields.status,'COMPLETED');assert.equal((await detail(reworkId)).work.fields.status,'COMPLETED');
  checks.push({branch:code,rootId,reworkId,atomicAssignment:true,duration:25,duplicateRejected:true,stageGuards:true,inspectionPass:true});
 }catch(error){
  for(const id of [reworkId,rootId].filter(Boolean)){const d=await detail(id);if(!['COMPLETED','CANCELLED','CLOSED'].includes(d.work.fields.status))await api(supervisor,`work/${id}/actions/cancel`,{rowVersion:d.work.fields.rowVersion,operationKey:crypto.randomUUID(),reason:'Kết thúc dữ liệu kiểm thử không đạt'});}
  throw error;
 }
}
fs.writeFileSync('artifacts/housekeeping-workspace/rework-runtime.json',JSON.stringify({checks},null,2));console.log(JSON.stringify({checks}));
