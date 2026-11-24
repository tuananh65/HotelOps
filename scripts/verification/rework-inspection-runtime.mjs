import assert from 'node:assert/strict';import fs from 'node:fs';
const f=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const backup=JSON.parse(fs.readFileSync('artifacts/workday/backup-HotelOpsCompactWorkdayTestWarehouse261124.json','utf8').replace(/^\uFEFF/,''));assert(backup.verified);
const target=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));assert.equal(target.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(target.port,5775);
const branch=f.branches.find(b=>b.code==='P0_TEST_DVCT'),roomId=branch.rooms.find(r=>r.code==='P0_101').roomId;
const dir='artifacts/remediation-inspection';fs.mkdirSync(dir,{recursive:true});const checkpoint=dir+'/assignment-runtime-checkpoint.json';
const state=fs.existsSync(checkpoint)?JSON.parse(fs.readFileSync(checkpoint,'utf8')):{reports:{},checks:[]};const save=()=>fs.writeFileSync(checkpoint,JSON.stringify(state,null,2));
async function api(who,path,body,method=body?'POST':'GET'){const r=await fetch('http://127.0.0.1:5775/api/v2/'+path,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${f.actors['DVCT.'+who].token}`},body:body?JSON.stringify(body):undefined});return {status:r.status,data:await r.json()};}
const ok=r=>{assert.equal(r.status,200,JSON.stringify(r));return r.data;};const key=()=>crypto.randomUUID();const note='Kiểm thử thực tế phòng có sự cố và xác nhận xử lý';
const room=async()=>ok(await api('reception',`rooms?branchId=${branch.branchId}`)).find(r=>r.roomId===roomId);
const detail=async()=>ok(await api('supervisor',`work/${state.workId}`));
const issue=async id=>ok(await api('supervisor',`work/room-issues/${id}`));
const issueAct=async(id,command,who='supervisor',extra={})=>{const d=await issue(id);return ok(await api(who,`work/room-issues/${id}/${command}`,{rowVersion:d.work.fields.rowVersion,operationKey:key(),note,roomRowVersion:d.room.rowVersion,...extra}));};
if(!state.workId){
 const allocations=ok(await api('hk',`supplies/allocations?branchId=${branch.branchId}&userId=${f.actors['DVCT.hk'].userId}`));
 const allocation=allocations.find(a=>a.status!=='CLOSED');assert(allocation);
 const need=allocation.items.filter(i=>i.available<2).map(i=>({itemId:i.itemId,quantity:2-i.available}));
 if(need.length){
  if(!state.refillId){state.refillId=ok(await api('hk','requests/refills',{allocationId:allocation.allocationId,items:need,reason:note,operationKey:key()})).request.workId;save();}
  for(const [who,command]of [['supervisor','approve'],['store','issue']]){
   let request=ok(await api(who,`requests/${state.refillId}`));
   if(request.actions.includes('claim'))request=ok(await api(who,`requests/${state.refillId}/actions/claim`,{rowVersion:request.request.rowVersion,operationKey:key(),reason:note}));
   if(request.actions.includes(command))ok(await api(who,`requests/${state.refillId}/actions/${command}`,{rowVersion:request.request.rowVersion,operationKey:key(),reason:note}));
  }
 }
 let r=await room();if(r.operationalStatus==='READY'){ok(await api('reception',`rooms/${roomId}/actions/reserve`,{rowVersion:r.rowVersion,operationKey:key(),reason:note}));r=await room();}
 if(r.operationalStatus==='RESERVED'){ok(await api('reception',`rooms/${roomId}/actions/check-in`,{rowVersion:r.rowVersion,operationKey:key(),reason:note}));r=await room();}
 if(r.operationalStatus==='OCCUPIED'){ok(await api('reception',`rooms/${roomId}/actions/check-out`,{rowVersion:r.rowVersion,operationKey:key(),reason:note}));r=await room();}
 assert.equal(r.operationalStatus,'DIRTY');
 const payload={branchId:branch.branchId,kind:'CLEANING',title:'TEST vệ sinh có sự cố',roomId,roomRowVersion:r.rowVersion,assigneeUserId:f.actors['DVCT.hk'].userId,durationMinutes:25};const responses=await Promise.all([api('supervisor','work',payload),api('supervisor','work',payload)]);assert.equal(responses.filter(r=>r.status===200).length,1,JSON.stringify(responses));const denied=responses.find(r=>r.status!==200);assert.equal(denied.data.title,'CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK');state.workId=responses.find(r=>r.status===200).data.fields.workId;state.checks.push('Concurrent create: exactly one assignment, other rejected for unaccepted work');save();
}

const act=async(id,command)=>{const d=ok(await api('hk','work/'+id));return ok(await api('hk','work/'+id+'/actions/'+command,{rowVersion:d.work.fields.rowVersion,operationKey:key(),reason:note}));};
let d=await detail();if(d.work.fields.status==='ASSIGNED')await act(state.workId,'start');
const options=ok(await api('supervisor','work/cleaning-creation-options?branchId='+branch.branchId));assert(options.candidates.some(c=>c.userId===f.actors['DVCT.hk'].userId),'Accepted employee eligible for another assignment');
if((await detail()).work.fields.status==='IN_PROGRESS')await act(state.workId,'complete');
d=await detail();if(d.work.fields.status==='PENDING_INSPECTION')ok(await api('supervisor','work/'+state.workId+'/inspection-result',{rowVersion:d.work.fields.rowVersion,operationKey:key(),result:'FAIL',note:'Bẩn'}));
if(!state.reworkId){state.reworkId=ok(await api('supervisor','work',{branchId:branch.branchId,kind:'REWORK',roomId,title:'TEST chờ nhận dọn lại',parentWorkId:state.workId,assigneeUserId:f.actors['DVCT.hk'].userId,durationMinutes:20})).fields.workId;save();}
let rework=ok(await api('hk','work/'+state.reworkId));if(rework.work.fields.status==='PENDING_ACCEPTANCE'){
 const rejected=await api('supervisor','work',{branchId:branch.branchId,kind:'CLEANING',title:'TEST bị chặn bởi dọn lại',roomId,assigneeUserId:f.actors['DVCT.hk'].userId,durationMinutes:20});assert.equal(rejected.data.title,'CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK');
 await act(state.reworkId,'start');}
if(ok(await api('hk','work/'+state.reworkId)).work.fields.status==='IN_PROGRESS')await act(state.reworkId,'complete');
const queue=ok(await api('supervisor',`work/cleaning-queue?branchId=${branch.branchId}&queue=inspect`));assert(queue.items.some(w=>w.workId===state.workId&&w.inspectionVariant==='DL'));assert(!(ok(await api('supervisor',`work/cleaning-queue?branchId=${branch.branchId}&queue=rework`))).items.some(w=>w.workId===state.workId));
d=await detail();assert.equal(d.inspectionVariant,'DL');ok(await api('supervisor','work/'+state.workId+'/inspection-result',{rowVersion:d.work.fields.rowVersion,operationKey:key(),result:'PASS',note:null}));
assert.equal((await room()).readiness,'READY');
state.checks.push('Short FAIL note accepted; PASS without note or checkbox resolves findings and makes room READY. Accepted staff becomes eligible; unaccepted REWORK blocks CLEANING; rework and independent inspection complete');save();console.log(JSON.stringify(state));
