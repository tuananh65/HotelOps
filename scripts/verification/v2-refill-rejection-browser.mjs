import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { writeFile } from 'node:fs/promises';

const require = createRequire(new URL('../../frontend/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');

// The caller prepares a PENDING_STOREKEEPER request and retains its before/after
// SQL snapshot checks. This helper performs the actual rejection through the UI.
export async function verifyRefillRejectionBrowser(fixture, requestId, reason, directory) {
  assert.ok(Number.isSafeInteger(requestId) && requestId > 0);
  assert.ok(reason.trim().length >= 10);
  const actor = fixture.actors.find(value => value.alias === 'store');
  assert.ok(actor?.token);
  const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
  let page;
  const errors = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url());
      if (url.pathname.startsWith('/api/v2/realtime/')) { await route.abort(); return; }
      url.hostname = '127.0.0.1'; url.port = '5779'; url.protocol = 'http:';
      try { await route.fulfill({ response: await route.fetch({ url: url.toString() }) }); }
      catch { await route.abort().catch(() => {}); }
    });
    await context.routeWebSocket('**/*', socket => socket.close());
    await context.addInitScript(token => sessionStorage.setItem('hotelops.compact.session', token), actor.token);
    page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    const loaded = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v2/requests/${requestId}` && response.status() === 200);
    await page.goto(`http://127.0.0.1:5174/login#requests?id=${requestId}`);
    const before = await (await loaded).json();
    assert.equal(before.request.status, 'PENDING_STOREKEEPER');
    await page.getByRole('button', { name: 'Từ chối', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Từ chối', exact: true });
    await expect(dialog).toBeVisible();
    const note = dialog.getByRole('textbox', { name: 'Lý do từ chối', exact: true });
    await expect(note).toHaveAttribute('required', '');
    assert.equal(await note.evaluate(element => element.validity.valueMissing), true);
    await note.fill(reason);
    const committed = page.waitForResponse(response => new URL(response.url()).pathname === `/api/v2/requests/${requestId}/actions/reject` && response.request().method() === 'POST');
    await dialog.getByRole('button', { name: 'Xác nhận', exact: true }).click();
    const response = await committed;
    assert.equal(response.status(), 200);
    const result = await response.json();
    assert.equal(result.request.status, 'REJECTED');
    assert.ok(result.history.some(event => event.action === 'reject' && event.reason === reason));
    await expect(dialog).toHaveCount(0);
    await expect(page.getByText(reason, { exact: true })).toBeVisible();
    await page.screenshot({ path: `${directory}/refill-rejection-desktop.png`, fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
    await page.screenshot({ path: `${directory}/refill-rejection-mobile.png`, fullPage: true });
    assert.deepEqual(errors, []);
    await writeFile(`${directory}/refill-rejection-browser.json`, JSON.stringify({ status: 'PASS', requestId, checks: ['Pending storekeeper request loaded from actual TEST API', 'Rejection requires a reason', 'Storekeeper rejection committed through confirmation popup', 'Exact reason persisted in history and rendered', 'Mobile has no horizontal overflow'], limits: 'Synthetic TEST authenticated session; caller verifies unchanged SQL stock/custody snapshot. No password login or SignalR.' }, null, 2));
    return result;
  } catch (error) {
    if (page) await page.screenshot({ path: `${directory}/refill-rejection-failure.png`, fullPage: true }).catch(() => {});
    await writeFile(`${directory}/refill-rejection-browser.json`, JSON.stringify({ status: 'FAIL', requestId, error: error.message, pageErrors: errors }, null, 2));
    throw error;
  } finally { await browser.close(); }
}
