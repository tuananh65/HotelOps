import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';import {IncidentTriagePanel} from './src/dev2/IncidentTriagePanel';
createRoot(document.getElementById('root')).render(<AuthProvider><BrowserRouter><IncidentTriagePanel data={{incidentId:7,severityId:2,canTriage:true,triageETag:'"0000000000000001"',triageOptions:[{priorityId:3,name:'Configured priority',responseDeadlineAt:'2026-11-16T08:37:00Z',resolutionDeadlineAt:'2026-11-16T20:01:00Z',targetVersion:'0000000000000005'}]}}/></BrowserRouter></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async () => { const executablePath = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
  browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) }); });
after(async () => browser?.close());
for (const stale of [false, true]) test(`Triage ${stale ? 'override with stale configuration' : 'configured defaults'} uses CSRF/version and never retries`, async () => {
  const page = await browser.newPage(), calls = []; page.setDefaultTimeout(5000);
  try {
    await page.route('**/*', route => {
      const request = route.request(), url = new URL(request.url());
      if (url.pathname === '/api/auth/session') return route.fulfill({ json: { user: { id:'42',username:'lead',name:'Lead' },expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic' } });
      if (url.pathname === '/api/auth/csrf') return route.fulfill({ json: { headerName:'X-CSRF-TOKEN',csrfToken:'synthetic' } });
      if (url.pathname === '/api/incidents/7/triage') {
        calls.push({ method:request.method(),body:request.postDataJSON(),headers:request.headers() });
        return stale ? route.fulfill({status:412,json:{}}) : route.fulfill({json:{incidentId:7,severityId:2,priorityId:3,status:'OPEN',triageETag:'"0000000000000002"',sla:{responseDeadlineAt:'2026-11-16T08:37:00Z',resolutionDeadlineAt:'2026-11-16T20:01:00Z'}}});
      }
      return route.fulfill({ contentType:'text/html',body:'<div id="root"></div>' });
    });
    await page.goto('https://hotelops.test'); await page.addScriptTag({content:bundle.outputFiles[0].text});
    const review = page.getByRole('button',{name:'Review triage',exact:true});
    await review.waitFor(); assert.equal(await review.isDisabled(),true);
    await page.getByLabel('Priority',{exact:true}).selectOption('3'); await page.getByLabel('Initial action plan',{exact:true}).fill('Inspect equipment and isolate the failed supply.');
    if(stale){ await page.getByLabel('Override response deadline (UTC, optional)',{exact:true}).fill('2026-11-16T10:00');assert.equal(await review.isDisabled(),true);
      await page.getByLabel('Reason for changing deadlines (at least 10 characters)',{exact:true}).fill('Approved access window'); }
    await review.click(); assert.equal(calls.length,0); await page.getByRole('button',{name:'Confirm triage',exact:true}).click();
    await page.getByText(stale?'Incident or SLA targets changed. Reload before trying again.':'Triage saved. The incident is pending assignment.',{exact:true}).waitFor();
    assert.equal(calls.length,1);assert.equal(calls[0].method,'PATCH');assert.equal(calls[0].headers['if-match'],'"0000000000000001"');assert.equal(calls[0].headers['x-csrf-token'],'synthetic');
    assert.equal(calls[0].body.expectedSlaTargetVersion,'0000000000000005');assert.equal(calls[0].body.actorUserId,42);assert.equal(calls[0].body.priorityId,3);
    if(stale){assert.equal(calls[0].body.responseDeadlineAt,'2026-11-16T10:00:00.000Z');assert.equal(await review.isDisabled(),true);}
    else {assert.equal(calls[0].body.responseDeadlineAt,undefined);assert.equal(await page.getByRole('link',{name:'Continue to assignment'}).getAttribute('href'),'/operations/maintenance/7?action=A004A-024');}
  } finally { await page.close(); }
});
