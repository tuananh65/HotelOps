import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import{createRoot}from'react-dom/client';
import{AuthProvider}from'./src/auth/AuthProvider';import{MaintenanceVerifyPanel}from'./src/dev2/MaintenanceVerifyPanel';
createRoot(document.getElementById('root')).render(<AuthProvider><MaintenanceVerifyPanel data={{maintenanceWorkId:7,incidentId:8,status:'COMPLETED',assigneeId:9,canVerify:true,submittedReview:{submissionAuditId:77},executionETag:'"0000000000000001"'}}/></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async()=>{const executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
 browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});});
after(async()=>browser?.close());
for(const stale of [false,true])test(`Verify binds snapshot and handles ${stale?'stale version':'success'} without retry`,async()=>{
 const page=await browser.newPage(),calls=[];page.setDefaultTimeout(5000);
 try{
  await page.route('**/*',async route=>{
   const url=new URL(route.request().url()),json=data=>route.fulfill({json:data});
   if(url.pathname==='/api/auth/session')return json({user:{id:'42',username:'technician',name:'Technician'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'});
   if(url.pathname==='/api/auth/csrf')return json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
   if(url.pathname==='/api/maintenance-works/7/verify'){
    calls.push({headers:route.request().headers(),body:route.request().postDataJSON()});
    if(stale)return route.fulfill({status:412,json:{status:412}});
    return json({maintenanceWorkId:7,incidentId:8,assigneeId:9,status:'CLOSED',executionETag:'"0000000000000002"',resolvedAt:'2026-11-16T12:00:00Z'});
   }
   return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
  });
  await page.goto('https://hotelops.test');await page.addScriptTag({content:bundle.outputFiles[0].text});
  const review=page.getByRole('button',{name:'Review closure',exact:true});await review.waitFor();assert.equal(await review.isDisabled(),true);
  await page.getByLabel("Ghi chú xác minh").fill('short');assert.equal(await review.isDisabled(),true);
  await page.getByLabel("Ghi chú xác minh").fill(' Repair is complete ');await review.click();assert.equal(calls.length,0);
  await page.getByRole('button',{name:'Confirm closure'}).click();
  await page.getByText(stale?"Công việc đã đổi. Tải lại trước khi xét duyệt tiếp.":'Work and Incident closed. Room readiness is recalculated; a released Incident block does not mean the room is Ready. Reload the work detail.',{exact:true}).waitFor();
  assert.equal(calls.length,1);assert.equal(calls[0].headers['if-match'],'"0000000000000001"');assert.equal(calls[0].headers['x-csrf-token'],'synthetic');
  assert.deepEqual(calls[0].body,{actorUserId:42,note:'Repair is complete',submissionAuditId:77});
  assert.equal(await review.isDisabled(),true);
 }finally{await page.close();}
});
