// Genuine workflow recovery for synthetic TEST rooms. Never reset/delete history or create stock.
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const fields = value => value?.fields ?? value;
const root = '/api/v2/work/room-issues';
const note = 'P0 TEST: resume interrupted synthetic remediation; preserve all prior history';

export async function draftIssue(c, suffix, branch, sourceId) {
  const found = [];
  for (let page = 1; ; page++) {
    const list = c.ok(await c.request(`${suffix}.store`, 'GET',
      `/api/v2/documents?branchId=${branch.branchId}&kind=ISSUE&page=${page}&pageSize=100`));
    found.push(...list.items.map(fields).filter(x => x.sourceDocumentId === sourceId && x.status === 'DRAFT'));
    if (page * list.pageSize >= list.total) break;
  }
  c.check(found.length <= 1, 'Multiple draft issues require explicit investigation; do not issue twice');
  return found[0];
}

export async function recoverRooms(c, { suffixes = ['DVCT', 'DVBV'], probe } = {}) {
  const results = [];
  for (const suffix of suffixes) {
    const branch = c.fixture.branches.find(b => b.code === `P0_TEST_${suffix}`);
    c.check(branch, 'Only isolated synthetic branch may be recovered');
    const roomId = branch.rooms.find(r => r.code === 'P0_101')?.roomId;
    const actors = new Set(Object.entries(c.fixture.actors).filter(([k]) => k.startsWith(`${suffix}.`)).map(([, a]) => a.userId));
    const call = (who, method, path, body) => c.request(`${suffix}.${who}`, method, path, body).then(r => c.ok(r));
    const detail = id => call('supervisor', 'GET', `${root}/${id}`);
    const action = async (id, command, who = 'supervisor', extra = {}) => {
      const d = await detail(id);
      return call(who, 'POST', `${root}/${id}/${command}`, { rowVersion: fields(d.work).rowVersion,
        operationKey: c.uuid(), note, ...(command === 'inspection-pass' ? { roomRowVersion: d.room.rowVersion } : {}), ...extra });
    };
    const reports = await call('supervisor', 'GET', `${root}?branchId=${branch.branchId}`);
    // Reject unverified reports first, leaving approved work to complete through normal routing.
    reports.sort((a, b) => Number(b.stage === 'ROOM_ISSUE_REPORTED') - Number(a.stage === 'ROOM_ISSUE_REPORTED'));
    for (const row of reports.filter(r => r.roomId === roomId && !['CLOSED', 'CANCELLED'].includes(r.status))) {
      let d = await detail(row.workId);
      c.check(actors.has(fields(d.work).createdBy), 'Refusing non-fixture report');
      const id = row.workId;
      if (d.stage === 'ROOM_ISSUE_REPORTED') {
        await action(id, 'reject', 'supervisor', { note: 'P0 TEST: interrupted unverified synthetic report rejected during recovery' });
        results.push({ suffix, workId: id, outcome: 'REJECTED_UNVERIFIED' }); continue;
      }
      if (d.info.kind === 'MISSING') {
        if (['ROOM_ISSUE_APPROVED', 'ROOM_ISSUE_INSPECTION_FAILED'].includes(d.stage)) {
          const item = c.fixture.items.find(x => x.itemId === d.info.itemId);
          c.check(item, 'Unknown fixture material');
          await action(id, 'request-material', 'supervisor', { itemId: item.itemId, unitId: item.unitId,
            quantity: d.info.quantity, warehouseId: branch.warehouseId });
          d = await detail(id);
        }
        if (d.stage === 'ROOM_ISSUE_MATERIAL_REQUESTED') {
          const latest = [...d.materials].sort((a, b) => b.documentId - a.documentId)[0];
          c.check(latest, 'Approved remediation has no material request');
          const source = await call('store', 'GET', `/api/v2/documents/${latest.documentId}`);
          if (fields(source.document).status !== 'FULLY_ISSUED') {
            c.check(fields(source.document).status === 'APPROVED', 'Partial or cancelled material requires manual investigation');
            const quantity = source.lines.map(fields).reduce((sum, l) => sum + Number(l.approvedQty ?? l.actualQty), 0);
            const available = Number(await probe('stock', { ...c.fixture,
              requests: { branchId: branch.branchId, itemId: d.info.itemId } }));
            if (available < quantity) { results.push({ suffix, workId: id, outcome: 'KEPT_STOCK_SHORTAGE' }); continue; }
            let issue = await draftIssue(c, suffix, branch, latest.documentId);
            if (!issue) issue = fields(await call('store', 'POST', `/api/v2/documents/${latest.documentId}/derive`, {
              rowVersion: fields(source.document).rowVersion, operationKey: c.uuid(), kind: 'ISSUE',
              warehouseId: branch.warehouseId, reason: note,
              lines: source.lines.map(fields).map(l => ({ lineId: l.lineId, quantity: l.approvedQty ?? l.actualQty })),
            }));
            const current = await call('store', 'GET', `/api/v2/documents/${issue.documentId}`);
            await call('store', 'POST', `/api/v2/documents/${issue.documentId}/actions/post`, {
              rowVersion: fields(current.document).rowVersion, operationKey: c.uuid(), reason: note });
          }
          await action(id, 'report-remedy', 'store', { documentId: latest.documentId });
        }
      } else if (d.info.kind === 'BROKEN') {
        if (d.stage === 'ROOM_ISSUE_REPAIR_REQUESTED') await action(id, 'create-repair', 'maintenance');
        d = await detail(id);
        if (d.stage === 'ROOM_ISSUE_REPAIR_CREATED') {
          const repair = d.maintenance.find(x => !['CLOSED', 'CANCELLED'].includes(x.status));
          c.check(repair, 'Missing repair task');
          if (repair.status === 'PENDING') await action(id, 'start-repair', 'maintenance');
          await action(id, 'report-remedy', 'maintenance');
        }
      }
      d = await detail(id);
      if (d.stage === 'ROOM_ISSUE_INSPECTION_REQUESTED') await action(id, 'inspection-pass');
      d = await detail(id);
      c.check(fields(d.work).status === 'CLOSED', `Recovery incomplete for report ${id}: ${d.stage}`);
      results.push({ suffix, workId: id, outcome: 'COMPLETED_THROUGH_API' });
    }
  }
  return results;
}

