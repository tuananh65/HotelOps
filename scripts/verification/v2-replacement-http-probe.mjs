import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {replacementDecisionBrowser} from './v2-maintenance-workflow-browser.mjs';

// Invoked within the complete maintenance fixture after an assigned technician
// starts the repair. The caller continues through technical and HK inspection.
export async function verifyReplacementHttp({fixture,repair,asset,room,test,ok,request,version,checklist,own}){
 const fields=x=>x.fields??x,id=repair.workId,reason='TEST actual replacement verification';
 const detail=()=>ok('technician',`/work/${id}/replacement`);
 const body=async(extra={})=>({rowVersion:await version('technician',id),operationKey:randomUUID(),reason,...extra});
 const action=async(role,command,extra={})=>request(role,`/work/${id}/replacement/${command}`,'POST',await body(extra));
 const assetDetail=assetId=>ok('manager','/assets/'+assetId);
 let replacement,locationId,stockBefore,checkedItems;
 await test('replacement: technician proposal requires reason and plan, then is sent through UI',async()=>{
  stockBefore=await own('Inspect');await checklist('technician',id);
  for(const item of (await ok('technician','/work/'+id)).checklist.map(fields)){
   await ok('technician',`/work/${id}/checklist/${item.workChecklistItemId}`,'PUT',{rowVersion:item.rowVersion,outcome:'PASS',note:'TEST original asset checked before replacement'});
  }
  checkedItems=(await ok('technician','/work/'+id)).checklist.map(fields);assert.ok(checkedItems.length>0);
  assert.ok(checkedItems.every(i=>i.outcome==='PASS'&&i.note&&i.checkedAt&&i.checkedBy));
  const before=await detail();
  assert.equal((await action('technician','propose',{reason:'',plan:'TEST replacement'})).status,400);
  assert.equal((await action('technician','propose',{plan:''})).status,400);
  assert.deepEqual(await detail(),before);
  await replacementDecisionBrowser(fixture,id,'technician','propose');assert.equal((await detail()).state,'REPLACEMENT_PROPOSED');
 });
 await test('replacement: pending proposal blocks completion; technician lacks supervisor authority and MS confirms through UI',async()=>{
  const blocked=await request('technician',`/work/${id}/actions/complete`,'POST',await body());assert.equal(blocked.status,409);assert.match(JSON.stringify(blocked.data),/REPLACEMENT_NOT_INSTALLED/);
  assert.equal((await action('technician','confirm')).status,403);
  await replacementDecisionBrowser(fixture,id,'lead','confirm');
  const queue=await ok('manager','/work/replacements?branchId='+fixture.branch);assert.equal(queue.find(w=>w.workId===id).canDecide,true);
 });
 await test('replacement: OM rejection requires reason, preserves assets and returns proposal to technician',async()=>{
  const before=await detail(),oldAsset=await assetDetail(asset.assetId);
  assert.equal((await action('manager','reject',{reason:''})).status,400);assert.deepEqual(await detail(),before);
  await replacementDecisionBrowser(fixture,id,'manager','reject');
  const rejected=await detail();assert.equal(rejected.state,'REPLACEMENT_REJECTED');assert.equal(rejected.canPropose,true);assert.ok(rejected.history.at(-1).reason);
  assert.deepEqual(await assetDetail(asset.assetId),oldAsset);
 });
 await test('replacement: revised proposal is confirmed then approved by OM; replay cannot approve twice',async()=>{
  await replacementDecisionBrowser(fixture,id,'technician','propose');await replacementDecisionBrowser(fixture,id,'lead','confirm');
  const {body:approval}=await replacementDecisionBrowser(fixture,id,'manager','approve');
  const approved=await detail();assert.equal(approved.state,'REPLACEMENT_APPROVED');assert.equal(approved.canInstall,true);
  const retry=await request('manager',`/work/${id}/replacement/approve`,'POST',{...approval,rowVersion:await version('technician',id)});
  assert.equal(retry.status,409);assert.match(JSON.stringify(retry.data),/OPERATION_ALREADY_APPLIED/);assert.deepEqual(await detail(),approved);
  const queue=await ok('manager','/work/replacements?branchId='+fixture.branch);assert.equal(queue.find(w=>w.workId===id).canDecide,false);
 });
 await test('replacement: installation rejects the original asset and a replacement not yet at the target room',async()=>{
  const locations=await ok('manager','/catalog/locations?branchId='+fixture.branch);locationId=fields(locations.items[0]).locationId;assert.ok(locationId);
  replacement=fields(await ok('manager','/catalog/assets?branchId='+fixture.branch,'POST',{fields:{assetCode:fixture.prefix+'-replacement',name:'TEST installed replacement',locationId,conditionCode:'GOOD'}}));
  const before=await detail();
  const same=await action('technician','install',{replacementAssetId:asset.assetId});assert.equal(same.status,400);assert.match(JSON.stringify(same.data),/REPLACEMENT_ASSET_REQUIRED/);
  const misplaced=await action('technician','install',{replacementAssetId:replacement.assetId});assert.equal(misplaced.status,409);assert.match(JSON.stringify(misplaced.data),/REPLACEMENT_ASSET_NOT_INSTALLED/);
  assert.deepEqual(await detail(),before);
 });
 await test('replacement: OM reconciles both actual locations, technician records install and checklist is reset',async()=>{
  let current=fields((await assetDetail(replacement.assetId)).asset);
  await ok('manager',`/assets/${replacement.assetId}/actions/move`,'POST',{rowVersion:current.rowVersion,operationKey:randomUUID(),reason,roomId:room.roomId});
  const oldStillPresent=await action('technician','install',{replacementAssetId:replacement.assetId});assert.equal(oldStillPresent.status,409);assert.match(JSON.stringify(oldStillPresent.data),/ORIGINAL_ASSET_LOCATION_RECONCILIATION_REQUIRED/);
  current=fields((await assetDetail(asset.assetId)).asset);
  await ok('manager',`/assets/${asset.assetId}/actions/move`,'POST',{rowVersion:current.rowVersion,operationKey:randomUUID(),reason,locationId});
  const {body:installation}=await replacementDecisionBrowser(fixture,id,'technician','install',replacement.assetId);
  const after=await detail(),work=await ok('technician','/work/'+id);assert.equal(after.state,'REPLACEMENT_INSTALLED');assert.equal(fields(work.work).assetId,replacement.assetId);
  const reset=work.checklist.map(fields);assert.deepEqual(reset.map(i=>i.workChecklistItemId).sort(),checkedItems.map(i=>i.workChecklistItemId).sort());
  assert.ok(reset.every(i=>i.outcome===null&&i.note===null&&i.checkedAt===null&&i.checkedBy===null));
  const original=await assetDetail(asset.assetId),installed=await assetDetail(replacement.assetId);
  assert.equal(fields(original.asset).roomId,null);assert.equal(fields(original.asset).locationId,locationId);assert.equal(fields(installed.asset).roomId,room.roomId);assert.equal(fields(installed.asset).locationId,null);
  assert.equal(original.history.map(fields).filter(e=>e.eventType==='REPLACEMENT_OUT'&&e.sourceWorkId===id).length,1);
  assert.equal(installed.history.map(fields).filter(e=>e.eventType==='REPLACEMENT_IN'&&e.sourceWorkId===id).length,1);
  const retry=await request('technician',`/work/${id}/replacement/install`,'POST',{...installation,rowVersion:await version('technician',id)});assert.equal(retry.status,409);assert.match(JSON.stringify(retry.data),/OPERATION_ALREADY_APPLIED/);
  assert.deepEqual(await assetDetail(asset.assetId),original);assert.deepEqual(await assetDetail(replacement.assetId),installed);assert.deepEqual(await detail(),after);
 });
 await test('replacement: lifecycle does not debit the fixture consumable stock and still requires the new checklist',async()=>{
  assert.deepEqual(await own('Inspect'),stockBefore);
  const blocked=await request('technician',`/work/${id}/actions/complete`,'POST',await body());assert.equal(blocked.status,409);
  assert.ok((await ok('technician','/work/'+id)).checklist.map(fields).every(i=>i.outcome===null));
 });
}
