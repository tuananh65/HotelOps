import { readFile,writeFile,mkdir } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';
import {reusableMoveBrowser} from './v2-reusable-browser.mjs';
import {inventoryIssueBrowser} from './v2-inventory-issue-browser.mjs';
const root=fileURLToPath(new URL('../../',import.meta.url)), directory=path.join(root,'artifacts/restructure-v2');
const base='http://127.0.0.1:5779/api/v2', results=[],runId=randomUUID();let fixture,started=false,cleanup;
const run=(action,args=[])=>new Promise((resolve,reject)=>{const child=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(root,'scripts/verification/v2-inventory-runtime-fixture.ps1'),'-Action',action,...args],{cwd:root,windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});let output='',errors='';child.stdout.on('data',d=>output+=d);child.stderr.on('data',d=>errors+=d);child.on('error',reject);child.on('exit',c=>c===0?resolve(JSON.parse(output.trim().replace(/^\uFEFF/,''))):reject(new Error(`${action}: ${errors.slice(0,1700)}`)));});
async function request(route,method='GET',body,role='store'){const response=await fetch(base+route,{method,headers:{Authorization:`Bearer ${fixture.sessions[role].token}`,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});let data;try{data=await response.json();}catch{data={};}return {status:response.status,data};}
async function ok(route,method='GET',body,role='store'){const r=await request(route,method,body,role);assert.equal(r.status,200,`${method} ${route}: ${r.status} ${r.data.title??JSON.stringify(r.data)}`);return r.data;}
async function test(name,action){if(process.env.HOTELOPS_BROWSER_ONLY==='1'&&!name.startsWith('request workspace browser'))return;if(process.env.HOTELOPS_ADJUSTMENT_ONLY==='1'&&!/new catalog item|count and selected|consumable reduction approval|approved disposal/.test(name))return;const start=Date.now();try{await action();results.push({name,status:'PASS',milliseconds:Date.now()-start});console.log(`PASS ${name}`);return true;
}catch(e){results.push({name,status:'FAIL',error:e.message.replace(/[A-Fa-f0-9]{64}/g,'[redacted]')});console.log(`FAIL ${name}: ${e.message}`);return false;}}
const reason='TEST isolated inventory runtime verification';
const date=(days)=>new Date(Date.now()+days*86400000).toISOString().slice(0,10);
const received=(days)=>new Date(Date.now()+days*86400000).toISOString();
const batches=item=>ok(`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${item}`);
async function receipt(item,quantity){const d=await ok('/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'TEST synthetic supplier'});const id=d.fields.documentId;const line=await ok(`/documents/${id}/lines`,'POST',{rowVersion:d.fields.rowVersion,itemId:item,unitId:fixture.unit,quantity});await ok(`/documents/${id}/actions/post`,'POST',{rowVersion:line.document.fields.rowVersion,operationKey:randomUUID(),reason});return id;}
async function metadata(batch,lot,receivedAt,expiresOn,status='READY'){return request(`/inventory-lifecycle/batches/${batch.batchId}`,'PUT',{rowVersion:batch.rowVersion,lotNumber:lot,receivedAt,expiresOn,status,reason});}
async function issueDraft(item,quantity){const source=await run('IssueSource',['-ItemId',String(item),'-Quantity',String(quantity)]);const detail=await ok(`/documents/${source.documentId}`);const d=await ok(`/documents/${source.documentId}/derive`,'POST',{rowVersion:detail.document.fields.rowVersion,operationKey:randomUUID(),kind:'ISSUE',warehouseId:fixture.warehouse,reason,lines:[{lineId:source.lineId,quantity}]});return d.fields;}
async function postIssue(d){return request(`/documents/${d.documentId}/actions/post`,'POST',{rowVersion:d.rowVersion,operationKey:randomUUID(),reason});}
async function reusable(){const data=await ok(`/reusable-inventory?branchId=${fixture.branch}`);return {...data,positions:data.positions.filter(p=>p.itemId===fixture.items.reusable),totals:data.totals.filter(p=>p.itemId===fixture.items.reusable)};}
async function move(source,status,quantity,location){const body={rowVersion:source.rowVersion,operationKey:randomUUID(),quantity,status,reason,...location};return {body,response:await request(`/reusable-inventory/positions/${source.positionId}/move`,'POST',body)};}
async function owned(expected){const state=await reusable();assert.equal(state.totals[0].owned,expected);return state;}
try{
 try{await run('Setup');}finally{
  try{const candidate=JSON.parse((await readFile(path.join(directory,'inventory-private/fixture.json'),'utf8')).replace(/^\uFEFF/,''));if(candidate.runId===runId)fixture=candidate;}catch{/* Never claim or clean a foreign fixture. */}
 }
 assert.ok(fixture,'Owned inventory fixture required');await run('Start');started=true;
 let apiItem;
 await test('new catalog item appears unclassified and OM configures its branch profile',async()=>{
  const item=await ok('/catalog/items?branchId='+fixture.branch,'POST',{fields:{code:fixture.prefix+'-API',name:fixture.prefix+' API-created item',baseUnitId:fixture.unit,isActive:true}});apiItem=item.fields.itemId;
  let profile=(await ok('/inventory-lifecycle/profiles?branchId='+fixture.branch)).find(p=>p.itemId===apiItem);
  assert.equal(profile.classification,'UNCLASSIFIED');assert.equal(profile.rowVersion,null);
  const configured=await ok('/inventory-lifecycle/profiles','PUT',{branchId:fixture.branch,itemId:apiItem,rowVersion:null,classification:'CONSUMABLE',expiryPolicy:'NONE',reason},'manager');
  assert.equal(configured.classification,'CONSUMABLE');await receipt(apiItem,10);
  assert.equal((await batches(apiItem)).reduce((n,b)=>n+b.warehouseQuantity,0),10);
 });
 await test('advisory fallback is honestly labeled and never requires an external provider',async()=>{const r=await request('/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:14});assert.equal(r.status,200,r.data.title);assert.equal(r.data.method,'STATISTICAL_RULES');assert.equal(r.data.externalStatus,'BLOCKED_EXTERNAL_CONFIGURATION');assert.equal(r.data.requiresHumanDecision,true);assert.ok(Array.isArray(r.data.evidence));});
 await test('advisory denies another branch and rejects invalid horizon',async()=>{assert.equal((await request('/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:14},'otherManager')).status,403);assert.equal((await request('/advisory','POST',{branchId:fixture.branch,kind:'DEMAND',days:0})).status,400);});
 let fefo, fifo, readyBeforeDamage;
 await test('receipt creates quarantined batch with exact ledger quantity and no invented expiry',async()=>{await receipt(fixture.items.fefo,5);const bs=await batches(fixture.items.fefo);assert.equal(bs.length,1);assert.equal(bs[0].status,'QUARANTINED');assert.equal(bs[0].warehouseQuantity,5);assert.equal(bs[0].available,0);assert.equal(bs[0].expiresOn,null);fefo=bs;});
 await test('expiry-required receipt cannot become READY without expiry',async()=>{const r=await metadata(fefo[0],'TEST-FEFO-OLD',received(-20),null);assert.equal(r.status,409);assert.equal(r.data.title,'BATCH_NOT_SAFE_TO_ISSUE');});
 await test('verified supplier lot metadata enables available quantity',async()=>{const r=await metadata(fefo[0],'TEST-FEFO-OLD',received(-20),date(90));assert.equal(r.status,200);await receipt(fixture.items.fefo,5);fefo=await batches(fixture.items.fefo);const next=fefo.find(b=>b.status==='QUARANTINED');assert.equal((await metadata(next,'TEST-FEFO-NEW',received(-10),date(10))).status,200);fefo=await batches(fixture.items.fefo);assert.equal(fefo.reduce((n,b)=>n+b.available,0),10);});
 await test('FEFO selects earliest expiry before older receipt',async()=>{const d=await issueDraft(fixture.items.fefo,3);const posted=await postIssue(d);assert.equal(posted.status,200,posted.data.title);const bs=await batches(fixture.items.fefo);assert.equal(bs.find(b=>b.lotNumber==='TEST-FEFO-NEW').warehouseQuantity,2);assert.equal(bs.find(b=>b.lotNumber==='TEST-FEFO-OLD').warehouseQuantity,5);});
 await test('issued lot identity cannot be changed',async()=>{const b=(await batches(fixture.items.fefo)).find(b=>b.lotNumber==='TEST-FEFO-NEW');const r=await metadata(b,'TEST-TAMPERED',b.receivedAt,b.expiresOn);assert.equal(r.status,409);assert.equal(r.data.title,'ISSUED_BATCH_IDENTITY_IMMUTABLE');});
 await test('FIFO ignores later expiry and chooses oldest receipt when expiry is not required',async()=>{await receipt(fixture.items.fifo,5);fifo=await batches(fixture.items.fifo);assert.equal((await metadata(fifo[0],'TEST-FIFO-OLD',received(-20),date(90))).status,200);await receipt(fixture.items.fifo,5);fifo=await batches(fixture.items.fifo);assert.equal((await metadata(fifo.find(b=>b.status==='QUARANTINED'),'TEST-FIFO-NEW',received(-10),date(10))).status,200);const d=await issueDraft(fixture.items.fifo,3);const r=process.env.HOTELOPS_INVENTORY_GAP_UI==='1'?await inventoryIssueBrowser(fixture,d,200,directory):await postIssue(d);assert.equal(r.status,200,r.data.title);fifo=await batches(fixture.items.fifo);assert.equal(fifo.find(b=>b.lotNumber==='TEST-FIFO-OLD').warehouseQuantity,2);assert.equal(fifo.find(b=>b.lotNumber==='TEST-FIFO-NEW').warehouseQuantity,5);});
 await test('expired and quarantined batches are excluded from available stock',async()=>{await receipt(fixture.items.fefo,7);let bs=await batches(fixture.items.fefo);const quarantine=bs.find(b=>b.status==='QUARANTINED');assert.equal((await metadata(quarantine,'TEST-EXPIRED',received(-20),date(-1),'EXPIRED')).status,200);await receipt(fixture.items.fefo,6);bs=await batches(fixture.items.fefo);assert.equal(bs.filter(b=>['EXPIRED','QUARANTINED'].includes(b.status)).reduce((s,b)=>s+b.available,0),0);assert.equal(bs.reduce((s,b)=>s+b.available,0),7);});
 await test('insufficient eligible batches rolls back posting header, movement, and allocations',async()=>{const d=await issueDraft(fixture.items.fefo,8);const before=await run('Inspect');const r=process.env.HOTELOPS_INVENTORY_GAP_UI==='1'?await inventoryIssueBrowser(fixture,d,409,directory):await postIssue(d);assert.equal(r.status,409);assert.equal(r.data.title,'BATCH_AVAILABLE_INSUFFICIENT');const after=await run('Inspect');assert.deepEqual(after,before);const detail=await ok(`/documents/${d.documentId}`);assert.equal(detail.document.fields.status,'DRAFT');assert.equal(detail.document.fields.postedAt,null);});
 await test('successful multi-batch issue has exact decomposition and ledger balance',async()=>{const d=await issueDraft(fixture.items.fefo,6);const r=await postIssue(d);assert.equal(r.status,200,r.data.title);const inspect=await run('Inspect');for(const item of inspect.items.filter(i=>[fixture.items.fefo,fixture.items.fifo].includes(i.item_id)))assert.equal(item.ledger,item.batches);const rows=inspect.entries.filter(e=>e.item_id===fixture.items.fefo&&e.quantity<0);const movement=Math.max(...rows.map(e=>e.movement_id));const split=rows.filter(e=>e.movement_id===movement);assert.equal(split.length,2);assert.equal(split.reduce((s,e)=>s+e.quantity,0),-6);});
 await test('UNKNOWN expiry policy cannot release batches as READY',async()=>{await receipt(fixture.items.unknown,3);const b=(await batches(fixture.items.unknown))[0];const r=await metadata(b,'TEST-UNKNOWN',received(-1),date(30));assert.equal(r.status,409);assert.equal((await batches(fixture.items.unknown))[0].available,0);});
 await test('lifecycle metadata is branch scoped and non-storekeeper cannot verify lots',async()=>{const b=(await batches(fixture.items.fefo))[0];assert.equal((await request(`/inventory-lifecycle/batches?branchId=${fixture.branch}`,'GET',undefined,'otherManager')).status,403);const r=await request(`/inventory-lifecycle/batches/${b.batchId}`,'PUT',{rowVersion:b.rowVersion,lotNumber:b.lotNumber,receivedAt:b.receivedAt,expiresOn:b.expiresOn,status:'QUARANTINED',reason},'manager');assert.equal(r.status,403);});
 await test('reusable receipt creates owned READY custody without consumable batch',async()=>{await receipt(fixture.items.reusable,10);const state=await owned(10);assert.equal(state.totals[0].ready,10);assert.equal((await batches(fixture.items.reusable)).length,0);});
 for(const [from,to,location] of [['READY','IN_USE','room'],['IN_USE','DIRTY','room'],['DIRTY','LAUNDRY','warehouse'],['LAUNDRY','READY','warehouse']]){
  if(from==='LAUNDRY'&&!(await reusable()).positions.some(p=>p.status==='LAUNDRY'&&p.quantity>=4)){results.push({name:'reusable LAUNDRY to READY preserves total ownership',status:'BLOCKED',reason:'Upstream DIRTY to LAUNDRY failed; no laundry custody exists.'});continue;}
  await test(`reusable ${from} to ${to} preserves total ownership`,async()=>{
   const state=await reusable(),source=state.positions.find(p=>p.status===from&&p.quantity>=4);assert.ok(source);
   const destination=p=>p.status===to&&(location==='room'?p.roomId===fixture.room:p.warehouseId===fixture.warehouse);
   const beforeDestination=state.positions.filter(destination).reduce((sum,p)=>sum+p.quantity,0);
   const response=await reusableMoveBrowser(fixture,source,to,4,location,directory);assert.equal(response.status,200,response.data.title);
   const after=await owned(10);assert.equal(after.positions.find(p=>p.positionId===source.positionId)?.quantity??0,source.quantity-4);
   assert.equal(after.positions.filter(destination).reduce((sum,p)=>sum+p.quantity,0),beforeDestination+4);
  });
 }
 await test('duplicate custody operation does not move or debit twice',async()=>{const initial=await reusable();readyBeforeDamage=initial.totals[0].ready;const source=initial.positions.find(p=>p.status==='READY'&&p.quantity>=2);const mutation=process.env.HOTELOPS_INVENTORY_GAP_UI==='1'?await reusableMoveBrowser(fixture,source,'DAMAGED',2,'warehouse',directory):null;const {body,response}=mutation?{body:mutation.requestBody,response:mutation}:await move(source,'DAMAGED',2,{warehouseId:fixture.warehouse});assert.equal(response.status,200,response.data.title);const before=await run('Inspect');const again=await request(`/reusable-inventory/positions/${source.positionId}/move`,'POST',body);assert.equal(again.status,200,again.data.title);assert.equal(again.data.alreadyApplied,true);assert.deepEqual(await run('Inspect'),before);const changed=await request(`/reusable-inventory/positions/${source.positionId}/move`,'POST',{...body,quantity:1});assert.equal(changed.status,409);});
 await test('quarantining an issued batch cannot bypass immutable lot identity',async()=>{
  let b=(await batches(fixture.items.fefo)).find(b=>b.lotNumber==='TEST-FEFO-NEW');
  assert.equal((await metadata(b,b.lotNumber,b.receivedAt,b.expiresOn,'QUARANTINED')).status,200);
  b=(await batches(fixture.items.fefo)).find(x=>x.batchId===b.batchId);
  const r=await metadata(b,'TEST-QUARANTINE-TAMPER',b.receivedAt,date(120));
  assert.equal(r.status,409);assert.equal(r.data.title,'ISSUED_BATCH_IDENTITY_IMMUTABLE');
  assert.equal((await metadata(b,b.lotNumber,b.receivedAt,b.expiresOn)).status,200);
 });
 let proposal;
 await test('damaged pending write-off reduces availability but not ownership',async()=>{const state=await owned(10);assert.equal(state.totals[0].ready,readyBeforeDamage-2);const damaged=state.positions.find(p=>p.status==='DAMAGED');const r=await ok(`/reusable-inventory/positions/${damaged.positionId}/write-off`,'POST',{rowVersion:damaged.rowVersion,operationKey:randomUUID(),quantity:1,reason});proposal=r.writeOffId;await owned(10);});
 const getProposal=async()=> (await ok(`/reusable-inventory/write-offs?branchId=${fixture.branch}`)).find(p=>p.writeOffId===proposal);
 await test('Storekeeper and different-branch OM cannot approve write-off',async()=>{const p=await getProposal(),body={rowVersion:p.rowVersion,operationKey:randomUUID(),approve:true};assert.equal((await request(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',body)).status,403);assert.equal((await request(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',body,'otherManager')).status,403);await owned(10);});
 await test('rejection requires reason and leaves ownership unchanged',async()=>{const p=await getProposal();const r=await request(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',{rowVersion:p.rowVersion,operationKey:randomUUID(),approve:false},'manager');assert.equal(r.status,400);assert.equal((await getProposal()).status,'PENDING');await owned(10);});
 await test('same-branch independent OM approval commits one ownership reduction',async()=>{const p=await getProposal(),body={rowVersion:p.rowVersion,operationKey:randomUUID(),approve:true};const r=await request(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',body,'manager');assert.equal(r.status,200,r.data.title);assert.equal(r.data.status,'APPROVED');await owned(9);const before=await run('Inspect');assert.equal((await request(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',body,'manager')).status,200);assert.deepEqual(await run('Inspect'),before);});
 await test('reasoned rejection is persisted without reducing ownership',async()=>{const damaged=(await reusable()).positions.find(p=>p.status==='DAMAGED');const r=await ok(`/reusable-inventory/positions/${damaged.positionId}/write-off`,'POST',{rowVersion:damaged.rowVersion,operationKey:randomUUID(),quantity:1,reason});proposal=r.writeOffId;const p=await getProposal();await ok(`/reusable-inventory/write-offs/${proposal}/decision`,'POST',{rowVersion:p.rowVersion,operationKey:randomUUID(),approve:false,reason:'TEST rejected after inspection'},'manager');const rejected=await getProposal();assert.equal(rejected.status,'REJECTED');assert.equal(rejected.decisionReason,'TEST rejected after inspection');await owned(9);});
 await test('reusable warehouse ledger matches physical custody excluding written-off',async()=>{const inspect=await run('Inspect');const item=inspect.items.find(i=>i.item_id===fixture.items.reusable);const physical=inspect.positions.filter(p=>p.item_id===item.item_id&&p.location_key===`W:${fixture.warehouse}`&&!['WRITTEN_OFF','LEGACY_REVIEW'].includes(p.status)).reduce((s,p)=>s+p.quantity,0);assert.equal(item.ledger,physical);});
 let genericIssue;
 await test('generic reusable ISSUE posts one debit and matching room custody with source provenance',async()=>{
  const before=await run('Inspect'),ledger=before.items.find(i=>i.item_id===fixture.items.reusable);
  genericIssue=await issueDraft(fixture.items.reusable,2);const posted=await postIssue(genericIssue);assert.equal(posted.status,200,posted.data.title);
  const detail=await ok(`/documents/${genericIssue.documentId}`);assert.equal(detail.document.fields.receivingRoomId,fixture.room);
  const after=await run('Inspect'),entry=after.items.find(i=>i.item_id===fixture.items.reusable);
  assert.equal(entry.ledger,ledger.ledger-2);assert.equal(entry.movementCount,ledger.movementCount+1);
  const position=after.positions.find(p=>p.item_id===fixture.items.reusable&&p.location_key===`R:${fixture.room}`&&p.status==='IN_USE');assert.equal(position.quantity,2);
  const history=await ok(`/reusable-inventory/history?branchId=${fixture.branch}`);assert.ok(history.some(h=>h.name==='TEST V2 Inventory reusable'&&h.kind==='HANDOVER'&&h.documentLineId));await owned(9);
 });
 await test('generic reusable RETURN restores warehouse once as DIRTY and reduces room custody',async()=>{
  assert.ok(genericIssue);const source=await ok(`/documents/${genericIssue.documentId}`);
  const line=source.lines[0].fields??source.lines[0];
  const draft=await ok(`/documents/${genericIssue.documentId}/derive`,'POST',{rowVersion:source.document.fields.rowVersion,operationKey:randomUUID(),kind:'RETURN',reason,lines:[{lineId:line.lineId,quantity:1}]});
  await run('ApproveReturn',['-DocumentId',String(draft.fields.documentId)]);
  const fresh=await ok(`/documents/${draft.fields.documentId}`),before=await run('Inspect'),old=before.items.find(i=>i.item_id===fixture.items.reusable);
  const r=await postIssue(fresh.document.fields);assert.equal(r.status,200,r.data.title);
  const after=await run('Inspect'),entry=after.items.find(i=>i.item_id===fixture.items.reusable);
  assert.equal(entry.ledger,old.ledger+1);assert.equal(entry.movementCount,old.movementCount+1);
  assert.equal(after.positions.find(p=>p.item_id===fixture.items.reusable&&p.status==='IN_USE'&&p.location_key===`R:${fixture.room}`).quantity,1);
  assert.equal(after.positions.find(p=>p.item_id===fixture.items.reusable&&p.status==='DIRTY'&&p.location_key===`W:${fixture.warehouse}`).quantity,1);await owned(9);
 });
 await test('reusable history and destinations deny different branch manager',async()=>{
  for(const route of ['history','destinations'])assert.equal((await request(`/reusable-inventory/${route}?branchId=${fixture.branch}`,'GET',undefined,'otherManager')).status,403);
 });
 let transferRequest,shipment;
 await test('destination OM requests reusable transfer and source OM independently approves eligible stock',async()=>{
  const d=await ok('/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason},'otherManager');transferRequest=d.fields.documentId;
  const line=await ok(`/documents/${transferRequest}/lines`,'POST',{rowVersion:d.fields.rowVersion,itemId:fixture.items.reusable,unitId:fixture.unit,quantity:3},'otherManager');
  const submitted=await ok(`/documents/${transferRequest}/actions/submit`,'POST',{rowVersion:line.document.fields.rowVersion,operationKey:randomUUID(),reason},'otherManager');
  await ok(`/documents/${transferRequest}/actions/approve`,'POST',{rowVersion:submitted.fields.rowVersion,operationKey:randomUUID(),reason},'manager');
 });
 await test('reusable shipment preserves origin ownership and records in transit without consumption',async()=>{
  assert.ok(transferRequest);const source=await ok(`/documents/${transferRequest}`),line=source.lines[0].fields??source.lines[0];
  const draft=await ok(`/documents/${transferRequest}/derive`,'POST',{rowVersion:source.document.fields.rowVersion,operationKey:randomUUID(),kind:'TRANSFER',reason,lines:[{lineId:line.lineId,quantity:3}]});
  shipment=draft.fields.documentId;await ok(`/documents/${shipment}/actions/post`,'POST',{rowVersion:draft.fields.rowVersion,operationKey:randomUUID(),reason});
  const state=await owned(9);assert.equal(state.positions.find(p=>p.status==='IN_TRANSIT').quantity,3);
 });
 for(const [amount,remaining] of [[2,1],[1,0]])await test(`reusable receipt of ${amount} transfers only actual ownership with ${remaining} still in transit`,async()=>{
  assert.ok(shipment);const source=await ok(`/documents/${shipment}`,'GET',undefined,'otherStore'),line=source.lines[0].fields??source.lines[0];
  const draft=await ok(`/documents/${shipment}/derive`,'POST',{rowVersion:source.document.fields.rowVersion,operationKey:randomUUID(),kind:'TRANSFER_RECEIPT',reason,lines:[{lineId:line.lineId,quantity:amount}]},'otherStore');
  await ok(`/documents/${draft.fields.documentId}/actions/post`,'POST',{rowVersion:draft.fields.rowVersion,operationKey:randomUUID(),reason},'otherStore');
  const state=await owned(6+remaining);assert.equal(state.positions.find(p=>p.status==='IN_TRANSIT').quantity,remaining);
  const destination=await ok(`/reusable-inventory?branchId=${fixture.otherBranch}`,'GET',undefined,'otherStore');
  const item=destination.totals.find(t=>t.itemId===fixture.items.reusable);assert.equal(item.owned,3-remaining);assert.equal(item.ready,3-remaining);
  const progress=(await ok(`/documents/${shipment}`,'GET',undefined,'otherStore')).transfer.progress;
  assert.equal(progress.length,1);assert.equal(progress[0].shippedQuantity,3);assert.equal(progress[0].receivedQuantity,3-remaining);assert.equal(progress[0].pendingReconciliationQuantity,remaining);
  const root=(await ok(`/documents/${transferRequest}`,'GET',undefined,'manager')).transfer.progress;
  assert.equal(root[0].requestedQuantity,3);assert.equal(root[0].approvedQuantity,3);assert.equal(root[0].pendingReconciliationQuantity,remaining);
 });
 await test('over receipt of reusable transfer is rejected without duplicate ownership',async()=>{
  const source=await ok(`/documents/${shipment}`,'GET',undefined,'otherStore'),line=source.lines[0].fields??source.lines[0];
  const r=await request(`/documents/${shipment}/derive`,'POST',{rowVersion:source.document.fields.rowVersion,operationKey:randomUUID(),kind:'TRANSFER_RECEIPT',reason,lines:[{lineId:line.lineId,quantity:1}]},'otherStore');
  assert.equal(r.status,409);await owned(6);
 });
 let reduction,expired,usable,selectedVersion;
 await test('count and selected expired batch produce a reduction proposal without stock mutation',async()=>{
  assert.ok(apiItem);const prior=(await batches(apiItem))[0];await metadata(prior,'TEST-EXPIRED-ADJUSTMENT',received(-20),date(-1),'EXPIRED');await receipt(apiItem,5);
  const all=await batches(apiItem);expired=all.find(b=>b.batchId===prior.batchId);usable=all.find(b=>b.batchId!==prior.batchId);await metadata(usable,'TEST-GOOD-ADJUSTMENT',received(-1),null);
  await receipt(fixture.items.fifo,1);
  const fifoCount=(await batches(fixture.items.fifo)).reduce((n,b)=>n+b.warehouseQuantity,0)+1;
  const count=await ok('/documents','POST',{branchId:fixture.branch,kind:'STOCKTAKE',sourceWarehouseId:fixture.warehouse,reason});
  const line=await ok('/documents/'+count.fields.documentId+'/lines','POST',{rowVersion:count.fields.rowVersion,itemId:apiItem,unitId:fixture.unit,quantity:5,countedQty:5});
  const increase=await ok('/documents/'+count.fields.documentId+'/lines','POST',{rowVersion:line.document.fields.rowVersion,itemId:fixture.items.fifo,unitId:fixture.unit,quantity:fifoCount,countedQty:fifoCount});
  const counted=await ok('/documents/'+count.fields.documentId+'/actions/count','POST',{rowVersion:increase.document.fields.rowVersion,operationKey:randomUUID(),reason});
  const draft=await ok('/documents/'+count.fields.documentId+'/derive','POST',{rowVersion:counted.fields.rowVersion,operationKey:randomUUID(),kind:'ADJUSTMENT',reason,lines:[{lineId:line.line.fields.lineId,quantity:10},{lineId:increase.line.fields.lineId,quantity:1}]});reduction=draft.fields;
  const detail=await ok('/documents/'+reduction.documentId);const id=detail.lines.find(l=>l.fields.itemId===apiItem).fields.lineId;
  const bad=await request('/documents/'+reduction.documentId+'/adjustment-batches','PUT',{rowVersion:reduction.rowVersion,operationKey:randomUUID(),lines:[{lineId:id,batchId:usable.batchId,quantity:10}],reason});assert.equal(bad.status,409);
  const saved=await ok('/documents/'+reduction.documentId+'/adjustment-batches','PUT',{rowVersion:reduction.rowVersion,operationKey:randomUUID(),lines:[{lineId:id,batchId:expired.batchId,quantity:10}],reason});selectedVersion=saved.fields.rowVersion;
  assert.equal((await batches(apiItem)).find(b=>b.batchId===expired.batchId).warehouseQuantity,10);
 });
 await test('consumable reduction approval is same-branch OM only, with reasoned rejection validation',async()=>{
  assert.ok(reduction);const route='/documents/'+reduction.documentId+'/actions/';const body={rowVersion:selectedVersion,operationKey:randomUUID(),reason};
  assert.equal((await request(route+'approve','POST',body)).status,403);
  assert.equal((await request(route+'approve','POST',body,'otherManager')).status,403);
  assert.equal((await request(route+'reject','POST',{...body,reason:null},'manager')).status,400);
  const details=await ok('/documents/'+reduction.documentId);assert.equal((await request(route+'approve','POST',{...body,approvedLines:details.lines.map(l=>({lineId:l.fields.lineId,quantity:0}))},'manager')).status,400);
  const approved=await ok(route+'approve','POST',body,'manager');selectedVersion=approved.fields.rowVersion;
 });
 await test('expiry warning is persisted once across reconciliations and visible only to the entitled branch',async()=>{
  const matches=r=>r.message.includes('TEST-EXPIRED-ADJUSTMENT')&&r.recipient_user_id===fixture.sessions.store.user;
  const first=(await run('Inspect')).warnings.filter(matches);assert.equal(first.length,1);assert.equal(first[0].expires_at,null);
  // Posting another receipt runs reconciliation again without altering the expiring lot.
  await receipt(fixture.items.unknown,1);
  const second=(await run('Inspect')).warnings.filter(matches);assert.equal(second.length,1);assert.equal(second[0].notification_id,first[0].notification_id);
  const inbox=await ok('/notifications?branchId='+fixture.branch+'&pageSize=100');
  assert.equal(inbox.items.map(x=>x.fields).filter(x=>x.notificationId===first[0].notification_id).length,1);
  const foreign=await ok('/notifications?branchId='+fixture.branch+'&pageSize=100','GET',undefined,'otherStore');
  assert.equal(foreign.items.map(x=>x.fields).filter(x=>x.message.includes(fixture.prefix)).length,0);
 });
 await test('branch expiry horizon is honored and returning warning becomes unread without a duplicate',async()=>{
  let batch=(await batches(apiItem)).find(b=>b.batchId===expired.batchId);
  const expiryDate=date(45);
  assert.equal((await metadata(batch,'TEST-EXPIRY-REOPEN',received(-20),expiryDate)).status,200);
  await receipt(fixture.items.unknown,1);
  const find=rows=>rows.filter(x=>x.message.includes('TEST-EXPIRY-REOPEN')&&x.recipient_user_id===fixture.sessions.store.user);
  const first=find((await run('Inspect')).warnings);assert.equal(first.length,1);assert.equal(first[0].expires_at,null);
  await ok('/notifications/'+first[0].notification_id+'/read','POST',{});
  batch=(await batches(apiItem)).find(b=>b.batchId===expired.batchId);
  assert.equal((await metadata(batch,'TEST-EXPIRY-REOPEN',received(-20),date(365))).status,200);
  await receipt(fixture.items.unknown,1);
  assert.ok(find((await run('Inspect')).warnings)[0].expires_at);
  batch=(await batches(apiItem)).find(b=>b.batchId===expired.batchId);
  assert.equal((await metadata(batch,'TEST-EXPIRY-REOPEN',received(-20),expiryDate)).status,200);
  await receipt(fixture.items.unknown,1);
  const reopened=find((await run('Inspect')).warnings);assert.equal(reopened.length,1);assert.equal(reopened[0].notification_id,first[0].notification_id);assert.equal(reopened[0].expires_at,null);assert.equal(reopened[0].read_at,null);
  batch=(await batches(apiItem)).find(b=>b.batchId===expired.batchId);
  assert.equal((await metadata(batch,'TEST-EXPIRED-ADJUSTMENT',received(-20),date(-1),'EXPIRED')).status,200);
  await receipt(fixture.items.unknown,1);
 });
 await test('approved disposal posts exactly expired stock, preserves good batch, and cannot repeat',async()=>{
  const route='/documents/'+reduction.documentId+'/actions/post';const body={rowVersion:selectedVersion,operationKey:randomUUID(),reason};
  assert.equal((await request(route,'POST',body,'manager')).status,403);
  await ok(route,'POST',body);const rows=await batches(apiItem);assert.equal(rows.find(b=>b.batchId===expired.batchId).warehouseQuantity,0);assert.equal(rows.find(b=>b.batchId===usable.batchId).warehouseQuantity,5);
  const history=await ok('/documents/'+reduction.documentId);assert.ok(history.inventory.adjustmentBatches.batches.some(b=>b.batchId===expired.batchId));
  const again=await request(route,'POST',body);assert.equal(again.status,409);
 });
 await test('disposal closes the expiry warning while retaining its persisted history',async()=>{
  const inbox=await ok('/notifications?branchId='+fixture.branch+'&pageSize=100');
  const warnings=inbox.items.map(x=>x.fields).filter(x=>x.notificationType==='EXPIRY_WARNING'&&x.message.includes(fixture.prefix)&&x.message.includes('TEST-EXPIRED-ADJUSTMENT'));
  // Disposal closes the expired-lot alert. Its persisted history remains in SQL; it is
  // no longer eligible for the inbox after stock reaches zero.
  assert.equal(warnings.length,0);
  const history=(await run('Inspect')).warnings.filter(x=>x.message.includes('TEST-EXPIRED-ADJUSTMENT')&&x.recipient_user_id===fixture.sessions.store.user);
  assert.equal(history.length,1);assert.ok(history[0].expires_at);
  const foreign=await ok('/notifications?branchId='+fixture.branch+'&pageSize=100','GET',undefined,'otherStore');
  assert.equal(foreign.items.map(x=>x.fields).filter(x=>x.message.includes(fixture.prefix)).length,0);
 });
 await test('pending migrated assets are visible but cannot be activated by an ordinary condition action',async()=>{
  const assets=await ok('/assets?branchId='+fixture.branch,'GET',undefined,'manager');
  const pending=assets.filter(a=>a.assetCode.startsWith('MIG222-'));assert.equal(pending.length,100);
  assert.ok(pending.every(a=>!a.isActive&&a.conditionCode==='PENDING_INSPECTION'&&a.roomId===null&&a.locationId===null));
  const asset=(await ok('/assets/'+pending[0].assetId,'GET',undefined,'manager')).asset.fields;
  const changed=await request('/assets/'+asset.assetId+'/actions/condition','POST',{rowVersion:asset.rowVersion,operationKey:randomUUID(),condition:'GOOD',reason},'manager');assert.equal(changed.status,409);
  const unchanged=(await ok('/assets/'+asset.assetId,'GET',undefined,'manager')).asset.fields;assert.equal(unchanged.rowVersion,asset.rowVersion);
 });
 if(process.env.HOTELOPS_UI_PROBE==='1')await test('request workspace browser uses real TEST API for manager and storekeeper',async()=>{
  await receipt(fixture.items.reusable,2);
  let state=await reusable();const ready=state.positions.find(p=>p.status==='READY'&&p.warehouseId===fixture.warehouse&&p.quantity>=1);
  assert.ok(ready);assert.equal((await move(ready,'DAMAGED',1,{warehouseId:fixture.warehouse})).response.status,200);
  state=await reusable();const damaged=state.positions.find(p=>p.status==='DAMAGED'&&p.warehouseId===fixture.warehouse);
  const proposal=await ok(`/reusable-inventory/positions/${damaged.positionId}/write-off`,'POST',{rowVersion:damaged.rowVersion,operationKey:randomUUID(),quantity:1,reason});
  fixture.uiProposal=proposal.writeOffId;
  fixture.uiOwnedBefore=state.totals[0].owned;
  const before=(await run('Inspect')).items.find(i=>i.item_id===fixture.items.reusable).ledger;
  const {verifyRequestWorkspace}=await import('./v2-request-browser.mjs');await verifyRequestWorkspace(fixture,directory);
  assert.equal((await run('Inspect')).items.find(i=>i.item_id===fixture.items.reusable).ledger,before-1);
  assert.equal((await reusable()).totals[0].owned,fixture.uiOwnedBefore-1);
 });
}catch(e){results.push({name:'harness/setup',status:'FAIL',error:e.message});}
finally{
 try{await run('Stop');started=false;}catch(e){results.push({name:'server shutdown',status:'FAIL',error:e.message});}
 try{if(fixture)cleanup=await run('Cleanup');}catch(e){results.push({name:'fixture cleanup',status:'FAIL',error:e.message});}
 const report={mode:process.env.HOTELOPS_BROWSER_ONLY==='1'?'browser-only':process.env.HOTELOPS_ADJUSTMENT_ONLY==='1'?'adjustment-only':'full',database:'HotelOpsCompactWorkdayTestV22611250010',api:'127.0.0.1:5779',at:new Date().toISOString(),notes:['Synthetic inventory items, receipts and approved material-request fixture sources only. Existing users used through isolated expiring sessions without credential or role changes.','Approved source material requests and independent return approval are fixture setup, not validation of the approval workflow. All stock receipts/postings, lot verification and custody mutations run through actual HTTP API.','Expiry is explicitly synthetic and only applies to synthetic catalog items. No existing catalog expiry modified.','All ledger/audit rows retained; fixture catalog deactivated and sessions revoked.'],cleanup,tests:results};await mkdir(directory,{recursive:true});await writeFile(path.join(directory,'inventory-runtime-results.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({passed:results.filter(t=>t.status==='PASS').length,failed:results.filter(t=>t.status==='FAIL').length,cleanup}));if(results.some(t=>t.status==='FAIL'))process.exitCode=1;
}
