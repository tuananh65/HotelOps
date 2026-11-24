import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';
import { cases } from './p0-runtime-cases.mjs';

export const database = 'HotelOpsCompactWorkdayTestP0261124';
const forbiddenPorts = new Set(['5080', '5769', '5186', '5173']);

export function validateTarget(value) {
  const url = new URL(value);
  assert.ok(['http:', 'https:'].includes(url.protocol), 'HTTP(S) required');
  assert.ok(['127.0.0.1', 'localhost', '[::1]'].includes(url.hostname), 'Local TEST only');
  assert.ok(url.port && !forbiddenPorts.has(url.port), 'Dedicated TEST port required');
  assert.equal(url.username + url.password + url.search + url.hash, '', 'Bare TEST origin required');
  assert.equal(url.pathname, '/', 'Bare TEST origin required');
  return url;
}

export function reconcileSupply(rows) {
  assert.ok(Array.isArray(rows) && rows.length, 'Supply evidence must not be empty');
  for (const row of rows) {
    for (const key of ['initialIssued', 'additionalIssued', 'reserved', 'used', 'returned', 'remaining', 'available'])
      assert.ok(Number.isFinite(row[key]) && row[key] >= 0, `Negative/invalid ${key} on balance ${row.balanceId}`);
    const close = (a, b) => assert.ok(Math.abs(a - b) < 0.00001, `Supply mismatch ${row.balanceId}: ${a} != ${b}`);
    close(row.remaining, row.initialIssued + row.additionalIssued - row.used - row.returned);
    close(row.available, row.remaining - row.reserved);
    if (row.closed) { close(row.remaining, 0); close(row.reserved, 0); }
  }
}

export function reconcileInventory(rows) {
  assert.ok(Array.isArray(rows) && rows.length, 'Inventory evidence must not be empty');
  const branches = new Set();
  for (const row of rows) {
    branches.add(row.branchId);
    for (const key of ['opening', 'receipts', 'issues', 'returns', 'closing'])
      assert.ok(Number.isFinite(row[key]) && row[key] >= 0, `Invalid ${key} for item ${row.itemId}`);
    assert.ok(Math.abs(row.opening + row.receipts + row.returns - row.issues - row.closing) < 0.00001,
      `Unexplained stock difference for branch ${row.branchId}, item ${row.itemId}`);
  }
  assert.ok(branches.size >= 2, 'E2E-28 requires evidence from both branches');
}

