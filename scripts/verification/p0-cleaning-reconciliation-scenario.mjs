// Integration-owner adapter: requires an explicitly prepared, synthetic TEST legacy task.
// Does not manufacture old dates, balances, stock or reservations in SQL.
export async function verifyCleaningReconciliation(c, { workId, supervisor = 'DVBV.supervisor', staff = 'DVBV.hk', allocationId }) {
  if (!Number.isSafeInteger(workId) || !Number.isSafeInteger(allocationId)) throw new Error('Explicit legacy TEST fixture IDs required');
  const fields = value => value.fields ?? value;
  const detail = async () => c.ok(await c.request(supervisor, 'GET', `/api/v2/work/${workId}`));
  const before = await detail();
  const work = fields(before.work);
  c.check(work.title.startsWith('P0 TEST'), 'Only synthetic TEST work is eligible for this scenario');
  c.equal(work.kind, 'CLEANING');
  c.check(['IN_PROGRESS', 'ON_HOLD'].includes(work.status), 'Fixture must already be started');
  c.equal(before.capabilities.canReconcileCleaningSupplies, true);
  const body = { rowVersion: work.rowVersion, operationKey: c.uuid(), reason: 'P0 TEST explicit legacy supply reconciliation' };
  const route = `/api/v2/work/${workId}/reconcile-supplies`;
  c.denied(await c.request(staff, 'POST', route, body));
  c.ok(await c.request(supervisor, 'POST', route, body));
  const after = await detail();
  c.equal(fields(after.work).status, work.status, 'Recovery must preserve progress status');
  c.equal(fields(after.work).actualStartAt, work.actualStartAt, 'Recovery must preserve actual start');
  c.equal(after.assignments, before.assignments, 'Recovery must preserve assignment history');
  c.equal(after.time, before.time, 'Recovery must preserve work intervals');
  c.equal(after.capabilities.canReconcileCleaningSupplies, false);
  const historyRoute = `/api/v2/supplies/allocations/${allocationId}/history`;
  const ledger = c.ok(await c.request(staff, 'GET', historyRoute));
  c.check(ledger.some(x => x.workId === workId && x.kind === 'RESERVE'), 'Reservation must have real ledger records');
  c.check(!ledger.some(x => x.workId === workId && x.kind === 'USE'), 'Recovery must not pretend work was completed');
  c.ok(await c.request(supervisor, 'POST', route, body));
  c.equal(c.ok(await c.request(staff, 'GET', historyRoute)), ledger, 'Retry must not double reserve');
}
