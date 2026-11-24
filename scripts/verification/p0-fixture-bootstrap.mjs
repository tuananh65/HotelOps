import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
const directory=path.resolve(process.argv[2]??'artifacts/p0-workflow/private');
const read=name=>JSON.parse(fs.readFileSync(path.join(directory,name),'utf8').replace(/^\uFEFF/,''));
const proof=read('target-proof.json'),manifest=read('manifest.json'),privateActors=read('credentials.json').actors;
assert.equal(proof.database,'HotelOpsCompactWorkdayTestP0261124');assert.equal(proof.allowSyntheticTestReceipt,true);
assert(Date.now()-Date.parse(proof.verifiedAt)<300000,'Fresh independent PID/SQL database probe required');
const origin=new URL(proof.baseUrl);assert(['localhost','127.0.0.1'].includes(origin.hostname));assert(!['5080','5769','5186','5173'].includes(origin.port));
const checkpointFile=path.join(directory,'bootstrap-checkpoint.json');
const state=fs.existsSync(checkpointFile)?read('bootstrap-checkpoint.json'):{steps:{},actors:{},receipts:[]};
const save=()=>fs.writeFileSync(checkpointFile,JSON.stringify(state,null,2),{mode:0o600});
const fields=value=>value.fields??value;
async function api(token,route,method='GET',body){
 const response=await fetch(new URL(`/api/v2/${route}`,origin),{method,headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 const value=await response.json();
 // Do not include response bodies: authentication responses contain private tokens.
 assert(response.ok,`${method} ${route} failed HTTP ${response.status}; business code ${value.code??value.error?.code??'unavailable'}`);return value;
}
async function step(name,execute){if(Object.hasOwn(state.steps,name))return state.steps[name];const value=await execute();state.steps[name]=value??true;save();return value;}
for(const actor of manifest.actors){
 const privateActor=privateActors.find(x=>x.username===actor.username);assert(privateActor);
 const session=await api(null,'auth/login','POST',{username:actor.username,password:privateActor.password});
 const me=await api(session.accessToken,'auth/me');assert(me.roles.some(x=>x.code===actor.roleCode));
 state.actors[`${actor.suffix}.${actor.alias}`]={...actor,token:session.accessToken};save();
 // Login limiter is per IP; stay below the configured burst while preparing twenty identities.
 await new Promise(resolve=>setTimeout(resolve,4200));
}
async function detail(token,id){return api(token,`documents/${id}`);}
async function action(token,id,command){const current=await detail(token,id);return api(token,`documents/${id}/actions/${command}`,'POST',{rowVersion:fields(current.document).rowVersion,operationKey:randomUUID(),reason:'Synthetic isolated TEST fixture operation'});}
async function addLine(token,id,item,quantity){const current=await detail(token,id);return api(token,`documents/${id}/lines`,'POST',{rowVersion:fields(current.document).rowVersion,itemId:item.itemId,unitId:item.unitId,quantity});}
for(const branch of manifest.branches){
 const suffix=branch.code.replace('P0_TEST_',''),actor=alias=>state.actors[`${suffix}.${alias}`];
 for(const item of manifest.items){
  const fill=item.code.startsWith('P0_FILL_');
  await step(`${suffix}.config.${item.itemId}`,()=>api(actor('om').token,'supplies/configuration','PUT',{branchId:branch.branchId,itemId:item.itemId,supplyGroup:fill?'A':'B',warehouseId:branch.warehouseId,unitId:item.unitId,initialQuantity:fill?10:0,minimumStock:fill?50:2,perGuestQuantity:fill?1:0,isActive:true}));
 }
 await step(`${suffix}.asset-config`,()=>api(actor('om').token,'supplies/configuration','PUT',{branchId:branch.branchId,assetId:branch.assetId,supplyGroup:'C',initialQuantity:0,minimumStock:0,perGuestQuantity:0,isActive:true}));
 const receipt=await step(`${suffix}.receipt`,async()=>fields(await api(actor('store').token,'documents','POST',{branchId:branch.branchId,kind:'RECEIPT',destinationWarehouseId:branch.warehouseId,reason:'Explicit synthetic TEST goods receipt; not real hotel stock',supplierName:'P0 TEST simulated delivery',operationKey:randomUUID()})));
 for(const item of manifest.items)await step(`${suffix}.receipt-line.${item.itemId}`,()=>addLine(actor('store').token,receipt.documentId,item,100));
 await step(`${suffix}.receipt-post`,()=>action(actor('store').token,receipt.documentId,'post'));
 for(const alias of ['om','supervisor','store','storePeer','maintenance','reception']){
  const a=actor(alias);if(a.assignmentId)await step(`${suffix}.start.${alias}`,()=>api(a.token,`workday/assignments/${a.assignmentId}/start`,'POST',{rowVersion:a.rowVersion,operationKey:randomUUID()}));
 }
 const durable=manifest.items.find(x=>x.code==='P0_DURABLE_KETTLE');
 await step(`${suffix}.room-line`,()=>addLine(actor('hk').token,branch.initialRoomRequestId,durable,1));
 await step(`${suffix}.room-submit`,()=>action(actor('hk').token,branch.initialRoomRequestId,'submit'));
 await step(`${suffix}.room-approve`,()=>action(actor('store').token,branch.initialRoomRequestId,'approve'));
 const issue=await step(`${suffix}.room-issue`,async()=>{const source=await detail(actor('storePeer').token,branch.initialRoomRequestId);return fields(await api(actor('storePeer').token,`documents/${branch.initialRoomRequestId}/derive`,'POST',{rowVersion:fields(source.document).rowVersion,operationKey:randomUUID(),kind:'ISSUE',warehouseId:branch.warehouseId,reason:'Synthetic TEST equipment assigned to room',lines:source.lines.map(x=>({lineId:fields(x).lineId,quantity:1}))}));});
 await step(`${suffix}.room-issue-post`,()=>action(actor('storePeer').token,issue.documentId,'post'));
 branch.receiptId=receipt.documentId;branch.roomIssueDocumentId=issue.documentId;
}
const output={database:proof.database,baseUrl:proof.baseUrl,allowSyntheticTestReceipt:true,actors:state.actors,branches:manifest.branches,items:manifest.items,preparedAt:new Date().toISOString()};
fs.writeFileSync(path.join(directory,'runtime-fixture.json'),JSON.stringify(output,null,2),{mode:0o600});
console.log('PASS fixture bootstrap: twenty role logins, two branches, configurations, actual TEST receipt and room issue APIs. HK shifts remain planned for start-allocation tests.');
