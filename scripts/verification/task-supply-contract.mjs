import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';

/** Real-workflow contract. The adapter must provision isolated users/rooms and call
 * actual services/API; do not substitute an in-memory inventory implementation.
 * Counts and stock snapshots are cumulative, so historical INITIAL documents are allowed. */
export async function verifyTaskSupplyLifecycle(adapter) {
  assert.match(await adapter.databaseName(), /^HotelOpsCompactWorkdayTestTaskSupplies\d+$/);
  const before = await adapter.snapshot();
  await adapter.startShift();
  const started = await adapter.snapshot();
  assert.deepEqual(started.stock, before.stock, 'Starting attendance must not issue stock');
  assert.equal(started.initialDocuments, before.initialDocuments);
  assert.equal(started.initialShortageNotifications, before.initialShortageNotifications);
  await adapter.retryStartShift();
  assert.deepEqual(await adapter.snapshot(), started, 'Start replay must not create another allocation');

  // A fresh allocation has no carried stock; select a room with capacity > 1 and
  // per-guest quantities which include a value other than one.
  const task = await adapter.assignCleaning();
  assert.ok(task.capacity > 1);
  assert.ok(task.items.some(item => item.perGuestQuantity !== 1));
  assert.deepEqual((await adapter.snapshot()).stock, started.stock, 'Assignment must not issue stock');
  const accepted = await adapter.accept(task);
  for (const item of task.items) {
    const line = accepted.items.find(row => row.itemId === item.itemId);
    assert.ok(line, `Missing supply item ${item.itemId}`);
    assert.equal(line.issued, task.capacity * item.perGuestQuantity);
    assert.equal(line.reserved, task.capacity * item.perGuestQuantity);
    assert.equal(line.used, 0);
  }
  const acceptedSnapshot = await adapter.snapshot();
  await adapter.retryAccept(task); // Conflict or replay response is acceptable, extra stock is not.
  assert.deepEqual(await adapter.snapshot(), acceptedSnapshot, 'Accept replay must not issue twice');
  await adapter.startTask(task);
  assert.deepEqual((await adapter.snapshot()).stock, acceptedSnapshot.stock, 'Start after accept must not issue again');

  const assessment = await adapter.assessPartialReplenishment(task);
  assert.ok(assessment.some(row => row.replenished < row.norm));
  const completed = await adapter.complete(task);
  for (const row of assessment) {
    const balance = completed.items.find(item => item.itemId === row.itemId);
    assert.equal(balance.used, row.replenished, 'Only actual placement is consumed');
    assert.equal(balance.reserved, 0);
  }
  assert.deepEqual((await adapter.snapshot()).stock, acceptedSnapshot.stock, 'Completion must not deduct warehouse stock again');
  await adapter.retryComplete(task);
  assert.deepEqual(await adapter.balances(), completed.items);
  await adapter.endAndReturnUnused();
  assert.ok((await adapter.balances()).every(row => row.remaining === 0));
}

export async function verifyTaskSupplyShortage(adapter) {
  assert.match(await adapter.databaseName(), /^HotelOpsCompactWorkdayTestTaskSupplies\d+$/);
  await adapter.startShift();
  const task = await adapter.assignCleaning();
  const before = await adapter.snapshot();
  await assert.rejects(() => adapter.accept(task), /STOCK_INSUFFICIENT|BATCH|REUSABLE/);
  assert.deepEqual(await adapter.snapshot(), before, 'A shortage must roll back every item, reservation and document');
  assert.equal(await adapter.acceptedAt(task), null);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const adapterPath = process.env.HOTELOPS_TASK_SUPPLIES_ADAPTER;
  if (!adapterPath) {
    console.error('NOT RUN: set HOTELOPS_TASK_SUPPLIES_ADAPTER to a real isolated-database adapter module.');
    process.exitCode = 2;
  } else {
    const { lifecycle, shortage } = await import(pathToFileURL(adapterPath).href);
    await verifyTaskSupplyLifecycle(lifecycle);
    await verifyTaskSupplyShortage(shortage);
    console.log('PASS: task supply lifecycle and atomic shortage on isolated SQL/API adapter.');
  }
}
