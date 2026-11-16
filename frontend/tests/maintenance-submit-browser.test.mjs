import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import{createRoot}from'react-dom/client';
import{AuthProvider}from'./src/auth/AuthProvider';import{MaintenanceSubmissionPanel}from'./src/dev2/MaintenanceSubmissionPanel';
createRoot(document.getElementById('root')).render(<AuthProvider><MaintenanceSubmissionPanel data={{maintenanceWorkId:7,incidentId:8,status:'IN_PROGRESS',canSubmit:true,executionETag:'"0000000000000001"'}}/></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async()=>{const executablePath=['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
 browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});});
after(async()=>browser?.close());
for(const stale of [false,true])test(`Submit validates fields, selects existing evidence and handles ${stale?'stale version':'success'} without retry`,async()=>{
 const page=await browser.newPage(),calls=[];page.setDefaultTimeout(5000);
 try{
  await page.route('**/*',async route=>{
   const url=new URL(route.request().url()),json=data=>route.fulfill({json:data});
   if(url.pathname==='/api/auth/session')return json({user:{id:'42',username:'technician',name:'Technician'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'});
   if(url.pathname==='/api/auth/csrf')return json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
   if(url.pathname==='/api/incidents/8/evidence')return json({incidentId:8,page:1,total:1,items:[{id:9,name:'result.png',note:null,authorId:42,uploadedAt:'2026-11-16T12:00:00Z'}],version:'A'.repeat(43)+'=',canAdd:false,noteTotal:0,notes:[]});
   if(url.pathname==='/api/maintenance-works/7/submit-result'){
    calls.push({headers:route.request().headers(),body:route.request().postDataJSON()});
    if(stale)return route.fulfill({status:412,json:{status:412}});
    return json({maintenanceWorkId:7,incidentId:8,assigneeId:42,status:'COMPLETED',executionETag:'"0000000000000002"',resolvedAt:'2026-11-16T12:00:00Z'});
   }
   return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
  });
  await page.goto('https://hotelops.test');await page.addScriptTag({content:bundle.outputFiles[0].text});
  const review=page.getByRole('button',{name:'Review submission',exact:true});await review.waitFor();assert.equal(await review.isDisabled(),true);
  await page.getByLabel('Diagnosis / root cause').fill(' Worn seal ');assert.equal(await review.isDisabled(),true);
  await page.getByLabel('Action taken',{exact:true}).fill(' Replaced seal ');
  await page.getByLabel('Use result.png as result evidence').check();await review.click();assert.equal(calls.length,0);
  await page.getByRole('button',{name:'Confirm submission'}).click();
  await page.getByText(stale?'Work changed. Reload before submitting again.':'Result submitted for verification. Reload the work detail.',{exact:true}).waitFor();
  assert.equal(calls.length,1);assert.equal(calls[0].headers['if-match'],'"0000000000000001"');assert.equal(calls[0].headers['x-csrf-token'],'synthetic');
  assert.deepEqual(calls[0].body,{actorUserId:42,diagnosis:'Worn seal',actionTaken:'Replaced seal',resolutionNote:null,resultAttachmentIds:[9]});
  assert.equal(await review.isDisabled(),true);
 }finally{await page.close();}
});

const readBundle = await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`
import React from 'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';
import{MaintenanceReadView}from'./src/dev2/MaintenanceReadView';
createRoot(document.getElementById('root')).render(<BrowserRouter><MaintenanceReadView detail canDetail data={{maintenanceWorkId:7,incidentId:8,status:'PENDING',source:'INCIDENT',maintenanceWorkLogs:[],...window.__maintenanceRead}}/></BrowserRouter>);
`},bundle:true,write:false,format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"production"'}});
for(const denied of [false,true])test('Maintenance evidence read '+(denied?'denies independent Incident access':'never exposes upload controls'),async()=>{
 const page=await browser.newPage(),writes=[];page.setDefaultTimeout(5000);
 try{
  await page.route('**/*',route=>{
   const req=route.request(),url=new URL(req.url());if(req.method()!=='GET')writes.push(req.method());
   if(url.pathname==='/api/incidents/8/evidence')return denied?route.fulfill({status:403,json:{detail:'PRIVATE-DIAGNOSIS'}})
    :route.fulfill({json:{incidentId:8,page:1,total:1,items:[{id:9,name:'reported.png',note:null,authorId:42,uploadedAt:'2026-11-16T12:00:00Z'}],version:'A'.repeat(43)+'=',canAdd:true,noteTotal:0,notes:[]}});
   return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});
  });
  await page.goto('https://hotelops.test');
  await page.evaluate(denied=>{window.__maintenanceRead={plannedEndAt:'2030-01-01T00:00:00Z',incidentSla:denied?{responseDeadlineAt:null,resolutionDeadlineAt:null,unavailableReason:'INCIDENT_SLA_ACCESS_UNAVAILABLE'}:{responseDeadlineAt:'2026-11-16T09:00:00Z',resolutionDeadlineAt:'2026-11-16T12:00:00Z',unavailableReason:null}}},denied);
  await page.addScriptTag({content:readBundle.outputFiles[0].text});
  const sla=page.getByRole('region',{name:'Incident SLA deadlines'});
  await sla.waitFor();
  if(denied){assert.equal(await sla.locator('dd').first().innerText(),'Unavailable');await sla.getByText('Current access does not allow viewing Incident SLA deadlines.',{exact:true}).waitFor();}
  else assert.equal(await sla.locator('dd').first().innerText(),await page.evaluate(()=>new Date('2026-11-16T09:00:00Z').toLocaleString()));
  if(denied)await page.getByText('Incident evidence is unavailable. Reload to check current access.',{exact:true}).waitFor();
  else {await page.getByRole('link',{name:'reported.png',exact:true}).waitFor();assert.equal(await page.getByRole('link',{name:'reported.png',exact:true}).getAttribute('href'),'/api/incidents/8/evidence/9');}
  assert.equal(await page.getByRole('button',{name:'Save evidence',exact:true}).count(),0);
  assert.ok(!(await page.locator('body').innerText()).includes('PRIVATE-DIAGNOSIS'));assert.deepEqual(writes,[]);
 }finally{await page.close();}
});
