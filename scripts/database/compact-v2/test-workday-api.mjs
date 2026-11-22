import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const base=process.env.WORKDAY_API||'http://127.0.0.1:5768/api/v2';
const credentials=JSON.parse(fs.readFileSync('artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json','utf8').replace(/^\uFEFF/,''));
const results=[];const tokens={};
async function request(user,path,body,expected=200,method=body===undefined?'GET':'POST'){
 const r=await fetch(`${base}/${path}`,{method,headers:{'Content-Type':'application/json',...(tokens[user]?{Authorization:`Bearer ${tokens[user]}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});const data=await r.json().catch(()=>null);
 assert.equal(r.status,expected,`${user} ${path}: ${r.status} ${data?.title||''}`);return data;
}
async function check(name,fn){try{await fn();results.push({name,pass:true});console.log('PASS '+name);}catch(e){results.push({name,pass:false,error:e.message});throw e;}}
const version=(r,extra={})=>({rowVersion:r.rowVersion,operationKey:crypto.randomUUID(),...extra});
const plus=(date,n)=>{const d=new Date(date+'T12:00:00Z');d.setUTCDate(d.getUTCDate()+n);return d.toISOString().slice(0,10);};
try{
 await check('health schema and 40-table budget',async()=>{const h=await (await fetch(base.replace('/api/v2','/health'))).json();assert.equal(h.tables,40);assert.equal(h.schema,'2.1.0');});
 for(const user of ['demo_admin','demo_head','demo_hk1','demo_hk2','demo_reception','demo_manager','demo_dn_hk','demo_store']){
  const r=await request(user,'auth/login',{username:user,password:credentials.password});tokens[user]=r.accessToken;
 }
 const me=await request('demo_head','auth/me'),branch=me.branches[0].branchId;
 const hk=await request('demo_hk1','auth/me'),hk2=await request('demo_hk2','auth/me');
 const probe=await request('demo_head',`workday?branchId=${branch}&date=${new Date().toISOString().slice(0,10)}`),today=probe.today;
 const shift=probe.shifts.find(s=>s.name==='Buồng phòng • sáng'),next=probe.shifts.find(s=>s.name==='Buồng phòng • chiều');assert.ok(shift&&next);
 const tomorrow=plus(today,1);
 await check('manager can see whole branch; own view is scoped',async()=>{assert.equal(probe.canManage,true);const p=await request('demo_hk1',`workday?branchId=${branch}&date=${today}`);assert.equal(p.canManage,false);assert.ok(p.assignments.every(a=>a.userId===hk.user.fields.userId));});
 await check('non-manager cannot assign; admin gets no implicit operations',async()=>{await request('demo_hk1','workday/assignments',version(shift,{shiftId:shift.shiftId,userId:hk.user.fields.userId,workDate:today}),403);await request('demo_admin',`workday?branchId=${branch}&date=${today}`,undefined,403);});
 await check('cross-branch read and candidate assignment denied',async()=>{await request('demo_dn_hk',`workday?branchId=${branch}&date=${today}`,undefined,403);const dn=await request('demo_dn_hk','auth/me');await request('demo_head','workday/assignments',version(shift,{shiftId:shift.shiftId,userId:dn.user.fields.userId,workDate:today}),409);});
 let a,b;
 await check('assign current and future shifts',async()=>{a=(await request('demo_head','workday/assignments',version(shift,{shiftId:shift.shiftId,userId:hk.user.fields.userId,workDate:today}))).fields;b=(await request('demo_head','workday/assignments',version(shift,{shiftId:shift.shiftId,userId:hk.user.fields.userId,workDate:tomorrow}))).fields;});
 await check('future start and someone else start denied',async()=>{await request('demo_hk1',`workday/assignments/${b.assignmentId}/start`,version(b),409);await request('demo_hk2',`workday/assignments/${a.assignmentId}/start`,version(a),403);});
 await check('planned shifts prevent employee deactivation and permission removal',async()=>{
  const users=await request('demo_admin','users'),user=users.find(x=>x.fields.userId===hk.user.fields.userId).fields;
  const denied=await request('demo_admin',`users/${user.userId}/deactivate`,version(user,{reason:'Verify workday responsibility guard'}),409);
  assert.equal(denied.title,'USER_HAS_WORKDAY_RESPONSIBILITIES');
  const roles=await request('demo_admin','catalog/roles?pageSize=100'),role=roles.items.find(x=>x.fields.code==='DEMO_HOUSEKEEPER').fields;
  const permissions=await request('demo_admin',`users/roles/${role.roleId}/permissions`,[],409,'PUT');
  assert.equal(permissions.title,'ROLE_HAS_WORKDAY_RESPONSIBILITIES');
 });
 await check('overlap refused; adjacent shifts allowed',async()=>{await request('demo_head','workday/assignments',version(shift,{shiftId:shift.shiftId,userId:hk.user.fields.userId,workDate:today}),409);await request('demo_head','workday/assignments',version(next,{shiftId:next.shiftId,userId:hk.user.fields.userId,workDate:today}));});
 await check('start actual timestamp and stale version protection',async()=>{const old=a;a=(await request('demo_hk1',`workday/assignments/${a.assignmentId}/start`,version(a))).fields;assert.equal(a.status,'WORKING');assert.ok(a.startedAt);await request('demo_hk1',`workday/assignments/${a.assignmentId}/end`,version(old),409);});
 await check('one active shift globally; end preserves independent task timer',async()=>{const week=await request('demo_hk1',`workday?branchId=${branch}&date=${today}`);const other=week.assignments.find(x=>x.shiftId===next.shiftId&&x.workDate===today);await request('demo_hk1',`workday/assignments/${other.assignmentId}/start`,version(other),409);const before=await request('demo_hk1',`insights/time?branchId=${branch}`);a=(await request('demo_hk1',`workday/assignments/${a.assignmentId}/end`,version(a))).fields;assert.equal(a.status,'ENDED');const after=await request('demo_hk1',`insights/time?branchId=${branch}`);assert.deepEqual(after,before);});
 await check('cancellation requires reason and own history remains private',async()=>{await request('demo_head',`workday/assignments/${b.assignmentId}/cancel`,version(b,{reason:'short'}),400);await request('demo_head',`workday/assignments/${b.assignmentId}/cancel`,version(b,{reason:'Điều chỉnh lịch kiểm thử độc lập'}));await request('demo_hk2',`workday/assignments/${a.assignmentId}/history`,undefined,403);const h=await request('demo_hk1',`workday/assignments/${a.assignmentId}/history`);assert.deepEqual(h.map(x=>x.action),['ASSIGN','START','END']);});
 await check('capacity enforced with simultaneous assignments',async()=>{
  const roles=await request('demo_admin','catalog/roles?pageSize=100'),role=roles.items.find(x=>x.fields.code==='DEMO_HOUSEKEEPER').fields;
  const options=await request('demo_head',`workspace/options?branchId=${branch}`),department=options.departments.find(d=>d.departmentId===shift.departmentId);
  const users=[hk.user.fields.userId,hk2.user.fields.userId];
  for(let i=0;i<3;i++){const unique='wd'+crypto.randomBytes(5).toString('hex');const u=(await request('demo_admin','users',{employeeCode:unique,username:unique,fullName:`Workday capacity test ${i+1}`,password:crypto.randomBytes(24).toString('base64url'),email:`${unique}@example.invalid`})).fields;await request('demo_admin',`users/${u.userId}/memberships`,{branchId:branch,kind:'BRANCH_ACCESS'});await request('demo_admin',`users/${u.userId}/memberships`,{branchId:branch,departmentId:department.departmentId,kind:'EMPLOYMENT'});await request('demo_admin',`users/${u.userId}/roles`,{branchId:branch,roleId:role.roleId});users.push(u.userId);}
  const date=plus(today,3);for(const userId of users.slice(0,3))await request('demo_head','workday/assignments',version(shift,{shiftId:shift.shiftId,userId,workDate:date}));
  const outcomes=await Promise.all(users.slice(3).map(async userId=>{const r=await fetch(`${base}/workday/assignments`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${tokens.demo_head}`},body:JSON.stringify(version(shift,{shiftId:shift.shiftId,userId,workDate:date}))});return r.status;}));assert.deepEqual(outcomes.sort(),[200,409]);
 });
 await check('real backend modules remain readable with scoped accounts',async()=>{await request('demo_reception',`rooms?branchId=${branch}`);for(const kind of ['CLEANING','INSPECTION','REWORK','OPERATIONAL','INCIDENT','MAINTENANCE','INTERNAL_REQUEST'])await request('demo_manager',`work?branchId=${branch}&kind=${kind}`);await request('demo_store',`documents?branchId=${branch}&kind=RECEIPT`);await request('demo_manager',`catalog/assets?branchId=${branch}`);await request('demo_manager',`insights/dashboard?branchId=${branch}`);await request('demo_manager','notifications');});
 await check('inspection requires checklist before creating work',async()=>{
  const rejected=await request('demo_manager','work',{branchId:branch,kind:'INSPECTION',title:'Missing checklist must be rejected'},400);
  assert.equal(rejected.title,'INSPECTION_CHECKLIST_REQUIRED');
  const options=await request('demo_manager',`workspace/options?branchId=${branch}`);
  assert.ok(options.checklists.some(t=>t.kind==='INSPECTION'));
 });
}catch(e){console.error(e.message);process.exitCode=1;}finally{fs.mkdirSync('artifacts/workday',{recursive:true});fs.writeFileSync('artifacts/workday/api-results.json',JSON.stringify({at:new Date().toISOString(),results},null,2));}
