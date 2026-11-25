import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {spawn} from 'node:child_process';
import {readFile,writeFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const directory='artifacts/restructure-v2',runId=randomUUID(),tests=[],errors=[];
let fixture,owned=false,started=false,browser,cleanup,page;
const run=(action,target='A')=>new Promise((resolve,reject)=>{
 const child=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/v2-realtime-fixture.ps1','-Action',action,'-Target',target],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});
 let stdout='',stderr='';child.stdout.on('data',d=>stdout+=d);child.stderr.on('data',d=>stderr+=d);child.on('error',reject);
 child.on('exit',code=>{if(code)return reject(new Error(stderr.slice(0,1200)));try{resolve(JSON.parse(stdout.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture output'));}});
});
async function test(name,fn){try{await fn();tests.push({name,status:'PASS'});console.log('PASS '+name);}catch(e){tests.push({name,status:'FAIL',error:String(e.message).replace(/[a-f0-9]{64}/ig,'[redacted]')});throw e;}}
async function open(actor){
 const frames=[];
 const context=await browser.newContext({viewport:{width:1440,height:1000}});
 await context.route('**/api/**',async route=>{
  const url=new URL(route.request().url());url.hostname='127.0.0.1';url.port='5777';url.protocol='http:';
  try{
   const response=await route.fetch({url:url.toString(),timeout:120000});
   if(url.pathname==='/api/v2/realtime/notifications'&&route.request().method()==='GET'&&response.status()===200){
    for(const part of (await response.text()).split('\u001e')){try{const message=JSON.parse(part);if(message.type===2&&Array.isArray(message.item?.notificationIds))frames.push(message.item);}catch{}}
   }
   await route.fulfill({response});
  }catch{await route.abort().catch(()=>{});}
 });
 await context.routeWebSocket('**/*',socket=>socket.close());
 await context.addInitScript(token=>{sessionStorage.setItem('hotelops.compact.session',token);window.__notificationChanges=0;window.addEventListener('compact:notification-change',()=>window.__notificationChanges++);},actor.token);
 const p=await context.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto('http://127.0.0.1:5174/login');
 await expect.poll(()=>p.evaluate(()=>window.__notificationChanges),{timeout:60000}).toBeGreaterThan(0);
 p.notificationFrames=frames;assert.ok(frames.length>0,'Must observe actual SignalR stream frames');return p;
}
const count=p=>p.evaluate(()=>Number(document.querySelector('button[aria-label="Thông báo"] span[aria-label]')?.getAttribute('aria-label')?.split(' ')[0]||0));
const notice=p=>p.getByText('Bạn có 1 thông báo mới. Mở Thông báo để xem chi tiết.',{exact:true});
try{
 try{await run('Setup');}finally{try{fixture=JSON.parse((await readFile(directory+'/realtime-private/fixture.json','utf8')).replace(/^\uFEFF/,''));owned=fixture.runId===runId;}catch{}}
 assert.equal(owned,true);started=true;await run('Start');
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let a,b,initialA,initialB;
 await test('two branch-scoped browsers receive initial real SignalR snapshots without duplicate popups',async()=>{
  a=await open(fixture.actors.A);page=a;b=await open(fixture.actors.B);
  initialA=await count(a);initialB=await count(b);await expect(notice(a)).toHaveCount(0);await expect(notice(b)).toHaveCount(0);
 });
 await test('committed notification updates recipient badge and popup but not another branch',async()=>{
  await run('Insert');await expect.poll(()=>count(a),{timeout:20000}).toBe(initialA+1);await expect(notice(a)).toBeVisible();
  await expect.poll(()=>count(b),{timeout:3000}).toBe(initialB);await expect(notice(b)).toHaveCount(0);
  await a.screenshot({path:directory+'/realtime-recipient-desktop.png',fullPage:true});
 });
 await test('dismissed popup stays dismissed across polling and foreign-branch addressed row',async()=>{
  await a.getByRole('button',{name:'Đóng thông báo',exact:true}).click();await expect(notice(a)).toBeHidden();
  const pulses=await a.evaluate(()=>window.__notificationChanges);await run('Insert','Cross');
  await a.waitForTimeout(6500);assert.equal(await count(a),initialA+1);assert.equal(await count(b),initialB);
  assert.equal(await a.evaluate(()=>window.__notificationChanges),pulses);await expect(notice(a)).toBeHidden();
 });
 await test('read state propagates to badge; reload snapshot does not repeat notification popup',async()=>{
  await run('Read');await expect.poll(()=>count(a),{timeout:20000}).toBe(initialA);
  await a.reload();await expect.poll(()=>a.evaluate(()=>window.__notificationChanges),{timeout:60000}).toBeGreaterThan(0);
  assert.equal(await count(a),initialA);await expect(notice(a)).toHaveCount(0);
  await run('Insert');await expect.poll(()=>count(a),{timeout:20000}).toBe(initialA+1);await expect(notice(a)).toBeVisible();
  await a.setViewportSize({width:390,height:844});assert.equal(await a.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  await a.screenshot({path:directory+'/realtime-recipient-mobile.png',fullPage:true});
 });
 await test('revoked browser session receives no further events and business API rejects access',async()=>{
  await a.getByRole('button',{name:'Đóng thông báo',exact:true}).click();
  await run('Revoke');const deniedNotification=(await run('Insert')).id;await a.waitForTimeout(6500);
  const denied=await a.evaluate(async()=>{const r=await fetch('/api/v2/auth/me',{headers:{Authorization:'Bearer '+sessionStorage.getItem('hotelops.compact.session')}});return r.status;});
  assert.equal(denied,401);assert.ok(!a.notificationFrames.some(f=>f.notificationIds.includes(deniedNotification)),'Post-revocation notification must never reach stream');
  await expect(notice(a)).toBeHidden();assert.deepEqual(errors,[]);
 });
}catch(e){if(!tests.some(t=>t.status==='FAIL'))tests.push({name:'setup',status:'FAIL',error:String(e.message).replace(/[a-f0-9]{64}/ig,'[redacted]')});process.exitCode=1;}
finally{
 if(browser)await browser.close();
 if(started)try{await run('Stop');}catch(e){tests.push({name:'Stop',status:'FAIL',error:e.message});process.exitCode=1;}
 if(owned)try{cleanup=await run('Cleanup');}catch(e){tests.push({name:'Cleanup',status:'FAIL',error:e.message});process.exitCode=1;}
 await writeFile(directory+'/realtime-browser-results.json',JSON.stringify({at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',transport:'Actual SignalR LongPolling via browser route proxy',limits:'Notification inserts/read state are TEST SQL fixtures, not full business UI triggers; preexisting scoped sessions, no password login. Timed waits cover negative/dedup assertions only.',cleanup,tests},null,2));
 console.log(JSON.stringify({passed:tests.filter(t=>t.status==='PASS').length,failed:tests.filter(t=>t.status==='FAIL').length,cleanup}));
}
