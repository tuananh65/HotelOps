// Close abandoned synthetic probes via the real workflow; preserve every event and stock row.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {verifyTarget} from './p0-runtime-adapter.mjs';
const f=JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
await verifyTarget(f);
async function api(actor,path,body){
 const response=await fetch(new URL('/api/v2/'+path,f.baseUrl),{method:body?'POST':'GET',headers:{Authorization:`Bearer ${f.actors[actor].token}`,'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined});
 const data=await response.json();assert(response.ok,`${path}: ${response.status} ${data.title??''}`);return data;
}
let count=0;
for(const b of f.branches){
 const suffix=b.code.replace('P0_TEST_','');
 const list=await api(`${suffix}.om`,`requests?branchId=${b.branchId}&queue=all&pageSize=100`);
 for(const r of list.items){
  if(!['CONSUMABLE_REFILL_REQUEST','WAREHOUSE_REPLENISHMENT_REQUEST'].includes(r.requestType)||!['PENDING_SUPERVISOR','PENDING_OPERATIONS_MANAGER','PENDING_STOREKEEPER'].includes(r.status))continue;
  const alias=r.queueRole==='HOUSEKEEPING_SUPERVISOR'?'supervisor':r.queueRole==='OPERATION_MANAGER'?'om':r.queueRole==='STOREKEEPER'?'store':null;
  assert(alias,'Unknown handler queue; no action taken');
  const d=await api(`${suffix}.${alias}`,`requests/${r.workId}`);
  assert(d.description?.includes('P0 TEST'),'Only explicitly marked synthetic probes may be closed');
  await api(`${suffix}.${alias}`,`requests/${r.workId}/actions/reject`,{rowVersion:d.request.rowVersion,operationKey:randomUUID(),reason:'P0 TEST: đóng lần kiểm thử bị gián đoạn, giữ nguyên lịch sử'});count++;
 }
}
console.log(`Closed ${count} abandoned TEST probe requests via audited rejection; no stock changed.`);
