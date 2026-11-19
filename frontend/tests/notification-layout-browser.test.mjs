import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';import {NotificationCenter} from './src/notifications/NotificationCenter';
import './src/styles.css';import './src/styles/cacao.css';import './src/styles/design-system.css';
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider><main className="cacao-content"><NotificationCenter/></main></AuthProvider></BrowserRouter>);
`},bundle:true,write:false,outfile:'notifications.js',format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"development"','import.meta.env':'{}'}});
let browser;
before(async()=>{browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});mkdirSync('../artifacts/workspace-usability-fix/screenshots',{recursive:true});});
after(async()=>browser?.close());
for(const width of [1280,390])test(`notification cards remain readable at ${width}px and preserve selected read`,async()=>{
 const p=await browser.newPage({viewport:{width,height:900}});const errors=[],writes=[];let read=false;
 p.on('pageerror',e=>errors.push(e.message));p.setDefaultTimeout(8000);
 await p.route('https://notice.test/**',route=>{
  const r=route.request(),u=new URL(r.url()),json=v=>route.fulfill({contentType:'application/json',body:JSON.stringify(v)});
  if(r.resourceType()==='document')return route.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><div id="root"></div></html>'});
  if(u.pathname==='/api/auth/session')return json({user:{id:'2',username:'mai.tran',name:'Trần Thị Mai'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
  if(u.pathname==='/api/auth/csrf')return json({csrfToken:'test',headerName:'X-CSRF-TOKEN'});
  if(r.method()==='POST'){writes.push({path:u.pathname,body:r.postDataJSON(),csrf:r.headers()['x-csrf-token']});read=true;return route.fulfill({status:204});}
  if(u.pathname==='/api/notifications')return json({items:[{id:7,type:'TASK_ASSIGNED',title:'Phòng 201 cần kiểm tra',message:'Công việc vệ sinh đã hoàn tất. Kiểm tra phòng trước khi bàn giao.',priority:'HIGH',createdAt:'2026-11-19T02:00:00Z',readAt:read?'2026-11-19T03:00:00Z':null,requiresAcknowledgment:true,acknowledgedAt:null}],totalCount:1,unreadCount:read?0:1,page:1,pageSize:20,snapshot:'2026-11-19T03:00:00Z',acknowledgmentAvailable:true});
  return route.fulfill({status:403,body:'{}'});
 });
 try{
  await p.goto('https://notice.test/notifications');for(const f of bundle.outputFiles.filter(f=>f.path.endsWith('.css')))await p.addStyleTag({content:f.text});await p.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});
  await p.getByRole('heading',{name:'Phòng 201 cần kiểm tra'}).waitFor();assert.equal(await p.getByRole('link',{name:'Tổng quan',exact:true}).count(),0);
  assert.ok(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));assert.match(await p.locator('time').last().innerText(),/09:00/);
  await p.screenshot({path:`../artifacts/workspace-usability-fix/screenshots/notifications-${width}.png`,fullPage:true});
  await p.getByLabel('Chọn thông báo Phòng 201 cần kiểm tra',{exact:true}).check();await p.getByRole('button',{name:'Đánh dấu mục đã chọn đã đọc',exact:true}).click();await p.getByText('0 chưa đọc',{exact:true}).waitFor();
  assert.deepEqual(writes,[{path:'/api/notifications/read-selected',body:{ids:[7]},csrf:'test'}]);assert.deepEqual(errors,[]);
 }finally{await p.close();}
});
