import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const base=process.env.STAFFING_API||'http://127.0.0.1:5786/api/v2',writes=process.env.STAFFING_FIXTURE_WRITES==='1';
const port=new URL(base).port;
assert(['5786','5787'].includes(port));if(writes)assert.equal(port,'5786','Business writes are fixture-only');
const database=port==='5786'?'HotelOpsCompactWorkdayTestStaffingV4202611221659':'HotelOpsCompactDB';
const snapshot=JSON.parse(fs.readFileSync(`artifacts/staffing-v4/validation-${database}-after.json`,'utf8').replace(/^\uFEFF/,''));
const roster=snapshot.roster,grants=snapshot.grants,results=[],tokens=new Map(),profiles=new Map();
const approval=fs.readFileSync('C:/Users/Hax/.codex/attachments/82bc7a51-170d-4678-8cc5-509c7bd68b67/Pasted text.txt','utf8');
const password=approval.match(/Mật khẩu dùng chung cho 56 tài khoản:\s*`([^`]+)`/)[1];
const dh=roster.find(u=>u.employee_code==='DH002').user_id,store=roster.find(u=>u.employee_code==='EMP050').user_id;
async function req(id,path,body,expected=200,method=body===undefined?'GET':'POST'){
 const r=await fetch(base+'/'+path,{method,headers:{'Content-Type':'application/json',...(tokens.has(id)?{Authorization:'Bearer '+tokens.get(id)}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 const data=await r.json().catch(()=>null);assert.equal(r.status,expected,`${id} ${method} ${path}: ${r.status} ${data?.title||''}`);return data;
}
async function login(user,expected=200){
 for(let i=0;i<6;i++){
  const r=await fetch(base+'/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:user.username,password})});
  if(r.status===429){console.log('Rate limit respected; waiting20s');await new Promise(r=>setTimeout(r,20000));continue;}
  assert.equal(r.status,expected,`login ${user.user_id}`);if(expected!==200)return;
  const a=await r.json();assert.equal(a.user.fields.userId,user.user_id);assert.equal(a.requiresPasswordChange,false);assert.equal(a.tokenType,'Bearer');assert.match(a.accessToken,/^[a-f0-9]{64}$/);assert(Date.parse(a.expiresAt)>Date.now());tokens.set(user.user_id,a.accessToken);return;
 }throw Error('Login rate limit did not clear');
}
async function check(name,fn){try{await fn();results.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){results.push({name,status:'FAIL',error:e.message});console.error('FAIL '+name+': '+e.message);}}
const action=(row,extra={})=>({rowVersion:row.rowVersion,operationKey:crypto.randomUUID(),reason:'Staffing V4 isolated verification',...extra});
const date=new Date().toLocaleDateString('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}),future=new Date(Date.parse(date+'T12:00:00Z')+86400000*2).toISOString().slice(0,10);
for(const u of roster)await check(`Account ${u.user_id}: login, no forced change, exact role/grants/branch`,async()=>{
 await login(u);const p=await req(u.user_id,'auth/me');profiles.set(u.user_id,p);assert.equal(p.requiresPasswordChange,false);
 assert.equal(p.roles.length,1);assert.equal(p.roles[0].code,u.role_code);assert.equal(p.roles[0].branchId,u.branch_id);
 const expected=grants.filter(g=>g.role_code===u.role_code&&g.permission_active&&g.role_active).map(g=>`${g.permission_code}:${g.data_scope}:${u.branch_id}`).sort();
 assert.deepEqual(p.grants.map(g=>`${g.code}:${g.dataScope}:${g.branchId}`).sort(),expected);
 assert.deepEqual(p.branches.map(b=>b.branchId).sort(),u.branch_id===null?[413,414]:[u.branch_id]);
 assert(!('passwordHash' in p.user.fields));assert(!('securityStamp' in p.user.fields));
});
await check('Admin56 active accounts; five system grants; operations denied',async()=>{
 const users=await req(424,'users');assert.equal(users.length,56);assert(users.every(u=>u.fields.accountStatus==='ACTIVE'));assert.equal(profiles.get(424).grants.length,5);
 await req(424,'insights/administration');await req(424,'work?branchId=413&kind=CLEANING',undefined,403);await req(424,`workday?branchId=413&date=${date}`,undefined,403);
});
await check('Owner12 global read grants on two branches; no user management',async()=>{
 assert.equal(profiles.get(438).grants.length,12);assert(profiles.get(438).grants.every(g=>g.dataScope==='ALL'&&g.branchId===null));
 for(const b of [413,414]){await req(438,`rooms?branchId=${b}`);await req(438,`insights/dashboard?branchId=${b}`);}await req(438,'users',undefined,403);
});
await check('All operational users read their branch; other branch rejected',async()=>{
 for(const u of roster.filter(x=>x.branch_id!==null)){
  const route=u.role_code==='OPERATION_MANAGER'?'insights/dashboard':u.role_code==='RECEPTION'?'rooms':u.role_code==='STOREKEEPER'?'insights/stock':u.role_code==='DEPT_MANAGER'?'workday':'work';
  const extra=route==='work'?`&kind=${u.role_code.startsWith('MAINTENANCE')?'MAINTENANCE':'CLEANING'}&mine=true`:route==='workday'?`&date=${date}`:'';
  await req(u.user_id,`${route}?branchId=${u.branch_id}${extra}`);await req(u.user_id,`${route}?branchId=${u.branch_id===413?414:413}${extra}`,undefined,403);
 }
});
const weeks=new Map();
await check('Both Department Heads manage all departments only in their branch;30 shift candidate lists correct',async()=>{
 for(const [id,b] of [[431,413],[dh,414]]){
  const w=await req(id,`workday?branchId=${b}&date=${date}`);weeks.set(b,w);assert(w.canManage);assert.equal(w.shifts.length,15);assert.equal(new Set(w.shifts.map(s=>s.departmentId)).size,4);
  await req(id,`workday?branchId=${b===413?414:413}&date=${date}`,undefined,403);
  for(const s of w.shifts){const c=await req(id,`workday/candidates?shiftId=${s.shiftId}&date=${future}`);const role=grants.find(g=>g.role_id===s.roleId).role_code;
   assert.deepEqual(c.map(x=>x.userId).sort((a,b)=>a-b),roster.filter(u=>u.branch_id===b&&u.role_code===role).map(u=>u.user_id).sort((a,b)=>a-b));assert.equal(s.capacity,s.roleId===4?4:s.roleId===5?3:1);
  }
 }
});
await check('DVCT Storekeeper inventory/document permissions preserved; DVBV denied',async()=>{
 await req(store,'insights/stock?branchId=414');for(const kind of ['RECEIPT','ISSUE','TRANSFER','STOCKTAKE','REPLENISHMENT','MATERIAL_REQUEST'])await req(store,`documents?branchId=414&kind=${kind}`);
 await req(store,'insights/stock?branchId=413',undefined,403);await req(store,'users',undefined,403);
});
if(writes){
 await check('Fixture: forbidden Owner/Admin writes do not bypass RBAC',async()=>{for(const id of [438,424])await req(id,'work',{branchId:413,kind:'MAINTENANCE',title:'Denied scope check'},403);});
 await check('Fixture: inactive staff excluded from Workday and login; restored ACTIVE',async()=>{
  const u=roster.filter(x=>x.branch_id===414&&x.role_code==='HK_STAFF').at(-1),shift=weeks.get(414).shifts.find(s=>s.roleId===4);
  let row=(await req(424,'users')).find(x=>x.fields.userId===u.user_id).fields;
  await req(u.user_id,'auth/logout',{});await req(424,`users/${u.user_id}/deactivate`,action(row));
  try{const c=await req(dh,`workday/candidates?shiftId=${shift.shiftId}&date=${future}`);assert(!c.some(x=>x.userId===u.user_id));await req(dh,'workday/assignments',action(shift,{shiftId:shift.shiftId,userId:u.user_id,workDate:future}),409);await login(u,401);}
  finally{row=(await req(424,'users')).find(x=>x.fields.userId===u.user_id).fields;await req(424,`users/${u.user_id}/activate`,action(row));await login(u);}
 });
 await check('Fixture: DVCT Workday assignment, stale versions, capacity, overlap, future-start and branch denial',async()=>{
  const w=weeks.get(414),s=w.shifts.find(x=>x.roleId===10&&x.startMinute===480),c=await req(dh,`workday/candidates?shiftId=${s.shiftId}&date=${future}`);assert(c.length>=2);
  await req(dh,'workday/assignments',action({...s,rowVersion:Buffer.alloc(8).toString('base64')},{shiftId:s.shiftId,userId:c[0].userId,workDate:future}),409);
  await req(431,'workday/assignments',action(s,{shiftId:s.shiftId,userId:c[0].userId,workDate:future}),403);
  const a=(await req(dh,'workday/assignments',action(s,{shiftId:s.shiftId,userId:c[0].userId,workDate:future}))).fields;
  const full=await req(dh,'workday/assignments',action(s,{shiftId:s.shiftId,userId:c[1].userId,workDate:future}),409);assert.equal(full.title,'SHIFT_CAPACITY_REACHED');
  await req(c[0].userId,`workday/assignments/${a.assignmentId}/start`,action(a),409);
  const hk=w.shifts.find(x=>x.roleId===4&&x.startMinute===480),staff=roster.find(x=>x.branch_id===414&&x.role_code==='HK_STAFF').user_id;
  const a2=(await req(dh,'workday/assignments',action(hk,{shiftId:hk.shiftId,userId:staff,workDate:future}))).fields;
  const overlap=await req(dh,'workday/assignments',action(hk,{shiftId:hk.shiftId,userId:staff,workDate:future}),409);assert.equal(overlap.title,'SHIFT_OVERLAP');
  for(const x of [a,a2])await req(dh,`workday/assignments/${x.assignmentId}/cancel`,action(x));
 });
 await check('Fixture: own Start/End today, one active shift, actual timestamps and stale rejection',async()=>{
  const w=weeks.get(413),s=w.shifts.find(x=>x.roleId===4&&x.startMinute===480),s2=w.shifts.find(x=>x.roleId===4&&x.startMinute===960);
  const eligible=await req(431,`workday/candidates?shiftId=${s.shiftId}&date=${date}`);assert(eligible.some(x=>x.userId===427));
  let a=(await req(431,'workday/assignments',action(s,{shiftId:s.shiftId,userId:427,workDate:date}))).fields;
  let b=(await req(431,'workday/assignments',action(s2,{shiftId:s2.shiftId,userId:427,workDate:date}))).fields;
  await req(428,`workday/assignments/${a.assignmentId}/start`,action(a),403);
  const initial=a;a=(await req(427,`workday/assignments/${a.assignmentId}/start`,action(a))).fields;assert.equal(a.status,'WORKING');assert(Math.abs(Date.parse(a.startedAt+'Z')-Date.now())<60000||Math.abs(Date.parse(a.startedAt)-Date.now())<60000);
  const duplicate=await req(427,`workday/assignments/${b.assignmentId}/start`,action(b),409);assert.equal(duplicate.title,'SHIFT_ALREADY_ACTIVE');
  await req(427,`workday/assignments/${a.assignmentId}/end`,action(initial),409);
  a=(await req(427,`workday/assignments/${a.assignmentId}/end`,action(a))).fields;assert.equal(a.status,'ENDED');assert(a.endedAt);
  b=(await req(427,`workday/assignments/${b.assignmentId}/start`,action(b))).fields;b=(await req(427,`workday/assignments/${b.assignmentId}/end`,action(b))).fields;assert.equal(b.status,'ENDED');
 });
 await check('Fixture: Reception checkout -> HKS assigns -> HK cleans -> independent inspection -> Reception checkin',async()=>{
  for(const id of [427,429]){for(const x of (await req(id,'insights/time?branchId=413')).filter(x=>x.fields.closedAt===null)){const w=(await req(id,`work/${x.fields.workId}`)).work.fields;await req(id,`work/${w.workId}/actions/pause`,action(w));}}
  const rooms=await req(426,'rooms?branchId=413'),works=await req(425,'work?branchId=413&kind=CLEANING&pageSize=100');
  let room=rooms.find(r=>r.operationalStatus==='OCCUPIED'&&!works.items.some(w=>w.fields.roomId===r.roomId&&!['COMPLETED','CANCELLED','CLOSED'].includes(w.fields.status)));assert(room,'Occupied room without active cleaning');
  room=(await req(426,`rooms/${room.roomId}/actions/check-out`,action(room))).room.fields;assert.equal(room.operationalStatus,'DIRTY');
  let w=(await req(425,'work',{branchId:413,departmentId:211,kind:'CLEANING',title:'StaffingV4 cleaning',roomId:room.roomId})).fields;
  w=(await req(425,`work/${w.workId}/actions/assign`,action(w,{userId:427}))).fields;
  await req(428,`work/${w.workId}/actions/start`,action(w),403);
  for(const step of ['accept','start','complete'])w=(await req(427,`work/${w.workId}/actions/${step}`,action(w))).fields;assert.equal(w.status,'PENDING_INSPECTION');
  const opts=await req(425,'workspace/options?branchId=413'),template=opts.checklists.find(x=>x.kind==='INSPECTION');assert(template);
  let i=(await req(425,'work',{branchId:413,departmentId:211,kind:'INSPECTION',title:'StaffingV4 inspection',roomId:room.roomId,parentWorkId:w.workId,checklistTemplateId:template.checklistTemplateId})).fields;
  i=(await req(425,`work/${i.workId}/actions/assign`,action(i,{userId:436}))).fields;
  for(const step of ['accept','start'])i=(await req(436,`work/${i.workId}/actions/${step}`,action(i))).fields;
  for(const item of (await req(436,`work/${i.workId}`)).checklist)await req(436,`work/${i.workId}/checklist/${item.fields.workChecklistItemId}`,{rowVersion:item.fields.rowVersion,outcome:'PASS',note:'Isolated V4 verification'},200,'PUT');
  i=(await req(436,`work/${i.workId}`)).work.fields;for(const step of ['complete','pass'])i=(await req(436,`work/${i.workId}/actions/${step}`,action(i))).fields;assert.equal(i.status,'PASSED');
  room=(await req(426,'rooms?branchId=413')).find(x=>x.roomId===room.roomId);assert.equal(room.readiness,'READY');room=(await req(426,`rooms/${room.roomId}/actions/check-in`,action(room))).room.fields;assert.equal(room.operationalStatus,'OCCUPIED');
 });
 await check('Fixture: Maintenance Lead assigns, staff executes, independent Lead verifies',async()=>{
  let w=(await req(437,'work',{branchId:413,departmentId:212,kind:'MAINTENANCE',title:'StaffingV4 maintenance'})).fields;w=(await req(437,`work/${w.workId}/actions/assign`,action(w,{userId:429}))).fields;
  for(const step of ['accept','start','complete'])w=(await req(429,`work/${w.workId}/actions/${step}`,action(w))).fields;w=(await req(437,`work/${w.workId}/actions/verify`,action(w))).fields;assert.equal(w.status,'CLOSED');
 });
 await check('Fixture: DVCT Storekeeper posts receipt and counts stocktake; cannot create DVBV document',async()=>{
  const o=await req(store,'workspace/options?branchId=414');assert(o.warehouses.length&&o.items.length);const warehouse=o.warehouses[0].warehouseId,item=o.items[0];
  await req(store,'documents',{branchId:413,kind:'RECEIPT',destinationWarehouseId:warehouse,reason:'Denied branch test'},403);
  let d=(await req(store,'documents',{branchId:414,kind:'RECEIPT',destinationWarehouseId:warehouse,reason:'Staffing V4 fixture'})).fields;
  d=(await req(store,`documents/${d.documentId}/lines`,{rowVersion:d.rowVersion,itemId:item.itemId,unitId:item.baseUnitId,quantity:2,unitCost:1})).document.fields;
  d=(await req(store,`documents/${d.documentId}/actions/post`,action(d))).fields;assert.equal(d.status,'POSTED');
  let count=(await req(store,'documents',{branchId:414,kind:'STOCKTAKE',sourceWarehouseId:warehouse,reason:'Staffing V4 fixture'})).fields;
  count=(await req(store,`documents/${count.documentId}/lines`,{rowVersion:count.rowVersion,itemId:item.itemId,unitId:item.baseUnitId,quantity:1,countedQty:2})).document.fields;
  count=(await req(store,`documents/${count.documentId}/actions/count`,action(count))).fields;assert.equal(count.status,'COUNTED');
 });
}
const output={database,base,fixture_writes:writes,at:new Date().toISOString(),passed:results.filter(x=>x.status==='PASS').length,failed:results.filter(x=>x.status==='FAIL').length,results};
fs.writeFileSync(`artifacts/staffing-v4/api-${port}-results.json`,JSON.stringify(output,null,2));console.log(JSON.stringify({passed:output.passed,failed:output.failed}));if(output.failed)process.exitCode=1;
