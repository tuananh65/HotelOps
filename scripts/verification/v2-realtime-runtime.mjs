import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(path.join(root, 'frontend/package.json'));
const { HubConnectionBuilder, HttpTransportType, LogLevel, HubConnectionState } = require('@microsoft/signalr');
const url = 'http://127.0.0.1:5777/api/v2/realtime/notifications';
const fixtureScript = path.join(root, 'scripts/verification/v2-realtime-fixture.ps1');
const results = [];
const runId=randomUUID();
const clients = [];
const recoveryOnly = process.env.HOTELOPS_REALTIME_RECOVERY_ONLY === '1';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const run = (action, target = 'A') => new Promise((resolve, reject) => {
  const child = spawn('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', fixtureScript, '-Action', action, '-Target', target], { cwd: root, windowsHide: true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId} });
  let stdout = '', stderr = '';
  child.stdout.on('data', data => { stdout += data; });
  child.stderr.on('data', data => { stderr += data; });
  child.on('error', reject);
  child.on('exit', code => code === 0 ? resolve(JSON.parse(stdout.trim().replace(/^\uFEFF/, ''))) : reject(new Error(`Fixture ${action} failed: ${stderr.slice(0, 1500)}`)));
});
async function until(predicate, description, timeout = 15000) {
  const deadline = Date.now() + timeout;
  while (!predicate()) { if (Date.now() > deadline) throw new Error(`Timeout: ${description}`); await sleep(100); }
}
async function test(name, action) {
  if (recoveryOnly && !/^(two connections|offline persisted|logout API|server restart|revoked session)/.test(name)) return;
  const start = Date.now();
  try { await action(); results.push({ name, status: 'PASS', milliseconds: Date.now() - start }); console.log(`PASS: ${name}`); }
  catch (error) { results.push({ name, status: 'FAIL', error: error.message }); throw error; }
}
async function stop(client) { client.stopped = true; clearTimeout(client.retry); await client.connection.stop(); }
async function connect(actor, branch, recoverAfterClose = false) {
  const client = { frames: [], errors: [], reconnects: 0, restarts: 0, transitions: [], stopped: false };
  client.connection = new HubConnectionBuilder().withUrl(url, { accessTokenFactory: () => actor.token, transport: HttpTransportType.LongPolling })
    .withAutomaticReconnect([0, 2000, 10000, 30000]).configureLogging(LogLevel.None).build();
  const subscribe = () => { client.subscription = client.connection.stream('Subscribe', branch).subscribe({
    next: value => client.frames.push(value), error: error => {
      client.errors.push(error.message);
      // Match notification-realtime.tsx: stream errors stop transport; onclose schedules recovery.
      if (recoverAfterClose && !client.stopped) void client.connection.stop();
    }, complete: () => {},
  }); };
  client.connection.onreconnected(() => { client.reconnects++; subscribe(); });
  client.connection.onreconnecting(error => client.transitions.push({ state: 'reconnecting', message: error?.message }));
  const scheduleRetry = () => {
    if (client.stopped || client.retry) return;
    client.retry = setTimeout(async () => {
      client.retry = undefined;
      if (client.stopped) return;
      try { await client.connection.start(); if (client.stopped) await client.connection.stop(); else { client.restarts++; subscribe(); } }
      catch { scheduleRetry(); }
    }, 15000);
  };
  client.connection.onclose(error => {
    client.transitions.push({ state: 'closed', message: error?.message });
    if (recoverAfterClose) scheduleRetry();
  });
  clients.push(client);
  await client.connection.start(); subscribe();
  // Existing TEST recipients have operational history; four concurrent snapshots can exceed 15s.
  // Keep a bounded integration budget and expose diagnostics instead of hiding stream failures.
  await until(() => client.frames.length > 0, 'initial SignalR frame', 45000)
    .catch(error => { throw new Error(`${error.message}; state=${client.connection.state}; streamErrors=${JSON.stringify(client.errors)}`); });
  return client;
}
let fixture, started = false;
try {
  await run('Setup');
  fixture = JSON.parse((await readFile(path.join(root, 'artifacts/restructure-v2/realtime-private/fixture.json'), 'utf8')).replace(/^\uFEFF/, ''));
  assert.equal(fixture.database, 'HotelOpsCompactWorkdayTestV22611250010');
  started = true;await run('Start');
  const { A, B } = fixture.actors;
  await test('anonymous negotiation rejected', async () => {
    const response = await fetch(`${url}/negotiate?negotiateVersion=1`, { method: 'POST' }); assert.equal(response.status, 401);
  });
  const a = await connect(A, A.branch, true), b = await connect(B, B.branch), cross = await connect(A, B.branch);
  if (recoveryOnly) { await stop(b); await stop(cross); }
  await test('foreign branch low-stock notification is excluded from scoped stream', async () => {
    // Existing cross-branch transfer notifications can legitimately be visible in both branches.
    const id = (await run('Insert', 'Cross')).id;
    await sleep(2500); assert.ok(!cross.frames.some(frame => frame.notificationIds.includes(id)));
  });
  let aId;
  await test('committed notification reaches only intended recipient and branch', async () => {
    const count = a.frames.at(-1).unreadCount;
    aId = (await run('Insert')).id;
    await until(() => a.frames.at(-1).notificationIds.includes(aId), 'recipient A notification');
    assert.equal(a.frames.at(-1).unreadCount, count + 1);
    await sleep(2500);
    assert.ok(!b.frames.some(frame => frame.notificationIds.includes(aId)));
    assert.ok(!cross.frames.some(frame => frame.notificationIds.includes(aId)));
  });
  await test('foreign branch row addressed to user is still filtered by current scope', async () => {
    const id = (await run('Insert', 'Cross')).id; const all = await connect(A, null);
    await sleep(2500); assert.ok(!all.frames.some(frame => frame.notificationIds.includes(id))); await all.connection.stop();
  });
  await test('unchanged persisted event emits once despite repeated polling', async () => {
    const count = a.frames.length; await sleep(6500); assert.equal(a.frames.length, count);
  });
  await test('two connections receive the same persisted event once per connection', async () => {
    const second = await connect(A, A.branch);
    const id = (await run('Insert')).id;
    await until(() => a.frames.at(-1).notificationIds.includes(id) && second.frames.at(-1).notificationIds.includes(id), 'both connections');
    const before = [a.frames.length, second.frames.length];
    await sleep(4500); assert.deepEqual([a.frames.length, second.frames.length], before);
    await stop(second);
  });
  await test('offline persisted notification is replayed in first snapshot after reconnect', async () => {
    const offline = await connect(A, A.branch); await stop(offline);
    const id = (await run('Insert')).id;
    const resumed = await connect(A, A.branch);
    assert.ok(resumed.frames[0].notificationIds.includes(id)); await stop(resumed);
    // Drain this committed event on the original connection before the next negative case.
    await until(()=>a.frames.at(-1).notificationIds.includes(id),'original connection receives offline-case event');
  });
  await test('second subscription on same connection rejected', async () => {
    let error;
    const duplicate = a.connection.stream('Subscribe', A.branch).subscribe({ next: () => { error = 'unexpected second subscription'; }, error: value => { error = value.message; }, complete: () => {} });
    await until(() => error, 'duplicate subscription rejection');
    assert.match(error, /SUBSCRIPTION_ALREADY_ACTIVE/); duplicate.dispose();
  });
  await test('rolled back notification never reaches SignalR stream', async () => {
    const count = a.frames.length, digest = a.frames.at(-1).eventId;
    const rolledBack=await run('Rollback'); await sleep(3000);
    assert.ok(!a.frames.some(frame=>frame.notificationIds.includes(rolledBack.id)),'Rolled-back notification must never appear');
    assert.equal(a.frames.length, count); assert.equal(a.frames.at(-1).eventId, digest);
  });
  await test('read change updates badge and removes unread event', async () => {
    await run('Read'); await until(() => !a.frames.at(-1).notificationIds.includes(aId), 'read status update');
  });
  await test('role revocation removes previously authorized notifications on existing connection', async () => {
    const id = (await run('Insert')).id; await until(() => a.frames.at(-1).notificationIds.includes(id), 'new role-scoped notification');
    try { await run('RoleRevoke'); await until(() => !a.frames.at(-1).notificationIds.includes(id), 'revoked role removes event'); }
    finally { await run('RoleRestore'); }
    await until(() => a.frames.at(-1).notificationIds.includes(id), 'restored role visibility');
  });
  await test('first-login restriction rejects negotiation and closes existing stream', async () => {
    try {
      await run('Restrict', 'B');
      const response = await fetch(`${url}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Authorization: `Bearer ${B.token}` } });
      assert.equal(response.status, 403); assert.match(await response.text(), /PASSWORD_CHANGE_REQUIRED/);
      const count = b.frames.length; await sleep(3000); assert.equal(b.frames.length, count);
      await until(() => b.connection.state !== HubConnectionState.Connected, 'restricted stream terminated');
    } finally { await run('Unrestrict', 'B'); await b.connection.stop(); }
  });
  await cross.connection.stop();
  await test('logout API revokes session and stops both active connections', async () => {
    const first = await connect(B, B.branch), second = await connect(B, B.branch);
    const response = await fetch('http://127.0.0.1:5777/api/v2/auth/logout', { method: 'POST', headers: { Authorization: `Bearer ${B.token}` } });
    assert.equal(response.status, 200);
    const negotiation = await fetch(`${url}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Authorization: `Bearer ${B.token}` } });
    assert.equal(negotiation.status, 401);
    await until(() => first.connection.state !== HubConnectionState.Connected && second.connection.state !== HubConnectionState.Connected, 'logout closes both connections');
    const before = [first.frames.length, second.frames.length]; await run('Insert', 'B'); await sleep(3000);
    assert.deepEqual([first.frames.length, second.frames.length], before);
  });
  await test('server restart recovers with frontend reconnect and onclose retry policy', async () => {
    const previous = a.frames.length;
    await run('Stop'); started = false;
    const offlineId = (await run('Insert')).id;
    await sleep(1500); await run('Start'); started = true;
    await until(() => a.reconnects + a.restarts > 0 && a.frames.length > previous, 'client recovery', 60000)
      .catch(error => { throw new Error(`${error.message}; state=${a.connection.state}; transitions=${JSON.stringify(a.transitions)}; streamErrors=${JSON.stringify(a.errors)}`); });
    assert.ok(a.frames.at(-1).notificationIds.includes(offlineId));
    const id = (await run('Insert')).id; await until(() => a.frames.at(-1).notificationIds.includes(id), 'post-reconnect event');
  });
  await test('revoked session is rejected and existing connection stops delivering', async () => {
    await run('Revoke');
    const response = await fetch(`${url}/negotiate?negotiateVersion=1`, { method: 'POST', headers: { Authorization: `Bearer ${A.token}` } }); assert.equal(response.status, 401);
    const id = (await run('Insert')).id;
    await until(() => a.connection.state !== HubConnectionState.Connected, 'session-revoked stream disconnected');
    // Revocation may race an in-flight snapshot, yielding an empty badge update before abort.
    // Assert the security boundary: the post-revocation event is never delivered and the stream stops.
    const count = a.frames.length; await sleep(3500); assert.equal(a.frames.length, count);
    assert.ok(!a.frames.some(frame => frame.notificationIds.includes(id)));
  });
} catch (error) {
  if (!results.some(result => result.status === 'FAIL')) results.push({ name: 'setup or harness', status: 'FAIL', error: error.message });
  process.exitCode = 1;
} finally {
  await Promise.allSettled(clients.map(stop));
  try { await run('Cleanup'); } catch (error) { results.push({ name: 'cleanup', status: 'FAIL', error: error.message }); process.exitCode = 1; }
  if (started) { try { await run('Stop'); } catch (error) { results.push({ name: 'owned API shutdown', status: 'FAIL', error: error.message }); process.exitCode = 1; } }
  const report = { database: 'HotelOpsCompactWorkdayTestV22611250010', at: new Date().toISOString(), transport: 'SignalR LongPolling', mode: recoveryOnly ? 'recovery-only' : 'full', tests: results };
  await writeFile(path.join(root, 'artifacts/restructure-v2/realtime-runtime-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: results.filter(r => r.status === 'PASS').length, failed: results.filter(r => r.status === 'FAIL').length, results }, null, 2));
}
