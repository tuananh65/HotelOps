import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import{createRoot}from'react-dom/client';
import{AuthProvider}from'./src/auth/AuthProvider';import{MaintenanceResumePanel}from'./src/dev2/MaintenanceResumePanel';
import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';
const action=window.action,hold=action==='hold',start=action==='start',calls=[];window.calls=calls;
window.fetch=async(url,options={})=>{
 if(url==='/api/auth/session')return Response.json({user:{id:'42',username:'technician',name:'Technician'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'});
 if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
 calls.push({url,...options,signal:undefined});
 return Response.json({maintenanceWorkId:7,assigneeId:42,status:hold?'ON_HOLD':'IN_PROGRESS',executionETag:'"0000000000000002"'});
};
createRoot(document.getElementById('root')).render(<AuthProvider store={createAuthStore(createAuthApi(window.fetch))}><MaintenanceResumePanel allowStart allowHold allowResume data={{maintenanceWorkId:7,status:start?'PENDING':hold||action==='returned'?'IN_PROGRESS':'ON_HOLD',canStart:start,canHold:hold,canResume:action==='resume'||action==='returned',executionETag:'"0000000000000001"'}}/></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async()=>{const executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
 browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});});
after(async()=>browser?.close());
for(const action of ['hold','resume','start','returned'])test(`${action} requires review and sends current CSRF and original version once`,async()=>{
 const hold=action==='hold';const page=await browser.newPage();page.setDefaultTimeout(5000);
 try{
  await page.route('**/*',r=>r.fulfill({contentType:'text/html',body:'<div id="root"></div>'}));
  await page.goto('https://hotelops.test');await page.evaluate(v=>window.action=v,action);await page.addScriptTag({content:bundle.outputFiles[0].text});
  const verb=action==='returned'?'Resume':action[0].toUpperCase()+action.slice(1),button=page.getByRole('button',{name:`${verb} maintenance`,exact:true});await button.waitFor();
  if(hold){assert.equal(await button.isDisabled(),true);await page.getByLabel('Hold reason and note').fill('Waiting for spare parts');}
  await button.click();assert.equal(await page.evaluate(()=>window.calls.length),0);
  await page.getByRole('button',{name:`Confirm ${verb.toLowerCase()}`,exact:true}).click();
  await page.getByText(`Work ${hold?'put on hold':action==='start'?'started':'resumed'}. Reload the work detail to view the current state.`,{exact:true}).waitFor();
  const calls=await page.evaluate(()=>window.calls);assert.equal(calls.length,1);const call=calls[0];
  assert.equal(call.url,`/api/maintenance-works/7/${verb.toLowerCase()}`);assert.equal(call.method,'POST');
  assert.equal(call.headers['If-Match'],'"0000000000000001"');assert.equal(call.headers['X-CSRF-TOKEN'],'synthetic');
  assert.deepEqual(JSON.parse(call.body),{actorUserId:42,...(hold?{reason:'Waiting for spare parts'}:{})});assert.equal(await button.isDisabled(),true);
 }finally{await page.close();}
});

