import assert from 'node:assert/strict';
import fs from 'node:fs';
const fixture=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const backup=JSON.parse(fs.readFileSync('artifacts/workday/backup-HotelOpsCompactWorkdayTestWarehouse261124.json','utf8').replace(/^\uFEFF/,''));
assert(backup.verified);
const checks=[];
async function call(actor,path,body,method=body?'POST':'GET'){
 const response=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${fixture.actors[actor].token}`},body:body?JSON.stringify(body):undefined});
 const data=await response.json();return {status:response.status,data};
}
const ok=r=>{assert.equal(r.status,200,JSON.stringify(r));return r.data;};
for(const code of ['DVBV','DVCT']){
 const om=code+'.om',hk=code+'.hk',sup=code+'.supervisor',branch=fixture.actors[om].branchId;
 const catalog=ok(await call(om,`work/internal-request-types?branchId=${branch}`));
 const sender=catalog.roles.find(r=>r.code==='HK_STAFF');assert(sender);
 const recipient=catalog.recipients.find(u=>u.userId===fixture.actors[sup].userId);assert(recipient);
 const options=ok(await call(om,`workspace/options?branchId=${branch}`));
 const dept=options.departments.find(d=>d.departmentId===fixture.actors[hk].departmentId)||options.departments[0];
 const body={name:'TEST cấu hình '+crypto.randomUUID().slice(0,8),description:'Kiểm thử vai trò gửi và nơi nhận',isActive:true,senderRoleIds:[sender.roleId],recipientDepartmentIds:[dept.departmentId],recipientUserIds:[recipient.userId]};
 const created=ok(await call(om,`work/internal-request-types?branchId=${branch}`,body));
 try{
  assert((ok(await call(hk,`work/internal-request-types?branchId=${branch}`))).items.some(t=>t.referenceId===created.referenceId));
  assert(!(ok(await call(code+'.reception',`work/internal-request-types?branchId=${branch}`))).items.some(t=>t.referenceId===created.referenceId));
  const make={branchId:branch,kind:'INTERNAL_REQUEST',title:'TEST yêu cầu có cấu hình',description:'Xác minh nội dung thử',categoryId:created.referenceId,requesterDepartmentId:fixture.actors[hk].departmentId,assigneeUserId:recipient.userId};
  const denied=await call(code+'.reception','work',{...make,requesterDepartmentId:fixture.actors[code+'.reception'].departmentId});assert.equal(denied.status,403);assert.equal(denied.data.title,'REQUEST_SENDER_NOT_ALLOWED');
  const wrong=await call(hk,'work',{...make,assigneeUserId:fixture.actors[code==='DVBV'?'DVCT.supervisor':'DVBV.supervisor'].userId});assert.equal(wrong.status,403);
  const work=ok(await call(hk,'work',make)).fields;
  const submit=ok(await call(hk,`work/${work.workId}/actions/submit`,{rowVersion:work.rowVersion,operationKey:crypto.randomUUID()}));assert.equal(submit.fields.status,'PENDING_ACCEPTANCE');
  const receiver=ok(await call(sup,`work/${work.workId}`));assert.equal(receiver.assignments[0].fields.assigneeUserId,recipient.userId);
  ok(await call(sup,`work/${work.workId}/actions/reject`,{rowVersion:receiver.work.fields.rowVersion,operationKey:crypto.randomUUID(),reason:'Đóng yêu cầu dùng kiểm thử cấu hình'}));
  const cfg=ok(await call(om,`workday/configuration?branchId=${branch}`));const shift=cfg.shifts.find(s=>s.isActive);assert(shift);
  const duplicate=await call(om,'workday/shifts',{...shift,branchId:branch,operationKey:crypto.randomUUID()});assert.equal(duplicate.status,409);assert.equal(duplicate.data.title,'SHIFT_DEFINITION_EXISTS');
  let start=5;while(cfg.shifts.some(s=>s.departmentId===shift.departmentId&&s.roleId===shift.roleId&&s.startMinute===start&&s.endMinute===start+1))start++;
  const newShift={...shift,branchId:branch,name:'TEST concurrent shift',startMinute:start,endMinute:start+1};
  const race=await Promise.all([1,2].map(()=>call(om,'workday/shifts',{...newShift,operationKey:crypto.randomUUID()})));
  assert.equal(race.filter(r=>r.status===200).length,1,JSON.stringify(race));assert(race.some(r=>r.status===409));
  const shiftCreated=race.find(r=>r.status===200).data.fields;ok(await call(om,`workday/shifts/${shiftCreated.shiftId}`,{...shiftCreated,isActive:false,operationKey:crypto.randomUUID()},'PUT'));
  const types=ok(await call(om,`work/room-issue-types?branchId=${branch}`));const type=types.find(t=>t.kind==='BROKEN');assert(type);
  try{
   ok(await call(om,`work/room-issue-types?branchId=${branch}`,{...type,requireNote:true,impacts:[{code:'READINESS',name:'Ảnh hưởng phục vụ TEST',isActive:true},{code:'SAFETY',name:'An toàn TEST',isActive:false}]},'PUT'));
   const fresh=ok(await call(hk,`work/room-issue-types?branchId=${branch}`)).find(t=>t.kind==='BROKEN');assert(fresh.requireNote);assert.equal(fresh.impacts[1].isActive,false);
   const rejected=await call(hk,`work/room-issue-types?branchId=${branch}`,{...fresh,name:'Không được sửa'},'PUT');assert.equal(rejected.status,403);
  }finally{const current=ok(await call(om,`work/room-issue-types?branchId=${branch}`)).find(t=>t.kind==='BROKEN');ok(await call(om,`work/room-issue-types?branchId=${branch}`,{...type,rowVersion:current.rowVersion},'PUT'));}
  checks.push({branch:code,senderRestriction:true,recipientRestriction:true,directRecipient:true,duplicateShift:true,concurrentShift:true,impactConfiguration:true});
 }finally{const current=ok(await call(om,`work/internal-request-types?branchId=${branch}`)).items.find(t=>t.referenceId===created.referenceId);ok(await call(om,`work/internal-request-types/${created.referenceId}?branchId=${branch}`,{...body,isActive:false,rowVersion:current.rowVersion},'PUT'));}
}
fs.mkdirSync('artifacts/incident-configuration',{recursive:true});fs.writeFileSync('artifacts/incident-configuration/runtime-config.json',JSON.stringify({checks},null,2));console.log(JSON.stringify({checks}));
