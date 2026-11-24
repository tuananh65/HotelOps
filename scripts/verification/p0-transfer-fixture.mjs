import { readFile, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { verifyTarget } from './p0-runtime-adapter.mjs';
const fixture = JSON.parse(await readFile('artifacts/p0-workflow/private/runtime-fixture.json', 'utf8'));
await verifyTarget(fixture);
const branch = suffix => {
  const row = fixture.branches.find(b => b.code === `P0_TEST_${suffix}`);
  assert(row);
  return { branchId: row.branchId, warehouseId: row.warehouseId,
    omToken: fixture.actors[`${suffix}.om`].token, storeToken: fixture.actors[`${suffix}.store`].token };
};
const item = fixture.items.find(i => i.code === 'P0_DURABLE_KETTLE');
assert(item);
await writeFile('artifacts/p0-workflow/private/transfer-fixture.json', JSON.stringify({
  database: fixture.database, baseUrl: fixture.baseUrl, source: branch('DVBV'), destination: branch('DVCT'),
  itemId: item.itemId, unitId: item.unitId, outsiderToken: fixture.actors['DVBV.hk'].token,
}));
console.log('Private TEST transfer fixture prepared; no credentials printed.');
