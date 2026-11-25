import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {verifyReplenishmentScope} from './v2-replenishment-scope-probe.mjs';

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
 const fill=fixture.items.find(i=>i.code.endsWith('-fill'));
 await ok('manager','/inventory-lifecycle/profiles','PUT',{branchId:fixture.branch,itemId:fill.itemId,classification:'CONSUMABLE',expiryPolicy:'NONE',reason});
 for(const who of ['store','manager']){
 const week=await ok(who,'/workday?branchId='+fixture.branch+'&date='+date);
 const row=week.assignments.find(a=>a.assignmentId===actor(who).assignment);assert.ok(row);
 await ok(who,'/workday/assignments/'+row.assignmentId+'/start','POST',{rowVersion:row.rowVersion,operationKey:randomUUID()});
 }
 const mapped={...fixture,items:{fifo:fill.itemId},sessions:{store:{token:actor('store').token,user:actor('store').user},manager:{token:actor('manager').token,user:actor('manager').user},otherStore:inventoryFixture.sessions.store,otherManager:inventoryFixture.sessions.manager}};
 const mappedRequest=async(alias,path,method='GET',body)=>{const response=await fetch(base+path,{method,headers:{Authorization:'Bearer '+mapped.sessions[alias].token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});return {status:response.status,data:await response.json()};};
 const inspect=async()=>{const sql=await own('Inspect');return {...sql,items:sql.stock.map(s=>({item_id:s.itemId,ledger:s.warehouseQuantity,batches:sql.batches.filter(b=>b.itemId===s.itemId).reduce((n,b)=>n+Number(b.warehouseQuantity),0)}))};};
 await test('warehouse replenishment permission, partial approval/receipt, rejection and browser',()=>verifyReplenishmentScope({fixture:mapped,request:mappedRequest,inspect,directory:dir}));
}catch(e){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:scrub(e.message)});console.log(scrub(e.message));}
finally{
 for(const [name,fn] of [...(inventoryOwned?[['stop',()=>inventory('Stop')]]:[]),...(fixtureOwned?[['fixture cleanup',()=>own('Cleanup')]]:[]),...(inventoryOwned?[['inventory cleanup',()=>inventory('Cleanup')]]:[])]){
  try{const result=await fn();if(name==='fixture cleanup')cleanup=result;}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});}
 }
 await writeFile(dir+'/replenishment-scope-runtime-results.json',JSON.stringify({at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',cleanup,tests},null,2));
 console.log(JSON.stringify({passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,cleanup}));if(tests.some(t=>t.status==='FAIL'))process.exitCode=1;
}
