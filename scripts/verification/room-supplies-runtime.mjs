import assert from 'node:assert/strict';
import fs from 'node:fs';
const read = path => JSON.parse(fs.readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const fixture = read('artifacts/p0-workflow/private/runtime-fixture.json');
const runtime = read('artifacts/warehouse-validation/process.json');
assert.equal(runtime.database, 'HotelOpsCompactWorkdayTestWarehouse261124');
assert.equal(runtime.port, 5775);
assert(read(`artifacts/workday/backup-${runtime.database}.json`).verified);
const rooms = read('artifacts/room-supplies/runtime-rooms.json').sort((a,b) => a.capacity-b.capacity);
const branch = fixture.branches.find(b => b.code === 'P0_TEST_DVCT');
const checkpoint = 'artifacts/room-supplies/runtime-checkpoint.json';
const state = fs.existsSync(checkpoint) ? read(checkpoint) : { rooms: {}, checks: [] };
const save = () => fs.writeFileSync(checkpoint, JSON.stringify(state, null, 2));
const key = () => crypto.randomUUID();
const note = 'TEST đối soát vật tư theo ID phòng và sức chứa tối đa';
async function api(who, path, body) {
  const r = await fetch(`http://127.0.0.1:5775/api/v2/${path}`, { method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${fixture.actors['DVCT.'+who].token}` }, body: body ? JSON.stringify(body) : undefined });
  return { status: r.status, data: await r.json() };
}
const ok = r => { assert.equal(r.status, 200, JSON.stringify(r)); return r.data; };
const allocation = async () => ok(await api('hk', `supplies/allocations?branchId=${branch.branchId}`)).find(a => a.status !== 'CLOSED');
const getRoom = async id => ok(await api('reception', `rooms?branchId=${branch.branchId}`)).find(r => r.roomId === id);
const detail = async id => ok(await api('supervisor', `work/${id}`));
const config = ok(await api('om', `supplies/configuration?branchId=${branch.branchId}`)).filter(c => c.supplyGroup === 'A' && c.isActive);
assert.equal(config.length, 8); assert(config.every(c => c.perGuestQuantity === 1));
if (!state.prepared) {
  const a = await allocation(); assert(a);
  const items = a.items.filter(i => i.available < 6).map(i => ({ itemId: i.itemId, quantity: 6-i.available }));
  if (items.length) {
    if (!state.refillId) { state.refillId = ok(await api('hk', 'requests/refills', { allocationId: a.allocationId, items, reason: note, operationKey: key() })).request.workId; save(); }
    for (const [who, action] of [['supervisor','approve'], ['store','issue']]) {
      let d = ok(await api(who, `requests/${state.refillId}`));
      if (d.actions.includes('claim')) d = ok(await api(who, `requests/${state.refillId}/actions/claim`, { rowVersion: d.request.rowVersion, operationKey: key(), reason: note }));
      if (d.actions.includes(action)) ok(await api(who, `requests/${state.refillId}/actions/${action}`, { rowVersion: d.request.rowVersion, operationKey: key(), reason: note }));
    }
  }
  state.prepared = true; save();
}
for (const room of rooms) {
  const s = state.rooms[room.roomId] ??= { capacity: room.capacity }; if (s.done) continue;
  if (!s.workId) {
    let r = await getRoom(room.roomId);
    for (const [status, action] of [['READY','reserve'],['RESERVED','check-in'],['OCCUPIED','check-out']]) if (r.operationalStatus === status) {
      ok(await api('reception', `rooms/${room.roomId}/actions/${action}`, { rowVersion: r.rowVersion, operationKey: key(), reason: note })); r = await getRoom(room.roomId);
    }
    assert.equal(r.operationalStatus, 'DIRTY'); s.before = await allocation();
    s.workId = ok(await api('supervisor','work', { branchId: branch.branchId, kind: 'CLEANING', roomId: room.roomId, title: note, roomRowVersion: r.rowVersion, assigneeUserId: fixture.actors['DVCT.hk'].userId, durationMinutes: 25 })).fields.workId; save();
  }
  let d = await detail(s.workId);
  if (d.work.fields.status === 'ASSIGNED') ok(await api('hk', `work/${s.workId}/actions/start`, { rowVersion: d.work.fields.rowVersion, operationKey: key() }));
  d = await detail(s.workId);
  if (d.work.fields.status === 'IN_PROGRESS') {
    const objects = ok(await api('hk', `work/room-issue-objects?branchId=${branch.branchId}&roomId=${room.roomId}&workId=${s.workId}`));
    const types = ok(await api('hk', `work/room-issue-types?branchId=${branch.branchId}`));
    const type = types.find(t => t.kind === 'MISSING'); assert(type);
    if (room.capacity === 2 && !s.issueId) {
      assert(objects.items.length > 0); state.durableItemId = objects.items[0].itemId;
      assert(!config.some(c => c.itemId === state.durableItemId), 'Fill must not appear as durable missing object');
      s.issueId = ok(await api('hk','work/room-issues', { branchId: branch.branchId, roomId: room.roomId, typeId: type.referenceId, itemId: state.durableItemId, quantity: 1, impact: 'READINESS', roomRowVersion: objects.rowVersion, cleaningWorkId: s.workId, operationKey: key(), note })).fields.workId; save();
    }
    if (room.capacity === 4) {
      assert(!objects.items.some(i => i.itemId === state.durableItemId));
      const denied = await api('hk','work/room-issues', { branchId: branch.branchId, roomId: room.roomId, typeId: type.referenceId, itemId: state.durableItemId, quantity: 1, impact: 'READINESS', roomRowVersion: objects.rowVersion, cleaningWorkId: s.workId, operationKey: key(), note });
      assert.equal(denied.status, 409); assert.equal(denied.data.title, 'ROOM_ISSUE_OBJECT_NOT_IN_ROOM');
      state.checks.push('Same durable item ID rejected for a different room without an assignment');
    }
    d = await detail(s.workId);
    s.completePayload = { rowVersion: d.work.fields.rowVersion, operationKey: key() }; save();
    ok(await api('hk', `work/${s.workId}/actions/complete`, s.completePayload));
  }
  if (s.completePayload) ok(await api('hk', `work/${s.workId}/actions/complete`, s.completePayload));
  s.after = await allocation();
  for (const c of config) {
    const before = s.before.items.find(i => i.itemId === c.itemId), after = s.after.items.find(i => i.itemId === c.itemId);
    assert.equal(after.used-before.used, room.capacity); assert.equal(before.remaining-after.remaining, room.capacity);
    assert.equal(after.reserved, before.reserved); assert(after.available >= 0);
  }
  const ledger = ok(await api('hk', `supplies/allocations/${s.after.allocationId}/history`)).filter(e => e.workId === s.workId && e.kind === 'USE');
  assert.equal(ledger.length, 8); assert.equal(new Set(ledger.map(e => e.itemId)).size, 8); assert(ledger.every(e => e.quantity === room.capacity));
  state.checks.push(`Room ${room.roomId}: capacity ${room.capacity}; eight item IDs each consumed ${room.capacity} once including completion retry`); save();
  if (s.issueId) {
    const issue = async () => ok(await api('supervisor', `work/room-issues/${s.issueId}`));
    let reported = await issue();
    if (reported.stage === 'ROOM_ISSUE_REPORTED') { d = await detail(s.workId); ok(await api('supervisor', `work/${s.workId}/confirm-room-issue`, { rowVersion: d.work.fields.rowVersion, operationKey: key(), relatedId: s.issueId, reason: note })); }
    reported = await issue(); assert.equal(reported.info.itemId, state.durableItemId);
    if (reported.stage === 'ROOM_ISSUE_MATERIAL_REQUESTED') {
      const materialId = reported.materials[0].documentId, doc = ok(await api('store', `documents/${materialId}`));
      assert.equal(doc.document.fields.receivingRoomId, room.roomId);
      assert.equal(doc.lines.length, 1); assert.equal(doc.lines[0].fields.itemId, state.durableItemId); assert.equal(doc.lines[0].fields.approvedQty, 1);
      if (doc.document.fields.status !== 'FULLY_ISSUED') {
        const issued = ok(await api('store', `documents/${materialId}/derive`, { rowVersion: doc.document.fields.rowVersion, operationKey: key(), kind: 'ISSUE', warehouseId: branch.warehouseId, reason: note, lines: doc.lines.map(l => ({lineId:l.fields.lineId,quantity:l.fields.approvedQty})) })).fields;
        ok(await api('store', `documents/${issued.documentId}/actions/post`, { rowVersion: issued.rowVersion, operationKey: key(), reason: note }));
      }
      reported = await issue(); ok(await api('store', `work/room-issues/${s.issueId}/report-remedy`, { rowVersion: reported.work.fields.rowVersion, roomRowVersion: reported.room.rowVersion, operationKey: key(), note, documentId: materialId }));
    }
    reported = await issue();
    if (reported.stage === 'ROOM_ISSUE_INSPECTION_REQUESTED') ok(await api('supervisor', `work/room-issues/${s.issueId}/inspection-pass`, { rowVersion: reported.work.fields.rowVersion, roomRowVersion: reported.room.rowVersion, operationKey: key(), note }));
    state.checks.push('Durable shortage preserved roomId/itemId through report, confirmation, warehouse issue and reinspection');
  }
  d = await detail(s.workId); if (d.capabilities.canRecordPaperInspection) ok(await api('supervisor', `work/${s.workId}/inspection-result`, { rowVersion: d.work.fields.rowVersion, operationKey: key(), result: 'PASS', note }));
  assert.equal((await getRoom(room.roomId)).readiness, 'READY'); s.done = true; save();
}
fs.writeFileSync('artifacts/room-supplies/runtime-result.json', JSON.stringify({checks:state.checks,rooms:Object.entries(state.rooms).map(([roomId,s])=>({roomId,workId:s.workId,capacity:s.capacity,issueId:s.issueId,done:s.done}))},null,2));
console.log(JSON.stringify({passed:state.checks.length,checks:state.checks}));
