import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium } from '@playwright/test';
const fixture = JSON.parse(fs.readFileSync(new URL('../../artifacts/p0-workflow/private/runtime-fixture.json', import.meta.url), 'utf8').replace(/^\uFEFF/, ''));
assert.equal(fixture.database, 'HotelOpsCompactWorkdayTestP0261124');
assert.equal(new URL(fixture.baseUrl).port, '5774');
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
const results = [];
try {
  for (const suffix of ['DVBV', 'DVCT']) {
    const actor = fixture.actors[`${suffix}.om`];
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    await context.addInitScript(token => sessionStorage.setItem('hotelops.compact.session', token), actor.token);
    const page = await context.newPage();
    await context.route('**/api/**', route => ['GET','HEAD','OPTIONS'].includes(route.request().method()) ? route.continue() : route.abort());
    try {
      await page.goto('http://127.0.0.1:5174/login#dashboard');
      await page.getByRole('button', { name: 'Đăng xuất', exact: true }).waitFor();
      await page.getByRole('region', { name: 'Công việc cần xử lý' }).waitFor();
      const summaries = await page.locator('.operations-action-center details summary').allTextContents();
      assert.equal(summaries.length, 8);
      results.push({ branch: suffix, check: 'action-center-server-groups', pass: true, groups: summaries });
      for (const report of ['transfers', 'stocktake-differences']) {
        await page.goto(`http://127.0.0.1:5174/login#reports?type=${report}`);
        await page.getByLabel('Loại báo cáo').waitFor();
        assert.equal(await page.getByLabel('Loại báo cáo').inputValue(), report);
        await page.waitForFunction(() => !document.querySelector('main .spinner, main [aria-busy="true"]'));
        const payload = await page.evaluate(async ({report, branchId}) => {
          const response = await fetch(`/api/v2/insights/reports/${report}?branchId=${branchId}`, { headers: {Authorization:`Bearer ${sessionStorage.getItem('hotelops.compact.session')}`} });
          if (!response.ok) throw Error(`Report HTTP ${response.status}`);
          return response.json();
        }, {report, branchId: actor.branchId});
        assert(Array.isArray(payload));
        assert(!(await page.locator('main').innerText()).includes('[object Object]'));
        const overflowing = await page.evaluate(() => [...document.querySelectorAll('main *')].some(e => e.clientWidth > 0 && e.scrollWidth > e.clientWidth + 2 && ['auto','scroll'].includes(getComputedStyle(e).overflowX)));
        assert.equal(overflowing, false);
        results.push({branch:suffix,check:report,pass:true,documents:payload.length,lines:report==='transfers'?payload.reduce((n,r)=>n+(r.lines?.length||0),0):payload.length});
      }
    } catch (error) { results.push({branch:suffix,pass:false,error:error.message.replaceAll(actor.token,'[REDACTED]')}); }
    finally { await context.close(); }
  }
} finally { await browser.close(); }
fs.writeFileSync(new URL('../../artifacts/p0-workflow/browser/insight-results.json',import.meta.url),JSON.stringify(results,null,2));
console.log(JSON.stringify(results,null,2));
if(results.some(r=>!r.pass))process.exitCode=1;
