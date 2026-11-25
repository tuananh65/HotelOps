import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
const directory='artifacts/restructure-v2',runId=randomUUID();
const report={at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',checks:[],actors:[],limits:'Actual GET and forbidden replenishment POST using owned short-lived sessions. Valid catalog IDs and request DTO; actor-scoped SQL work/document/movement counts unchanged. No password, role or successful business mutation/email. Not all E2E families covered.'};
let fixture;
const readJson=async p=>JSON.parse((await readFile(p,'utf8')).replace(/^\uFEFF/,''));
const run=action=>new Promise((resolve,reject)=>{const child=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/v2-invitation-fixture.ps1','-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});let out='',err='';child.stdout.on('data',d=>out+=d);child.stderr.on('data',d=>err+=d);child.on('error',reject);child.on('exit',code=>{if(code)return reject(new Error(action+': '+err.slice(0,1000)));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});});
async function api(token,path,expected=200){const response=await fetch('http://127.0.0.1:5778/api/v2'+path,{headers:{Authorization:'Bearer '+token},signal:AbortSignal.timeout(60000)});assert.equal(response.status,expected,path);return response.json();}
try{
 try{await run('Setup');}finally{try{const c=await readJson(directory+'/invitation-private/fixture.json');if(c.runId===runId)fixture=c;}catch{}}
 assert.ok(fixture);await run('OwnerSession');fixture=await readJson(directory+'/invitation-private/fixture.json');await run('Start');
 for(const [role,token] of [['SYS_ADMIN',fixture.token],['OWNER',fixture.ownerToken]]){
  const me=await api(token,'/auth/me');assert.ok(me.roles.some(r=>r.code===role&&r.branchId==null));assert.ok(me.branches.length>=2);
  report.actors.push({role,branches:me.branches.map(b=>b.branchId),roles:me.roles,grants:me.grants});
  const global=code=>me.grants.some(g=>g.code===code&&g.dataScope==='ALL'&&g.branchId==null);
  assert.ok(me.grants.some(g=>g.dataScope==='ALL'&&g.branchId==null));
  if(role==='OWNER'){const mutations=me.grants.filter(g=>!/(?:_VIEW|_EXPORT)$/.test(g.code));assert.deepEqual(mutations,[],'OWNER must retain read-only grants');report.checks.push({name:'OWNER has no management grants',status:'PASS'});}
  for(const branch of me.branches){
   const reportStatus=global('REPORT_VIEW')&&global('ROOM_VIEW')?200:403;
   const data=await api(token,`/insights/owner/reports/rooms?branchId=${branch.branchId}`,reportStatus);
   if(reportStatus===200){assert.ok(Array.isArray(data.items));assert.ok(data.items.every(r=>r.branchId===branch.branchId));}
   report.checks.push({name:role+' room report scoped to branch '+branch.branchId,status:'PASS',httpStatus:reportStatus,rows:data.items?.length});
   const expected=global('IR_VIEW')?200:403;await api(token,`/requests?branchId=${branch.branchId}&queue=all`,expected);
   report.checks.push({name:role+' request read obeys existing IR_VIEW grant in branch '+branch.branchId,status:'PASS',httpStatus:expected});
  }
 }
 const boundary=(await run('DomainBoundary'))[0];assert.ok(boundary.branch_id&&boundary.warehouse_id&&boundary.item_id);
 for(const [role,token] of [['SYS_ADMIN',fixture.token],['OWNER',fixture.ownerToken]]){
  const before=await run('DomainBoundary');
  const response=await fetch('http://127.0.0.1:5778/api/v2/requests/replenishments',{method:'POST',headers:{Authorization:'Bearer '+token,'Content-Type':'application/json'},body:JSON.stringify({branchId:boundary.branch_id,warehouseId:boundary.warehouse_id,items:[{itemId:boundary.item_id,quantity:1}],reason:'Synthetic TEST global read-only boundary',operationKey:randomUUID()}),signal:AbortSignal.timeout(60000)});
  const data=await response.json();assert.equal(response.status,403,role+' replenishment creation must be denied');assert.equal(data.title,'PERMISSION_DENIED','Expected replenishment permission gate denial');
  assert.deepEqual(await run('DomainBoundary'),before,'Forbidden operation must preserve actor-scoped business counts');
  report.checks.push({name:role+' cannot create replenishment with valid catalog IDs',status:'PASS',httpStatus:response.status,errorCode:data.title,sqlCounts:before[0]});
 }
 report.status='PASS';
}catch(error){report.status='FAIL';report.error=error.message;process.exitCode=1;}
finally{if(fixture){try{await run('Stop');report.cleanup=await run('Cleanup');}catch(error){report.cleanup={error:error.message};process.exitCode=1;}}await writeFile(directory+'/global-role-read-scope-results.json',JSON.stringify(report,null,2));console.log(JSON.stringify({status:report.status,checks:report.checks.length,error:report.error,cleanup:report.cleanup}));}
