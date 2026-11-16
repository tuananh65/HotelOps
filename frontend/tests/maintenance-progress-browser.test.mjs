import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import{createRoot}from'react-dom/client';
import{AuthProvider}from'./src/auth/AuthProvider';import{MaintenanceProgressPanel}from'./src/dev2/MaintenanceProgressPanel';
createRoot(document.getElementById('root')).render(<AuthProvider><MaintenanceProgressPanel allowMaterials={true} data={{maintenanceWorkId:7,status:'ON_HOLD',canLog:true,progressPercentAvailable:true,executionETag:'"0000000000000001"'}}/></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async () => {
  const executablePath = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
  browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) });
});
after(async () => browser?.close());
for (const mode of ['materials', 'note-only', 'stale']) test(`Progress: ${mode}, versioned write without retry`, async () => {
  const page = await browser.newPage(), calls = []; page.setDefaultTimeout(5000);
  try {
    await page.route('**/*', async route => {
      const url = new URL(route.request().url()), json = data => route.fulfill({ json: data });
      if (url.pathname === '/api/auth/session') return json({ user: { id: '42', username: 'technician', name: 'Technician' }, expiresAt: '2099-01-01T00:00:00Z', absoluteExpiresAt: '2099-01-02T00:00:00Z', mustChangePassword: false, csrfToken: 'synthetic' });
      if (url.pathname === '/api/auth/csrf') return json({ headerName: 'X-CSRF-TOKEN', csrfToken: 'synthetic' });
      if (url.pathname.endsWith('/material-availability')) {
        if (mode === 'note-only') return route.fulfill({ status: 403, json: {} });
        const p = Number(url.searchParams.get('page'));
        return json({ page: p, pageSize: 20, total: 21, items: [{ stockDocumentLineId: p === 1 ? 100 : 101,
          itemId: 5, documentNo: 'ISS-1', lineNo: p, baseUomId: 1, remainingBaseQty: 10,
          allowedUoms: [{ uomId: 1, conversionFactor: 1 }, { uomId: 2, conversionFactor: 5 }] }] });
      }
      if (url.pathname === '/api/maintenance-works/7/logs') {
        const body = route.request().postDataJSON(); calls.push({ headers: route.request().headers(), body });
        if (mode === 'stale') return route.fulfill({ status: 412, json: {} });
        return json({ logId: 3, maintenanceWorkId: 7, loggedBy: 42, status: 'ON_HOLD', workNote: body.workNote, progressPercent: body.progressPercent,
          executionETag: '"0000000000000002"', materialUsages: body.materialUsages.map(x => ({ ...x, maintenanceWorkId: 7, maintenanceWorkLogId: 3 })) });
      }
      return route.fulfill({ contentType: 'text/html', body: '<div id="root"></div>' });
    });
    await page.goto('https://hotelops.test'); await page.addScriptTag({ content: bundle.outputFiles[0].text });
    const review = page.getByRole('button', { name: 'Review progress', exact: true }); await review.waitFor();
    assert.equal(await review.isDisabled(), true);
    await page.getByLabel('Progress note').fill(' Replaced worn seal ');
    for(const value of ['-1','101','0.5']) {await page.getByLabel('Progress (%)',{exact:true}).fill(value);assert.equal(await review.isDisabled(),true);}
    await page.getByLabel('Progress (%)',{exact:true}).fill('100');
    await page.getByRole('button', { name: 'Load issued materials' }).click();
    if (mode === 'note-only') await page.getByText('Material availability is unavailable. Current inventory and material request access are required.', { exact: true }).waitFor();
    else {
      await page.getByRole('button', { name: 'Add line 1 from ISS-1' }).click();
      assert.equal(await review.isDisabled(), true);
      await page.getByLabel('Unit for line 100', { exact: true }).selectOption('2');
      await page.getByLabel('Used quantity for line 100').fill('3'); assert.equal(await review.isDisabled(), true);
      await page.getByLabel('Used quantity for line 100').fill('1.0001'); assert.equal(await review.isDisabled(), true);
      await page.getByLabel('Used quantity for line 100').fill('2');
      await page.getByRole('button', { name: 'Next materials' }).click();
      await page.getByRole('button', { name: 'Add line 2 from ISS-1' }).click();
      await page.getByLabel('Used quantity for line 101').fill('1');
      assert.equal(await page.getByLabel('Used quantity for line 100').inputValue(), '2');
    }
    await review.click(); assert.equal(calls.length, 0);
    await page.getByRole('button', { name: 'Confirm progress' }).click();
    await page.getByText(mode === 'stale' ? 'Work changed. Reload before recording progress again.' : 'Progress recorded. Reload the work detail before another action.', { exact: true }).waitFor();
    assert.equal(calls.length, 1); assert.equal(calls[0].headers['if-match'], '"0000000000000001"');
    assert.equal(calls[0].headers['x-csrf-token'], 'synthetic');
    assert.deepEqual(calls[0].body, { actorUserId: 42, progressPercent: 100, workNote: 'Replaced worn seal', spentMinutes: 0, materialUsages: mode === 'note-only' ? [] : [
      { stockDocumentLineId: 100, uomId: 2, usedQty: 2 }, { stockDocumentLineId: 101, uomId: 1, usedQty: 1 }] });
    assert.equal(await review.isDisabled(), true);
  } finally { await page.close(); }
});
