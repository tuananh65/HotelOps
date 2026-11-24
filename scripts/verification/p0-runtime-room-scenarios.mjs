// Real API scenarios only. Import through p0-runtime-adapter; never execute on import.
// Reserve DVCT.hk and DVCT/P0_101 for this suite. DVBV/P0_101 is used only
// for the deliberately unresolved stock-shortage case. No direct database writes.
import { recoverRooms, draftIssue } from './p0-fixture-recover-room.mjs';
export function roomScenarios({ probe }) {
  const fields = value => value?.fields ?? value;
  const root = '/api/v2/work/room-issues';
  const note = 'P0 TEST: kiểm tra quy trình khắc phục trên dữ liệu kiểm thử';
  const localDate = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

  function scope(c, suffix = 'DVCT') {
    const branch = c.fixture.branches.find(b => b.code === `P0_TEST_${suffix}`);
    c.check(branch, `Missing isolated branch ${suffix}`);
    const room = branch.rooms.find(r => r.code === 'P0_101');
    const item = c.fixture.items.find(i => i.code === 'P0_DURABLE_KETTLE');
    c.check(room && item && branch.assetId, 'Assigned durable item, asset and room fixture required');
    const actor = name => `${suffix}.${name}`;
    const call = (name, method, path, body) => c.request(actor(name), method, path, body);
    return { c, branch, roomId: room.roomId, item, actor, call };
  }
  async function room(s) {
    const rows = s.c.ok(await s.call('supervisor', 'GET', `/api/v2/rooms?branchId=${s.branch.branchId}`));
    const value = rows.find(r => r.roomId === s.roomId);
    s.c.check(value, 'Room missing from branch-scoped API');
    return value;
  }
  async function stock(s) {
    return Number(await probe('stock', { ...s.c.fixture,
      requests: { branchId: s.branch.branchId, itemId: s.item.itemId } }));
  }
  async function configure(s) {
    let types = s.c.ok(await s.call('om', 'GET', `/api/v2/work/room-issue-types?branchId=${s.branch.branchId}`));
    for (const kind of ['MISSING', 'BROKEN']) {
      const current = types.find(t => t.kind === kind);
      if (!current?.isActive) s.c.ok(await s.call('om', 'PUT',
        `/api/v2/work/room-issue-types?branchId=${s.branch.branchId}`, {
          kind, name: kind === 'MISSING' ? 'Thiếu đồ vật' : 'Hỏng đồ vật', isActive: true,
          rowVersion: current?.rowVersion, description: 'Loại báo cáo dùng cho kiểm thử P0',
        }));
    }
    types = s.c.ok(await s.call('supervisor', 'GET', `/api/v2/work/room-issue-types?branchId=${s.branch.branchId}`));
    s.types = Object.fromEntries(types.filter(t => t.isActive).map(t => [t.kind, t.referenceId]));
    s.c.check(s.types.MISSING && s.types.BROKEN, 'Both configured report types must be visible');
  }
  async function report(s, kind, reporter = 'supervisor', cleaningWorkId, quantity = 1) {
    await configure(s);
    const objects = s.c.ok(await s.call(reporter, 'GET',
      `/api/v2/work/room-issue-objects?branchId=${s.branch.branchId}&roomId=${s.roomId}`));
    s.c.check(kind === 'MISSING' ? objects.items.some(i => i.itemId === s.item.itemId)
      : objects.assets.some(a => a.assetId === s.branch.assetId), 'Reported object must actually belong to room');
    const result = fields(s.c.ok(await s.call(reporter, 'POST', root, {
      branchId: s.branch.branchId, roomId: s.roomId, typeId: s.types[kind],
      itemId: kind === 'MISSING' ? s.item.itemId : null,
      assetId: kind === 'BROKEN' ? s.branch.assetId : null,
      roomRowVersion: objects.rowVersion, operationKey: s.c.uuid(), note,
      quantity, impact: 'READINESS', cleaningWorkId,
    })));
    s.c.check(result.workId > 0, 'Report must persist a real work item');
    const request = s.c.ok(await s.call(reporter, 'GET', `/api/v2/requests/${result.workId}`));
    s.c.equal(request.request.status, 'PENDING_SUPERVISOR');
    return result.workId;
  }
  const detail = (s, id, who = 'supervisor') => s.call(who, 'GET', `${root}/${id}`).then(r => s.c.ok(r));
  async function action(s, id, command, who = 'supervisor', extra = {}, retry = false) {
    const d = await detail(s, id, who);
    const body = { rowVersion: fields(d.work).rowVersion, operationKey: s.c.uuid(), note, ...extra };
    if (command === 'inspection-pass') body.roomRowVersion = d.room.rowVersion;
    const result = fields(s.c.ok(await s.call(who, 'POST', `${root}/${id}/${command}`, body)));
    if (retry) {
      const before = await detail(s, id, who);
      s.c.ok(await s.call(who, 'POST', `${root}/${id}/${command}`, body));
      const after = await detail(s, id, who);
      s.c.equal(after.events.length, before.events.length, 'Retry must not append duplicate room event');
      s.c.equal(fields(after.work).status, result.status);
    }
    return result;
  }
  async function material(s, id, quantity = 1) {
    await action(s, id, 'request-material', 'supervisor', {
      itemId: s.item.itemId, unitId: s.item.unitId, quantity, warehouseId: s.branch.warehouseId,
    }, true);
    const d = await detail(s, id);
    const latest = [...d.materials].sort((a, b) => b.documentId - a.documentId)[0];
    s.c.equal(latest.status, 'APPROVED', 'Confirmed room remediation goes directly to warehouse');
    return latest.documentId;
  }
  const document = (s, id) => s.call('store', 'GET', `/api/v2/documents/${id}`).then(r => s.c.ok(r));
  async function derive(s, documentId) {
    const existing = await draftIssue(s.c, s.actor('store').split('.')[0], s.branch, documentId);
    if (existing) return existing;
    const source = await document(s, documentId);
    return fields(s.c.ok(await s.call('store', 'POST', `/api/v2/documents/${documentId}/derive`, {
      rowVersion: fields(source.document).rowVersion, operationKey: s.c.uuid(), kind: 'ISSUE',
      warehouseId: s.branch.warehouseId, reason: note,
      lines: source.lines.map(value => { const line = fields(value);
        return { lineId: line.lineId, quantity: line.approvedQty ?? line.actualQty }; }),
    })));
  }
  async function deliver(s, documentId, quantity = 1) {
    const before = await stock(s), issue = await derive(s, documentId);
    const d = await document(s, issue.documentId);
    s.c.ok(await s.call('store', 'POST', `/api/v2/documents/${issue.documentId}/actions/post`, {
      rowVersion: fields(d.document).rowVersion, operationKey: s.c.uuid(), reason: note,
    }));
    s.c.equal(await stock(s), before - quantity, 'Warehouse issue must deduct exactly the requested base quantity');
    s.c.equal(fields((await document(s, documentId)).document).status, 'FULLY_ISSUED');
    return issue.documentId;
  }
  async function fixMissing(s, id, { approve = true, quantity = 1 } = {}) {
    if (approve) await action(s, id, 'approve');
    const documentId = await material(s, id, quantity);
    await deliver(s, documentId, quantity);
    const before = await stock(s);
    await action(s, id, 'report-remedy', 'store', { documentId }, true);
    s.c.equal(await stock(s), before, 'Reporting remediation must not issue inventory again');
    s.c.equal((await detail(s, id)).stage, 'ROOM_ISSUE_INSPECTION_REQUESTED');
    return documentId;
  }
  async function fixBroken(s, id, { approve = true, create = true } = {}) {
    if (approve) await action(s, id, 'approve');
    if (create) await action(s, id, 'create-repair', 'maintenance', {}, true);
    await rejectGenericRepairBypass(s, id);
    await action(s, id, 'start-repair', 'maintenance');
    await action(s, id, 'report-remedy', 'maintenance', {}, true);
    await rejectGenericRepairBypass(s, id);
    const d = await detail(s, id);
    s.c.equal(d.stage, 'ROOM_ISSUE_INSPECTION_REQUESTED');
    s.c.equal(d.maintenance.length, 1, 'External repair uses one internal supervisor task');
    s.c.equal(d.maintenance[0].status, 'COMPLETED');
    return d.maintenance[0].workId;
  }
  async function rejectGenericRepairBypass(s, id) {
    const current = await detail(s, id, 'maintenance');
    s.c.check(current.maintenance.length === 1, 'Repair child required for bypass probe');
    const repairId = current.maintenance[0].workId;
    const before = s.c.ok(await s.call('maintenance', 'GET', `/api/v2/work/${repairId}`));
    for (const command of ['start', 'verify', 'reopen', 'progress', 'complete']) {
      const denied = await s.call('maintenance', 'POST', `/api/v2/work/${repairId}/actions/${command}`, {
        rowVersion: fields(before.work).rowVersion, operationKey: s.c.uuid(), progressPercent: 100, reason: note,
      });
      s.c.ok(denied, 403);
      s.c.equal(denied.body?.code, 'ROOM_ISSUE_USE_WORKFLOW', 'Denial must come from workflow guard, not invalid status');
    }
    const after = s.c.ok(await s.call('maintenance', 'GET', `/api/v2/work/${repairId}`));
    s.c.equal(fields(after.work).rowVersion, fields(before.work).rowVersion);
    s.c.equal(after.events.length, before.events.length, 'Rejected generic actions must not append repair history');
    s.c.equal((await detail(s, id, 'maintenance')).stage, current.stage);
  }
  async function roomAction(s, command) {
    const r = await room(s);
    s.c.ok(await s.call('reception', 'POST', `/api/v2/rooms/${s.roomId}/actions/${command}`, {
      rowVersion: r.rowVersion, operationKey: s.c.uuid(), reason: note,
    }));
  }
  async function startCleaner(s) {
    const a = s.c.fixture.actors[s.actor('hk')];
    s.c.check(a?.assignmentId, 'DVCT.hk shift fixture required');
    const week = s.c.ok(await s.call('hk', 'GET', `/api/v2/workday?branchId=${s.branch.branchId}&date=${localDate()}`));
    const shift = week.assignments.find(x => x.assignmentId === a.assignmentId);
    s.c.check(shift, 'Cleaning shift missing from personal calendar');
    if (shift.status !== 'WORKING') {
      s.c.equal(shift.status, 'PLANNED', 'Reserve DVCT.hk for room scenarios; a closed shift cannot be reused');
      s.c.ok(await s.call('hk', 'POST', `/api/v2/workday/assignments/${a.assignmentId}/start`, {
        rowVersion: shift.rowVersion, operationKey: s.c.uuid(),
      }));
    }
  }
  async function cleaning(s) {
    await startCleaner(s);
    let r = await room(s);
    if (r.operationalStatus === 'READY') {
      await roomAction(s, 'reserve'); await roomAction(s, 'check-in'); await roomAction(s, 'check-out');
      r = await room(s);
    }
    s.c.equal(r.operationalStatus, 'DIRTY', 'Cleaning fixture requires an actual dirty room');
    const work = fields(s.c.ok(await s.call('supervisor', 'POST', '/api/v2/work', {
      branchId: s.branch.branchId, kind: 'CLEANING', title: `P0 TEST: dọn ${r.code}`, roomId: r.roomId,
      roomRowVersion: r.rowVersion, assigneeUserId: s.c.fixture.actors[s.actor('hk')].userId,
      durationMinutes: 30,
    })));
    s.c.ok(await s.call('hk', 'POST', `/api/v2/work/${work.workId}/actions/start`, {
      rowVersion: work.rowVersion, operationKey: s.c.uuid(),
    }));
    return work.workId;
  }
  async function finishCleaning(s, workId) {
    const current = s.c.ok(await s.call('hk', 'GET', `/api/v2/work/${workId}`));
    const done = fields(s.c.ok(await s.call('hk', 'POST', `/api/v2/work/${workId}/actions/complete`, {
      rowVersion: fields(current.work).rowVersion, operationKey: s.c.uuid(),
    })));
    s.c.equal(done.status, 'PENDING_INSPECTION');
    s.c.ok(await s.call('supervisor', 'POST', `/api/v2/work/${workId}/inspection-result`, {
      rowVersion: done.rowVersion, operationKey: s.c.uuid(), result: 'PASS', note,
    }));
  }
  async function passed(s, id) {
    const before = await stock(s);
    const result = await action(s, id, 'inspection-pass', 'supervisor', {}, true);
    s.c.equal(result.status, 'CLOSED');
    s.c.equal(await stock(s), before, 'Inspection retry cannot change warehouse balance');
    const request = s.c.ok(await s.call('supervisor', 'GET', `/api/v2/requests/${id}`));
    s.c.equal(request.request.status, 'COMPLETED');
    const history = s.c.ok(await s.call('supervisor', 'GET', `/api/v2/rooms/${s.roomId}/history`));
    const blocks = history.blocks.map(fields).filter(b => b.sourceWorkId === id);
    s.c.check(blocks.length === 1 && blocks[0].releasedAt, 'Only completed report blocker must be released');
  }

  const scenarios = {
    'E2E-17': async c => {
      const s = scope(c), workId = await cleaning(s);
      const id = await report(s, 'MISSING', 'hk', workId);
      await finishCleaning(s, workId);
      c.equal((await room(s)).readiness, 'BLOCKED', 'Cleaning PASS cannot bypass unresolved missing item');
      await fixMissing(s, id); await passed(s, id);
      c.equal((await room(s)).readiness, 'READY');
    },
    'E2E-18': async c => {
      const s = scope(c), before = await stock(s), id = await report(s, 'MISSING');
      const first = await fixMissing(s, id);
      const pending = await detail(s, id);
      c.ok(await s.call('supervisor', 'POST', `${root}/${id}/inspection-fail`, {
        rowVersion: fields(pending.work).rowVersion, operationKey: c.uuid(),
      }), 400);
      await action(s, id, 'inspection-fail');
      c.equal(await stock(s), before - 1, 'Rejection must not automatically repeat warehouse issue');
      c.equal((await room(s)).readiness, 'BLOCKED');
      const second = await fixMissing(s, id, { approve: false });
      c.check(first !== second, 'Rework must use a new explicit material request');
      await passed(s, id);
      const d = await detail(s, id);
      c.equal(d.materials.length, 2); c.equal(await stock(s), before - 2);
      c.check(d.events.some(e => e.eventType === 'ROOM_ISSUE_INSPECTION_FAILED'), 'Failed inspection history lost');
    },
    'E2E-19': async c => {
      // This case deliberately remains pending; keep it in a separate branch from later room tests.
      const s = scope(c, 'DVBV'), before = await stock(s);
      let quantity = before + 1;
      c.check(before >= 0 && Number.isInteger(quantity), 'Finite synthetic durable stock required');
      const prior = c.ok(await s.call('supervisor', 'GET', `${root}?branchId=${s.branch.branchId}`));
      let id, documentId;
      for (const row of prior.filter(r => r.roomId === s.roomId && r.stage === 'ROOM_ISSUE_MATERIAL_REQUESTED')) {
        const pending = await detail(s, row.workId);
        if (pending.info.kind === 'MISSING' && pending.info.itemId === s.item.itemId && pending.info.quantity > before) {
          id = row.workId; quantity = pending.info.quantity;
          documentId = [...pending.materials].sort((a, b) => b.documentId - a.documentId)[0]?.documentId;
          c.check(documentId, 'Existing shortage must retain its material request'); break;
        }
      }
      if (!id) {
        id = await report(s, 'MISSING', 'supervisor', undefined, quantity);
        await action(s, id, 'approve');
        documentId = await material(s, id, quantity);
      }
      const issue = await derive(s, documentId);
      const d = await document(s, issue.documentId);
      const failed = await s.call('store', 'POST', `/api/v2/documents/${issue.documentId}/actions/post`, {
        rowVersion: fields(d.document).rowVersion, operationKey: c.uuid(), reason: note,
      });
      c.ok(failed, 409);
      c.check(/STOCK|BALANCE|INVENTORY/i.test(failed.body?.code ?? failed.body?.error?.code ?? ''), 'Failure must specifically prove insufficient inventory');
      c.equal(await stock(s), before);
      const pending = await detail(s, id, 'store');
      c.ok(await s.call('store', 'POST', `${root}/${id}/report-remedy`, {
        rowVersion: fields(pending.work).rowVersion, operationKey: c.uuid(), note, documentId,
      }), 409);
      c.equal((await room(s)).readiness, 'BLOCKED');
      const replenishment = c.ok(await s.call('store', 'POST', '/api/v2/requests/replenishments', {
        branchId: s.branch.branchId, warehouseId: s.branch.warehouseId,
        items: [{ itemId: s.item.itemId, quantity: 1 }], operationKey: c.uuid(), reason: note,
      }));
      c.check(replenishment.request.workId > 0, 'Shortage must allow a tracked replenishment request');
      c.equal(await stock(s), before, 'Creating replenishment cannot invent received stock');
    },
    'E2E-20': async c => {
      const s = scope(c), workId = await cleaning(s);
      const id = await report(s, 'BROKEN', 'hk', workId);
      await finishCleaning(s, workId);
      const repairId = await fixBroken(s, id);
      const repair = c.ok(await s.call('maintenance', 'GET', `/api/v2/work/${repairId}`));
      c.check(repair.assignments.map(fields).every(a => a.assigneeUserId === c.fixture.actors[s.actor('maintenance')].userId),
        'External repair must be supervised by Maintenance Supervisor, without internal technician assignment');
      await passed(s, id); c.equal((await room(s)).readiness, 'READY');
    },
    'E2E-21': async c => {
      const s = scope(c); await roomAction(s, 'reserve'); await roomAction(s, 'check-in');
      const id = await report(s, 'BROKEN', 'reception');
      c.equal((await room(s)).operationalStatus, 'OCCUPIED');
      await fixBroken(s, id); c.equal((await room(s)).operationalStatus, 'OCCUPIED');
      await passed(s, id); c.equal((await room(s)).operationalStatus, 'OCCUPIED');
      await roomAction(s, 'check-out');
      const workId = await cleaning(s); await finishCleaning(s, workId);
      c.equal((await room(s)).readiness, 'READY');
    },
    'E2E-22': async c => {
      const s = scope(c), id = await report(s, 'BROKEN'), repairId = await fixBroken(s, id);
      await action(s, id, 'inspection-fail');
      const failed = c.ok(await s.call('maintenance', 'GET', `/api/v2/work/${repairId}`));
      c.equal(fields(failed.work).status, 'PENDING');
      c.check(failed.events.map(fields).some(e => e.eventType === 'REPAIR_REINSPECTION_FAILED' && e.snapshotJson),
        'Rework must retain prior completion evidence');
      c.equal((await room(s)).readiness, 'BLOCKED');
      c.equal(await fixBroken(s, id, { approve: false, create: false }), repairId);
      await passed(s, id);
      const after = c.ok(await s.call('maintenance', 'GET', `/api/v2/work/${repairId}`));
      c.equal(after.events.map(fields).filter(e => e.eventType === 'COMPLETE').length, 2);
      c.equal((await room(s)).readiness, 'READY');
    },
    'E2E-23': async c => {
      const s = scope(c), before = await stock(s), id = await report(s, 'BROKEN', 'reception');
      const pending = await detail(s, id);
      c.ok(await s.call('supervisor', 'POST', `${root}/${id}/reject`, {
        rowVersion: fields(pending.work).rowVersion, operationKey: c.uuid(),
      }), 400);
      await action(s, id, 'reject', 'supervisor', {}, true);
      const d = await detail(s, id);
      c.equal(fields(d.work).status, 'CANCELLED');
      c.equal(d.materials.length, 0); c.equal(d.maintenance.length, 0);
      c.equal(await stock(s), before); c.equal((await room(s)).readiness, 'READY');
    },
    'E2E-24': async c => {
      const s = scope(c), missing = await report(s, 'MISSING'), broken = await report(s, 'BROKEN');
      const initial = c.ok(await s.call('supervisor', 'GET', `/api/v2/rooms/${s.roomId}/history`));
      c.equal(initial.blocks.map(fields).filter(b => !b.releasedAt && [missing, broken].includes(b.sourceWorkId)).length, 2);
      await fixMissing(s, missing); await passed(s, missing);
      c.equal((await room(s)).readiness, 'BLOCKED', 'A second unresolved blocker must prevent readiness');
      c.denied(await s.call('reception', 'POST', `/api/v2/rooms/${s.roomId}/actions/reserve`, {
        rowVersion: (await room(s)).rowVersion, operationKey: c.uuid(),
      }), [409]);
      await fixBroken(s, broken); await passed(s, broken);
      c.equal((await room(s)).readiness, 'READY', 'All remediation and cleaning conditions must pass');
    },
  };
  return Object.fromEntries(Object.entries(scenarios).map(([id, test]) => [id, async c => {
    // Resume failed earlier cases before creating another report; stock-shortage evidence stays pending.
    if (id !== 'E2E-19') await recoverRooms(c, { suffixes: ['DVCT'], probe });
    return test(c);
  }]));
}
