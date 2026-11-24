import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

// Parent prepares this isolated clone and session fixture. Never seed or alter production balances.
const fixture = JSON.parse(fs.readFileSync(process.env.P0_TRANSFER_FIXTURE, 'utf8'));
assert.equal(fixture.database, 'HotelOpsCompactWorkdayTestP0261124');
const endpoint = new URL(fixture.baseUrl);
assert(['127.0.0.1', 'localhost'].includes(endpoint.hostname));
assert(!['5080', '5769', '5186'].includes(endpoint.port), 'Production and development endpoints are forbidden');
const { source: s, destination: d, itemId, unitId, outsiderToken } = fixture;
assert(s.branchId !== d.branchId && s.warehouseId !== d.warehouseId);
for (const token of [s.omToken, s.storeToken, d.omToken, d.storeToken, outsiderToken]) assert(token);
const checks = [];
const fields = row => row.fields ?? row;
async function request(token, path, method = 'GET', body, expected = 200) {
  const response = await fetch(new URL(`/api/v2/${path}`, endpoint), {
    method, headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const result = await response.json();
  assert.equal(response.status, expected, `${method} ${path}: ${JSON.stringify(result)}`);
  return result;
}
async function detail(token, id) { return request(token, `documents/${id}`); }
async function act(token, id, action, extra = {}, expected = 200) {
  const current = await detail(token, id);
  return request(token, `documents/${id}/actions/${action}`, 'POST', {
    rowVersion: fields(current.document).rowVersion, operationKey: randomUUID(), reason: 'Isolated transfer workflow verification', ...extra,
  }, expected);
}
async function create(token, body) {
  return fields(await request(token, 'documents', 'POST', { operationKey: randomUUID(), ...body }));
}
async function line(token, doc, quantity, extras = {}) {
  const current = await detail(token, doc.documentId);
  return request(token, `documents/${doc.documentId}/lines`, 'POST', {
    rowVersion: fields(current.document).rowVersion, itemId, unitId, quantity, ...extras,
  });
}
async function catalog() { return request(d.omToken, `documents/transfer-catalog?branchId=${d.branchId}`); }
async function balance(warehouse) {
  const values = await catalog();
  return values.balances.find(x => x.warehouseId === warehouse && x.itemId === itemId)?.availableBaseQty ?? 0;
}
async function newRequest(quantity) {
  const doc = await create(d.omToken, { kind: 'TRANSFER_REQUEST', branchId: s.branchId, destinationBranchId: d.branchId,
    sourceWarehouseId: s.warehouseId, destinationWarehouseId: d.warehouseId, reason: 'Isolated transfer request from destination OM' });
  await line(d.omToken, doc, quantity);
  await act(d.omToken, doc.documentId, 'submit');
  return doc;
}
async function derive(token, id, kind, quantity, expected = 200) {
  const current = await detail(token, id), original = fields(current.lines[0]);
  return request(token, `documents/${id}/derive`, 'POST', {
    rowVersion: fields(current.document).rowVersion, operationKey: randomUUID(), kind,
    reason: 'Isolated transfer linked stock movement', lines: [{ lineId: original.lineId, quantity }],
  }, expected);
}

// Seed through the same receipt and posting workflow, exclusively in the named test clone.
const seed = await create(s.storeToken, { branchId: s.branchId, kind: 'RECEIPT', destinationWarehouseId: s.warehouseId,
  reason: 'Isolated test transfer inventory seed' });
await line(s.storeToken, seed, 100);
await act(s.storeToken, seed.documentId, 'post');
const beforeSource = await balance(s.warehouseId), beforeDestination = await balance(d.warehouseId);

const transfer = await newRequest(50);
assert.equal(await balance(s.warehouseId), beforeSource);
assert.equal(await balance(d.warehouseId), beforeDestination);
checks.push('Request changes neither warehouse balance');
await request(d.omToken, `documents?branchId=${s.branchId}&kind=RECEIPT`, 'GET', undefined, 403);
await request(d.omToken, `documents/${seed.documentId}`, 'GET', undefined, 403);
await request(outsiderToken, `documents/${transfer.documentId}`, 'GET', undefined, 403);
await act(d.omToken, transfer.documentId, 'approve', {}, 403);
await act(d.storeToken, transfer.documentId, 'approve', {}, 403);
checks.push('Destination OM cannot approve; unrelated ledger, outsider and storekeeper approval denied');
await act(s.omToken, transfer.documentId, 'approve');
assert.equal(await balance(s.warehouseId), beforeSource);
assert.equal(await balance(d.warehouseId), beforeDestination);
checks.push('Approval is independent and does not move stock');
await derive(d.storeToken, transfer.documentId, 'TRANSFER', 50, 403);
await derive(s.omToken, transfer.documentId, 'TRANSFER', 50, 403);
const dispatched = fields(await derive(s.storeToken, transfer.documentId, 'TRANSFER', 50));
await act(d.storeToken, dispatched.documentId, 'post', {}, 403);
await act(s.storeToken, dispatched.documentId, 'post');
assert.equal(await balance(s.warehouseId), beforeSource - 50);
assert.equal(await balance(d.warehouseId), beforeDestination);
await act(d.omToken, transfer.documentId, 'cancel', {}, 409);
await act(s.storeToken, dispatched.documentId, 'post', {}, 409);
checks.push('Dispatch source only, source decremented once; cancellation after dispatch blocked');

await derive(s.storeToken, dispatched.documentId, 'TRANSFER_RECEIPT', 50, 403);
await derive(d.storeToken, dispatched.documentId, 'TRANSFER_RECEIPT', 51, 409);
const receipt = fields(await derive(d.storeToken, dispatched.documentId, 'TRANSFER_RECEIPT', 20));
await act(d.storeToken, receipt.documentId, 'post');
assert.equal(await balance(s.warehouseId), beforeSource - 50);
assert.equal(await balance(d.warehouseId), beforeDestination + 20);
assert.equal(fields((await detail(d.omToken, dispatched.documentId)).document).status, 'PARTIALLY_RECEIVED');
checks.push('Partial actual receipt increases only destination; projected state is PARTIALLY_RECEIVED');
const finalReceipt = fields(await derive(d.storeToken, dispatched.documentId, 'TRANSFER_RECEIPT', 30));
const current = await detail(d.storeToken, finalReceipt.documentId);
const payload = { rowVersion: fields(current.document).rowVersion, operationKey: randomUUID(), reason: 'Concurrent receipt posting verification' };
const responses = await Promise.all([1, 2].map(() => fetch(new URL(`/api/v2/documents/${finalReceipt.documentId}/actions/post`, endpoint), {
  method: 'POST', headers: { Authorization: `Bearer ${d.storeToken}`, 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
})));
assert.deepEqual(responses.map(x => x.status).sort(), [200, 409]);
assert.equal(await balance(s.warehouseId), beforeSource - 50);
assert.equal(await balance(d.warehouseId), beforeDestination + 50);
assert.equal(fields((await detail(s.omToken, dispatched.documentId)).document).status, 'RECEIVED');
assert.equal((await detail(d.omToken, transfer.documentId)).transfer.workflowStatus, 'COMPLETED');
checks.push('Concurrent receipt is posted once; totals conserved; both OMs see completion');

const rejected = await newRequest(1);
await act(s.omToken, rejected.documentId, 'reject');
await derive(s.storeToken, rejected.documentId, 'TRANSFER', 1, 403);
const cancelled = await newRequest(1);
await act(s.omToken, cancelled.documentId, 'approve');
const prepared = fields(await derive(s.storeToken, cancelled.documentId, 'TRANSFER', 1));
await act(d.omToken, cancelled.documentId, 'cancel');
await act(s.storeToken, prepared.documentId, 'post', {}, 409);
checks.push('Rejected request cannot dispatch; approved cancellation invalidates prepared drafts');
const insufficient = await newRequest((await balance(s.warehouseId)) + 1);
await act(s.omToken, insufficient.documentId, 'approve', {}, 409);
checks.push('Insufficient stock rejects approval');

// Intrabranch regression uses an explicit optional second source warehouse from the isolated fixture.
if (s.secondWarehouseId) {
  const local = await create(s.storeToken, { kind: 'TRANSFER', branchId: s.branchId,
    sourceWarehouseId: s.warehouseId, destinationWarehouseId: s.secondWarehouseId, reason: 'Intrabranch regression transfer' });
  await line(s.storeToken, local, 1);
  await act(s.storeToken, local.documentId, 'post');
  const localReceipt = fields(await derive(s.storeToken, local.documentId, 'TRANSFER_RECEIPT', 1));
  await act(s.storeToken, localReceipt.documentId, 'post');
  assert.equal(fields((await detail(s.storeToken, local.documentId)).document).status, 'RECEIVED');
  checks.push('Existing intrabranch direct transfer and receipt preserved');
}
const out = process.env.P0_TRANSFER_RESULT ?? new URL('../../artifacts/p0-workflow/transfer-api-results.json', import.meta.url).pathname.replace(/^\/(\w:)/, '$1');
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify({ at: new Date().toISOString(), checks, documents: {
  request: transfer.documentId, dispatched: dispatched.documentId, receipt: receipt.documentId, finalReceipt: finalReceipt.documentId,
}}, null, 2));
console.log(`PASS ${checks.length} cross-branch transfer workflow groups`);
