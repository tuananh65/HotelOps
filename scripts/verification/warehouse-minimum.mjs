import assert from 'node:assert/strict';
import fs from 'node:fs';
const fixture = JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json', 'utf8'));
const runtime = JSON.parse(fs.readFileSync('artifacts/warehouse-validation/process.json', 'utf8').replace(/^\uFEFF/, ''));
assert.equal(runtime.database, 'HotelOpsCompactWorkdayTestWarehouse261124');
assert.equal(runtime.port, 5775);
const checks = [];
async function api(actor, path, body) {
  const r = await fetch('http://127.0.0.1:5775/api/v2/' + path, {method: body ? 'PUT' : 'GET', headers: {'Content-Type': 'application/json', ...(actor ? {Authorization: `Bearer ${fixture.actors[actor].token}`} : {})}, body: body ? JSON.stringify(body) : undefined});
  return {status:r.status, data: await r.json().catch(() => null)};
}
for(const suffix of ['DVBV','DVCT']) {
  const actor = `${suffix}.om`, branch = fixture.branches.find(b => b.code === `P0_TEST_${suffix}`), other = fixture.branches.find(b => b.code !== branch.code);
  const listPath = `supplies/configuration?branchId=${branch.branchId}`;
  const list = await api(actor,listPath); assert.equal(list.status,200);
  const original = list.data.find(c => c.itemId && c.supplyGroup === 'A'); assert(original);
  const subject = {branchId:branch.branchId, warehouseId:branch.warehouseId, itemId:original.itemId};
  const path = 'supplies/minimum?' + new URLSearchParams(subject);
  const current = await api(actor,path); assert.equal(current.status,200);
  const request = {...subject,...current.data, minimumStock:current.data.minimumStock + 3};
  const saved = await api(actor,'supplies/minimum',request); assert.equal(saved.status,200,JSON.stringify(saved));
  assert.equal(saved.data.minimumStock,request.minimumStock);
  const after = (await api(actor,listPath)).data.find(c => c.itemId === original.itemId);
  assert.equal(after.minimumStock,request.minimumStock); assert.equal(after.revision,original.revision+1);
  for(const key of ['supplyGroup','initialQuantity','perGuestQuantity','isActive','warehouseId','unitId']) assert.equal(after[key],original[key],key);
  const stock = await api(actor,`insights/stock?branchId=${branch.branchId}`); assert.equal(stock.status,200);
  assert.equal(stock.data.find(r => r.itemId === original.itemId).minimumQty,request.minimumStock);
  assert.equal((await api(actor,'supplies/minimum',request)).status,409);
  const valid = {...subject,...saved.data};
  for(const minimumStock of [-1,1.12345]) assert.equal((await api(actor,'supplies/minimum',{...valid,minimumStock})).status,400);
  for(const denied of ['hk','store','owner']) assert.equal((await api(`${suffix}.${denied}`,'supplies/minimum',valid)).status,403,denied);
  assert.equal((await api(actor,'supplies/minimum',{...valid,warehouseId:other.warehouseId})).status,409);
  assert.equal((await api(actor,`supplies/minimum?branchId=${other.branchId}&warehouseId=${other.warehouseId}&itemId=${original.itemId}`)).status,403);
  // Two callers edit the same version: exactly one may commit.
  const concurrent = await Promise.all([api(actor,'supplies/minimum',{...valid,minimumStock:6}),api(actor,'supplies/minimum',{...valid,minimumStock:7})]);
  assert.equal(concurrent.filter(r=>r.status===200).length,1,JSON.stringify(concurrent));
  assert.equal(concurrent.filter(r=>r.status===409).length,1);
  const latest = await api(actor,path);
  assert.equal((await api(actor,'supplies/minimum',{...subject,...latest.data,minimumStock:current.data.minimumStock})).status,200);
  // A disabled auto-allocation policy must remain disabled when editing its stock threshold.
  const activeConfig=(await api(actor,listPath)).data.find(c=>c.itemId===original.itemId);
  const disabled=await api(actor,'supplies/configuration',{...activeConfig,isActive:false});assert.equal(disabled.status,200);
  const disabledMinimum=await api(actor,path);
  assert.equal((await api(actor,'supplies/minimum',{...subject,...disabledMinimum.data,minimumStock:17})).status,200);
  const inactiveAfter=(await api(actor,listPath)).data.find(c=>c.itemId===original.itemId);
  assert.equal(inactiveAfter.isActive,false);assert.equal(inactiveAfter.minimumStock,17);
  assert.equal((await api(actor,'supplies/configuration',{...original,rowVersion:inactiveAfter.rowVersion})).status,200);
  checks.push(`${suffix}: minimum synchronized, disabled allocation stays disabled, original settings preserved, stale/invalid/foreign/unauthorized denied, concurrent update single commit`);
}
assert.equal((await api(null,'supplies/minimum?branchId=1&warehouseId=1&itemId=1')).status,401);
checks.push('Unauthenticated API rejected');
fs.writeFileSync('artifacts/warehouse-validation/api-results.json',JSON.stringify({passed:true,checks},null,2));
console.log(`PASS ${checks.length} real API groups (isolated TEST)`);
