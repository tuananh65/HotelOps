import assert from 'node:assert/strict';

/** Integration contract for the P0 clone harness. Adapter methods invoke real API/workflows;
 * the harness owns authenticated users, scheduled shifts and legitimate receipt documents.
 * No inventory inserts or production writes are performed by this contract. */
export async function verifySupplyLifecycle(adapter) {
  assert.match(await adapter.databaseName(), /^HotelOpsCompact.*Test/);
  const started = await adapter.start();
  const retried = await adapter.start();
  assert.equal(retried.allocationId, started.allocationId, 'start retries reuse allocation');
  assert.equal(await adapter.initialDocumentCount(), 1, 'one initial document per configured warehouse');
  const initial = await adapter.balances();
  assert.ok(initial.length > 0);
  for (const row of initial) assert.equal(row.initialIssued, 10);
  await adapter.assignCapacity(2);
  await adapter.assignCapacity(3);
  const four = await adapter.assignCapacity(4);
  for (const row of await adapter.balances()) {
    assert.equal(row.reserved, 9);
    assert.equal(row.available, 1);
    assert.equal(row.used, 0);
  }
  await assert.rejects(() => adapter.assignCapacity(2), /STAFF_SUPPLIES_INSUFFICIENT/);
  await assert.rejects(() => adapter.end(), /SUPPLY_RESERVATIONS_OPEN|CLEANING_WORK_OPEN/);
  const centralBefore = await adapter.stock();
  await adapter.complete(four);
  await adapter.retryComplete(four);
  assert.deepEqual(await adapter.stock(), centralBefore, 'completing cleaning never deducts central stock');
  for (const row of await adapter.balances()) {
    assert.equal(row.used, 4);
    assert.equal(row.reserved, 5);
    assert.equal(row.remaining, 6);
    assert.equal(row.available, 1);
  }
  await adapter.releaseOtherTasks();
  await adapter.end();
  await adapter.retryEnd();
  for (const row of await adapter.balances()) {
    assert.equal(row.returned, 6);
    assert.equal(row.remaining, 0);
    assert.equal(row.available, 0);
  }
  assert.equal(await adapter.returnDocumentCount(), 1);
  await assert.rejects(() => adapter.refillClosedShift(), /SUPPLY_ALLOCATION_CLOSED|ACTIVE_SHIFT_REQUIRED/);
}

export async function verifySupplyShortage(adapter) {
  assert.match(await adapter.databaseName(), /^HotelOpsCompact.*Test/);
  const before = await adapter.stock();
  const result = await adapter.start();
  assert.equal(result.status, 'SHORT');
  assert.equal(await adapter.workdayStatus(), 'WORKING');
  assert.deepEqual(await adapter.stock(), before);
  assert.equal(await adapter.initialDocumentCount(), 0);
  await assert.rejects(() => adapter.assignCapacity(2), /STAFF_SUPPLIES_INSUFFICIENT|SUPPLY_CONFIGURATION_REQUIRED/);
}

export async function verifySupplyScope(adapter) {
  assert.match(await adapter.databaseName(), /^HotelOpsCompact.*Test/);
  for (const role of ['HK_STAFF', 'HOUSEKEEPING_SUPERVISOR', 'STOREKEEPER', 'OWNER'])
    assert.equal(await adapter.saveConfigurationAs(role), 403);
  assert.equal(await adapter.readOtherStaffAsHk(), 403);
  assert.equal(await adapter.readForeignBranch(), 403);
  assert.equal(await adapter.genericPostSupplyDocumentAsHk(), 403);
}
