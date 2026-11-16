import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';import {MaintenanceAssignmentPanel} from './src/dev2/MaintenanceAssignmentPanel';
createRoot(document.getElementById('root')).render(<AuthProvider><BrowserRouter><MaintenanceAssignmentPanel incidentId={9}/></BrowserRouter></AuthProvider>);
` }, bundle: true, write: false, format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"production"' } });
let browser;
before(async () => { const executablePath = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe','C:/Program Files/Google/Chrome/Application/chrome.exe'].find(existsSync);
  browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) }); });
after(async () => browser?.close());


for(const stale of [false,true])test('Assignment '+(stale?'stale':'success')+' sends both versions and UTC planned end',async()=>{
 const page=await browser.newPage(),calls=[];page.setDefaultTimeout(5000);
 try{
 await page.route('**/*',route=>{const r=route.request(),path=new URL(r.url()).pathname;
 if(path==='/api/auth/session')return route.fulfill({json:{user:{id:'42',username:'lead',name:'Lead'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'synthetic'}});
 if(path==='/api/auth/csrf')return route.fulfill({json:{headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}});
 if(path==='/api/incidents/9/assignment' && r.method()==='GET')return route.fulfill({json:{incidentId:9,incidentVersion:'"0000000000000001"',workId:7,workVersion:'"0000000000000002"',assigneeId:42,technicians:[{userId:43,name:'Technician',openIncidents:2,openOccurrences:1}]}});
 if(path==='/api/incidents/9/assignment'){calls.push({headers:r.headers(),body:r.postDataJSON()});return route.fulfill(stale?{status:412,json:{}}:{json:{workId:7,auditId:15,workflow:'ASSIGNED'}});}
 return route.fulfill({contentType:'text/html',body:'<div id="root"></div>'});});
 await page.goto('https://hotelops.test');await page.addScriptTag({content:bundle.outputFiles[0].text});
 await page.getByRole('button',{name:'Load eligible technicians',exact:true}).click();
 await page.getByLabel('Technician',{exact:true}).selectOption('43');
 await page.getByLabel('Planned end (Asia/Ho_Chi_Minh, UTC+7)',{exact:true}).fill('2026-11-17T15:00');
 await page.getByRole('button',{name:'Review assignment',exact:true}).click();assert.equal(calls.length,0);
 await page.getByRole('button',{name:'Confirm assignment',exact:true}).click();
 await page.getByRole('status').filter({hasText:stale?'Assignment changed.':'Assignment saved.'}).waitFor();
 assert.equal(calls.length,1);assert.equal(calls[0].headers['if-match'],'"0000000000000001"');assert.equal(calls[0].headers['x-csrf-token'],'synthetic');
 assert.deepEqual(calls[0].body,{assigneeId:43,workVersion:'"0000000000000002"',instruction:null,plannedEndAt:'2026-11-17T08:00:00.000Z'});
 assert.equal(await page.getByRole('button',{name:'Review assignment',exact:true}).isDisabled(),true);
 if(!stale)assert.equal(await page.getByRole('link',{name:'Open assigned maintenance work'}).getAttribute('href'),'/operations/maintenance/7?action=A004A-036');
 }finally{await page.close();}
});