export async function run(fixturePath, adapterPath, outputPath) {
  assert.ok(fixturePath && adapterPath, 'P0_RUNTIME_FIXTURE and P0_RUNTIME_ADAPTER required');
  const fixture = JSON.parse(await readFile(resolve(fixturePath), 'utf8'));
  assert.equal(fixture.database, database);
  const endpoint = validateTarget(fixture.baseUrl);
  const adapter = typeof adapterPath === 'string'
    ? await import(pathToFileURL(resolve(adapterPath)).href) : adapterPath;
  assert.equal(typeof adapter.verifyTarget, 'function', 'Adapter must verify live process/database identity, not echo fixture metadata');
  // Parent-supplied adapter probes the running TEST process and its actual SQL connection.
  // No mutation is attempted before that independent verification succeeds.
  const target = await adapter.verifyTarget(fixture);
  assert.equal(target.database, database);
  assert.equal(new URL(target.baseUrl).origin, endpoint.origin);
  assert.ok(Number.isInteger(target.processId) && target.processId > 0, 'Running TEST process required');
  assert.ok(target.verifiedAt && Math.abs(Date.now() - Date.parse(target.verifiedAt)) < 60_000, 'Fresh target probe required');
  const results = [], traces = [], secrets = Object.values(fixture.actors ?? {}).map(a => a.token).filter(Boolean);
  const scrub = text => secrets.reduce((value, secret) => value.replaceAll(secret, '[REDACTED]'), String(text));
  let active = null, checks = 0;
  async function request(actor, method, route, body) {
    assert.match(route, /^\/api\/v2\//, 'Only Compact API routes are allowed');
    const url = new URL(route, endpoint);
    assert.equal(url.origin, endpoint.origin);
    assert.ok(fixture.actors?.[actor]?.token, `Missing fixture actor ${actor}`);
    const started = Date.now();
    const response = await fetch(url, {
      method, redirect: 'error', signal: AbortSignal.timeout(120_000),
      headers: { Authorization: `Bearer ${fixture.actors[actor].token}`, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    traces.push({ case: active, actor, method, path: url.pathname, status: response.status, elapsedMs: Date.now() - started });
    const text = await response.text();
    let value = null;
    if (text) { try { value = JSON.parse(text); } catch { throw new Error(`Non-JSON ${method} ${url.pathname}: HTTP ${response.status}`); } }
    if(!response.ok && value?.title && !value.code)value.code=value.title;
    return { status: response.status, body: value };
  }
  function check(condition, message) { checks++; assert.ok(condition, message); }
  function equal(actual, expected, message) { checks++; assert.deepEqual(actual, expected, message); }
  function ok(response, status = 200) {
    equal(response.status, status, `Expected HTTP ${status}; actual ${response.status}; code ${response.body?.code ?? response.body?.error?.code ?? ''}`);
    return response.body;
  }
  function denied(response, statuses = [403, 404]) {
    check(statuses.includes(response.status), `Authorization requires ${statuses.join('/')}; actual ${response.status}`);
  }
  async function race(operations, expectedStatuses) {
    assert.ok(operations.length >= 2);
    const result = await Promise.all(operations.map(operation => operation()));
    equal(result.map(r => r.status).sort(), [...expectedStatuses].sort(), 'Concurrent winners/losers differ');
    return result;
  }
  const context = { fixture, request, check, equal, ok, denied, race, uuid: randomUUID,
    clients: Object.fromEntries(Object.keys(fixture.actors ?? {}).map(actor => [actor, (method, route, body) => request(actor, method, route, body)])),
    reconcileSupply: rows => { reconcileSupply(rows); checks++; },
    reconcileInventory: rows => { reconcileInventory(rows); checks++; } };
  const save = async () => {
    const report = { at: new Date().toISOString(), timezone: 'Asia/Saigon', database,
      baseUrl: endpoint.origin, processId: target.processId,
      verdict: results.length === cases.length && results.every(r => r.status === 'VERIFIED') ? 'VERIFIED' : 'PARTIAL', results, traces };
    await mkdir(dirname(resolve(outputPath)), { recursive: true });
    await writeFile(resolve(outputPath), JSON.stringify(report, null, 2));
  };
  for (const item of cases) {
    if(process.env.P0_RUNTIME_CASES && !process.env.P0_RUNTIME_CASES.split(',').includes(item.id))continue;
    active = item.id; checks = 0;
    const test = adapter.scenarios?.[item.id];
    if (!test) { results.push({ ...item, status: 'NOT_TESTED', reason: 'No executable fixture scenario supplied' }); continue; }
    const started = Date.now(), traceStart = traces.length;
    try {
      const evidence = await test(context);
      assert.ok(checks > 0, 'Scenario did not assert any business invariant');
      assert.ok(traces.length > traceStart || evidence?.liveDatabaseProbe === true, 'No real API/database evidence');
      results.push({ ...item, status: 'VERIFIED', assertions: checks, elapsedMs: Date.now() - started });
    } catch (error) {
      results.push({ ...item, status: 'FAILED', assertions: checks, elapsedMs: Date.now() - started, error: scrub(error.message) });
    }
    await save();
  }
  await save();
  console.log(JSON.stringify({ verified: results.filter(r => r.status === 'VERIFIED').length,
    failed: results.filter(r => r.status === 'FAILED').length, notTested: results.filter(r => r.status === 'NOT_TESTED').length }));
  return results.every(r => r.status === 'VERIFIED') ? 0 : 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    process.exitCode = await run(process.env.P0_RUNTIME_FIXTURE, process.env.P0_RUNTIME_ADAPTER,
      process.env.P0_RUNTIME_RESULT ?? 'artifacts/p0-workflow/runtime-results.json');
  } catch (error) {
    console.error(`P0 runtime preflight failed: ${error.message}`);
    process.exitCode = 1;
  }
}
