import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {verifyTransferBrowser} from './v2-transfer-browser.mjs';
import {verifyAdvisoryBrowser} from './v2-advisory-browser.mjs';
import {verifyBatchBrowser} from './v2-batch-browser.mjs';
import {verifyBusinessRealtime} from './v2-business-realtime-probe.mjs';
import {verifyTransferCompletionBrowser} from './v2-transfer-completion-browser.mjs';
import {verifyTransferRejection} from './v2-transfer-rejection-browser.mjs';
const dir='artifacts/restructure-v2',base='http://127.0.0.1:5779/api/v2',runId=randomUUID(),tests=[];
let fixture,owned=false,cleanup,root,first,second,businessRoot;
const fields=x=>x.fields??x,reason='Synthetic TEST transfer reconciliation';
const scrub=x=>String(x).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
const run=(action,owner=runId)=>new Promise((resolve,reject)=>{
 const p=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/v2-inventory-runtime-fixture.ps1','-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:owner}});
 let out='',err='';p.stdout.on('data',d=>out+=d);p.stderr.on('data',d=>err+=d);p.on('error',reject);
 p.on('exit',code=>{if(code)return reject(new Error(scrub(err.slice(0,1800))));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture output'));}});
});
async function request(role,path,method='GET',body){
 const r=await fetch(base+path,{method,headers:{Authorization:'Bearer '+fixture.sessions[role].token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
 let data;try{data=await r.json();}catch{data={};}return {status:r.status,data};
}
async function ok(role,path,method='GET',body){const r=await request(role,path,method,body);assert.equal(r.status,200,method+' '+path+': '+r.status+' '+JSON.stringify(r.data));return r.data;}
async function test(name,fn){try{await fn();tests.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){tests.push({name,status:'FAIL',error:scrub(e.message)});throw e;}}
const detail=(id,role='store')=>ok(role,'/documents/'+id);
const batchList=(destination=false)=>ok(destination?'otherStore':'store',`/inventory-lifecycle/batches?branchId=${destination?fixture.otherBranch:fixture.branch}&itemId=${fixture.items.fefo}`);
async function derive(id,kind,quantity,role='store'){
 const d=await detail(id,role);
 return fields(await ok(role,`/documents/${id}/derive`,'POST',{rowVersion:fields(d.document).rowVersion,operationKey:randomUUID(),kind,reason,lines:[{lineId:fields(d.lines[0]).lineId,quantity}]}));
}
const postBody=d=>({rowVersion:d.rowVersion,operationKey:randomUUID(),reason});
async function snapshot(){return {source:await batchList(),destination:await batchList(true),sql:await run('Inspect')};}
function verifyOrigins(source,destination){
 for(const b of destination){const origin=source.find(s=>s.batchId===b.originBatchId);assert.ok(origin,'Destination must retain its actual source batch');
  assert.equal(b.expiresOn,origin.expiresOn);assert.equal(b.lotNumber,origin.lotNumber);assert.equal(b.receivedAt,origin.receivedAt);}
}
try{
 try{await run('Setup');}finally{
  try{fixture=JSON.parse((await readFile(dir+'/inventory-private/fixture.json','utf8')).replace(/^\uFEFF/,''));owned=fixture.runId===runId;}catch{}
 }
 assert.equal(owned,true,'Fixture ownership must match before Start');await run('Start');
 await test('foreign run cannot inspect, clean up or stop this TEST fixture',async()=>{
  const state=await snapshot(),foreign=randomUUID();
  await assert.rejects(run('Inspect',foreign),/Fixture belongs to another run/);
  await assert.rejects(run('Cleanup',foreign),/Fixture belongs to another run/);
  assert.equal((await run('Stop',foreign)).notOwned,true);assert.deepEqual(await snapshot(),state);
 });
 await test('two actual TEST receipts establish ten usable FEFO units with different expiry dates',async()=>{
  for(const [lot,days] of [['LATE',60],['EARLY',10]]){
   const d=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST supplier'}));
   const added=await ok('store',`/documents/${d.documentId}/lines`,'POST',{rowVersion:d.rowVersion,itemId:fixture.items.fefo,unitId:fixture.unit,quantity:5});
   await ok('store',`/documents/${d.documentId}/actions/post`,'POST',postBody(fields(added.document)));
   const b=(await batchList()).find(x=>x.status==='QUARANTINED');assert.ok(b);
   await ok('store',`/inventory-lifecycle/batches/${b.batchId}`,'PUT',{rowVersion:b.rowVersion,lotNumber:fixture.prefix+'-'+lot,receivedAt:new Date().toISOString(),expiresOn:new Date(Date.now()+days*86400000).toISOString().slice(0,10),status:'READY',reason});
  }
  assert.equal((await batchList()).reduce((n,b)=>n+b.available,0),10);
 });
 if(process.env.HOTELOPS_TRANSFER_REJECTION_PROBE==='1')await test('source OM rejects through popup with required reason and unchanged stock',async()=>{
  await verifyTransferRejection({fixture,request,snapshot,directory:dir});
 });
 await test('destination OM requests eight; source OM approves and destination cannot self-approve',async()=>{
  root=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason}));
  const added=await ok('otherManager',`/documents/${root.documentId}/lines`,'POST',{rowVersion:root.rowVersion,itemId:fixture.items.fefo,unitId:fixture.unit,quantity:8});
  const submitted=fields(await ok('otherManager',`/documents/${root.documentId}/actions/submit`,'POST',postBody(fields(added.document))));
  assert.equal((await request('otherManager',`/documents/${root.documentId}/actions/approve`,'POST',postBody(submitted))).status,403);
  await ok('manager',`/documents/${root.documentId}/actions/approve`,'POST',postBody(submitted));
 });
 await test('first four-unit shipment uses early expiry; destination cannot post source shipment',async()=>{
  first=await derive(root.documentId,'TRANSFER',4);
  assert.equal((await request('otherStore',`/documents/${first.documentId}/actions/post`,'POST',postBody(first))).status,403);
  const body=postBody(first);await ok('store',`/documents/${first.documentId}/actions/post`,'POST',body);
  const state=await snapshot();assert.equal(state.source.find(b=>b.lotNumber===fixture.prefix+'-EARLY').warehouseQuantity,1);assert.equal(state.source.find(b=>b.lotNumber===fixture.prefix+'-LATE').warehouseQuantity,5);
  const retry=await request('store',`/documents/${first.documentId}/actions/post`,'POST',body);
  assert.equal(retry.status,409);assert.equal(retry.data.title,'STALE_ROW_VERSION');assert.deepEqual(await snapshot(),state);
 });
 await test('second shipment spans remaining early lot and late lot without changing approved request',async()=>{
  second=await derive(root.documentId,'TRANSFER',4);await ok('store',`/documents/${second.documentId}/actions/post`,'POST',postBody(second));
  const batches=await batchList();assert.equal(batches.find(b=>b.lotNumber===fixture.prefix+'-EARLY').warehouseQuantity,0);assert.equal(batches.find(b=>b.lotNumber===fixture.prefix+'-LATE').warehouseQuantity,2);
  const d=await detail(root.documentId,'manager'),p=d.transfer.progress[0];assert.equal(p.requestedQuantity,8);assert.equal(p.approvedQuantity,8);assert.equal(p.shippedQuantity,8);assert.equal(p.receivedQuantity,0);
 });
 await test('shipment cannot exceed approval and source cannot receive for destination',async()=>{
  const before=await snapshot(),d=await detail(root.documentId);
  const excess=await request('store',`/documents/${root.documentId}/derive`,'POST',{rowVersion:fields(d.document).rowVersion,operationKey:randomUUID(),kind:'TRANSFER',reason,lines:[{lineId:fields(d.lines[0]).lineId,quantity:1}]});assert.equal(excess.status,409);
  const receipt=await derive(first.documentId,'TRANSFER_RECEIPT',2,'otherStore');
  assert.equal((await request('store',`/documents/${receipt.documentId}/actions/post`,'POST',postBody(receipt))).status,403);assert.deepEqual(await snapshot(),before);
  await ok('otherStore',`/documents/${receipt.documentId}/actions/post`,'POST',postBody(receipt));
 });
 await test('partial receipt preserves source lot expiry and leaves missing two pending reconciliation',async()=>{
  const d=await detail(first.documentId,'otherStore'),p=d.transfer.progress[0];assert.equal(p.shippedQuantity,4);assert.equal(p.receivedQuantity,2);assert.equal(p.pendingReconciliationQuantity,2);
  const source=await batchList(),destination=await batchList(true);assert.equal(destination.reduce((n,b)=>n+b.warehouseQuantity,0),2);
  verifyOrigins(source,destination);
 });
 await test('concurrent exact receipt retry commits only one receipt and preserves both lot origins',async()=>{
  const receipt=await derive(second.documentId,'TRANSFER_RECEIPT',4,'otherStore'),body=postBody(receipt);
  const results=await Promise.all([request('otherStore',`/documents/${receipt.documentId}/actions/post`,'POST',body),request('otherStore',`/documents/${receipt.documentId}/actions/post`,'POST',body)]);
  assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);
  assert.ok(['STALE_ROW_VERSION','TRANSACTION_RETRY_REQUIRED'].includes(results.find(r=>r.status===409).data.title),JSON.stringify(results));
  const state=await snapshot();assert.equal(state.destination.reduce((n,b)=>n+b.warehouseQuantity,0),6);
  assert.equal(state.destination.filter(b=>b.lotNumber===fixture.prefix+'-EARLY').reduce((n,b)=>n+b.warehouseQuantity,0),3);
  assert.equal(state.destination.filter(b=>b.lotNumber===fixture.prefix+'-LATE').reduce((n,b)=>n+b.warehouseQuantity,0),3);
  verifyOrigins(state.source,state.destination);
  const retry=await request('otherStore',`/documents/${receipt.documentId}/actions/post`,'POST',body);
  assert.equal(retry.status,409);assert.equal(retry.data.title,'STALE_ROW_VERSION');assert.deepEqual(await snapshot(),state);
 });
 await test('over receipt is rejected; outstanding shortage never silently writes off or returns stock',async()=>{
  const before=await snapshot(),d=await detail(first.documentId,'otherStore');
  const excess=await request('otherStore',`/documents/${first.documentId}/derive`,'POST',{rowVersion:fields(d.document).rowVersion,operationKey:randomUUID(),kind:'TRANSFER_RECEIPT',reason,lines:[{lineId:fields(d.lines[0]).lineId,quantity:3}]});assert.equal(excess.status,409);
  assert.deepEqual(await snapshot(),before);
  const final=await detail(root.documentId,'manager'),p=final.transfer.progress[0];assert.equal(p.requestedQuantity,8);assert.equal(p.approvedQuantity,8);assert.equal(p.shippedQuantity,8);assert.equal(p.receivedQuantity,6);assert.equal(p.pendingReconciliationQuantity,2);assert.equal(final.transfer.workflowStatus,'IN_TRANSIT');
  assert.equal(before.source.reduce((n,b)=>n+b.warehouseQuantity,0)+before.destination.reduce((n,b)=>n+b.warehouseQuantity,0)+p.pendingReconciliationQuantity,10);
 });
 await test('four actors complete transfer UI with partial approval and one-unit receipt, shortage remains pending',async()=>{
  const d=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST supplier'}));
  const added=await ok('store',`/documents/${d.documentId}/lines`,'POST',{rowVersion:d.rowVersion,itemId:fixture.items.fifo,unitId:fixture.unit,quantity:5});
  await ok('store',`/documents/${d.documentId}/actions/post`,'POST',postBody(fields(added.document)));
  const batch=(await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fixture.items.fifo}`))[0];
  await ok('store',`/inventory-lifecycle/batches/${batch.batchId}`,'PUT',{rowVersion:batch.rowVersion,lotNumber:'TEST-TRANSFER-BROWSER',receivedAt:new Date().toISOString(),expiresOn:null,status:'READY',reason});
  const ids=await verifyTransferBrowser(fixture,fixture.items.fifo,dir),final=await detail(ids.root,'manager'),p=final.transfer.progress[0];
  assert.deepEqual([p.requestedQuantity,p.approvedQuantity,p.shippedQuantity,p.receivedQuantity,p.pendingReconciliationQuantity],[3,2,2,1,1]);
  const source=await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fixture.items.fifo}`);
  const destination=await ok('otherStore',`/inventory-lifecycle/batches?branchId=${fixture.otherBranch}&itemId=${fixture.items.fifo}`);
  assert.equal(source.reduce((n,b)=>n+b.warehouseQuantity,0),3);assert.equal(destination.reduce((n,b)=>n+b.warehouseQuantity,0),1);
 });
 await test('converted units and competing receipts preserve approved base quantities and reject excess',async()=>{
  const item=fixture.items.unknown,unit=(await run('ConversionUnit')).unit;
  for(const [role,branch] of [['manager',fixture.branch],['otherManager',fixture.otherBranch]]){
   const profiles=await ok(role,`/inventory-lifecycle/profiles?branchId=${branch}`),profile=profiles.find(p=>p.itemId===item);
   await ok(role,'/inventory-lifecycle/profiles','PUT',{branchId:branch,itemId:item,rowVersion:profile.rowVersion,classification:'CONSUMABLE',expiryPolicy:'NONE',reason});
  }
  const incoming=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST supplier'}));
  const incomingLines=await ok('store',`/documents/${incoming.documentId}/lines`,'POST',{rowVersion:incoming.rowVersion,itemId:item,unitId:fixture.unit,quantity:10});
  await ok('store',`/documents/${incoming.documentId}/actions/post`,'POST',postBody(fields(incomingLines.document)));
  const batches=role=>ok(role,`/inventory-lifecycle/batches?branchId=${role==='store'?fixture.branch:fixture.otherBranch}&itemId=${item}`);
  const batch=(await batches('store'))[0];await ok('store',`/inventory-lifecycle/batches/${batch.batchId}`,'PUT',{rowVersion:batch.rowVersion,lotNumber:'TEST-CONVERTED',receivedAt:new Date().toISOString(),expiresOn:null,status:'READY',reason});
  const created=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason}));
  const added=await ok('otherManager',`/documents/${created.documentId}/lines`,'POST',{rowVersion:created.rowVersion,itemId:item,unitId:unit,quantity:4});
  assert.equal(fields(added.line).conversionFactor,2);assert.equal(fields(added.line).baseQty,8);
  const submitted=fields(await ok('otherManager',`/documents/${created.documentId}/actions/submit`,'POST',postBody(fields(added.document))));
  await ok('manager',`/documents/${created.documentId}/actions/approve`,'POST',{...postBody(submitted),approvedLines:[{lineId:fields(added.line).lineId,quantity:3}]});
  const sent=await derive(created.documentId,'TRANSFER',3);await ok('store',`/documents/${sent.documentId}/actions/post`,'POST',postBody(sent));
  assert.equal((await batches('store')).reduce((n,b)=>n+b.warehouseQuantity,0),4);
  const a=await derive(sent.documentId,'TRANSFER_RECEIPT',2,'otherStore'),b=await derive(sent.documentId,'TRANSFER_RECEIPT',2,'otherStore');
  const results=await Promise.all([request('otherStore',`/documents/${a.documentId}/actions/post`,'POST',postBody(a)),request('otherStore',`/documents/${b.documentId}/actions/post`,'POST',postBody(b))]);
  assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);
  assert.equal((await batches('otherStore')).reduce((n,b)=>n+b.warehouseQuantity,0),4);
  const losing=results[0].status===409?a:b,before=await run('Inspect');
  const denied=await request('otherStore',`/documents/${losing.documentId}/actions/post`,'POST',postBody(losing));
  assert.equal(denied.status,409);assert.equal(denied.data.title,'SOURCE_QUANTITY_EXCEEDED');assert.deepEqual(await run('Inspect'),before);
  const finalReceipt=await derive(sent.documentId,'TRANSFER_RECEIPT',1,'otherStore');await ok('otherStore',`/documents/${finalReceipt.documentId}/actions/post`,'POST',postBody(finalReceipt));
  const source=await batches('store'),destination=await batches('otherStore');verifyOrigins(source,destination);
  assert.equal(source.reduce((n,b)=>n+b.warehouseQuantity,0),4);assert.equal(destination.reduce((n,b)=>n+b.warehouseQuantity,0),6);
  const p=(await detail(created.documentId,'manager')).transfer.progress[0];assert.deepEqual([p.requestedQuantity,p.approvedQuantity,p.shippedQuantity,p.receivedQuantity,p.pendingReconciliationQuantity],[4,3,3,3,0]);
 });
 await test('advisory fallback distinguishes transfer stock from consumption, enforces branch scope and never changes ledger',async()=>{
  const before=await snapshot();
  const demand=await ok('manager','/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:14});
  assert.equal(demand.method,'STATISTICAL_RULES');assert.equal(demand.externalStatus,'BLOCKED_EXTERNAL_CONFIGURATION');assert.equal(demand.requiresHumanDecision,true);
  for(const [item,available] of [[fixture.items.fefo,2],[fixture.items.fifo,3],[fixture.items.unknown,4]]){
   const estimate=demand.evidence.find(r=>r.itemId===item);assert.ok(estimate);assert.equal(estimate.usedInLast30Days,0);assert.equal(estimate.expectedDemand,0);assert.equal(estimate.issuedInLast30Days,0);assert.equal(estimate.available,available);
  }
  assert.equal((await request('manager','/advisory','POST',{branchId:fixture.otherBranch,kind:'DEMAND',days:14})).status,403);
  assert.equal((await request('store','/advisory','POST',{branchId:fixture.branch,kind:'TRANSFER',days:14})).status,403);
  assert.equal((await request('manager','/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:91})).status,400);
  await verifyAdvisoryBrowser(fixture,dir);assert.deepEqual(await snapshot(),before);
 });
 await test('batch list and history paginate without truncation, search before paging and preserve branch authorization',async()=>{
  const all=await batchList();assert.equal(all.length,2);
  const path=`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fixture.items.fefo}`;
  for(let page=1;page<=3;page++){
   const result=await ok('store',path+`&page=${page}&pageSize=1`);assert.equal(result.total,2);assert.equal(result.page,page);assert.equal(result.pageSize,1);
   assert.deepEqual(result.items,all.slice(page-1,page));
  }
  const filtered=await ok('store',path+`&page=1&pageSize=1&search=${encodeURIComponent(all[1].lotNumber)}`);assert.equal(filtered.total,1);assert.equal(filtered.items[0].batchId,all[1].batchId);
  for(const suffix of ['&page=0','&page=1&pageSize=0','&page=1&pageSize=101','&page=2147483647&pageSize=100'])assert.equal((await request('store',path+suffix)).status,400);
  assert.equal((await request('otherStore',path+'&page=1')).status,403);
  const historyPath=`/inventory-lifecycle/batches/${all[0].batchId}/history`,entries=await ok('store',historyPath);assert.ok(entries.length>1);
  for(let page=1;page<=entries.length+1;page++){
   const result=await ok('store',historyPath+`?page=${page}&pageSize=1`);assert.equal(result.total,entries.length);assert.deepEqual(result.items,entries.slice(page-1,page));
  }
  assert.equal((await request('otherStore',historyPath+'?page=1')).status,403);
  assert.equal((await request('store',historyPath+'?page=1&pageSize=0')).status,400);
  await verifyBatchBrowser(fixture,all[0],entries,dir);
 });
 await test('positive transfer advice preserves source minimum and excludes incoming commitments without posting stock',async()=>{
  const itemId=fixture.items.fifo;
  for(const [role,branchId,warehouseId,minimumStock] of [['manager',fixture.branch,fixture.warehouse,1],['otherManager',fixture.otherBranch,fixture.otherWarehouse,4]]){
   const current=await ok(role,`/supplies/minimum?branchId=${branchId}&warehouseId=${warehouseId}&itemId=${itemId}`);
   await ok(role,'/supplies/minimum','PUT',{...current,branchId,warehouseId,itemId,minimumStock});
  }
  const query=()=>ok('otherManager','/advisory','POST',{branchId:fixture.otherBranch,kind:'TRANSFER',days:14});
  const before=await snapshot(),advice=await query();
  const suggestions=advice.evidence.filter(r=>r.itemId===itemId);assert.equal(suggestions.length,1);
  assert.equal(suggestions[0].quantity,2);assert.equal(suggestions[0].sourceWarehouseId,fixture.warehouse);assert.equal(suggestions[0].destinationWarehouseId,fixture.otherWarehouse);
  await verifyAdvisoryBrowser(fixture,dir,{itemId,quantity:2});assert.deepEqual(await snapshot(),before);
  if(process.env.HOTELOPS_BUSINESS_REALTIME_PROBE==='1'){
   const stockBefore={source:await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${itemId}`),destination:await ok('otherStore',`/inventory-lifecycle/batches?branchId=${fixture.otherBranch}&itemId=${itemId}`)};
   businessRoot=(await verifyBusinessRealtime(fixture,suggestions[0],dir)).documentId;
   assert.deepEqual({source:await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${itemId}`),destination:await ok('otherStore',`/inventory-lifecycle/batches?branchId=${fixture.otherBranch}&itemId=${itemId}`)},stockBefore,'Human approval must not itself ship physical stock');
  }else{
   const pending=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason}));
   const added=await ok('otherManager',`/documents/${pending.documentId}/lines`,'POST',{rowVersion:pending.rowVersion,itemId,unitId:fixture.unit,quantity:2});
   await ok('otherManager',`/documents/${pending.documentId}/actions/submit`,'POST',postBody(fields(added.document)));
  }
  const committed=await snapshot();assert.equal((await query()).evidence.filter(r=>r.itemId===itemId).length,0);assert.deepEqual(await snapshot(),committed);
 });
 if(businessRoot)await test('human-reviewed shortage suggestion completes its own actual shipment and two reconciled receipts',async()=>{
  const item=fixture.items.fifo;
  const balances=async()=>({source:await ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${item}`),destination:await ok('otherStore',`/inventory-lifecycle/batches?branchId=${fixture.otherBranch}&itemId=${item}`)});
  const quantity=rows=>rows.reduce((sum,row)=>sum+row.warehouseQuantity,0);
  const before=await balances();assert.deepEqual([quantity(before.source),quantity(before.destination)],[3,1]);
  const shipment=await derive(businessRoot,'TRANSFER',2);
  await ok('store',`/documents/${shipment.documentId}/actions/post`,'POST',postBody(shipment));
  const firstReceipt=await derive(shipment.documentId,'TRANSFER_RECEIPT',1,'otherStore');
  await ok('otherStore',`/documents/${firstReceipt.documentId}/actions/post`,'POST',postBody(firstReceipt));
  let progress=(await detail(businessRoot,'manager')).transfer.progress[0];
  assert.deepEqual([progress.requestedQuantity,progress.approvedQuantity,progress.shippedQuantity,progress.receivedQuantity,progress.pendingReconciliationQuantity],[2,2,2,1,1]);
  const finalReceipt=await derive(shipment.documentId,'TRANSFER_RECEIPT',1,'otherStore'),body=postBody(finalReceipt);
  await ok('otherStore',`/documents/${finalReceipt.documentId}/actions/post`,'POST',body);
  progress=(await detail(businessRoot,'manager')).transfer.progress[0];
  assert.deepEqual([progress.requestedQuantity,progress.approvedQuantity,progress.shippedQuantity,progress.receivedQuantity,progress.pendingReconciliationQuantity],[2,2,2,2,0]);
  const after=await balances();assert.deepEqual([quantity(after.source),quantity(after.destination)],[1,3]);verifyOrigins(after.source,after.destination);
  assert.equal(quantity(after.source)+quantity(after.destination),quantity(before.source)+quantity(before.destination));
  const ledger=await snapshot();assert.equal((await request('otherStore',`/documents/${finalReceipt.documentId}/actions/post`,'POST',body)).status,409);assert.deepEqual(await snapshot(),ledger);
  await verifyTransferCompletionBrowser(fixture,businessRoot,dir);assert.deepEqual(await snapshot(),ledger);
 });
}catch(error){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:scrub(error.message)});console.log(scrub(error.message));}
finally{
 if(owned)for(const action of ['Stop','Cleanup'])try{const r=await run(action);if(action==='Cleanup')cleanup=r;}catch(e){tests.push({name:action,status:'FAIL',error:scrub(e.message)});}
 await writeFile(dir+'/transfer-runtime-results.json',JSON.stringify({at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',mode:'Actual HTTP/SQL and four-actor browser transfer; synthetic catalog/receipts, base and converted UOM; no password login',businessRealtimeProbe:process.env.HOTELOPS_BUSINESS_REALTIME_PROBE==='1'?'See business-realtime-results.json for actual business-triggered SignalR scope/results':'NOT_RUN',cleanup,tests},null,2));
 console.log(JSON.stringify({passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,cleanup}));if(tests.some(t=>t.status==='FAIL'))process.exitCode=1;
}
