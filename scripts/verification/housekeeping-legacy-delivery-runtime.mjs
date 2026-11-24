import assert from 'node:assert/strict';import fs from 'node:fs';
const f=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
const backup=JSON.parse(fs.readFileSync('artifacts/workday/backup-HotelOpsCompactWorkdayTestWarehouse261124.json','utf8').replace(/^\uFEFF/,''));assert(backup.verified);
const target=JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json','utf8').replace(/^\uFEFF/,''));assert.equal(target.database,'HotelOpsCompactWorkdayTestWarehouse261124');assert.equal(target.port,5775);
const branch=f.branches.find(b=>b.code==='P0_TEST_DVCT'),roomId=branch.rooms.find(r=>r.code==='P0_101').roomId;
const dir='artifacts/housekeeping-fulfillment/legacy';fs.mkdirSync(dir,{recursive:true});const checkpoint=dir+'/cleaning-runtime-checkpoint.json';
const state=fs.existsSync(checkpoint)?JSON.parse(fs.readFileSync(checkpoint,'utf8')):{reports:{},checks:[]};const save=()=>fs.writeFileSync(checkpoint,JSON.stringify(state,null,2));
async function api(who,path,body,method=body?'POST':'GET'){const r=await fetch('http://127.0.0.1:'+(path.startsWith('documents/')&&path.endsWith('/actions/post')?'5776':'5775')+'/api/v2/'+path,{method,headers:{'Content-Type':'application/json',Authorization:`Bearer ${f.actors['DVCT.'+who].token}`},body:body?JSON.stringify(body):undefined});return {status:r.status,data:await r.json()};}
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
 state.workId=ok(await api('supervisor','work',{branchId:branch.branchId,kind:'CLEANING',title:'TEST vệ sinh có sự cố',roomId,roomRowVersion:r.rowVersion,assigneeUserId:f.actors['DVCT.hk'].userId,durationMinutes:25})).fields.workId;save();
}
let d=await detail();if(d.work.fields.status==='ASSIGNED'){
 const options=ok(await api('supervisor','work/cleaning-creation-options?branchId='+branch.branchId));
 assert(!options.candidates.some(c=>c.userId===f.actors['DVCT.hk'].userId));
 for(const kind of ['CLEANING','REWORK']){const rejected=await api('supervisor','work',{branchId:branch.branchId,kind,title:'TEST phải bị chặn',roomId,assigneeUserId:f.actors['DVCT.hk'].userId,durationMinutes:25,parentWorkId:kind==='REWORK'?state.workId:null});assert.equal(rejected.status,409,JSON.stringify(rejected));assert.equal(rejected.data.title,'CLEANING_ASSIGNEE_HAS_UNACCEPTED_WORK');}
 state.checks.push('Unaccepted cleaning blocks both cleaning and rework; candidates exclude staff');save();
}
if(d.work.fields.status==='ASSIGNED')ok(await api('hk',`work/${state.workId}/actions/start`,{rowVersion:d.work.fields.rowVersion,operationKey:key()}));
if((await detail()).work.fields.status==='IN_PROGRESS'){
 const types=ok(await api('hk',`work/room-issue-types?branchId=${branch.branchId}`));
 for(const kind of ['BROKEN','MISSING'])if(!state.reports[kind]){
  const objects=ok(await api('hk',`work/room-issue-objects?branchId=${branch.branchId}&roomId=${roomId}&workId=${state.workId}`));
  assert(kind==='BROKEN'?objects.assets.length:objects.items.length,'Assigned object required for TEST');
  const payload={branchId:branch.branchId,roomId,typeId:types.find(t=>t.kind===kind).referenceId,assetId:kind==='BROKEN'?objects.assets[0].assetId:null,itemId:kind==='MISSING'?objects.items[0].itemId:null,quantity:1,impact:'READINESS',roomRowVersion:objects.rowVersion,operationKey:key(),note,cleaningWorkId:state.workId};
  state.reports[kind]=ok(await api('hk','work/room-issues',payload)).fields.workId;save();
  assert.equal((await api('hk','work/room-issues',payload)).status,409);
  const report=await issue(state.reports[kind]);const premature=await api('supervisor',`work/room-issues/${state.reports[kind]}/approve`,{rowVersion:report.work.fields.rowVersion,operationKey:key(),note});assert.equal(premature.status,409);assert.equal(premature.data.title,'CLEANING_NOT_READY_FOR_REVIEW');
 }
 d=await detail();const complete={rowVersion:d.work.fields.rowVersion,operationKey:key(),reason:'Đã dọn xong nhưng phòng có sự cố'};
 ok(await api('hk',`work/${state.workId}/actions/complete`,complete));ok(await api('hk',`work/${state.workId}/actions/complete`,complete));state.checks.push('Report and completion retries do not duplicate; early approval blocked');save();
}
d=await detail();assert(d.hasOpenRoomIssues);assert.equal(d.capabilities.canRecordPaperInspection,false);assert(d.capabilities.canConfirmRoomIssue);assert.equal(d.roomIssues.length,2);
const queues=async queue=>ok(await api('supervisor',`work/cleaning-queue?branchId=${branch.branchId}&queue=${queue}`));
assert((await queues('incidents')).items.some(w=>w.workId===state.workId));assert(!(await queues('inspect')).items.some(w=>w.workId===state.workId));
assert.equal((await api('supervisor',`work/${state.workId}/inspection-result`,{rowVersion:d.work.fields.rowVersion,operationKey:key(),result:'PASS',note})).data.title,'CLEANING_INCIDENT_REVIEW_REQUIRED');
for(const [kind,id]of Object.entries(state.reports)){
 const report=await issue(id);if(report.stage==='ROOM_ISSUE_REPORTED'){
  d=await detail();const payload={rowVersion:d.work.fields.rowVersion,operationKey:key(),relatedId:id,reason:note};
  assert.equal((await api('hk',`work/${state.workId}/confirm-room-issue`,payload)).status,403);
  ok(await api('supervisor',`work/${state.workId}/confirm-room-issue`,payload));ok(await api('supervisor',`work/${state.workId}/confirm-room-issue`,payload));
  const routed=await issue(id);assert.equal(routed.stage,kind==='BROKEN'?'ROOM_ISSUE_REPAIR_REQUESTED':'ROOM_ISSUE_MATERIAL_REQUESTED');
  if(kind==='MISSING')assert.equal(routed.materials.length,1);
 }
}
assert(!(await queues('all')).items.some(w=>w.workId===state.workId),'Forwarded work must leave current queue');
state.checks.push('Separate incident inspection queue; normal PASS blocked; correct technical/warehouse routing; confirmation retry safe');save();
const broken=state.reports.BROKEN;
if((await issue(broken)).stage==='ROOM_ISSUE_REPAIR_REQUESTED')await issueAct(broken,'create-repair','maintenance');
let b=await issue(broken);if(b.stage==='ROOM_ISSUE_REPAIR_CREATED'){
 if(b.maintenance.some(w=>w.status==='PENDING'))await issueAct(broken,'start-repair','maintenance');
 await issueAct(broken,'report-remedy','maintenance');
}
if((await issue(broken)).stage==='ROOM_ISSUE_INSPECTION_REQUESTED')await issueAct(broken,'inspection-pass');
assert.notEqual((await room()).readiness,'READY');
const missing=state.reports.MISSING;let m=await issue(missing);
if(m.stage==='ROOM_ISSUE_MATERIAL_REQUESTED'){
 const blocked=await api('supervisor','work/room-issues/'+missing+'/request-inspection',{rowVersion:m.work.fields.rowVersion,operationKey:key(),note});assert.equal(blocked.data.title,'ROOM_ISSUE_MATERIAL_NOT_FULFILLED');
 const material=m.materials[0];let doc=ok(await api('store',`documents/${material.documentId}`));
 if(doc.document.fields.status!=='FULLY_ISSUED'){
  assert.equal(doc.document.fields.status,'APPROVED');
  const derived=ok(await api('store',`documents/${material.documentId}/derive`,{rowVersion:doc.document.fields.rowVersion,operationKey:key(),kind:'ISSUE',warehouseId:branch.warehouseId,reason:note,lines:doc.lines.map(l=>({lineId:l.fields.lineId,quantity:l.fields.approvedQty}))}));
  const fields=derived.fields??derived.document?.fields;assert(fields,'Derived document response required');
  ok(await api('store',`documents/${fields.documentId}/actions/post`,{rowVersion:fields.rowVersion,operationKey:key(),reason:note}));
 }
 assert.equal((await issue(missing)).stage,'ROOM_ISSUE_MATERIAL_REQUESTED');assert((await issue(missing)).canRequestInspection);assert((await queues('incidents')).items.some(w=>w.workId===state.workId));await issueAct(missing,'request-inspection');assert.equal((await issue(missing)).stage,'ROOM_ISSUE_INSPECTION_REQUESTED');
 assert((await queues('incidents')).items.some(w=>w.workId===state.workId),'Delivered work returns to inspection queue');
 assert((await api('store','documents/'+material.documentId)).data.inventory.receivingRoom.includes('P0_101'));
 state.checks.push('Full issue automatically returns root and child workflows to inspection with room destination');save();
}
if((await issue(missing)).stage==='ROOM_ISSUE_INSPECTION_REQUESTED')await issueAct(missing,'inspection-pass');
d=await detail();assert.equal(d.hasOpenRoomIssues,false);assert(d.capabilities.canRecordPaperInspection);
ok(await api('supervisor',`work/${state.workId}/inspection-result`,{rowVersion:d.work.fields.rowVersion,operationKey:key(),result:'PASS',note}));
assert.equal((await room()).readiness,'READY');state.checks.push('Both remedies and independent reinspection completed; room Ready only after final cleaning inspection');save();
fs.writeFileSync(dir+'/cleaning-runtime.json',JSON.stringify(state,null,2));console.log(JSON.stringify(state));
