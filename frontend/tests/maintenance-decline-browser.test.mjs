import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';import {MaintenanceDeclinePanel} from './src/dev2/MaintenanceDeclinePanel';
createRoot(document.getElementById('root')).render(<AuthProvider><BrowserRouter><MaintenanceDeclinePanel data={{maintenanceWorkId:7,assigneeId:42,canDecline:true,status:"PENDING",executionETag:'"0000000000000001"'}}/></BrowserRouter></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async () => { const executablePath = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
  browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) }); });
after(async () => browser?.close());

for (const stale of [false, true]) test('Decline '+(stale?'stale version':'success')+' confirms once with CSRF and no actor claim',async()=>{
 const page=await browser.newPage(),calls=[];page.setDefaultTimeout(5000);
 try{
  await page.route('**/*',route=>{const req=route.request(),path=new URL(req.url()).pathname;
   if(path==='/api/auth/session')return route.fulfill({json:{user:{id:'42',username:'tech',name:'Technician'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'}});
   if(path==='/api/auth/csrf')return route.fulfill({json:{headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}});
   if(path==='/api/maintenance-works/7/decline'){calls.push({body:req.postDataJSON(),headers:req.headers(),method:req.method()});return route.fulfill(stale?{status:412,json:{}}:{json:{maintenanceWorkId:7,auditId:8,workflow:'PENDING_ASSIGNMENT',declinedAt:'2026-11-16T10:00:00Z'}});}
   return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
  });
  await page.goto('https://hotelops.test');await page.addScriptTag({content:bundle.outputFiles[0].text});
  const review=page.getByRole('button',{name:'Review decline',exact:true});await review.waitFor();assert.equal(await review.isDisabled(),true);
  await page.getByLabel('Decline reason',{exact:true}).fill('Cannot safely take this assignment');await review.click();assert.equal(calls.length,0);
  await page.getByRole('button',{name:'Confirm decline',exact:true}).click();
  await page.getByRole('status').filter({hasText:stale?'Work changed.':'Assignment declined.'}).waitFor();
  assert.equal(calls.length,1);assert.equal(calls[0].method,'POST');assert.equal(calls[0].headers['if-match'],'"0000000000000001"');assert.equal(calls[0].headers['x-csrf-token'],'synthetic');
  assert.deepEqual(calls[0].body,{reason:'Cannot safely take this assignment'});assert.equal(await review.isDisabled(),true);
 }finally{await page.close();}
});
