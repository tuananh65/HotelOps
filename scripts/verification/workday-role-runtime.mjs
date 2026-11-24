import assert from 'node:assert/strict';
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
const f=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const runtime=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));
assert.equal(runtime.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(runtime.port,5775);
const checks=[];
async function api(actor,path,body,method=body?'POST':'GET'){
 const r=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${f.actors[actor].token}`},body:body?JSON.stringify(body):undefined});
 return {status:r.status,data:await r.json()};
}
const fixture=mode=>execFileSync('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/reception-shift-fixture.ps1','-Mode',mode],{stdio:'pipe'});
for(const code of ['DVBV','DVCT']){
 const actor=f.actors[`${code}.maintenance`],om=`${code}.om`;
 const week=await api(om,`workday?branchId=${actor.branchId}&date=2026-11-24`);assert.equal(week.status,200);
 const config=await api(om,`workday/configuration?branchId=${actor.branchId}`);assert.equal(config.status,200);
 const shift=config.data.shifts.find(s=>s.isActive&&s.roleCode==='MAINTENANCE_SUPERVISOR')||config.data.shifts.find(s=>s.isActive&&week.data.shifts.some(w=>w.shiftId===s.shiftId&&w.role==='Giám sát bảo trì'));assert(shift);
 const date=new Date(week.data.today+'T12:00:00Z');date.setUTCDate(date.getUTCDate()+2);const day=date.toISOString().slice(0,10);
 const candidates=await api(om,`workday/candidates/batch?shiftId=${shift.shiftId}&dates=${day}`);assert.equal(candidates.status,200);assert(candidates.data.users.some(u=>u.userId===actor.userId));
 assert.equal((await api(code==='DVBV'?'DVCT.om':'DVBV.om',`workday/candidates/batch?shiftId=${shift.shiftId}&dates=${day}`)).status,403);
 const body={shiftId:shift.shiftId,userId:actor.userId,workDate:day,rowVersion:shift.rowVersion,operationKey:crypto.randomUUID()};
 const assigned=await api(om,'workday/assignments',body);assert.equal(assigned.status,200,JSON.stringify(assigned));
 try{
  const again=await api(om,'workday/assignments',{...body,operationKey:crypto.randomUUID()});assert.equal(again.status,409);
  const busy=await api(om,`workday/candidates/batch?shiftId=${shift.shiftId}&dates=${day}`);assert(!busy.data.users.some(u=>u.userId===actor.userId));
  let overlapStart=shift.startMinute+1;while(config.data.shifts.some(s=>s.roleId===shift.roleId&&s.departmentId===shift.departmentId&&s.startMinute===overlapStart&&s.endMinute===shift.endMinute))overlapStart++;
  const overlap=await api(om,'workday/shifts',{...shift,branchId:actor.branchId,name:'TEST ca chồng giờ',startMinute:overlapStart,operationKey:crypto.randomUUID()});assert.equal(overlap.status,200,JSON.stringify(overlap));
  const overlapping=overlap.data.fields;
  try{
   const response=await api(om,'workday/assignments',{...body,shiftId:overlapping.shiftId,rowVersion:overlapping.rowVersion,operationKey:crypto.randomUUID()});assert.equal(response.status,409);assert.equal(response.data.title,'SHIFT_OVERLAP');
  }finally{assert.equal((await api(om,`workday/shifts/${overlapping.shiftId}`,{...shift,...overlapping,isActive:false,operationKey:crypto.randomUUID()},'PUT')).status,200);}
 }finally{
  const a=assigned.data.fields;const cancelled=await api(om,`workday/assignments/${a.assignmentId}/cancel`,{rowVersion:a.rowVersion,operationKey:crypto.randomUUID(),reason:'Dọn dữ liệu kiểm thử phân ca'});assert.equal(cancelled.status,200,JSON.stringify(cancelled));
 }
 const inactive=config.data.shifts.find(s=>!s.isActive);if(inactive)assert.equal((await api(om,`workday/candidates?shiftId=${inactive.shiftId}&date=${day}`)).data.title,'SHIFT_INACTIVE');
 checks.push(`${code}: maintenance candidate, assignment, overlap rejection, cross-branch denial, cancellation`);
 if(code==='DVBV'){
  let start=3;while(config.data.shifts.some(s=>s.roleId===shift.roleId&&s.departmentId===shift.departmentId&&s.startMinute===start&&s.endMinute===start+1))start++;
  assert(start+1<week.data.currentMinute);
  const created=await api(om,'workday/shifts',{...shift,branchId:actor.branchId,name:'TEST ca đã hết giờ',startMinute:start,endMinute:start+1,operationKey:crypto.randomUUID()});assert.equal(created.status,200,JSON.stringify(created));
  const s=created.data.fields;
  try {
   for(const path of [`workday/candidates?shiftId=${s.shiftId}&date=${week.data.today}`,`workday/candidates/batch?shiftId=${s.shiftId}&dates=${week.data.today}`]){
    const r=await api(om,path);assert.equal(r.status,409);assert.equal(r.data.title,'SHIFT_ALREADY_ENDED');
   }
   const rejected=await api(om,'workday/assignments',{...body,shiftId:s.shiftId,rowVersion:s.rowVersion,workDate:week.data.today,operationKey:crypto.randomUUID()});assert.equal(rejected.data.title,'SHIFT_ALREADY_ENDED');
  }finally{assert.equal((await api(om,`workday/shifts/${s.shiftId}`,{...shift,...s,isActive:false,operationKey:crypto.randomUUID()},'PUT')).status,200);}
  checks.push('Expired shift: single/batch candidates and direct assignment denied');
 }
}
const reception=f.actors['DVBV.reception'];
const roomsPath=`rooms?branchId=${reception.branchId}`;
const rooms=await api('DVBV.reception',roomsPath);assert.equal(rooms.status,200);assert(rooms.data.length>0);assert(rooms.data.every(r=>r.canOperate));
const room=rooms.data[0],objectsPath=`work/room-issue-objects?branchId=${reception.branchId}&roomId=${room.roomId}`;
assert.equal((await api('DVBV.reception',objectsPath)).status,200);
// An invalid room reaches business validation only while the receptionist is on shift.
const report={branchId:reception.branchId,roomId:999999999,typeId:1,assetId:1,roomRowVersion:room.rowVersion,quantity:1,impact:'READINESS',operationKey:crypto.randomUUID()};
assert.equal((await api('DVBV.reception','work/room-issues',report)).data.title,'ROOM_NOT_FOUND');
fixture('save');
try{
 for(const state of ['planned','ended','expired']){
  fixture(state);
  const r=await api('DVBV.reception',roomsPath);assert.equal(r.status,200);assert(r.data.every(r=>r.canOperate===false));
  for(const response of [await api('DVBV.reception',objectsPath),await api('DVBV.reception','work/room-issues',{...report,operationKey:crypto.randomUUID()})]){
   assert.equal(response.status,403);assert.equal(response.data.title,'RECEPTION_NOT_ON_SHIFT');
  }
  checks.push(`Reception ${state}: room capability false; direct form/API denied`);
 }
}finally{fixture('restore');}
assert((await api('DVBV.reception',roomsPath)).data.every(r=>r.canOperate));
fs.writeFileSync('artifacts/workday-role-validation/api-results.json',JSON.stringify({passed:true,checks},null,2));
console.log('PASS '+checks.join('; '));
