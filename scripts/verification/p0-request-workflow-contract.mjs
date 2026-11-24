import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

// Real-API contract. Adapter uses authenticated test-clone clients and genuine existing stock.
// client(method, path, body?) => { status, body }; never mocks stock or fabricates fixture balances.
// Fixture: branchId, warehouseId, allocationId, itemId, foreignAllocationId;
// readStock()/readAdditionalIssued() read the clone through a scoped API or read-only SQL adapter.
export async function verifyRequestWorkflow(clients, fixture) {
  const { hk, supervisor, store, om, foreignOm } = clients;
  const root = '/api/v2/requests';
  const ok = (response) => {
    assert.equal(response.status, 200, JSON.stringify(response.body));
    return response.body;
  };
  const denied = (response) => assert.ok([403, 404, 409].includes(response.status), JSON.stringify(response));
  const action = (client, detail, name, extra = {}) => client('POST', `${root}/${detail.request.workId}/actions/${name}`, {
    rowVersion: detail.request.rowVersion, operationKey: randomUUID(), ...extra,
  });
  const amounts = [{ itemId: fixture.itemId, quantity: 2 }];
  const stockBeforeRefill = await fixture.readStock();
  const heldBefore = await fixture.readAdditionalIssued();
  const createBody = { allocationId: fixture.allocationId, items: amounts, reason: 'Bổ sung vật tư cho ca đang làm', operationKey: randomUUID() };
  let refill = ok(await hk('POST', `${root}/refills`, createBody));
  assert.equal(refill.request.status, 'PENDING_SUPERVISOR');
  const refillId = refill.request.workId;
  const retryCreate = ok(await hk('POST', `${root}/refills`, createBody));
  assert.equal(retryCreate.request.workId, refillId);
  denied(await hk('POST', `${root}/refills`, { ...createBody, operationKey: randomUUID() }));
  denied(await hk('POST', `${root}/refills`, { ...createBody, allocationId: fixture.foreignAllocationId, operationKey: randomUUID() }));
  denied(await action(om, refill, 'approve'));
  denied(await action(store, refill, 'issue'));
  const outdated = refill;
  refill = ok(await action(supervisor, refill, 'approve'));
  assert.equal(refill.request.workId, refillId);
  assert.equal(refill.request.status, 'PENDING_STOREKEEPER');
  assert.equal(refill.request.queueRole, 'STOREKEEPER');
  denied(await action(supervisor, outdated, 'approve'));
  assert.equal(await fixture.readStock(), stockBeforeRefill, 'Approval must not issue stock');
  const issueBody = { rowVersion: refill.request.rowVersion, operationKey: randomUUID() };
  refill = ok(await store('POST', `${root}/${refillId}/actions/issue`, issueBody));
  assert.equal(refill.request.status, 'FULFILLED');
  assert.equal(await fixture.readStock(), stockBeforeRefill - 2);
  assert.equal(await fixture.readAdditionalIssued(), heldBefore + 2);
  const issueRetry = ok(await store('POST', `${root}/${refillId}/actions/issue`, issueBody));
  assert.equal(issueRetry.history.length, refill.history.length);
  assert.equal(await fixture.readStock(), stockBeforeRefill - 2, 'Retry must not deduct again');
  assert.equal(await fixture.readAdditionalIssued(), heldBefore + 2, 'Retry must not add again');
  assert.ok(refill.history.some(e => e.previousQueue === 'HOUSEKEEPING_SUPERVISOR' && e.newQueue === 'STOREKEEPER'));

  const beforeReceipt = await fixture.readStock();
  let replenishment = ok(await store('POST', `${root}/replenishments`, {
    branchId: fixture.branchId, warehouseId: fixture.warehouseId, items: amounts,
    reason: 'Đề nghị bổ sung hàng thực tế vào kho', operationKey: randomUUID(),
  }));
  assert.equal(replenishment.request.status, 'PENDING_OPERATIONS_MANAGER');
  denied(await action(foreignOm, replenishment, 'approve'));
  denied(await action(store, replenishment, 'approve'));
  denied(await store('POST', `${root}/${replenishment.request.workId}/receive`, {
    rowVersion: replenishment.request.rowVersion, operationKey: randomUUID(), items: amounts,
  }));
  replenishment = ok(await action(om, replenishment, 'approve'));
  assert.equal(replenishment.request.status, 'PENDING_RECEIPT');
  assert.equal(await fixture.readStock(), beforeReceipt, 'OM approval must not manufacture stock');
  const receiveBody = { rowVersion: replenishment.request.rowVersion, operationKey: randomUUID(), items: [{ itemId: fixture.itemId, quantity: 1 }] };
  replenishment = ok(await store('POST', `${root}/${replenishment.request.workId}/receive`, receiveBody));
  assert.equal(replenishment.request.status, 'PENDING_RECEIPT');
  assert.equal(await fixture.readStock(), beforeReceipt + 1);
  ok(await store('POST', `${root}/${replenishment.request.workId}/receive`, receiveBody));
  assert.equal(await fixture.readStock(), beforeReceipt + 1, 'Receipt retry must not duplicate stock');
  replenishment = ok(await store('POST', `${root}/${replenishment.request.workId}/receive`, {
    ...receiveBody, rowVersion: replenishment.request.rowVersion, operationKey: randomUUID(),
  }));
  assert.equal(replenishment.request.status, 'FULFILLED');
  assert.equal(await fixture.readStock(), beforeReceipt + 2);
  assert.equal(replenishment.items[0].receivedQuantity, 2);
  assert.ok(replenishment.history.every(e => e.correlationId === replenishment.request.correlationId));
  return { refillId, replenishmentId: replenishment.request.workId };
}

// Call against a separate open request whose handler has been released to its role queue.
export async function verifyExclusiveClaim(first, second, detail) {
  const path = `/api/v2/requests/${detail.request.workId}/actions/claim`;
  const result = await Promise.all([first, second].map(client => client('POST', path, {
    rowVersion: detail.request.rowVersion, operationKey: randomUUID(),
  })));
  assert.equal(result.filter(r => r.status === 200).length, 1, 'Exactly one concurrent handler wins');
  assert.equal(result.filter(r => [403, 409].includes(r.status)).length, 1);
}

export async function verifyRefillRejectedWithoutIssue(store, detail, fixture) {
  const before = await fixture.readStock();
  const path = `/api/v2/requests/${detail.request.workId}/actions`;
  const missingReason = await store('POST', `${path}/reject`, { rowVersion: detail.request.rowVersion, operationKey: randomUUID() });
  assert.equal(missingReason.status, 400);
  const rejected = await store('POST', `${path}/reject`, { rowVersion: detail.request.rowVersion, operationKey: randomUUID(), reason: 'Kho thiếu hàng để cấp đủ yêu cầu' });
  assert.equal(rejected.status, 200);
  assert.equal(rejected.body.request.status, 'REJECTED');
  assert.equal(await fixture.readStock(), before);
}
