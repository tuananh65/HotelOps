// Exercises the existing shift-deactivation API on the backed-up isolated clone only.
import assert from 'node:assert/strict';
import fs from 'node:fs';
const fixture=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const runtime=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));
assert.equal(runtime.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(runtime.port,5775);
async function api(actor,path,body,method=body?'PUT':'GET'){
 const r=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${fixture.actors[actor].token}`},body:body?JSON.stringify(body):undefined});return {status:r.status,data:await r.json().catch(()=>null)};
}
const branch=fixture.branches.find(b=>b.code==='P0_TEST_DVBV');
const get=()=>api('DVBV.om',`workday/configuration?branchId=${branch.branchId}`);
const config=await get();assert.equal(config.status,200);
const shift=config.data.shifts.find(s=>s.isActive&&config.data.departmentRoles.some(d=>d.departmentId===s.departmentId&&d.roleId===s.roleId));assert(shift);
let start=1;while(config.data.shifts.some(s=>s.departmentId===shift.departmentId&&s.roleId===shift.roleId&&s.startMinute===start&&s.endMinute===start+1))start++;
const create=await api('DVBV.om','workday/shifts',{...shift,branchId:branch.branchId,name:'Kiểm thử xóa ca '+Date.now(),startMinute:start,endMinute:start+1,capacity:1,rowVersion:null,operationKey:crypto.randomUUID()},'POST');assert.equal(create.status,200,JSON.stringify(create));
const row=create.data.fields;
const path=`workday/shifts/${row.shiftId}`;
const remove={...row,isActive:false,operationKey:crypto.randomUUID()};
assert.equal((await api('DVBV.hk',path,remove)).status,403);
const foreign=await api('DVCT.om',path,remove);assert.equal(foreign.status,403);
assert.equal((await api('DVBV.om',path,remove)).status,200);
const after=(await get()).data.shifts.find(s=>s.shiftId===row.shiftId);
assert(after);assert.equal(after.isActive,false);assert.equal(after.name,row.name);
assert.equal((await api('DVBV.om',path,{...remove,operationKey:crypto.randomUUID()})).status,409);
const checks=['Unused shift deactivated and retained','HK/foreign branch denied','Stale row version denied'];
// Inspect existing week for an open assignment; never cancel or change that assignment.
const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
const week=await api('DVBV.om',`workday?branchId=${branch.branchId}&date=${date}`);assert.equal(week.status,200,JSON.stringify(week));
const assignments=week.data.assignments||[];
const open=assignments.find(a=>['PLANNED','WORKING'].includes((a.fields||a).status));
if(open){
 const a=open.fields||open,s=(await get()).data.shifts.find(s=>s.shiftId===a.shiftId);assert(s);
 const result=await api('DVBV.om',`workday/shifts/${s.shiftId}`,{...s,branchId:branch.branchId,isActive:false,operationKey:crypto.randomUUID()});
 assert.equal(result.status,409);assert.equal(result.data.title,'SHIFT_HAS_OPEN_ASSIGNMENTS');
 assert.equal((await get()).data.shifts.find(x=>x.shiftId===s.shiftId).isActive,s.isActive);
 checks.push('Open assigned shift cannot be removed; unchanged');
}
fs.mkdirSync('artifacts/shift-delete-validation',{recursive:true});
fs.writeFileSync('artifacts/shift-delete-validation/api-results.json',JSON.stringify({passed:true,checks,openAssignmentVerified:!!open},null,2));
console.log('PASS shift deactivation API: '+checks.join('; '));
