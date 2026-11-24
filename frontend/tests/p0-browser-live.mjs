import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';

// Read-only browser audit. Never sign in again, log credentials, or submit business actions.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const fixture = JSON.parse(fs.readFileSync(path.join(root, 'artifacts/p0-workflow/private/runtime-fixture.json'), 'utf8').replace(/^\uFEFF/, ''));
const base = 'http://127.0.0.1:5174';
assert.equal(fixture.database, 'HotelOpsCompactWorkdayTestP0261124');
assert.equal(new URL(fixture.baseUrl).port, '5774');
const out = path.join(root, 'artifacts/p0-workflow/browser');
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
const results = [];
const aliases = ['admin', 'owner', 'om', 'supervisor', 'hk', 'maintenance', 'reception', 'store'];
try {
  for (const suffix of ['DVBV', 'DVCT']) for (const alias of aliases) {
    const actorKey = `${suffix}.${alias}`, actor = fixture.actors[actorKey];
    assert(actor?.token, `Missing private session for ${actorKey}`);
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
    await context.addInitScript(token => sessionStorage.setItem('hotelops.compact.session', token), actor.token);
    const page = await context.newPage();
    let pageErrors = [], apiErrors = [], blockedWrites = [];
    page.on('pageerror', error => pageErrors.push(error.message.replaceAll(actor.token, '[REDACTED]')));
    page.on('response', response => {
      const url = new URL(response.url());
      if (url.pathname.startsWith('/api/') && response.status() >= 400) apiErrors.push({ path: url.pathname, status: response.status() });
    });
    await context.route('**/api/**', async route => {
      if (!['GET', 'HEAD', 'OPTIONS'].includes(route.request().method())) {
        blockedWrites.push({ method: route.request().method(), path: new URL(route.request().url()).pathname });
        await route.abort('blockedbyclient');
      } else await route.continue();
    });
    try {
      await page.goto(`${base}/login#dashboard`);
      await page.getByRole('button', { name: 'Đăng xuất', exact: true }).waitFor({ timeout: 20000 });
      const profile = await page.evaluate(async () => {
        const response = await fetch('/api/v2/auth/me', { headers: { Authorization: `Bearer ${sessionStorage.getItem('hotelops.compact.session')}` } });
        if (!response.ok) throw new Error(`Profile HTTP ${response.status}`);
        const value = await response.json();
        return { roles: value.roles.map(x => x.code), grants: value.grants.map(x => x.code) };
      });
      assert(profile.roles.includes(actor.roleCode), `Role mismatch ${actorKey}`);
      const nav = await page.locator('nav[aria-label="Điều hướng chính"] button').allTextContents();
      assert(nav.length > 0, 'Navigation empty');
      const routes = ['dashboard'];
      if (profile.grants.includes('IR_VIEW') && alias !== 'owner') routes.push('requests');
      if (profile.grants.some(x => ['ATTENDANCE_RECORD', 'SHIFT_MANAGE'].includes(x))) routes.push('workday');
      if (alias === 'hk') routes.push('staff-supplies', 'cleaning');
      if (alias === 'om') routes.push('supply-config');
      if (alias === 'store') routes.push('supplies', 'stock');
      for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: width === 390 ? 844 : 1000 });
        for (const route of routes) {
          pageErrors = []; apiErrors = []; blockedWrites = [];
          const label = `${actorKey}-${route}-${width}`;
          let failure;
          try {
            await page.evaluate(value => { location.hash = value; }, route);
            await page.waitForTimeout(900);
            await page.waitForLoadState('networkidle', { timeout: 12000 }).catch(() => {});
            await page.waitForFunction(() => !document.querySelector('main .spinner, main [aria-busy="true"]'), undefined, { timeout: 30000 });
            assert(await page.getByRole('button', { name: 'Đăng xuất', exact: true }).count(), 'Session lost');
            assert((await page.locator('main').innerText()).trim().length > 0, 'Main content empty');
            assert.equal(pageErrors.length, 0, `JavaScript errors: ${pageErrors.join('; ')}`);
            assert.equal(apiErrors.length, 0, `API errors: ${JSON.stringify(apiErrors)}`);
            assert.equal(blockedWrites.length, 0, 'Page attempted a business mutation on read');
            const layout = await page.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth,
              horizontalScroll: [...document.querySelectorAll('main *')].filter(element => {
                const style = getComputedStyle(element), box = element.getBoundingClientRect();
                return box.width > 0 && element.clientWidth > 0 && element.scrollWidth > element.clientWidth + 2 && ['auto', 'scroll'].includes(style.overflowX);
              }).slice(0, 8).map(element => ({ tag: element.tagName, class: element.className, width: element.clientWidth, contentWidth: element.scrollWidth })),
              overflowing: [...document.querySelectorAll('main *, .topbar *')].filter(element => {
                const box = element.getBoundingClientRect(), style = getComputedStyle(element);
                return box.width > 0 && style.visibility !== 'hidden' && box.right > innerWidth + 2;
              }).slice(0, 8).map(element => ({ tag: element.tagName, class: element.className, right: Math.round(element.getBoundingClientRect().right) })) }));
            assert(layout.document <= layout.viewport + 2, `Horizontal overflow ${JSON.stringify(layout)}`);
            if (width === 390) assert.equal(layout.horizontalScroll.length, 0, `Horizontal scroll containers ${JSON.stringify(layout.horizontalScroll)}`);
          } catch (error) { failure = error.message.replaceAll(actor.token, '[REDACTED]'); }
          if (failure || route === 'dashboard') await page.screenshot({ path: path.join(out, `${label}.png`), fullPage: true });
          results.push({ actor: actorKey, role: actor.roleCode, route, width, pass: !failure, error: failure, apiErrors: [...apiErrors], pageErrors: [...pageErrors], navigation: nav.map(x => x.trim()) });
          console.log(`${failure ? 'FAIL' : 'PASS'} ${label}${failure ? `: ${failure}` : ''}`);
        }
      }
    } catch (error) {
      results.push({ actor: actorKey, pass: false, error: error.message.replaceAll(actor.token, '[REDACTED]') });
      console.log(`FAIL ${actorKey}: authentication/navigation setup`);
    } finally { await context.close(); }
    fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify({ base, database: fixture.database, recordedAt: new Date().toISOString(), readOnly: true, results }, null, 2));
  }
} finally { await browser.close(); }
const failures = results.filter(x => !x.pass);
console.log(`Browser audit: ${results.length - failures.length} passed; ${failures.length} failed.`);
if (failures.length) process.exitCode = 1;
