// Isolated TEST only. No credentials, response bodies or SQL connection strings are logged.
// Missing acceptance scenarios intentionally remain NOT_TESTED in the runner.
import assert from 'node:assert/strict';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {fileURLToPath} from 'node:url';
import {requestScenarios} from './p0-runtime-request-scenarios.mjs';
import {supplyScenarios} from './p0-runtime-supply-scenarios.mjs';
import {roomScenarios} from './p0-runtime-room-scenarios.mjs';
import {concurrencyScenarios} from './p0-runtime-concurrency-scenarios.mjs';
const execute=promisify(execFile);
const probePath=fileURLToPath(new URL('./p0-runtime-probe.ps1',import.meta.url));
export async function probe(mode,fixture) {
 const f=fixture.requests??{};
 const args=['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',probePath,'-Mode',mode];
 if(mode==='target')args.push('-Endpoint',fixture.baseUrl);
 else for(const [key,value] of Object.entries({BranchId:f.branchId,ItemId:f.itemId,AllocationId:f.allocationId}))if(value!==undefined){assert(Number.isSafeInteger(value)&&value>0);args.push(`-${key}`,String(value));}
 try {const {stdout}=await execute('powershell.exe',args,{windowsHide:true,maxBuffer:4*1024*1024});return JSON.parse(stdout.trim().replace(/^\uFEFF/,''));}
 catch {throw new Error(`Read-only TEST ${mode} probe failed; inspect the probe locally without exposing credentials`);}
}
export async function verifyTarget(fixture) {
 assert.equal(fixture.database,'HotelOpsCompactWorkdayTestP0261124');
 const url=new URL(fixture.baseUrl);
 assert(['127.0.0.1','localhost'].includes(url.hostname));
 assert(!['5080','5769','5186','5173'].includes(url.port));
 // SQL pooling can expire while a build runs. An authenticated read reopens the
 // connection before checking the listener PID's actual SQL database.
 const actor=fixture.actors?.['DVBV.supervisor'];
 if(actor){
  const response=await fetch(new URL('/api/v2/auth/me',url),{headers:{Authorization:`Bearer ${actor.token}`},signal:AbortSignal.timeout(120000)});
  assert.equal(response.status,200,'TEST session read must succeed before target proof');
  await response.arrayBuffer();
 }
 return probe('target',fixture);
}
export const fields=value=>value?.fields??value;
export function branch(fixture,suffix='DVBV') {
 const b=fixture.branches.find(x=>x.code===`P0_TEST_${suffix}`);assert(b,'Synthetic branch missing');return b;
}
// Existing request scenarios use concise aliases; actual private fixture uses branch-qualified names.
export function scoped(c,suffix='DVBV') {
 const actor=name=>name.includes('.')?name:name==='foreignOm'?'DVCT.om':`${suffix}.${name}`;
 return {...c,request:(name,...rest)=>c.request(actor(name),...rest)};
}
export async function allocations(c,suffix='DVBV',alias='hk') {
 const b=branch(c.fixture,suffix);
 return c.ok(await c.request(`${suffix}.${alias}`,'GET',`/api/v2/supplies/allocations?branchId=${b.branchId}`));
}
async function prepareRequestContext(c) {
 const b=branch(c.fixture),a=(await allocations(c)).find(x=>x.assignmentId===c.fixture.actors['DVBV.hk'].assignmentId);
 c.check(a?.status==='OPEN','HK allocation must be open; run E2E-03 first');
 const item=c.fixture.items.find(x=>x.code.startsWith('P0_FILL_'));
 c.fixture.requests={branchId:b.branchId,warehouseId:b.warehouseId,itemId:item.itemId,allocationId:a.allocationId,allowSyntheticTestReceipt:c.fixture.allowSyntheticTestReceipt===true};
 return scoped(c);
}
const requests=requestScenarios({readStock:f=>probe('stock',f),readAdditionalIssued:f=>probe('additional',f)});
export const scenarios={...supplyScenarios({probe,branch,fields,scoped,allocations}),...roomScenarios({probe}),...concurrencyScenarios({probe,branch,fields,allocations})};
// E2E-11 from the original scaffold does not prove an actual stock shortage; leave it unregistered.
for(const id of ['E2E-09','E2E-10','E2E-16','E2E-26'])scenarios[id]=async c=>requests[id](await prepareRequestContext(c));
scenarios['E2E-27']=async c=>{
 const expected=['SYS_ADMIN','OWNER','OPERATION_MANAGER','HOUSEKEEPING_SUPERVISOR','HK_STAFF','MAINTENANCE_SUPERVISOR','RECEPTION','STOREKEEPER'];
 for(const suffix of ['DVBV','DVCT']){
  const b=branch(c.fixture,suffix),other=branch(c.fixture,suffix==='DVBV'?'DVCT':'DVBV');
  for(const alias of ['admin','owner','om','supervisor','hk','maintenance','reception','store']){
   const a=c.fixture.actors[`${suffix}.${alias}`],me=c.ok(await c.request(`${suffix}.${alias}`,'GET','/api/v2/auth/me'));
   c.check(me.roles.some(r=>r.code===a.roleCode)&&expected.includes(a.roleCode),`Role mismatch: ${suffix}.${alias}`);
   c.check(!me.roles.some(r=>['DEPT_MANAGER','MAINTENANCE_LEAD','MAINTENANCE_STAFF'].includes(r.code)),'Legacy role still active');
  }
  c.denied(await c.request(`${suffix}.om`,'GET',`/api/v2/supplies/configuration?branchId=${other.branchId}`));
  c.denied(await c.request(`${suffix}.hk`,'GET',`/api/v2/supplies/allocations?branchId=${other.branchId}`));
  c.denied(await c.request(`${suffix}.store`,'GET',`/api/v2/requests?branchId=${other.branchId}&queue=all`));
  const config=c.ok(await c.request(`${suffix}.om`,'GET',`/api/v2/supplies/configuration?branchId=${b.branchId}`))[0];
  for(const alias of ['owner','admin','hk','reception','maintenance','store'])
   c.denied(await c.request(`${suffix}.${alias}`,'PUT','/api/v2/supplies/configuration',config));
  c.denied(await c.request(`${suffix}.hk`,'POST','/api/v2/documents',{branchId:b.branchId,kind:'RECEIPT',destinationWarehouseId:b.warehouseId,operationKey:c.uuid()}));
 }
};
scenarios['E2E-28']=async c=>{
 c.reconcileInventory(await probe('inventory',c.fixture));
 c.reconcileSupply(await probe('balances',c.fixture));
 return {liveDatabaseProbe:true};
};
