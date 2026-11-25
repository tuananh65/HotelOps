import { createRequire } from 'node:module';
import { writeFile } from 'node:fs/promises';
const require = createRequire(new URL('../../frontend/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');

export async function verifyRequestWorkspace(fixture, directory) {
  const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
  const results = [];
  try {
    for (const role of ['manager', 'store']) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
      // Use the existing local frontend only for static assets. Every API call is isolated
      // onto the TEST server; never contact the frontend's configured operational API.
      await context.route('**/api/**', async route => {
        const url = new URL(route.request().url());
        // This probe covers the request workspace, not SignalR (separately verified).
        // A long-poll transport must not hang route.fetch or expose headers in its error.
        if (url.pathname.startsWith('/api/v2/realtime/')) { await route.abort(); return; }
        url.hostname = '127.0.0.1'; url.port = '5779'; url.protocol = 'http:';
        try { await route.fulfill({ response: await route.fetch({ url: url.toString() }) }); }
        catch { await route.abort().catch(() => {}); }
      });
      await context.routeWebSocket('**/*', socket => socket.close());
      await context.addInitScript(token => sessionStorage.setItem('hotelops.compact.session', token), fixture.sessions[role].token);
      const page = await context.newPage();
      const errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5174/login#requests');
      await expect(page.getByRole('navigation', { name: 'Nhóm yêu cầu' })).toBeVisible({ timeout: 30000 });
      const tabs = page.getByRole('navigation', { name: 'Nhóm yêu cầu' });
      await expect(tabs.getByRole('button', { name: 'Ghi giảm vật tư', exact: true })).toBeVisible();
      await expect(tabs.getByRole('button', { name: 'Thay tài sản', exact: true })).toHaveCount(role === 'manager' ? 1 : 0);
      await expect(page.getByRole('row').filter({ hasText: 'Ghi giảm vật tư tái sử dụng' }).first()).toBeVisible();
      const refreshed = Promise.all([
        page.waitForResponse(r => new URL(r.url()).pathname === '/api/v2/reusable-inventory/write-offs' && r.status() === 200),
        page.waitForResponse(r => new URL(r.url()).pathname === '/api/v2/requests' && r.status() === 200),
      ]);
      await page.evaluate(() => window.dispatchEvent(new Event('compact:notification-change')));
      await refreshed;
      await page.getByRole('button', { name: 'Bộ lọc', exact: true }).click();
      await page.getByLabel('Hàng đợi').selectOption('action');
      const response = await context.request.get('http://127.0.0.1:5779/api/v2/reusable-inventory/write-offs?branchId=' + fixture.branch,
        { headers: { Authorization: 'Bearer ' + fixture.sessions[role].token } });
      if (response.status() !== 200) throw new Error('Write-off baseline API failed: ' + response.status());
      const pending = (await response.json()).filter(r => role === 'manager' && r.status === 'PENDING' && r.createdBy !== fixture.sessions[role].user);
      await expect(page.getByRole('row').filter({ hasText: 'Ghi giảm vật tư tái sử dụng' })).toHaveCount(Math.min(25, pending.length));
      await tabs.getByRole('button', { name: 'Ghi giảm vật tư', exact: true }).click();
      await expect(page.getByRole('heading', { name: 'Vật tư tái sử dụng', exact: true })).toBeVisible();
      await expect(page.getByRole('columnheader', { name: 'Trạng thái / Lý do', exact: true })).toBeVisible();
      if (role === 'manager' && fixture.uiProposal) {
        const pendingRow=page.getByRole('row').filter({hasText:'Chờ OM xác nhận'}).first();
        await pendingRow.getByRole('button',{name:'Xác nhận',exact:true}).click();
        const dialog=page.getByRole('dialog',{name:'Xác nhận ghi giảm',exact:true});
        await expect(dialog).toBeVisible();
        await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
        await expect(dialog).toHaveCount(0);
        const verify=await context.request.get('http://127.0.0.1:5779/api/v2/reusable-inventory/write-offs?branchId='+fixture.branch,
          {headers:{Authorization:'Bearer '+fixture.sessions[role].token}});
        const decided=(await verify.json()).find(r=>r.writeOffId===fixture.uiProposal);
        if(decided?.status!=='APPROVED'||decided.decidedBy!==fixture.sessions.manager.user)throw new Error('UI approval did not commit the expected proposal');
      }
      if (role === 'store') await expect(page.getByRole('button', { name: 'Xác nhận', exact: true })).toHaveCount(0);
      await page.screenshot({ path: `${directory}/request-workspace-${role}.png`, fullPage: true });
      await page.setViewportSize({ width: 390, height: 844 });
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
      if (overflow) throw new Error(role + ' request workspace overflows on mobile');
      if (errors.length) throw new Error(errors.join('\n'));
      results.push({ role, verdict: 'PASS', checks: ['domain tabs', 'inbox projection', 'notification-change event reloads both request sources (SignalR transport tested separately)', 'action queue', 'domain navigation', ...(role==='manager'&&fixture.uiProposal?['write-off approval through browser, verified by API']:[]), 'mobile width', 'no page errors'] });
      await context.close();
    }
  } catch (error) {
    for (const [index, context] of browser.contexts().entries()) {
      const page = context.pages()[0];
      if (page) await page.screenshot({ path: `${directory}/request-workspace-failure-${index}.png`, fullPage: true }).catch(() => {});
    }
    throw error;
  } finally { await browser.close(); }
  await writeFile(`${directory}/request-workspace-browser.json`, JSON.stringify({ results, limits: 'Real TEST API. OM write-off approval is executed in the browser; stock fixture and proposal setup use HTTP. Does not prove all Master demo workflows.' }, null, 2));
}
