// OWNER STEERING 25/11 14:11: Username ONLY for login/recovery. EmployeeCode aliases
// explicitly rejected by Owner. See plans/reports/login-username-owner-steering.md.
// Keep parent UI labels "Tên đăng nhập" and recovery button "Gửi liên kết đặt lại".
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import net from 'node:net';
import { randomBytes, randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';
import {verifyInvitationBrowser} from './v2-invitation-browser.mjs';
import {invitationAdminBrowser} from './v2-invitation-admin-browser.mjs';
import {recoveryBrowser} from './v2-invitation-recovery-browser.mjs';
import {verifyRoleRevocation} from './v2-role-revocation-browser.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const fixtureScript = path.join(root, 'scripts/verification/v2-invitation-fixture.ps1');
const base = 'http://127.0.0.1:5778/api/v2';
const results = [], messages = [], sockets = new Set();
const runId=randomUUID();
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
let failNext = false, fixture, started = false, cleanup;
const run = (action, extra = []) => new Promise((resolve, reject) => {
  const child = spawn('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', fixtureScript, '-Action', action, ...extra], { cwd: root, windowsHide: true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId} });
  let stdout = '', stderr = '';
  child.stdout.on('data', data => { stdout += data; }); child.stderr.on('data', data => { stderr += data; });
  child.on('error', reject);
  child.on('exit', code => code === 0 ? resolve(JSON.parse(stdout.trim().replace(/^\uFEFF/, ''))) : reject(new Error(`Fixture ${action}: ${stderr.slice(0, 1200)}`)));
});
async function request(route, { method = 'GET', body, token = fixture?.token } = {}) {
  const response = await fetch(base + route, { method, headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await response.text(); let data;
  try { data = JSON.parse(text); } catch { data = {}; }
  return { status: response.status, data, text };
}
async function until(predicate, description, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (!(await predicate())) { if (Date.now() > deadline) throw new Error(`Timeout: ${description}`); await sleep(500); }
}
async function test(name, action) {
  const time = Date.now();
  try { await action(); results.push({ name, status: 'PASS', milliseconds: Date.now() - time }); console.log(`PASS ${name}`); }
  catch (error) { results.push({ name, status: 'FAIL', error: error.message.replace(/[a-f\d]{64}/gi, '[redacted]') }); throw error; }
}
function noSecrets(data) {
  const keys = [];
  const visit = value => { if (!value || typeof value !== 'object') return; for (const [key, item] of Object.entries(value)) { keys.push(key.toLowerCase()); visit(item); } };
  visit(data); assert.ok(!keys.some(key => ['token', 'tokenhash', 'protectedtoken', 'passwordhash', 'passwordfingerprint', 'securitystamp', 'password'].includes(key)), 'Response exposes secret fields');
}
const smtp = net.createServer(socket => {
  sockets.add(socket); socket.on('close', () => sockets.delete(socket)); socket.on('error', () => {});
  let buffer = '', dataMode = false, content = [], recipient = '';
  socket.write('220 localhost HotelOps isolated test sink\r\n');
  socket.on('data', chunk => {
    buffer += chunk.toString('utf8');
    while (buffer.includes('\r\n')) {
      const end = buffer.indexOf('\r\n'), line = buffer.slice(0, end); buffer = buffer.slice(end + 2);
      if (dataMode) {
        if (line !== '.') { content.push(line); continue; }
        dataMode = false;
        if (failNext) { failNext = false; socket.write('451 4.3.0 Simulated local retryable failure\r\n'); continue; }
        const mail = content.join('\r\n'), split = mail.indexOf('\r\n\r\n');
        const headers = mail.slice(0, split), rawBody = mail.slice(split + 4);
        const body = /Content-Transfer-Encoding:\s*base64/i.test(headers) ? Buffer.from(rawBody.replace(/\s/g, ''), 'base64').toString('utf8') : rawBody.replace(/=\r\n/g, '').replace(/=([0-9a-f]{2})/gi, (_, hex) => String.fromCharCode(parseInt(hex, 16)));
        const token = body.match(/setup-password\?token=([a-f\d]{64})/i)?.[1];
        messages.push({ recipient, token, hasUsername: /Tên đăng nhập/.test(body), hasFragmentLink: body.includes('/#setup-password?token='), hasPassword: body.includes('V2Temp!'), messageId: headers.match(/Message-ID:([^\r\n]+)/i)?.[1]?.trim() });
        socket.write('250 2.0.0 Captured locally\r\n'); continue;
      }
      if (/^(EHLO|HELO) /i.test(line)) socket.write('250-localhost\r\n250 SIZE 1048576\r\n');
      else if (/^MAIL FROM:/i.test(line)) socket.write('250 OK\r\n');
      else if (/^RCPT TO:/i.test(line)) { recipient = line.match(/<([^>]+)>/)?.[1] ?? ''; socket.write('250 OK\r\n'); }
      else if (/^DATA$/i.test(line)) { dataMode = true; content = []; socket.write('354 End with dot\r\n'); }
      else if (/^QUIT$/i.test(line)) { socket.end('221 Bye\r\n'); }
      else socket.write('250 OK\r\n');
    }
  });
});
const password = () => `V2Temp!${randomBytes(12).toString('hex')}aA9`;
async function create(suffix, browser = false) {
  const username = `${fixture.prefix}_${suffix}`, initialPassword = password(), email = `${username.toLowerCase()}@example.invalid`;
  const fullName = `V2 Invitation Test ${suffix}`;
  const response = browser ? {status:200,data:await invitationAdminBrowser(fixture,{username,fullName,email,initialPassword},'create',path.join(root,'artifacts/restructure-v2'))} : await request('/users', { method: 'POST', body: { username, fullName, password: initialPassword, branchId: fixture.branch, email } });
  assert.equal(response.status, 200, `Create response: ${response.data.title}`); noSecrets(response.data);
  return { id: response.data.fields.userId, username, email, initialPassword, employeeCode:response.data.fields.employeeCode };
}
async function status(user) {
  const response = await request(`/invitations/users/${user.id}`); assert.equal(response.status, 200); noSecrets(response.data); return response.data;
}
async function resend(user) {
  const detail = await request(`/users/${user.id}`); assert.equal(detail.status, 200);
  return request(`/invitations/users/${user.id}/resend`, { method: 'POST', body: { userRowVersion: detail.data.user.fields.rowVersion } });
}
async function setup(token, newPassword) { return request('/invitations/setup-password', { method: 'POST', token: null, body: { token, newPassword } }); }
async function restart(enabled) { if (started) { await run('Stop'); started = false; } started = true; await run('Start', enabled ? ['-EmailEnabled'] : []); }
async function received(user, count = 1) {
  await until(() => messages.filter(message => message.recipient === user.email).length >= count, 'local SMTP message', 45000);
  await until(async () => (await status(user)).items[0]?.deliveryStatus === 'SENT', 'delivery marked SENT');
  const message = messages.filter(message => message.recipient === user.email).at(-1); assert.ok(message.token, 'Setup token not found in captured local message'); return message;
}
try {
  await new Promise((resolve, reject) => { smtp.once('error', reject); smtp.listen(2526, '127.0.0.1', resolve); });
  try { await run('Setup'); } finally {
    try { const candidate=JSON.parse((await readFile(path.join(root, 'artifacts/restructure-v2/invitation-private/fixture.json'), 'utf8')).replace(/^\uFEFF/, ''));if(candidate.runId===runId)fixture=candidate; } catch {}
  }
  assert.ok(fixture,'Owned TEST fixture required');
  await restart(false);
  let a;
  await test('account creation atomically queues invitation without exposing credentials', async () => {
    a = await create('A', true); const state = await status(a); assert.equal(state.items.length, 1); assert.equal(state.items[0].deliveryStatus, 'PENDING'); assert.equal(state.items[0].attempts, 0);
  });
  await test('missing email configuration is explicitly visible and does not attempt delivery', async () => {
    const state = await status(a); assert.equal(state.configurationError, 'BLOCKED_EXTERNAL_CONFIGURATION'); await sleep(1000); assert.equal(messages.length, 0);
  });
  let temporarySession;
  await test('first login requires password change and backend blocks business API', async () => {
    const login = await request('/auth/login', { method: 'POST', token: null, body: { username: a.username, password: a.initialPassword } }); assert.equal(login.status, 200); assert.equal(login.data.requiresPasswordChange, true); temporarySession = login.data.accessToken;
    const denied = await request('/users/creation-options', { token: temporarySession }); assert.equal(denied.status, 403); assert.equal(denied.data.title, 'PASSWORD_CHANGE_REQUIRED');
    assert.equal((await request('/auth/me', { token: temporarySession })).status, 200);
  });
  await restart(true);
  let aMessage;
  await test('local SMTP delivers single-use setup link and safe delivery status', async () => {
    aMessage = await received(a); assert.ok(aMessage.hasUsername && aMessage.hasFragmentLink && !aMessage.hasPassword); assert.ok(aMessage.messageId?.startsWith('<hotelops-invitation-')); assert.equal((await status(a)).configurationError, null);
  });
  await test('setup rejects unchanged temporary password', async () => {
    const response = await setup(aMessage.token, a.initialPassword); assert.equal(response.status, 400); assert.equal(response.data.title, 'PASSWORD_MUST_CHANGE');
  });
  await test('setup enforces password policy', async () => {
    const response = await setup(aMessage.token, 'password1234'); assert.equal(response.status, 400); assert.equal(response.data.title, 'PASSWORD_POLICY_NOT_MET');
  });
  const newPassword = password();
  await test('browser first login and setup activate account and invalidate pre-setup sessions', async () => {
    await verifyInvitationBrowser(a,aMessage.token,newPassword,path.join(root,'artifacts/restructure-v2'));
    assert.equal((await request('/auth/me', { token: temporarySession })).status, 401);
    const login = await request('/auth/login', { method: 'POST', token: null, body: { username: a.username, password: newPassword } }); assert.equal(login.status, 200); assert.equal(login.data.requiresPasswordChange, false);
  });
  await test('used invitation cannot set password a second time', async () => {
    const response = await setup(aMessage.token, password()); assert.equal(response.status, 400); assert.equal(response.data.title, 'INVITATION_INVALID_OR_EXPIRED');
  });
  await test('activated account cannot be re-invited', async () => { const response = await resend(a); assert.equal(response.status, 409); assert.equal(response.data.title, 'ACCOUNT_ALREADY_ACTIVATED'); });
  await test('username login succeeds and employee code is not a login alias',async()=>{
    a.employeeCode=fixture.prefix+'_CODE';await run('Identity',['-UserId',String(a.id),'-Identifier',a.employeeCode]);
    const valid=await request('/auth/login',{method:'POST',token:null,body:{username:a.username,password:newPassword}});
    assert.equal(valid.status,200);assert.equal(valid.data.user.fields.userId,a.id);
    assert.equal((await request('/auth/login',{method:'POST',token:null,body:{username:a.employeeCode,password:newPassword}})).status,401);
  });
  const recoveredPassword=password();
  // The preceding negative setup cases deliberately exhaust the shared account-reset
  // window on fast machines. Isolate the recovery scenario with a fresh owned host;
  // retain persisted account/session state and leave production rate limits intact.
  await restart(true);
  await test('mobile password toggle and forgot via username complete through local SMTP setup and login',async()=>{
    const prior=await request('/auth/login',{method:'POST',token:null,body:{username:a.username,password:newPassword}});
    assert.equal(prior.status,200);
    await run('RecoveryReady',['-UserId',String(a.id)]);
    await recoveryBrowser(a,recoveredPassword,()=>received(a,2),path.join(root,'artifacts/restructure-v2'));
    assert.equal((await request('/auth/me',{token:prior.data.accessToken})).status,401,'Recovery must revoke previously active sessions');
    assert.equal((await request('/auth/login',{method:'POST',token:null,body:{username:a.username,password:newPassword}})).status,401);
  });
  await test('unknown recovery is generic and repeated recovery does not duplicate outbox',async()=>{
    const before=await status(a);
    const known=await request('/invitations/forgot-password',{method:'POST',token:null,body:{username:a.username}});
    const unknown=await request('/invitations/forgot-password',{method:'POST',token:null,body:{username:fixture.prefix+'_NONE'}});
    assert.equal(known.status,200);assert.equal(unknown.status,200);assert.deepEqual(known.data,unknown.data);
    assert.equal((await status(a)).items.length,before.items.length);
  });
  await restart(true);
  let b, bMessage;
  await test('SMTP failure records redacted error and schedules bounded retry', async () => {
    failNext = true; b = await create('B');
    await until(async () => (await status(b)).items[0]?.deliveryStatus === 'FAILED', 'SMTP failure status', 45000);
    const state = (await status(b)).items[0]; assert.equal(state.lastError, 'EMAIL_DELIVERY_FAILED'); assert.equal(state.attempts, 1); assert.equal(state.sentAt, null);
    assert.equal(messages.filter(message => message.recipient === b.email).length, 0);
  });
  await test('retry succeeds through local sink without duplicating invitation', async () => {
    await run('RetryNow', ['-UserId', String(b.id)]); bMessage = await received(b); const state = await status(b); assert.equal(state.items.length, 1); assert.equal(state.items[0].attempts, 2); assert.equal(state.items[0].lastError, null);
  });
  let secondMessage;
  await test('resend revokes old token and does not create duplicate account', async () => {
    const before = await run('Inspect'); noSecrets(await invitationAdminBrowser(fixture,b,'resend',path.join(root,'artifacts/restructure-v2'))); secondMessage = await received(b, 2);
    const state = await status(b); assert.equal(state.items.length, 2); assert.ok(state.items[1].revokedAt); assert.ok(secondMessage.token !== bMessage.token);
    const after = await run('Inspect'); assert.equal(after.length, before.length);
    const response = await setup(bMessage.token, password()); assert.equal(response.status, 400); assert.equal(response.data.title, 'INVITATION_INVALID_OR_EXPIRED');
  });
  await test('expired token is rejected', async () => {
    await run('Expire', ['-UserId', String(b.id)]); const response = await setup(secondMessage.token, password()); assert.equal(response.status, 400); assert.equal(response.data.title, 'INVITATION_INVALID_OR_EXPIRED');
  });
  const bPassword=password();
  await test('fresh resend recovers expired invitation and setup succeeds', async () => {
    assert.equal((await resend(b)).status, 200); const fresh = await received(b, 3); assert.equal((await setup(fresh.token, bPassword)).status, 200);
  });
  await restart(true);
  await test('employee code cannot shadow another username or request recovery',async()=>{
    await run('Identity',['-UserId',String(a.id),'-Identifier',b.username]);
    await run('RecoveryReady',['-UserId',String(a.id)]);await run('RecoveryReady',['-UserId',String(b.id)]);
    const beforeA=(await status(a)).items.length,beforeB=(await status(b)).items.length;
    const login=await request('/auth/login',{method:'POST',token:null,body:{username:b.username,password:recoveredPassword}});
    assert.equal(login.status,401);assert.equal(login.data.title,'INVALID_CREDENTIALS');
    const valid=await request('/auth/login',{method:'POST',token:null,body:{username:b.username,password:bPassword}});
    assert.equal(valid.status,200);assert.equal(valid.data.user.fields.userId,b.id);
    const recovery=await request('/invitations/forgot-password',{method:'POST',token:null,body:{username:b.username}});
    assert.equal(recovery.status,200);
    assert.equal((await status(a)).items.length,beforeA);
    assert.equal((await status(b)).items.length,beforeB+1,'Username owner receives recovery despite another account employee code');
    const recovered=await received(b,4);
    assert.equal((await setup(recovered.token,password())).status,200);
    await run('Identity',['-UserId',String(a.id),'-Identifier',a.employeeCode]);
    const forgot=await request('/invitations/forgot-password',{method:'POST',token:null,body:{username:a.employeeCode}});
    assert.equal(forgot.status,200);assert.equal((await status(a)).items.length,beforeA);assert.equal((await status(b)).items.length,beforeB+1);
  });
  if(process.env.HOTELOPS_ROLE_REVOCATION_PROBE==='1'){
    await test('admin role grants and revocations invalidate two independent browser sessions',async()=>{
      await verifyRoleRevocation({fixture,user:a,password:recoveredPassword,request,directory:path.join(root,'artifacts/restructure-v2')});
    });
  }
  await test('no further SMTP delivery occurs after completed invitations', async () => { const count = messages.length; await sleep(11000); assert.equal(messages.length, count); });
} catch (error) {
  if (!results.some(result => result.status === 'FAIL')) results.push({ name: 'setup or harness', status: 'FAIL', error: error.message.replace(/[a-f\d]{64}/gi, '[redacted]') });
  process.exitCode = 1;
} finally {
  if (started) { try { await run('Stop'); } catch (error) { results.push({ name: 'owned API shutdown', status: 'FAIL', error: error.message }); process.exitCode = 1; } }
  if (fixture) { try { cleanup = await run('Cleanup'); } catch (error) { results.push({ name: 'cleanup', status: 'FAIL', error: error.message }); process.exitCode = 1; } }
  for (const socket of sockets) socket.destroy(); await new Promise(resolve => smtp.close(resolve));
  const report = { database: 'HotelOpsCompactWorkdayTestV22611250010', at: new Date().toISOString(), smtp: 'local 127.0.0.1:2526; no external mail', api: '127.0.0.1:5778', testNotes: ['Only synthetic account passwords changed.', 'Retry due time advanced on fixture invitation after checking recorded failure; normal backoff duration not waited.', 'Expired token fixture timestamps changed only on synthetic invitation.', 'Inactive fixture accounts/audit retained for traceability; admin fixture session removed.'], capturedMessages: messages.length, cleanup, tests: results };
  await mkdir(path.join(root, 'artifacts/restructure-v2'), { recursive: true });
  await writeFile(path.join(root, 'artifacts/restructure-v2/invitation-runtime-results.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: results.filter(r => r.status === 'PASS').length, failed: results.filter(r => r.status === 'FAIL').length, cleanup, results }, null, 2));
}