async function main() {
  assert(process.argv.includes('--apply'), 'Explicit --apply required after TEST API restart');
  const fixture = JSON.parse(await readFile(resolve(process.env.P0_RUNTIME_FIXTURE), 'utf8'));
  const { verifyTarget, probe } = await import('./p0-runtime-adapter.mjs');
  const target = await verifyTarget(fixture);
  assert.equal(target.database, 'HotelOpsCompactWorkdayTestP0261124');
  const traces = [];
  const c = { fixture, uuid: randomUUID, check: assert.ok,
    ok(r) { assert.equal(r.status, 200, `HTTP ${r.status}: ${r.body?.code ?? r.body?.title ?? 'API error'}`); return r.body; },
    async request(actor, method, route, body) {
      assert(route.startsWith('/api/v2/'));
      const response = await fetch(new URL(route, fixture.baseUrl), { method, redirect: 'error',
        headers: { Authorization: `Bearer ${fixture.actors[actor].token}`, 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(120000) });
      traces.push({ actor, method, path: route.split('?')[0], status: response.status });
      return { status: response.status, body: await response.json() };
    } };
  let results;
  try { results = await recoverRooms(c, { probe }); }
  finally { await writeFile('artifacts/p0-workflow/room-recovery.json', JSON.stringify({ at: new Date().toISOString(), target, results, traces }, null, 2)); }
  console.log(JSON.stringify(results));
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
