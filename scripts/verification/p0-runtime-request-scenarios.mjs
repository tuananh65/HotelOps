// Compose these executable cases with the integration owner's lifecycle/room cases.
// readStock/readAdditionalIssued must query live API or read-only TEST SQL.
export function requestScenarios(probe) {
  const root = '/api/v2/requests';
  const getFixture = c => c.fixture.requests;
  async function createRefill(c) {
    const f = getFixture(c);
    const body = { allocationId: f.allocationId, items: [{ itemId: f.itemId, quantity: 2 }],
      reason: 'P0 TEST: bổ sung vật tư trong ca', operationKey: c.uuid() };
    return { body, detail: c.ok(await c.request('hk', 'POST', `${root}/refills`, body)) };
  }
  const act = (c, actor, detail, action, extra = {}) => c.request(actor, 'POST', `${root}/${detail.request.workId}/actions/${action}`, {
    rowVersion: detail.request.rowVersion, operationKey: c.uuid(), ...extra,
  });
  return {
    'E2E-09': async c => {
      const before = await probe.readStock(c.fixture), { detail } = await createRefill(c);
      c.ok(await act(c, 'supervisor', detail, 'reject'), 400);
      const rejected = c.ok(await act(c, 'supervisor', detail, 'reject', { reason: 'P0 TEST: nhu cầu không hợp lệ' }));
      c.equal(rejected.request.status, 'REJECTED');
      c.equal(await probe.readStock(c.fixture), before, 'Rejection must not move inventory');
      c.denied(await act(c, 'store', rejected, 'issue'), [403, 409]);
    },
    'E2E-10': async c => {
      const before = await probe.readStock(c.fixture), held = await probe.readAdditionalIssued(c.fixture);
      const { body, detail } = await createRefill(c);
      const retried = c.ok(await c.request('hk', 'POST', `${root}/refills`, body));
      c.equal(retried.request.workId, detail.request.workId);
      c.denied(await act(c, 'store', detail, 'issue'), [403, 409]);
      c.denied(await act(c, 'om', detail, 'approve'));
      const approved = c.ok(await act(c, 'supervisor', detail, 'approve'));
      c.equal(approved.request.status, 'PENDING_STOREKEEPER');
      c.equal(await probe.readStock(c.fixture), before);
      const payload = { rowVersion: approved.request.rowVersion, operationKey: c.uuid() };
      const path = `${root}/${approved.request.workId}/actions/issue`;
      const issued = c.ok(await c.request('store', 'POST', path, payload));
      c.equal(issued.request.status, 'FULFILLED');
      c.equal(await probe.readStock(c.fixture), before - 2);
      c.equal(await probe.readAdditionalIssued(c.fixture), held + 2);
      const duplicate = c.ok(await c.request('store', 'POST', path, payload));
      c.equal(duplicate.history.length, issued.history.length);
      c.equal(await probe.readStock(c.fixture), before - 2);
      c.equal(await probe.readAdditionalIssued(c.fixture), held + 2);
      c.check(issued.history.some(h => h.previousQueue === 'HOUSEKEEPING_SUPERVISOR' && h.newQueue === 'STOREKEEPER'), 'Routing history missing');
    },
    'E2E-11': async c => {
      const before = await probe.readStock(c.fixture), { detail } = await createRefill(c);
      const approved = c.ok(await act(c, 'supervisor', detail, 'approve'));
      c.ok(await act(c, 'store', approved, 'reject'), 400);
      const rejected = c.ok(await act(c, 'store', approved, 'reject', { reason: 'P0 TEST: kho không đủ hàng để cấp phát' }));
      c.equal(rejected.request.status, 'REJECTED');
      c.equal(await probe.readStock(c.fixture), before);
    },
    'E2E-16': async c => {
      const f = getFixture(c);
      c.check(f.allowSyntheticTestReceipt === true, 'Explicit isolated fixture receipt authorization required');
      const before = await probe.readStock(c.fixture);
      let detail = c.ok(await c.request('store', 'POST', `${root}/replenishments`, {
        branchId: f.branchId, warehouseId: f.warehouseId, items: [{ itemId: f.itemId, quantity: 2 }],
        reason: 'P0 TEST ONLY: simulated physical goods receipt', operationKey: c.uuid(),
      }));
      c.denied(await act(c, 'foreignOm', detail, 'approve'));
      c.denied(await act(c, 'store', detail, 'approve'));
      const route = `${root}/${detail.request.workId}/receive`;
      c.denied(await c.request('store', 'POST', route, { rowVersion: detail.request.rowVersion,
        operationKey: c.uuid(), items: [{ itemId: f.itemId, quantity: 2 }] }), [403, 409]);
      detail = c.ok(await act(c, 'om', detail, 'approve'));
      c.equal(detail.request.status, 'PENDING_RECEIPT');
      c.equal(await probe.readStock(c.fixture), before, 'Approval cannot create stock');
      const payload = { rowVersion: detail.request.rowVersion, operationKey: c.uuid(), items: [{ itemId: f.itemId, quantity: 1 }] };
      detail = c.ok(await c.request('store', 'POST', route, payload));
      c.equal(detail.request.status, 'PENDING_RECEIPT');
      c.ok(await c.request('store', 'POST', route, payload));
      c.equal(await probe.readStock(c.fixture), before + 1);
      detail = c.ok(await c.request('store', 'POST', route, { ...payload, rowVersion: detail.request.rowVersion, operationKey: c.uuid() }));
      c.equal(detail.request.status, 'FULFILLED');
      c.equal(await probe.readStock(c.fixture), before + 2);
      c.equal(detail.items[0].receivedQuantity, 2);
    },
    'E2E-26': async c => {
      const { detail } = await createRefill(c);
      c.ok(await c.request('om', 'GET', `${root}/${detail.request.workId}`));
      for (const action of ['claim', 'approve', 'reject', 'issue'])
        c.denied(await act(c, 'om', detail, action, { reason: 'P0 TEST: forbidden OM mutation' }));
      c.ok(await act(c, 'supervisor', detail, 'reject', { reason: 'P0 TEST: close authorization probe request' }));
    },
  };
}
