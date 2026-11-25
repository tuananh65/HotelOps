import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {lowStockBrowser} from './v2-low-stock-browser.mjs';

const dir='artifacts/restructure-v2',base='http://127.0.0.1:5779/api/v2',runId=randomUUID(),tests=[];
const fields=x=>x.fields??x,reason='Synthetic TEST low-stock reconciliation';
let fixture,owned=false,cleanup;
const scrub=x=>String(x).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
const run=action=>new Promise((resolve,reject)=>{
 const child=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/v2-inventory-runtime-fixture.ps1','-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});
 let out='',err='';child.stdout.on('data',d=>out+=d);child.stderr.on('data',d=>err+=d);child.on('error',reject);
 child.on('exit',code=>{if(code)return reject(new Error(scrub(err.slice(0,1800))));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});
});
async function request(role,path,method='GET',body){
 const response=await fetch(base+path,{method,headers:{Authorization:'Bearer '+fixture.sessions[role].token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
 let data;try{data=await response.json();}catch{data={};}return {status:response.status,data};
}
async function ok(role,path,method='GET',body){const r=await request(role,path,method,body);assert.equal(r.status,200,method+' '+path+': '+r.status+' '+JSON.stringify(r.data));return r.data;}
async function test(name,fn){try{await fn();tests.push({name,status:'PASS'});console.log('PASS '+name);}catch(error){tests.push({name,status:'FAIL',error:scrub(error.message)});throw error;}}
async function minimum(itemId,minimumStock,destination=false){
 const role=destination?'otherManager':'manager',branchId=destination?fixture.otherBranch:fixture.branch,warehouseId=destination?fixture.otherWarehouse:fixture.warehouse;
 const current=await ok(role,`/supplies/minimum?branchId=${branchId}&warehouseId=${warehouseId}&itemId=${itemId}`);
 return ok(role,'/supplies/minimum','PUT',{...current,branchId,warehouseId,itemId,minimumStock});
}
async function receipt(itemId,quantity){
 const draft=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'Synthetic TEST supplier'}));
 const added=await ok('store',`/documents/${draft.documentId}/lines`,'POST',{rowVersion:draft.rowVersion,itemId,unitId:fixture.unit,quantity});
 await ok('store',`/documents/${draft.documentId}/actions/post`,'POST',{rowVersion:fields(added.document).rowVersion,operationKey:randomUUID(),reason});
}
const batches=()=>ok('store',`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fixture.items.fefo}`);
async function verifyBatch(batch,status,days){
 return ok('store',`/inventory-lifecycle/batches/${batch.batchId}`,'PUT',{rowVersion:batch.rowVersion,lotNumber:fixture.prefix+'-'+batch.batchId,receivedAt:new Date(Date.now()-86400000*10).toISOString(),expiresOn:new Date(Date.now()+86400000*days).toISOString().slice(0,10),status,reason});
}
async function alerts(role='store',destination=false){
 const inbox=await ok(role,'/notifications?branchId='+(destination?fixture.otherBranch:fixture.branch)+'&pageSize=100');
 return inbox.items.map(fields).filter(n=>n.notificationType==='LOW_STOCK');
}
async function reusable(destination=false){return ok(destination?'otherStore':'store','/reusable-inventory?branchId='+(destination?fixture.otherBranch:fixture.branch));}
async function move(position,status,quantity,location){
 return ok('store',`/reusable-inventory/positions/${position.positionId}/move`,'POST',{rowVersion:position.rowVersion,operationKey:randomUUID(),quantity,status,reason,...location});
}
async function document(id,role='store'){return ok(role,'/documents/'+id);}
const postBody=d=>({rowVersion:d.rowVersion,operationKey:randomUUID(),reason});
let firstAlert,consumableName,reusableName,sourceAlert,destinationAlert,shipment;
try{
 try{await run('Setup');}finally{try{fixture=JSON.parse((await readFile(dir+'/inventory-private/fixture.json','utf8')).replace(/^\uFEFF/,''));owned=fixture.runId===runId;}catch{}}
 assert.equal(owned,true);await run('Start');
 await test('consumable: physical 100 contains only 40 usable; alert reports actual shortage ten',async()=>{
  await receipt(fixture.items.fefo,40);await verifyBatch((await batches())[0],'READY',60);
  await receipt(fixture.items.fefo,40);await verifyBatch((await batches()).find(b=>b.status==='QUARANTINED'),'EXPIRED',-1);
  await receipt(fixture.items.fefo,20);
  const stock=await batches();assert.equal(stock.reduce((n,b)=>n+b.warehouseQuantity,0),100);assert.equal(stock.reduce((n,b)=>n+b.available,0),40);
  await minimum(fixture.items.fefo,50);
  const rows=await alerts();assert.equal(rows.length,1);firstAlert=rows[0];
  assert.equal(firstAlert.recipientUserId,fixture.sessions.store.user);assert.equal(firstAlert.branchId,fixture.branch);
  assert.match(firstAlert.message,/khả dụng 40/);assert.match(firstAlert.message,/mức tối thiểu 50/);assert.match(firstAlert.message,/thiếu 10/);
  const profile=(await ok('store','/inventory-lifecycle/profiles?branchId='+fixture.branch)).find(p=>p.itemId===fixture.items.fefo);
  consumableName=profile.name;assert.ok(firstAlert.message.includes(consumableName));
 });
 if(process.env.HOTELOPS_LOWSTOCK_UI_PROBE==='1')await test('consumable low-stock real inbox desktop and mobile read-only proof',()=>lowStockBrowser(fixture,{kind:'consumable',itemName:consumableName,notificationId:firstAlert.notificationId,available:40,minimum:50,missing:10},dir));
 await test('low-stock recipient scope excludes OM and the other branch storekeeper',async()=>{
  assert.equal((await alerts('manager')).length,0);assert.equal((await alerts('otherStore')).length,0);
  const foreign=await request('otherStore',`/notifications/${firstAlert.notificationId}/read`,'POST',{});assert.equal(foreign.status,404);
 });
 await test('same shortage episode updates amount without duplicate or losing read history',async()=>{
  await ok('store',`/notifications/${firstAlert.notificationId}/read`,'POST',{});
  const before=(await alerts())[0];assert.ok(before.readAt);
  await minimum(fixture.items.fefo,60);await minimum(fixture.items.fefo,60);
  const rows=await alerts();assert.equal(rows.length,1);assert.equal(rows[0].notificationId,firstAlert.notificationId);assert.equal(rows[0].eventKey,firstAlert.eventKey);assert.equal(rows[0].readAt,before.readAt);
  assert.match(rows[0].message,/mức tối thiểu 60/);assert.match(rows[0].message,/thiếu 20/);
 });
 await test('recovery closes alert; later shortage creates one new unread episode',async()=>{
  const quarantine=(await batches()).find(b=>b.status==='QUARANTINED');await verifyBatch(quarantine,'READY',60);
  assert.ok(!(await alerts()).some(n=>n.notificationId===firstAlert.notificationId),'Closed alerts should leave the active inbox');
  await minimum(fixture.items.fefo,80);await minimum(fixture.items.fefo,80);
  const rows=await alerts();assert.equal(rows.length,1);assert.notEqual(rows[0].notificationId,firstAlert.notificationId);assert.notEqual(rows[0].eventKey,firstAlert.eventKey);assert.equal(rows[0].readAt,null);
  assert.match(rows[0].message,/khả dụng 60/);assert.match(rows[0].message,/thiếu 20/);
  await minimum(fixture.items.fefo,0);assert.equal((await alerts()).length,0);
 });
 await test('reusable: ownership 100 with 90 in laundry alerts on ten READY, not total ownership',async()=>{
  await receipt(fixture.items.reusable,100);
  let state=await reusable(),ready=state.positions.find(p=>p.itemId===fixture.items.reusable&&p.status==='READY');assert.ok(ready);
  await move(ready,'IN_USE',90,{roomId:fixture.room});state=await reusable();
  await move(state.positions.find(p=>p.itemId===fixture.items.reusable&&p.status==='IN_USE'),'DIRTY',90,{roomId:fixture.room});state=await reusable();
  await move(state.positions.find(p=>p.itemId===fixture.items.reusable&&p.status==='DIRTY'),'LAUNDRY',90,{warehouseId:fixture.warehouse});
  await minimum(fixture.items.reusable,50);
  state=await reusable();assert.equal(state.totals.find(t=>t.itemId===fixture.items.reusable).owned,100);assert.equal(state.totals.find(t=>t.itemId===fixture.items.reusable).ready,10);
  const rows=await alerts();assert.equal(rows.length,1);sourceAlert=rows[0];
  assert.match(sourceAlert.message,/Thiếu vật tư sạch sẵn sàng/);assert.match(sourceAlert.message,/khả dụng 10/);assert.match(sourceAlert.message,/thiếu 40/);assert.match(sourceAlert.message,/LAUNDRY\): 90/);
  reusableName=(await ok('store','/inventory-lifecycle/profiles?branchId='+fixture.branch)).find(p=>p.itemId===fixture.items.reusable).name;
  assert.ok(sourceAlert.message.includes(reusableName));
 });
 if(process.env.HOTELOPS_LOWSTOCK_UI_PROBE==='1')await test('reusable low-stock real inbox desktop and mobile read-only proof',()=>lowStockBrowser(fixture,{kind:'reusable',itemName:reusableName,notificationId:sourceAlert.notificationId,available:10,minimum:50,missing:40},dir));
 await test('approved transfer does not fabricate stock reservation or in-transit goods',async()=>{
  await minimum(fixture.items.reusable,5,true);destinationAlert=(await alerts('otherStore',true))[0];assert.ok(destinationAlert);
  const draft=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:fixture.warehouse,destinationWarehouseId:fixture.otherWarehouse,reason}));
  const added=await ok('otherManager',`/documents/${draft.documentId}/lines`,'POST',{rowVersion:draft.rowVersion,itemId:fixture.items.reusable,unitId:fixture.unit,quantity:2});
  const submitted=fields(await ok('otherManager',`/documents/${draft.documentId}/actions/submit`,'POST',postBody(fields(added.document))));
  await ok('manager',`/documents/${draft.documentId}/actions/approve`,'POST',postBody(submitted));
  assert.equal((await reusable()).totals.find(t=>t.itemId===fixture.items.reusable).ready,10);
  assert.ok(!(await alerts('otherStore',true))[0].message.includes('Đang điều chuyển đến kho'));
  const approved=await document(draft.documentId);
  shipment=fields(await ok('store',`/documents/${draft.documentId}/derive`,'POST',{...postBody(fields(approved.document)),kind:'TRANSFER',lines:[{lineId:fields(approved.lines[0]).lineId,quantity:2}]}));
 });
 await test('actual shipment updates both alerts with pending quantity but incoming goods remain unavailable',async()=>{
  await ok('store',`/documents/${shipment.documentId}/actions/post`,'POST',postBody(shipment));
  const source=(await alerts())[0],destination=(await alerts('otherStore',true))[0];
  assert.equal(source.notificationId,sourceAlert.notificationId);assert.equal(destination.notificationId,destinationAlert.notificationId);
  assert.match(source.message,/khả dụng 8/);assert.match(source.message,/thiếu 42/);assert.match(source.message,/Đang điều chuyển đi: 2/);
  assert.match(destination.message,/khả dụng 0/);assert.match(destination.message,/thiếu 5/);assert.match(destination.message,/Đang điều chuyển đến kho: 2/);
 });
 await test('actual partial receipt updates available and shortage while retaining discrepancy in transit',async()=>{
  const source=await document(shipment.documentId,'otherStore');
  const received=fields(await ok('otherStore',`/documents/${shipment.documentId}/derive`,'POST',{...postBody(fields(source.document)),kind:'TRANSFER_RECEIPT',lines:[{lineId:fields(source.lines[0]).lineId,quantity:1}]}));
  const body=postBody(received);await ok('otherStore',`/documents/${received.documentId}/actions/post`,'POST',body);
  const local=(await alerts())[0],destination=(await alerts('otherStore',true))[0];
  assert.equal(local.notificationId,sourceAlert.notificationId);assert.equal(destination.notificationId,destinationAlert.notificationId);
  assert.match(local.message,/Đang điều chuyển đi: 1/);assert.match(destination.message,/khả dụng 1/);assert.match(destination.message,/thiếu 4/);assert.match(destination.message,/Đang điều chuyển đến kho: 1/);
  const before={source:await reusable(),destination:await reusable(true),sourceAlerts:await alerts(),destinationAlerts:await alerts('otherStore',true)};
  assert.equal((await request('otherStore',`/documents/${received.documentId}/actions/post`,'POST',body)).status,409);
  assert.deepEqual({source:await reusable(),destination:await reusable(true),sourceAlerts:await alerts(),destinationAlerts:await alerts('otherStore',true)},before);
  assert.equal(before.source.totals.find(t=>t.itemId===fixture.items.reusable).owned+before.destination.totals.find(t=>t.itemId===fixture.items.reusable).owned,100);
 });
}catch(error){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:scrub(error.message)});console.log(scrub(error.message));}
finally{if(owned){try{await run('Stop');cleanup=await run('Cleanup');}catch(error){cleanup={error:scrub(error.message)};}}}
const result={at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,tests,cleanup};
await writeFile(dir+'/low-stock-runtime-results.json',JSON.stringify(result,null,2));console.log(JSON.stringify({passed:result.passed,failed:result.failed,cleanup}));
process.exitCode=result.failed||!cleanup?.cleaned?1:0;
