// Run only against the backed-up isolated Supply TEST clone, never Production.
import assert from 'node:assert/strict';
import fs from 'node:fs';
const fixture = JSON.parse(fs.readFileSync('artifacts/p0-workflow/private/runtime-fixture.json', 'utf8'));
const process = JSON.parse(fs.readFileSync('artifacts/supply-config-validation/process.json', 'utf8').replace(/^\uFEFF/, ''));
assert.equal(process.database, 'HotelOpsCompactWorkdayTestSupply261124');
assert.equal(process.port, 5775);
const base = 'http://127.0.0.1:5775/api/v2/';
const checks = [];
async function api(actor, path, body) {
  const response = await fetch(base + path, { method: body ? 'PUT' : 'GET', headers: {
    'Content-Type': 'application/json', ...(actor ? { Authorization: `Bearer ${fixture.actors[actor].token}` } : {}),
  }, body: body ? JSON.stringify(body) : undefined });
  return { status: response.status, data: await response.json().catch(() => null) };
}
for (const suffix of ['DVBV', 'DVCT']) {
  const actor = `${suffix}.om`;
  const branch = fixture.branches.find(b => b.code === `P0_TEST_${suffix}`);
  const other = fixture.branches.find(b => b.code !== branch.code);
  const path = `supplies/configuration?branchId=${branch.branchId}`;
  const list = await api(actor, path); assert.equal(list.status, 200);
  const original = list.data.find(r => r.itemId && r.supplyGroup === 'A'); assert(original);
  let current = original;
  for (const group of ['A', 'B', 'C', 'A']) {
    const saved = await api(actor, 'supplies/configuration', { ...current, warehouseId: null, supplyGroup: group,
      initialQuantity: group === 'A' ? original.initialQuantity : 0,
      perGuestQuantity: group === 'A' ? original.perGuestQuantity : 0 });
    assert.equal(saved.status, 200, `${suffix} save ${group}`);
    assert.equal(saved.data.warehouseId, branch.warehouseId);
    assert.equal(saved.data.revision, current.revision + 1);
    current = saved.data;
  }
  checks.push(`${suffix}: A/B/C auto-resolve one branch warehouse, versioned updates`);
  const foreign = await api(actor, 'supplies/configuration', { ...current, warehouseId: other.warehouseId });
  assert.equal(foreign.status, 409); assert.equal(foreign.data.title, 'WAREHOUSE_UNAVAILABLE');
  const stale = await api(actor, 'supplies/configuration', { ...original, warehouseId: null });
  assert.equal(stale.status, 409); assert.equal(stale.data.title, 'STALE_ROW_VERSION');
  const denied = await api(`${suffix}.hk`, 'supplies/configuration', { ...current, warehouseId: null });
  assert.equal(denied.status, 403);
  const cross = await api(actor, `supplies/configuration?branchId=${other.branchId}`); assert.equal(cross.status, 403);
  const after = await api(actor, path);
  const normalize = r => ({ ...r, updatedAt: r.updatedAt.replace(/Z$/, '') });
  assert.deepEqual(normalize(after.data.find(r => r.itemId === original.itemId)), normalize(current));
  checks.push(`${suffix}: foreign warehouse, stale version, HK write and cross-branch denied without changes`);
}
assert.equal((await api(null, 'supplies/configuration?branchId=1')).status, 401);
checks.push('Unauthenticated request denied');
fs.writeFileSync('artifacts/supply-config-validation/api-results.json', JSON.stringify({ passed: true, checks }, null, 2));
console.log(`PASS ${checks.length} API groups on isolated Supply TEST clone`);
