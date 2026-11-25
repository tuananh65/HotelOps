import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {verifyMaintenanceInspection,verifyIncidentAdvisoryBrowser} from './v2-maintenance-browser.mjs';
import {createRepairBrowser,prepareRepairBrowser,completeRepairBrowser,reviewRepairBrowser,reportDamageBrowser,confirmDamageBrowser,inspectRepairBrowser} from './v2-maintenance-workflow-browser.mjs';
import {verifyCleaningCompletionBrowser} from './v2-cleaning-browser.mjs';
const dir='artifacts/restructure-v2',base='http://127.0.0.1:5779/api/v2',tests=[];
const runId=randomUUID();
const focused=process.env.HOTELOPS_INCIDENT_FOCUSED_PROBE==='1';
let fixture,inventoryOwned=false,fixtureOwned=false,cleanup;
const scrub=s=>String(s).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
const run=(script,action,owner=runId,extraEnv={})=>new Promise((resolve,reject)=>{
 const p=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/'+script+'.ps1','-Action',action],{windowsHide:true,env:{...process.env,...extraEnv,HOTELOPS_V2_RUN_ID:owner}});
 let out='',err='';p.stdout.on('data',d=>out+=d);p.stderr.on('data',d=>err+=d);p.on('error',reject);
 p.on('exit',c=>{if(c)return reject(new Error(scrub(script+'/'+action+': '+err.slice(0,2000))));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});
});
const inventory=a=>run('v2-inventory-runtime-fixture',a),own=a=>run('v2-housekeeping-fixture',a),fields=x=>x.fields??x;
const actor=a=>fixture.actors.find(x=>x.alias===a);
async function setupOwned(script,metadata,setOwned){
 try{await run(script,'Setup');setOwned(true);}
 catch(error){
  try{const partial=JSON.parse((await readFile(dir+'/'+metadata+'/fixture.json','utf8')).replace(/^\uFEFF/,''));if(partial.runId===runId)setOwned(true);}catch{/* Preserve unowned fixtures. */}
  throw error;
 }
}
async function request(a,path,method='GET',body){
 const r=await fetch(base+path,{method,headers:{Authorization:'Bearer '+actor(a).token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
 let data;try{data=await r.json();}catch{data={};}return {status:r.status,data};
}
async function ok(a,path,method='GET',body){const r=await request(a,path,method,body);assert.equal(r.status,200,method+' '+path+': '+r.status+' '+JSON.stringify(r.data));return r.data;}
async function test(name,fn){try{await fn();tests.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});throw e;}}
const version=async(a,id)=>fields((await ok(a,'/work/'+id)).work).rowVersion;
const act=async(a,id,command,extra={})=>ok(a,'/work/'+id+'/actions/'+command,'POST',{rowVersion:await version(a,id),operationKey:randomUUID(),...extra});
const detail=id=>ok('lead','/work/room-issues/'+id);
async function issueBody(id,extra={}){const d=await detail(id);return {rowVersion:fields(d.work).rowVersion,roomRowVersion:d.room.rowVersion,operationKey:randomUUID(),...extra};}
const issueAct=async(a,id,command,extra={})=>ok(a,'/work/room-issues/'+id+'/'+command,'POST',await issueBody(id,extra));
async function checklist(a,id){const d=await ok(a,'/work/'+id);assert.ok(d.checklist.length);for(const i of d.checklist.map(fields))await ok(a,'/work/'+id+'/checklist/'+i.workChecklistItemId,'PUT',{rowVersion:i.rowVersion,outcome:'PASS'});}
const reason='Synthetic TEST physical verification',date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
let type;
try{
 await setupOwned('v2-inventory-runtime-fixture','inventory-private',v=>inventoryOwned=v);
 await setupOwned('v2-housekeeping-fixture','housekeeping-private',v=>fixtureOwned=v);
 fixture=JSON.parse((await readFile(dir+'/housekeeping-private/fixture.json','utf8')).replace(/^\uFEFF/,''));await inventory('Start');
 await test('cleanup from another run cannot stop this TEST API',async()=>{
  const result=await run('v2-inventory-runtime-fixture','Stop',randomUUID());assert.equal(result.notOwned,true);
  assert.equal((await fetch('http://127.0.0.1:5779/health')).status,200);
 });
 await test('TEST maintenance staff and supervisor retain separate roles; OM configures room assets',async()=>{
  const fill=fixture.items.find(x=>x.code.endsWith('-fill'));
  await ok('manager','/inventory-lifecycle/profiles','PUT',{branchId:fixture.branch,itemId:fill.itemId,classification:'CONSUMABLE',expiryPolicy:'NONE',reason});
  await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,itemId:fill.itemId,supplyGroup:'A',warehouseId:fixture.warehouse,unitId:fixture.unit,initialQuantity:10,minimumStock:0,perGuestQuantity:1,isActive:true});
  const receipt=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST delivery'}));
  const line=await ok('store','/documents/'+receipt.documentId+'/lines','POST',{rowVersion:receipt.rowVersion,itemId:fill.itemId,unitId:fixture.unit,quantity:30});
  await ok('store','/documents/'+receipt.documentId+'/actions/post','POST',{rowVersion:fields(line.document).rowVersion,operationKey:randomUUID(),reason});
  const batches=await ok('store','/inventory-lifecycle/batches?branchId='+fixture.branch+'&itemId='+fill.itemId);assert.equal(batches.length,1);
  await ok('store','/inventory-lifecycle/batches/'+batches[0].batchId,'PUT',{rowVersion:batches[0].rowVersion,lotNumber:'TEST-MAINT-FIFO',receivedAt:new Date().toISOString(),status:'READY',reason});
  for(const a of ['manager','supervisor','hk','lead','technician']){
   const week=await ok(a,'/workday?branchId='+fixture.branch+'&date='+date),row=week.assignments.find(x=>x.assignmentId===actor(a).assignment);assert.ok(row);
   await ok(a,'/workday/assignments/'+row.assignmentId+'/start','POST',{rowVersion:row.rowVersion,operationKey:randomUUID()});
  }
  for(const asset of fixture.assets)await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,assetId:asset.assetId,supplyGroup:'C',initialQuantity:0,minimumStock:0,perGuestQuantity:0,isActive:true});
  type=await ok('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'BROKEN',name:'TEST broken asset',isActive:true});
 });
 if(focused)await test('focused precondition: two synthetic rooms become Ready through actual cleaning and inspection',async()=>{
  for(const room of fixture.rooms.slice(0,2)){
   const cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST focused setup cleaning',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
   await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
   const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
   await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:l.requiredQuantity,usableQuantity:l.requiredQuantity,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
   await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');
   await ok('supervisor',`/work/${cleaning.workId}/inspection-result`,'POST',{rowVersion:await version('supervisor',cleaning.workId),operationKey:randomUUID(),result:'PASS'});
   assert.equal((await ok('manager','/rooms?branchId='+fixture.branch)).find(r=>r.roomId===room.roomId).readiness,'READY');
  }
 });
 for(const external of focused?[]:[false,true]){
  const label=external?'external':'internal',room=fixture.rooms[external?1:0],asset=fixture.assets.find(x=>x.roomId===room.roomId);let cleaning,issue,repair;
  await test(label+': assigned cleaner reports damaged asset without note; discoverer stays distinct from cause',async()=>{
   cleaning=fields(await ok('supervisor','/work','POST',{branchId:fixture.branch,kind:'CLEANING',title:'TEST '+label+' cleaning',roomId:room.roomId,assigneeUserId:actor('hk').user,durationMinutes:30,checklistTemplateId:fixture.checklist}));
   await act('hk',cleaning.workId,'accept');await act('hk',cleaning.workId,'start');
   const lines=await ok('hk','/supplies/room-assessments/'+cleaning.workId);
   await ok('hk','/supplies/room-assessments/'+cleaning.workId,'POST',{operationKey:randomUUID(),lines:lines.map(l=>({reservationId:l.reservationId,rowVersion:l.rowVersion,currentQuantity:4,usableQuantity:4,unusableQuantity:0,expiredQuantity:0,replenishedQuantity:0}))});
   const objects=await ok('hk','/work/room-issue-objects?branchId='+fixture.branch+'&roomId='+room.roomId+'&workId='+cleaning.workId);
   issue=fields(external?await ok('hk','/work/room-issues','POST',{branchId:fixture.branch,roomId:room.roomId,typeId:type.referenceId,assetId:asset.assetId,cleaningWorkId:cleaning.workId,roomRowVersion:objects.rowVersion,operationKey:randomUUID(),detectedByUserId:actor('peer').user}):await reportDamageBrowser(fixture,cleaning.workId,room.roomId,asset.assetId,type.referenceId));
   const d=await detail(issue.workId);assert.equal(d.reporterUserId,actor('hk').user);assert.equal(d.info.detectedByUserId,actor(external?'peer':'hk').user);assert.equal(d.investigation.length,0);
   if(external){await checklist('hk',cleaning.workId);await act('hk',cleaning.workId,'complete');}else await verifyCleaningCompletionBrowser(fixture,cleaning.workId);
  });
  await test(label+': HKS confirms damage; OM and cleaner cannot accept technical work',async()=>{
   await confirmDamageBrowser(fixture,cleaning.workId);
   assert.equal((await detail(issue.workId)).stage,'ROOM_ISSUE_REPAIR_REQUESTED');
   for(const a of ['manager','hk']){const r=await request(a,'/work/room-issues/'+issue.workId+'/create-repair','POST',await issueBody(issue.workId,{externalRepair:external,userId:actor('technician').user,checklistTemplateId:fixture.repairChecklist}));assert.equal(r.status,403);}
   const queue=await ok('supervisor','/work/cleaning-queue?branchId='+fixture.branch+'&queue=all');assert.ok(!queue.items.some(x=>x.workId===cleaning.workId));
  });
  await test(label+': cause investigation preserves reporter and prior opinions',async()=>{
   for(const [a,cause,note] of [['supervisor','UNKNOWN','Inspection pending technical diagnosis'],['lead','WEAR','Technical wear observed']]){
    await ok(a,'/work/room-issues/'+issue.workId+'/investigation','POST',{rowVersion:fields((await detail(issue.workId)).work).rowVersion,operationKey:randomUUID(),cause,note});
   }
   const d=await detail(issue.workId);assert.equal(d.investigation.length,2);assert.equal(d.reporterUserId,actor('hk').user);assert.equal(d.info.detectedByUserId,actor(external?'peer':'hk').user);
   const denied=await request('technician','/work/room-issues/'+issue.workId+'/investigation','POST',{rowVersion:fields(d.work).rowVersion,operationKey:randomUUID(),cause:'EMPLOYEE',note:'Unauthorized opinion',relatedUserId:actor('hk').user});assert.equal(denied.status,403);
  });
  await test(label+': incident advisory respects branch, reports statistical evidence and cannot change incident or inventory',async()=>{
   const before={issue:await detail(issue.workId),inventory:await own('Inspect')};
   const role=external?'lead':'supervisor';
   const advice=await verifyIncidentAdvisoryBrowser(fixture,issue.workId,role,dir);
   assert.equal(advice.evidence.hasAsset,true);assert.equal(advice.evidence.suggestedCategory,type.name);assert.equal(advice.evidence.repeatedAssetReportsIn90Days,0);
   assert.ok(Array.isArray(advice.evidence.similarReports));
   const otherBranch=JSON.parse((await readFile(dir+'/inventory-private/fixture.json','utf8')).replace(/^\uFEFF/,'')).otherBranch;
   assert.equal((await request(role,'/advisory','POST',{branchId:otherBranch,kind:'INCIDENT',incidentId:issue.workId,days:14})).status,403);
   assert.equal((await request(role,'/advisory','POST',{branchId:fixture.branch,kind:'INCIDENT',days:14})).status,400);
   assert.equal((await request(role,'/advisory','POST',{branchId:fixture.branch,kind:'INCIDENT',incidentId:cleaning.workId,days:14})).status,404);
   assert.deepEqual({issue:await detail(issue.workId),inventory:await own('Inspect')},before);
  });
  await test(label+': repair acceptance needs no note, retries create one child and wrong executors are denied',async()=>{
   for(const invalid of [{externalRepair:false,userId:actor('hk').user,checklistTemplateId:fixture.repairChecklist},{externalRepair:external,userId:actor('technician').user,checklistTemplateId:fixture.checklist}]){
    const denied=await request('lead','/work/room-issues/'+issue.workId+'/create-repair','POST',await issueBody(issue.workId,invalid));assert.equal(denied.status,400);assert.equal((await detail(issue.workId)).maintenance.length,0);
   }
   const body=await createRepairBrowser(fixture,issue.workId,external);
   const created=await detail(issue.workId);
   await ok('lead','/work/room-issues/'+issue.workId+'/create-repair','POST',body);assert.deepEqual(await detail(issue.workId),created);
   const d=await detail(issue.workId);assert.equal(d.maintenance.length,1);repair=d.maintenance[0];
   await prepareRepairBrowser(fixture,repair.workId,external);
   const denied=await request('hk','/work/'+repair.workId+'/actions/complete','POST',{rowVersion:await version('lead',repair.workId),operationKey:randomUUID(),reason});assert.equal(denied.status,403);
   assert.equal(fields((await ok('lead','/work/'+repair.workId)).work).status,'IN_PROGRESS');
  });
  if(!external&&process.env.HOTELOPS_REPLACEMENT_PROBE==='1'){
   const {verifyReplacementHttp}=await import('./v2-replacement-http-probe.mjs');
   await verifyReplacementHttp({fixture,repair,asset,room,test,ok,request,version,checklist,own});
  }
  await test(label+': checklist and technical review block early inspection; complete repair returns HKS inspection queue',async()=>{
   const early=await request('supervisor','/work/room-issues/'+issue.workId+'/inspection-pass','POST',await issueBody(issue.workId));assert.ok([403,409].includes(early.status));
   const blocked=external?await request('lead','/work/room-issues/'+issue.workId+'/report-remedy','POST',await issueBody(issue.workId,{note:reason})):await request('technician','/work/'+repair.workId+'/actions/complete','POST',{rowVersion:await version('technician',repair.workId),operationKey:randomUUID(),reason});
   assert.equal(blocked.status,409);
   await completeRepairBrowser(fixture,repair.workId,external);
   if(!external){
    const selfReview=await request('technician','/work/room-issues/'+issue.workId+'/report-remedy','POST',await issueBody(issue.workId,{note:reason}));assert.equal(selfReview.status,403);
   }
   await reviewRepairBrowser(fixture,issue.workId,external);
   const d=await detail(issue.workId);assert.equal(d.stage,'ROOM_ISSUE_INSPECTION_REQUESTED');assert.equal(d.maintenance[0].technicalReviewed,true);
   const q=await ok('supervisor','/work/cleaning-queue?branchId='+fixture.branch+'&queue=inspect');assert.ok(q.items.some(x=>x.workId===cleaning.workId));
  });
  if(!external)await test('internal: failed HK inspection resets repair checklist and keeps previous review history',async()=>{
   await inspectRepairBrowser(fixture,issue.workId,false);
   const d=await detail(issue.workId);assert.equal(d.stage,'ROOM_ISSUE_REPAIR_CREATED');assert.equal(d.maintenance[0].status,'PENDING');
   const reset=await ok('technician','/work/'+repair.workId);assert.ok(reset.checklist.map(fields).every(i=>i.outcome===null));
   await act('technician',repair.workId,'start');await checklist('technician',repair.workId);await act('technician',repair.workId,'complete',{reason});
   await issueAct('lead',issue.workId,'report-remedy',{note:reason});
   const again=await detail(issue.workId);assert.equal(again.stage,'ROOM_ISSUE_INSPECTION_REQUESTED');assert.equal(again.events.filter(e=>e.eventType==='ROOM_ISSUE_INSPECTION_REQUESTED').length,2);
  });
  await test(label+': one HKS PASS without note closes repair, incident and cleaning and releases Ready',async()=>{
   if(external)await verifyMaintenanceInspection(fixture,issue.workId,dir);
   else await inspectRepairBrowser(fixture,issue.workId,true);
   const d=await detail(issue.workId);assert.equal(fields(d.work).status,'CLOSED');assert.equal(d.maintenance[0].status,'CLOSED');
   const rooms=await ok('manager','/rooms?branchId='+fixture.branch);assert.equal(rooms.find(x=>x.roomId===room.roomId).readiness,'READY');
   assert.equal(fields((await ok('supervisor','/work/'+cleaning.workId)).work).status,'COMPLETED');
  });
 }
 if(process.env.HOTELOPS_LEGACY_PROBE==='1'){
  const {verifyMaintenanceLegacy}=await import('./v2-maintenance-legacy-probe.mjs');
  await verifyMaintenanceLegacy({fixture,test,ok,request,act,checklist,issueAct,detail,seedLegacy:payload=>run('v2-maintenance-legacy-fixture','Seed',runId,{HOTELOPS_LEGACY_SEED:JSON.stringify(payload)})});
 }
 if(process.env.HOTELOPS_QUANTITY_PROBE==='1'){
  const {verifyMaintenanceQuantity}=await import('./v2-maintenance-quantity-probe.mjs');
  await verifyMaintenanceQuantity({fixture,test,ok,request,act,checklist,issueAct,detail,issueBody,snapshot:()=>run('v2-maintenance-snapshot','Inspect')});
 }
 if(process.env.HOTELOPS_BLOCKERS_PROBE==='1'){
  const {verifyMaintenanceBlockers}=await import('./v2-maintenance-blockers-probe.mjs');
  await verifyMaintenanceBlockers({fixture,test,ok,request,act,checklist,issueAct,detail});
 }
}catch(e){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:scrub(e.message)});console.log(scrub(e.message));}
finally{
 for(const [name,fn] of [...(inventoryOwned?[['stop',()=>inventory('Stop')]]:[]),...(fixtureOwned?[['fixture cleanup',()=>own('Cleanup')]]:[]),...(inventoryOwned?[['inventory cleanup',()=>inventory('Cleanup')]]:[])]){
  try{const result=await fn();if(name==='fixture cleanup')cleanup=result;}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});}
 }
 await writeFile(dir+'/maintenance-runtime-results.json',JSON.stringify({at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',mode:focused?'Focused optional incident probes; only tests listed below were executed; base repair/replacement not rerun':'HTTP and SQL synthetic fixture; external final cause investigation and HKS inspection through browser; no password login coverage',probes:{legacy:process.env.HOTELOPS_LEGACY_PROBE==='1',quantity:process.env.HOTELOPS_QUANTITY_PROBE==='1',blockers:process.env.HOTELOPS_BLOCKERS_PROBE==='1'},cleanup,tests},null,2));
 console.log(JSON.stringify({passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,cleanup}));if(tests.some(t=>t.status==='FAIL'))process.exitCode=1;
}

