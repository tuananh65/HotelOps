// Supplement to E2E-25. Run LAST: permanently ends DVBV.storePeer's TEST shift.
// No API call or private-file read occurs on import.
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { run } from './p0-runtime-runner.mjs';
import { verifyTarget, probe, branch } from './p0-runtime-adapter.mjs';
export { verifyTarget };

export async function staleHandlerScenario(c) {
  c.check(process.env.P0_ALLOW_END_STORE_TEST_SHIFT === '1',
    'Explicit final-phase opt-in required: this case ends DVBV.storePeer shift');
  const b = branch(c.fixture), prefix = '/api/v2/requests';
  const first = 'DVBV.storePeer', next = 'DVBV.store';
  const firstUser = c.fixture.actors[first]?.userId, nextUser = c.fixture.actors[next]?.userId;
  c.check(firstUser && nextUser && firstUser !== nextUser, 'Two existing isolated TEST storekeepers required');
  const date = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  const calendar = who => c.request(who, 'GET', `/api/v2/workday?branchId=${b.branchId}&date=${date}`).then(r => c.ok(r));
  const firstShift = (await calendar(first)).active;
  c.check(firstShift?.canEnd === true, 'Store A must have an active shift without unfinished assigned work');
  c.check((await calendar(next)).active, 'Store B must remain on duty');
  const allocations = c.ok(await c.request('DVBV.hk', 'GET', `/api/v2/supplies/allocations?branchId=${b.branchId}`));
  const allocation = allocations.find(a => a.assignmentId === c.fixture.actors['DVBV.hk'].assignmentId);
  c.equal(allocation?.status, 'OPEN', 'Run before HK shift ends, after room scenarios finish');
  const item = c.fixture.items.find(i => i.code.startsWith('P0_FILL_'));
  c.check(item, 'Existing synthetic fill item required');
  const stock = () => probe('stock', { ...c.fixture, requests: { branchId: b.branchId, itemId: item.itemId } });
  const beforeStock = Number(await stock());
  let detail = c.ok(await c.request('DVBV.hk', 'POST', `${prefix}/refills`, {
    allocationId: allocation.allocationId, items: [{ itemId: item.itemId, quantity: 1 }],
    reason: 'P0 TEST stale handler reclaim; reject without issuing stock', operationKey: c.uuid(),
  }));
  const workId = detail.request.workId;
  const read = who => c.request(who, 'GET', `${prefix}/${workId}`).then(r => c.ok(r));
  const act = (who, command, state = detail, extra = {}) => c.request(who, 'POST', `${prefix}/${workId}/actions/${command}`, {
    rowVersion: state.request.rowVersion, operationKey: c.uuid(), ...extra,
  });
  detail = c.ok(await act('DVBV.supervisor', 'approve'));
  detail = c.ok(await act(first, 'claim'));
  c.equal(detail.request.handlerUserId, firstUser);
  c.check(!(await read(next)).actions.includes('claim'), 'Another active handler must prevent claim in UI contract');
  for (const command of ['claim', 'issue', 'reject']) {
    const denied = await act(next, command, detail, { reason: 'P0 TEST forbidden takeover' });
    c.ok(denied, 403);
    c.equal(denied.body?.code, 'REQUEST_ALREADY_CLAIMED');
  }
  const stable = await read(first);
  c.equal(stable.request.rowVersion, detail.request.rowVersion, 'Denied takeover must not mutate workflow');
  c.equal(stable.history.length, detail.history.length);
  const endable = (await calendar(first)).active;
  c.check(endable?.canEnd === true, 'No unrelated work may be closed to enable this probe');
  const endBody = { rowVersion: endable.rowVersion, operationKey: c.uuid() };
  const endRoute = `/api/v2/workday/assignments/${endable.assignmentId}/end`;
  c.ok(await c.request(first, 'POST', endRoute, endBody));
  c.ok(await c.request(first, 'POST', endRoute, endBody));
  c.equal((await calendar(first)).active, null, 'Store A shift must really end through API');
  detail = await read(next);
  c.check(detail.actions.includes('claim'), 'On-duty storekeeper must see stale-handler claim');
  c.check(!detail.actions.includes('issue') && !detail.actions.includes('reject'), 'Reclaim must precede fulfillment/rejection');
  c.ok(await act(next, 'issue'), 403);
  const claimBody = { rowVersion: detail.request.rowVersion, operationKey: c.uuid() };
  const claimRoute = `${prefix}/${workId}/actions/claim`;
  detail = c.ok(await c.request(next, 'POST', claimRoute, claimBody));
  c.equal(detail.request.handlerUserId, nextUser);
  const event = detail.history.find(h => h.action === 'claim' && h.previousHandler === firstUser && h.newHandler === nextUser);
  c.check(event?.reason && event.actorUserId === nextUser, 'Reclaim audit must preserve both handlers and reason');
  const duplicate = c.ok(await c.request(next, 'POST', claimRoute, claimBody));
  c.equal(duplicate.history.length, detail.history.length, 'Reclaim retry must not duplicate history');
  c.denied(await act(first, 'reject', detail, { reason: 'P0 TEST ended handler forbidden' }), [403, 409]);
  detail = c.ok(await act(next, 'reject', detail, { reason: 'P0 TEST close stale-handler probe without inventory changes' }));
  c.equal(detail.request.status, 'REJECTED');
  c.check(detail.history.some(h => h.action === 'reject' && h.actorUserId === nextUser), 'New handler rejection audit missing');
  c.equal(Number(await stock()), beforeStock, 'Claim/end/reject cannot change stock');
}

export const scenarios = { 'E2E-25': staleHandlerScenario };

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  if (!process.argv.includes('--apply') || process.env.P0_ALLOW_END_STORE_TEST_SHIFT !== '1') {
    console.error('Not run. Use --apply and P0_ALLOW_END_STORE_TEST_SHIFT=1 only after room workflows finish.');
    process.exitCode = 1;
  } else {
    process.env.P0_RUNTIME_CASES = 'E2E-25';
    try {
      process.exitCode = await run(process.env.P0_RUNTIME_FIXTURE, {verifyTarget, scenarios},
        process.env.P0_RUNTIME_RESULT ?? 'artifacts/p0-workflow/runtime-handler-supplement.json');
    } catch {
      console.error('Handler supplement failed during preflight; inspect TEST target and private fixture locally.');
      process.exitCode = 1;
    }
  }
}
