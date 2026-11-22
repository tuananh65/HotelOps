import fs from 'node:fs';import assert from 'node:assert/strict';import crypto from 'node:crypto';
const base=process.env.STAFFING_API||'http://127.0.0.1:5784/api/v2',writes=process.env.STAFFING_FIXTURE_WRITES==='1';
if(writes)assert.equal(new URL(base).port,'5784','Business writes restricted to restored fixture port');
const manifest=JSON.parse(fs.readFileSync('docs/database/compact-v2/staffing-v3-dry-run/staffing-v3-manifest.json','utf8'));
const credentials=JSON.parse(fs.readFileSync('artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json','utf8').replace(/^\uFEFF/,''));
const tokens=new Map(),profiles=new Map(),results=[];const suffix=Date.now();
async function req(id,path,body,expected=200,method=body===undefined?'GET':'POST'){
 const response=await fetch(base+'/'+path,{method,headers:{'Content-Type':'application/json',...(tokens.has(id)?{Authorization:'Bearer '+tokens.get(id)}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 const data=await response.json().catch(()=>null);assert.equal(response.status,expected,`${id} ${method} ${path}: ${response.status} ${data?.title||''}`);return data;
}
async function check(name,fn){try{await fn();results.push({name,status:'PASS'});console.log('PASS '+name)}catch(e){results.push({name,status:'FAIL',error:e.message});console.error('FAIL '+name+': '+e.message)}}
const action=(row,extra={})=>({rowVersion:row.rowVersion,operationKey:crypto.randomUUID(),reason:'Staffing V3 isolated verification',...extra});
async function pauseExistingTimer(id){
 const intervals=await req(id,'insights/time?branchId=413');
 for(const x of intervals.filter(x=>x.fields.closedAt===null)){const w=(await req(id,`work/${x.fields.workId}`)).work.fields;await req(id,`work/${w.workId}/actions/pause`,action(w));}
}
async function cleanAndInspectOne(){
 const rooms=await req(425,'rooms?branchId=413'),works=await req(425,'work?branchId=413&kind=CLEANING&pageSize=100');
 const room=rooms.find(r=>r.operationalStatus==='DIRTY'&&!works.items.some(w=>w.fields.roomId===r.roomId&&!['COMPLETED','CANCELLED','CLOSED'].includes(w.fields.status)));assert(room,'Unassigned dirty room');
 let w=(await req(425,'work',{branchId:413,departmentId:211,kind:'CLEANING',title:'StaffingV3 cleaning '+suffix,roomId:room.roomId})).fields;
 w=(await req(425,`work/${w.workId}/actions/assign`,action(w,{userId:427}))).fields;
 for(const step of ['accept','start','complete'])w=(await req(427,`work/${w.workId}/actions/${step}`,action(w))).fields;assert.equal(w.status,'PENDING_INSPECTION');
 const options=await req(425,'workspace/options?branchId=413'),template=options.checklists.find(c=>c.kind==='INSPECTION');assert(template);
 let i=(await req(425,'work',{branchId:413,departmentId:211,kind:'INSPECTION',title:'StaffingV3 inspection '+suffix,roomId:room.roomId,parentWorkId:w.workId,checklistTemplateId:template.checklistTemplateId})).fields;
 i=(await req(425,`work/${i.workId}/actions/assign`,action(i,{userId:436}))).fields;
 for(const step of ['accept','start'])i=(await req(436,`work/${i.workId}/actions/${step}`,action(i))).fields;
 const detail=await req(436,`work/${i.workId}`);for(const item of detail.checklist)await req(436,`work/${i.workId}/checklist/${item.fields.workChecklistItemId}`,{rowVersion:item.fields.rowVersion,outcome:'PASS',note:'Isolated staffing verification'},200,'PUT');
 i=(await req(436,`work/${i.workId}`)).work.fields;
 for(const step of ['complete','pass'])i=(await req(436,`work/${i.workId}/actions/${step}`,action(i))).fields;assert.equal(i.status,'PASSED');
 return room.roomId;
}
async function login(t){for(let retry=0;retry<5;retry++){const r=await fetch(base+'/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:t.identity_after.username,password:credentials.password})});if(r.status===429){console.log('Authentication rate window: waiting 20 seconds');await new Promise(r=>setTimeout(r,20000));continue;}assert.equal(r.status,200,'login '+t.user_id);const a=await r.json();assert.equal(a.user.fields.userId,t.user_id);tokens.set(t.user_id,a.accessToken);profiles.set(t.user_id,await req(t.user_id,'auth/me'));return;}throw Error('Authentication rate limit remained active');}
for(const t of manifest.existing_users)await check('login normalized existing user '+t.user_id,()=>login(t));
await check('all 10 active roles represented; approved grants/scopes preserved',async()=>{
 const roleSet=new Set();for(const t of manifest.existing_users){const p=profiles.get(t.user_id);assert(p);assert.equal(p.roles.length,1);assert.equal(p.roles[0].code,t.role_after_proposal.role_code);roleSet.add(p.roles[0].code);
 const expected=t.active_grants_if_role_approved.map(g=>`${g.permission}:${g.scope}`).sort();assert.deepEqual(p.grants.map(g=>`${g.code}:${g.dataScope}`).sort(),expected);
 const branch=t.user_id===438||t.user_id===424?null:t.role_after_proposal.branch_id;assert.equal(p.roles[0].branchId,branch);}
 assert.equal(roleSet.size,10);
});
await check('Administrator user list and role management readable; no operations grants',async()=>{const users=await req(424,'users');assert.equal(users.length,54);assert.equal(users.filter(u=>u.fields.accountStatus==='INACTIVE').length,39);assert(users.every(u=>!('passwordHash' in u.fields)));await req(424,'users/roles/1/permissions');await req(424,'insights/administration');assert(!profiles.get(424).grants.some(g=>g.code==='SHIFT_MANAGE'));});
await check('Operations Manager each branch allowed; other branch denied',async()=>{await req(435,'insights/dashboard?branchId=413');await req(435,'insights/dashboard?branchId=414',undefined,403);await req(433,'insights/dashboard?branchId=414');await req(433,'insights/dashboard?branchId=413',undefined,403);});
await check('HKS cleaning queue, inspection and candidates exclude inactive new users',async()=>{await req(425,'work/cleaning-queue?branchId=413');await req(436,'work?branchId=413&kind=INSPECTION');const c=await req(425,'work/candidates?branchId=413&kind=CLEANING');assert(c.some(u=>u.userId===427));assert(c.every(u=>u.userId>=424&&u.userId<=438));await req(425,'work/cleaning-queue?branchId=414',undefined,403);});
await check('HK staff assigned cleaning and scope',async()=>{const work=await req(427,'work?branchId=413&kind=CLEANING&mine=true');assert(work.total>0);await req(427,'work?branchId=414&kind=CLEANING',undefined,403);await req(434,'work?branchId=414&kind=CLEANING&mine=true');});
await check('Maintenance Lead and staff views and assignment candidates',async()=>{await req(437,'work?branchId=413&kind=MAINTENANCE');const c=await req(437,'work/candidates?branchId=413&kind=MAINTENANCE');assert(c.some(u=>u.userId===429));assert(c.every(u=>u.userId<=438));await req(429,'work?branchId=413&kind=MAINTENANCE&mine=true');});
await check('Reception both existing accounts have occupancy rights',async()=>{for(const id of [426,432]){await req(id,'rooms?branchId=413');for(const g of ['ROOM_CHECKIN','ROOM_CHECKOUT','ROOM_OCCUPANCY_MOVE'])assert(profiles.get(id).grants.some(x=>x.code===g));}await req(432,'rooms?branchId=414',undefined,403);});
await check('Storekeeper inventory scope',async()=>{await req(430,'insights/stock?branchId=413');await req(430,'documents?branchId=413&kind=RECEIPT');await req(430,'insights/stock?branchId=414',undefined,403);});
await check('Owner global read on both branches and no write grants',async()=>{assert(profiles.get(438).grants.every(g=>g.dataScope==='ALL'&&g.branchId===null));for(const branch of [413,414]){await req(438,`insights/dashboard?branchId=${branch}`);await req(438,`rooms?branchId=${branch}`);}await req(438,'users',undefined,403);await req(438,'work',{branchId:413,kind:'MAINTENANCE',title:'Denied owner write'},403);});
let week,today;
await check('Department Head sees all four departments; DVCT/OM/Admin cannot schedule',async()=>{
 const date=new Date().toLocaleDateString('en-CA',{timeZone:'Asia/Ho_Chi_Minh'});week=await req(431,`workday?branchId=413&date=${date}`);today=week.today;assert(week.canManage);assert.equal(new Set(week.shifts.map(s=>s.departmentId)).size,4);assert.equal(week.shifts.length,15);
 for(const id of [424,433,435,438])await req(id,`workday?branchId=${id===433?414:413}&date=${today}`,undefined,403);await req(431,`workday?branchId=414&date=${today}`,undefined,403);
 for(const s of week.shifts){const c=await req(431,`workday/candidates?shiftId=${s.shiftId}&date=${new Date(Date.parse(today+'T12:00:00Z')+86400000).toISOString().slice(0,10)}`);assert(c.every(u=>u.userId>=424&&u.userId<=438));const capacity=s.roleId===4?4:s.roleId===5?3:1;assert.equal(s.capacity,capacity);}
});
if(writes){
 await check('Fixture: pause restored active timers before independent execution scenarios',async()=>{await pauseExistingTimer(427);await pauseExistingTimer(429);});
 await check('Fixture: HKS assigns cleaning, HK completes, independent HKS inspects',async()=>{await cleanAndInspectOne();await cleanAndInspectOne();});
 await check('Fixture: Reception check-in, room transfer, checkout; Owner write denied',async()=>{
  let rooms=await req(426,'rooms?branchId=413');const available=rooms.filter(r=>r.readiness==='READY'&&['READY','AVAILABLE'].includes(r.operationalStatus));assert(available.length>=2,'Two ready and unoccupied rooms needed');let a=available[0],b=available[1];
  await req(438,`rooms/${a.roomId}/actions/check-in`,action(a),403);
  const entered=await req(426,`rooms/${a.roomId}/actions/check-in`,action(a));assert.equal(entered.room.fields.operationalStatus,'OCCUPIED');a=entered.room.fields;
  await req(426,`rooms/${a.roomId}/actions/move`,action(a,{targetRoomId:b.roomId,targetRowVersion:b.rowVersion}));rooms=await req(426,'rooms?branchId=413');b=rooms.find(r=>r.roomId===b.roomId);assert.equal(b.operationalStatus,'OCCUPIED');
  const exited=await req(426,`rooms/${b.roomId}/actions/check-out`,action(b));assert.equal(exited.room.fields.operationalStatus,'DIRTY');
 });
 await check('Fixture: Maintenance assignment, staff execution and independent Lead verification',async()=>{
  let w=(await req(437,'work',{branchId:413,departmentId:212,kind:'MAINTENANCE',title:'StaffingV3 maintenance '+suffix})).fields;
  w=(await req(437,`work/${w.workId}/actions/assign`,action(w,{userId:429}))).fields;
  for(const step of ['accept','start','complete'])w=(await req(429,`work/${w.workId}/actions/${step}`,action(w))).fields;
  w=(await req(437,`work/${w.workId}/actions/verify`,action(w))).fields;assert.equal(w.status,'CLOSED');
 });
 await check('Fixture: Workday assignment conflict, stale version, capacity and inactive denial',async()=>{
  const date=new Date(Date.parse(today+'T12:00:00Z')+2*86400000).toISOString().slice(0,10);const shift=week.shifts.find(s=>s.roleId===10&&s.startMinute===480);assert(shift);const candidates=await req(431,`workday/candidates?shiftId=${shift.shiftId}&date=${date}`);assert(candidates.some(c=>c.userId===425)&&candidates.some(c=>c.userId===436));
  const userList=await req(424,'users');const inactive=userList.find(u=>u.fields.employeeCode==='EMP023').fields;
  await req(431,'workday/assignments',action(shift,{shiftId:shift.shiftId,userId:inactive.userId,workDate:date}),409);
  const stale={...shift,rowVersion:Buffer.alloc(8).toString('base64')};await req(431,'workday/assignments',action(stale,{shiftId:shift.shiftId,userId:425,workDate:date}),409);
  let a=(await req(431,'workday/assignments',action(shift,{shiftId:shift.shiftId,userId:425,workDate:date}))).fields;
  await req(431,'workday/assignments',action(shift,{shiftId:shift.shiftId,userId:436,workDate:date}),409);await req(431,'workday/assignments',action(shift,{shiftId:shift.shiftId,userId:425,workDate:date}),409);
  const shift2=week.shifts.find(s=>s.roleId===4&&s.startMinute===480);const a2=(await req(431,'workday/assignments',action(shift2,{shiftId:shift2.shiftId,userId:427,workDate:date}))).fields;
  const clash=await req(431,'workday/assignments',action(shift2,{shiftId:shift2.shiftId,userId:427,workDate:date}),409);assert.equal(clash.title,'SHIFT_OVERLAP');
  await req(425,`workday/assignments/${a.assignmentId}/start`,action(a),409);
  for(const x of [a,a2])await req(431,`workday/assignments/${x.assignmentId}/cancel`,action(x));
 });
}
await check('New inactive account cannot login (no credentials issued)',async()=>{for(let i=0;i<5;i++){const r=await fetch(base+'/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:manifest.new_users[0].username,password:'unissued-credential-test'})});if(r.status===429){await new Promise(r=>setTimeout(r,20000));continue;}assert.equal(r.status,401);return;}throw Error('Rate limit prevented inactive login check');});
const output={base,fixture_writes:writes,at:new Date().toISOString(),passed:results.filter(r=>r.status==='PASS').length,failed:results.filter(r=>r.status==='FAIL').length,results};
fs.writeFileSync(`artifacts/staffing-v3-implementation/api-${new URL(base).port}-results.json`,JSON.stringify(output,null,2));console.log(JSON.stringify({passed:output.passed,failed:output.failed}));if(output.failed)process.exitCode=1;
