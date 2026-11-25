import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {verifyConfigurationDeltaBrowser} from './v2-configuration-delta-browser.mjs';

const dir='artifacts/restructure-v2',base='http://127.0.0.1:5779/api/v2',tests=[],runId=randomUUID();
let fixture,inventoryFixture,inventoryOwned=false,fixtureOwned=false,cleanup;
const scrub=s=>String(s).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
const run=(script,action,owner=runId)=>new Promise((resolve,reject)=>{
 const p=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/'+script+'.ps1','-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:owner}});
 let out='',err='';p.stdout.on('data',d=>out+=d);p.stderr.on('data',d=>err+=d);p.on('error',reject);
 p.on('exit',c=>{if(c)return reject(new Error(scrub(script+'/'+action+': '+err.slice(0,2000))));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});
});
const inventory=a=>run('v2-inventory-runtime-fixture',a),own=a=>run('v2-housekeeping-fixture',a),fields=x=>x.fields??x;
const actor=a=>fixture.actors.find(x=>x.alias===a);
async function setupOwned(script,metadata,setOwned){
 try{await run(script,'Setup');setOwned(true);}
 catch(error){
  // Setup records ownership before creating any TEST rows/sessions. If a later
  // step fails, clean only metadata belonging to this exact run in finally.
  try{const partial=JSON.parse((await readFile(dir+'/'+metadata+'/fixture.json','utf8')).replace(/^\uFEFF/,''));if(partial.runId===runId)setOwned(true);}catch{/* No owned metadata; preserve other runs. */}
  throw error;
 }
}
async function request(a,path,method='GET',body){
 const r=await fetch(base+path,{method,headers:{Authorization:'Bearer '+actor(a).token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
 let data;try{data=await r.json();}catch{data={};}return {status:r.status,data};
}
async function ok(a,path,method='GET',body){const r=await request(a,path,method,body);assert.equal(r.status,200,method+' '+path+': '+r.status+' '+JSON.stringify(r.data));return r.data;}
async function test(name,fn){try{await fn();tests.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});throw e;}}
const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
const reason='Synthetic TEST role scope verification';
let broken,missing,room,asset,report,ordinary;
const rooms=()=>ok('manager','/rooms?branchId='+fixture.branch);
const issues=()=>ok('supervisor','/work/room-issues?branchId='+fixture.branch);
const reportBody=async(extra={})=>({branchId:fixture.branch,roomId:room.roomId,typeId:broken.referenceId,assetId:asset.assetId,roomRowVersion:(await rooms()).find(r=>r.roomId===room.roomId).rowVersion,operationKey:randomUUID(),...extra});
async function shift(command){
 const week=await ok('reception','/workday?branchId='+fixture.branch+'&date='+date),row=week.assignments.find(x=>x.assignmentId===actor('reception').assignment);assert.ok(row);
 return ok('reception','/workday/assignments/'+row.assignmentId+'/'+command,'POST',{rowVersion:row.rowVersion,operationKey:randomUUID()});
}
try{
 await setupOwned('v2-inventory-runtime-fixture','inventory-private',v=>inventoryOwned=v);
 await setupOwned('v2-housekeeping-fixture','housekeeping-private',v=>fixtureOwned=v);
 fixture=JSON.parse((await readFile(dir+'/housekeeping-private/fixture.json','utf8')).replace(/^\uFEFF/,''));
 inventoryFixture=JSON.parse((await readFile(dir+'/inventory-private/fixture.json','utf8')).replace(/^\uFEFF/,''));
 await inventory('Start');
 if(process.env.HOTELOPS_PHONE_REPORTS_ONLY!=='1'){
 await test('another run cannot inspect or clean the synthetic housekeeping fixture',async()=>{
  for(const action of ['Inspect','SeedExpiredShift','Cleanup'])await assert.rejects(run('v2-housekeeping-fixture',action,randomUUID()),/Fixture belongs to another run/);
  assert.ok((await own('Inspect')).stock.length>0);
 });
 await test('isolated Reception account and room object use current role and configuration APIs',async()=>{
  room=fixture.rooms[0];asset=fixture.assets.find(a=>a.roomId===room.roomId);
  await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,assetId:asset.assetId,supplyGroup:'C',initialQuantity:0,minimumStock:0,perGuestQuantity:0,isActive:true});
  broken=await ok('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'BROKEN',name:'TEST broken asset',isActive:true});
  missing=await ok('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'MISSING',name:'TEST missing item',isActive:true});
 });
 await test('Reception assigned but not started cannot report; room and issue queue remain unchanged',async()=>{
  const before={rooms:await rooms(),issues:await issues()};
  const r=await request('reception','/work/room-issues','POST',await reportBody());assert.equal(r.status,403);assert.match(JSON.stringify(r.data),/RECEPTION_NOT_ON_SHIFT/);
  assert.deepEqual({rooms:await rooms(),issues:await issues()},before);
 });
 await test('OM room report is denied by IR_CREATE permission; generic incident and ordinary create use the read-only role guard',async()=>{
  const before={rooms:await rooms(),issues:await issues()};
  const denied=await request('manager','/work/room-issues','POST',await reportBody());assert.equal(denied.status,403);assert.match(JSON.stringify(denied.data),/PERMISSION_DENIED/);
  for(const kind of ['INCIDENT','INTERNAL_REQUEST']){
   const r=await request('manager','/work','POST',{branchId:fixture.branch,kind,title:'TEST forbidden OM create',roomId:room.roomId});assert.equal(r.status,403);assert.match(JSON.stringify(r.data),/WORK_ROLE_READ_ONLY/);
  }
  assert.deepEqual({rooms:await rooms(),issues:await issues()},before);
 });
 await test('on-shift Reception reports configured damage without notes or cleaning assignment',async()=>{
  await shift('start');
  const body=await reportBody();report=fields(await ok('reception','/work/room-issues','POST',body));
  const detail=await ok('supervisor','/work/room-issues/'+report.workId);
  assert.equal(detail.reporterUserId,actor('reception').user);assert.equal(detail.info.detectedByUserId,actor('reception').user);assert.equal(detail.info.cleaningWorkId,null);
  assert.equal(detail.stage,'ROOM_ISSUE_REPORTED');assert.equal(fields(detail.work).description,null);
  const before=await issues();
  const duplicate=await request('reception','/work/room-issues','POST',{...body,roomRowVersion:(await rooms()).find(r=>r.roomId===room.roomId).rowVersion});assert.equal(duplicate.status,409);assert.match(JSON.stringify(duplicate.data),/OPERATION_ALREADY_APPLIED/);
  assert.deepEqual(await issues(),before);assert.deepEqual(await ok('supervisor','/work/room-issues/'+report.workId),detail);
 });
 await test('Reception cannot report missing items or another branch; neither attempt mutates room or issue',async()=>{
  const before={rooms:await rooms(),issues:await issues()};
  const missingResult=await request('reception','/work/room-issues','POST',await reportBody({typeId:missing.referenceId}));assert.equal(missingResult.status,403);assert.match(JSON.stringify(missingResult.data),/RECEPTION_DAMAGE_REPORT_ONLY/);
  assert.equal((await request('reception','/work/room-issues','POST',await reportBody({branchId:inventoryFixture.otherBranch}))).status,403);
  assert.deepEqual({rooms:await rooms(),issues:await issues()},before);
 });
 await test('Reception after ending shift cannot report new damage',async()=>{
  await shift('end');const before={rooms:await rooms(),issues:await issues()};
  const r=await request('reception','/work/room-issues','POST',await reportBody());assert.equal(r.status,403);assert.match(JSON.stringify(r.data),/RECEPTION_NOT_ON_SHIFT/);
  assert.deepEqual({rooms:await rooms(),issues:await issues()},before);
 });
 await test('OM reads ordinary HK request while room incidents stay outside ordinary request inbox',async()=>{
  const catalog=await ok('manager','/work/internal-request-types?branchId='+fixture.branch),sender=catalog.roles.find(r=>r.code==='HK_STAFF');assert.ok(sender);
  const type=await ok('manager','/work/internal-request-types?branchId='+fixture.branch,'POST',{name:'TEST ordinary request '+fixture.prefix,isActive:true,senderRoleIds:[sender.roleId],recipientUserIds:[actor('supervisor').user]});
  ordinary=fields(await ok('hk','/work','POST',{branchId:fixture.branch,kind:'INTERNAL_REQUEST',title:'TEST ordinary '+fixture.prefix,categoryId:type.referenceId,assigneeUserId:actor('supervisor').user}));
  const listing=await ok('manager','/requests?branchId='+fixture.branch+'&queue=all');assert.ok(listing.items.some(r=>r.workId===ordinary.workId));assert.ok(!listing.items.some(r=>r.workId===report.workId));
  const detail=await ok('manager','/requests/'+ordinary.workId);assert.deepEqual(detail.actions,[]);
  assert.equal(fields((await ok('manager','/work/'+ordinary.workId)).work).workId,ordinary.workId);
  assert.equal((await request('manager','/requests/'+report.workId)).status,403);
 });
 await test('OM cannot execute approve reject assign cancel or complete ordinary request; all history is unchanged',async()=>{
  const before=await ok('supervisor','/work/'+ordinary.workId),beforeFlow=await ok('manager','/requests/'+ordinary.workId);
  for(const command of ['accept','assign','approve','reject','cancel','complete']){
   const r=await request('manager','/work/'+ordinary.workId+'/actions/'+command,'POST',{rowVersion:fields(before.work).rowVersion,operationKey:randomUUID(),reason,userId:actor('supervisor').user});assert.equal(r.status,403,command+': '+JSON.stringify(r.data));assert.match(JSON.stringify(r.data),/WORK_ROLE_READ_ONLY/);
  }
  assert.deepEqual(await ok('supervisor','/work/'+ordinary.workId),before);assert.deepEqual(await ok('manager','/requests/'+ordinary.workId),beforeFlow);
 });
 await test('branch-scoped OM cannot read the foreign branch ordinary request inbox',async()=>{
  assert.equal((await request('manager','/requests?branchId='+inventoryFixture.otherBranch+'&queue=all')).status,403);
 });
 await test('room report types can be created without replacing existing kinds; duplicate and foreign edits are rejected',async()=>{
  const before=await ok('manager','/work/room-issue-types?branchId='+fixture.branch);
  const created=await ok('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'BROKEN',name:'TEST custom '+fixture.prefix,isActive:true,createNew:true});
  const after=await ok('manager','/work/room-issue-types?branchId='+fixture.branch);
  assert.equal(after.length,before.length+1);assert.ok(after.some(r=>r.referenceId===broken.referenceId));assert.ok(after.some(r=>r.referenceId===created.referenceId&&r.kind==='BROKEN'));
  assert.equal((await request('manager','/work/room-issue-types?branchId='+fixture.branch,'PUT',{kind:'MISSING',name:'TEST custom '+fixture.prefix,isActive:true,createNew:true})).status,409);
  assert.equal((await request('manager','/work/room-issue-types?branchId='+inventoryFixture.otherBranch,'PUT',{kind:'BROKEN',name:'Forbidden',isActive:true,referenceId:created.referenceId,rowVersion:created.rowVersion})).status,403);
  assert.deepEqual(await ok('manager','/work/room-issue-types?branchId='+fixture.branch),after);
 });
 await test('role-based recipient settings enforce current branch role and do not rewrite existing request creator',async()=>{
  const catalog=await ok('manager','/work/internal-request-types?branchId='+fixture.branch);
  const sender=catalog.roles.find(r=>r.code==='HK_STAFF'),receiver=catalog.recipientRoles.find(r=>r.code==='HOUSEKEEPING_SUPERVISOR');assert.ok(sender&&receiver);
  assert.ok(!catalog.recipientRoles.some(r=>r.code==='OPERATION_MANAGER'));
  const type=await ok('manager','/work/internal-request-types?branchId='+fixture.branch,'POST',{name:'TEST role receiver '+fixture.prefix,isActive:true,senderRoleIds:[sender.roleId],recipientRoleIds:[receiver.roleId]});
  const created=fields(await ok('hk','/work','POST',{branchId:fixture.branch,kind:'INTERNAL_REQUEST',title:'TEST role delivery '+fixture.prefix,categoryId:type.referenceId,assigneeUserId:actor('supervisor').user}));
  assert.equal(created.createdBy,actor('hk').user);
  const denied=await request('hk','/work','POST',{branchId:fixture.branch,kind:'INTERNAL_REQUEST',title:'Wrong receiver',categoryId:type.referenceId,assigneeUserId:actor('reception').user});assert.equal(denied.status,403);assert.match(JSON.stringify(denied.data),/REQUEST_RECIPIENT_NOT_ALLOWED/);
  const noReceiver=await request('hk','/work','POST',{branchId:fixture.branch,kind:'INTERNAL_REQUEST',title:'No receiver',categoryId:type.referenceId});assert.equal(noReceiver.status,400);
  const rows=(await ok('manager','/work/internal-request-types?branchId='+fixture.branch)).items;assert.deepEqual(rows.find(r=>r.referenceId===type.referenceId).recipientRoleIds,[receiver.roleId]);
 });
 await test('OM activity report contains actual shift and incident events, scopes branch and validates range',async()=>{
  const rows=await ok('manager','/insights/reports/operations-events?branchId='+fixture.branch);
  assert.ok(Array.isArray(rows));assert.ok(rows.some(r=>r.category==='Ca làm việc'&&r.person));assert.ok(rows.some(r=>r.category==='Sự cố phòng'&&r.subject===report.title));
  assert.equal((await request('manager','/insights/reports/operations-events?branchId='+inventoryFixture.otherBranch)).status,403);
  assert.equal((await request('manager','/insights/reports/operations-events?branchId='+fixture.branch+'&from=2024-01-01&to=2026-11-25')).status,400);
 });
 await test('expired attendance appears once in OM report and local-day filter equals the branch UTC interval',async()=>{
  const expired=await own('SeedExpiredShift');
  for(let i=0;i<2;i++)await ok('manager','/workday?branchId='+fixture.branch+'&date='+date);
  const history=await ok('manager','/workday/assignments/'+expired.assignmentId+'/history');
  assert.equal(history.filter(e=>e.action==='AUTO_END').length,1);
  const local=await ok('manager','/insights/reports/operations-events?branchId='+fixture.branch+'&from='+date+'&to='+date+'&localDates=true');
  assert.equal(local.filter(e=>e.action==='Tự kết thúc ca hết giờ'&&e.person==='TEST hk').length,1);
  const start=new Date(date+'T00:00:00+07:00'),end=new Date(start.getTime()+86400000-1);
  const utc=await ok('manager','/insights/reports/operations-events?branchId='+fixture.branch+'&from='+encodeURIComponent(start.toISOString())+'&to='+encodeURIComponent(end.toISOString()));
  assert.deepEqual(local,utc);
 });
 await test('configuration and request workspace browser: role-only settings, custom report creation, one header and no staff-supplies view',async()=>{
  await verifyConfigurationDeltaBrowser(fixture,dir);
 });
 }
 if(process.env.HOTELOPS_PHONE_REPORTS_PROBE==='1'||process.env.HOTELOPS_PHONE_REPORTS_ONLY==='1'){
  const {verifyPhoneAndResourceReports}=await import('./v2-phone-resource-reports-probe.mjs');
  await test('actual candidate phone and OM stock/assets report API and browser',()=>verifyPhoneAndResourceReports({fixture,foreignBranch:inventoryFixture.otherBranch,ok,request,directory:dir}));
 }
}catch(e){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:scrub(e.message)});console.log(scrub(e.message));}
finally{
 for(const [name,fn] of [...(inventoryOwned?[['stop',()=>inventory('Stop')]]:[]),...(fixtureOwned?[['fixture cleanup',()=>own('Cleanup')]]:[]),...(inventoryOwned?[['inventory cleanup',()=>inventory('Cleanup')]]:[])]){
  try{const result=await fn();if(name==='fixture cleanup')cleanup=result;}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});}
 }
 await writeFile(dir+'/role-scope-runtime-results.json',JSON.stringify({at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',mode:process.env.HOTELOPS_PHONE_REPORTS_ONLY==='1'?'Focused phone and resource reports only; baseline role/configuration suite not rerun':'HTTP and Edge on isolated synthetic branch and expiring sessions; no password login or external delivery',cleanup,tests},null,2));
 console.log(JSON.stringify({passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,cleanup}));if(tests.some(t=>t.status==='FAIL'))process.exitCode=1;
}
