// Explicit manual Tester launcher. Credentials/cookies stay in process memory; no storageState/HAR/trace.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const repository = path.resolve(process.argv[2] || '.');
const account = process.argv[3] || 'admin';
if (!['admin', 'owner', 'other', 'denied'].includes(account)) throw Error('Unknown synthetic account');
const directory = path.join(repository, 'scripts/verification/FS011Tester');
const fixture = JSON.parse(fs.readFileSync(path.join(directory, '.private/fixture.json'), 'utf8').replace(/^\uFEFF/, ''));
if (fixture.owner !== 'DEV1_FS011Tester' || fixture.database !== 'HotelOps_Canonical_20261106_f11tester_Fresh') throw Error('Tester fixture mismatch');
const helper = path.join(directory, 'bin/Debug/net10.0/FS011Tester.Verification.dll');
const ipc = spawnSync('dotnet', [helper, 'credentials-ipc', '--private-ipc'], { env: { ...process.env, FS011Tester_LIVE_ENABLED: 'APPROVED_FS011Tester_LOCAL_TRIAL' }, windowsHide: true, timeout: 15000 });
if (ipc.status !== 0) throw Error('Private local credential IPC failed');
const credentials = JSON.parse(ipc.stdout.toString('utf8'));
ipc.stdout.fill(0);
const { chromium } = require(path.join(repository, 'frontend/node_modules/playwright'));
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: process.env.FS011_TEST_LAUNCHER_SMOKE === '1' });
  try {
    const context = await browser.newContext({ baseURL: 'https://localhost:5173', ignoreHTTPSErrors: false });
    const response = await context.request.get('/api/auth/csrf');
    if (response.status() !== 200) throw Error('Owned Tester HTTPS environment not ready');
    const csrf = await response.json();
    const login = await context.request.post('/api/auth/login', { headers: { 'X-CSRF-TOKEN': csrf.csrfToken }, data: { username: credentials[account].username, password: credentials[account].password } });
    if (login.status() !== 200) throw Error('Synthetic Tester login failed');
    const page = await context.newPage();
    await page.goto('/cleaning/tasks?branchId=' + fixture.branchId);
    console.log('TESTER_AUTHENTICATED_BROWSER_READY account=' + account + '; no credentials/cookies exported');
    if (process.env.FS011_TEST_LAUNCHER_SMOKE !== '1') await new Promise(resolve => browser.on('disconnected', resolve));
  } finally { await browser.close(); }
})().catch(() => { console.error('TESTER_LOGIN_FAILED: inspect local environment; private provider payload redacted'); process.exitCode = 1; });
