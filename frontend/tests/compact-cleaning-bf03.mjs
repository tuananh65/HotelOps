import assert from 'node:assert/strict';
import fs from 'node:fs';
import {randomUUID} from 'node:crypto';
import {chromium,expect} from '@playwright/test';
const fixture=JSON.parse(fs.readFileSync(process.env.HOTELOPS_CLEANING_SESSION||'../deployment/private/cleaning-bf03-http-session.json','utf8'));
const base=process.env.HOTELOPS_CLEANING_API||'http://127.0.0.1:5082';
assert.equal(new URL(base).port,'5082','Use isolated API only');
const workId=Number(process.env.HOTELOPS_CLEANING_WORK_ID||fixture.workId||690);
const out='../artifacts/cleaning-bf03';fs.mkdirSync(out,{recursive:true});
const results=[];
function pass(name){results.push({name,status:'PASS'});console.log('PASS '+name);fs.writeFileSync(out+'/results.json',JSON.stringify(results,null,2));}
async function http(path,body,token=fixture.staffToken){const r=await fetch(base+'/api/v2/'+path,{method:body===undefined?'GET':'POST',headers:{Authorization:'Bearer '+token,...(body!==undefined&&!(body instanceof FormData)?{'Content-Type':'application/json'}:{})},body:body===undefined?undefined:body instanceof FormData?body:JSON.stringify(body)});return {status:r.status,body:await r.json().catch(()=>null)};}
async function detail(id,token){const r=await http('work/'+id,undefined,token);assert.equal(r.status,200,JSON.stringify(r.body));return r.body;}
async function action(id,command,extra={}){const d=await detail(id);return http(`work/${id}/actions/${command}`,{rowVersion:d.work.fields.rowVersion,operationKey:randomUUID(),...extra});}
async function upload(id,bytes,name='room.png',token=fixture.staffToken){const body=new FormData();body.set('branchId',String(fixture.branchId));body.set('workId',String(id));body.set('file',new Blob([bytes],{type:'image/png'}),name);return http('evidence',body,token);}
const browser=await chromium.launch({executablePath:process.env.HOTELOPS_BROWSER_PATH||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[];
try{
 const context=await browser.newContext({viewport:{width:1440,height:1000}});
 await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.staffToken);
 await context.route('**/api/v2/**',async route=>{const u=new URL(route.request().url());try{const r=await route.fetch({url:base+u.pathname+u.search});await route.fulfill({response:r});}catch{await route.abort().catch(()=>{});}});
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:5173/#workday');await expect(page.getByRole('heading',{name:'Lịch làm việc',exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'Bắt đầu ca',exact:true})).toHaveCount(0);pass('No start shift button');
 const png=await page.screenshot({path:out+'/proof-photo.png'});
 let d=await detail(workId);
 if(d.work.fields.status==='ON_HOLD'){const resumed=await action(workId,'resume');assert.equal(resumed.status,200,JSON.stringify(resumed));}
 d=await detail(workId);assert.equal(d.work.fields.status,'IN_PROGRESS');
 fs.writeFileSync(out+'/started-detail.json',JSON.stringify(d,null,2));
 const noPhoto=await action(workId,'complete');assert.equal(noPhoto.status,400);assert.equal(noPhoto.body.title,'CLEANING_PHOTO_REQUIRED');pass('Completion requires photo');
 const wrong=await upload(workId,png,'room.png',fixture.supervisorToken);assert.ok([403,404].includes(wrong.status),JSON.stringify(wrong));pass('Non-assignee upload rejected');
 for(const bytes of [Buffer.from('bad file'),Buffer.from([137,80,78,71,13,10,26,10,0])]){const bad=await upload(workId,bytes);assert.equal(bad.status,400,JSON.stringify(bad));}pass('Invalid and truncated image rejected');
 await page.goto(`http://127.0.0.1:5173/#work?kind=CLEANING&id=${workId}`);
 await expect(page.getByRole('dialog')).toBeVisible();
 fs.writeFileSync(out+'/staff-text.txt',await page.locator('body').innerText());
 await page.getByRole('button',{name:'Gửi hoàn thành',exact:true}).click();
 const input=page.locator('input[type=file]').first();await expect(input).toBeVisible();
 await input.setInputFiles({name:'first.png',mimeType:'image/png',buffer:png});await expect(page.locator('form .cleaning-photo-grid figure')).toHaveCount(1);
 await page.locator('.cleaning-photo-actions input').setInputFiles({name:'replacement.png',mimeType:'image/png',buffer:png});await expect(page.locator('form .cleaning-photo-grid')).toContainText('replacement.png');
 await page.getByRole('button',{name:'Xóa ảnh',exact:true}).click();await expect(page.locator('form .cleaning-photo-grid figure')).toHaveCount(0);
 await input.setInputFiles({name:'final.png',mimeType:'image/png',buffer:png});pass('Browser preview replace remove');
 for(const width of [1440,390]){await page.setViewportSize({width,height:1000});await page.screenshot({path:out+`/completion-${width}.png`,fullPage:true});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`overflow ${width}`);}pass('Desktop and mobile no horizontal overflow');
 await expect(page.locator('.room-supply-assessment')).toHaveCount(0);pass('Legacy completion has no manual material verification');
 const photo=await upload(workId,png);assert.equal(photo.status,200,JSON.stringify(photo));const photoId=photo.body.fields.attachmentId;
 d=await detail(workId);const body={rowVersion:d.work.fields.rowVersion,operationKey:randomUUID(),attachmentIds:[photoId],reason:'BF03 verified room photo'};
 const completed=await Promise.all([http(`work/${workId}/actions/complete`,body),http(`work/${workId}/actions/complete`,body)]);assert.ok(completed.some(r=>r.status===200),JSON.stringify(completed));assert.ok(completed.every(r=>[200,409].includes(r.status)),JSON.stringify(completed));
 const retry=await http(`work/${workId}/actions/complete`,body);assert.equal(retry.status,200,JSON.stringify(retry));
 d=await detail(workId);assert.equal(d.work.fields.status,'PENDING_INSPECTION');assert.equal(d.events.filter(e=>e.fields.eventType==='COMPLETE').length,1);pass('Concurrent completion and retry produce one completion');
 const invalid=await http(`evidence/${photoId}/invalidate`,{rowVersion:photo.body.fields.rowVersion,operationKey:randomUUID(),reason:'Attempt invalidate submitted photo'});assert.ok([400,403,409].includes(invalid.status),JSON.stringify(invalid));pass('Submitted photo immutable');
 const supervisor=await detail(workId,fixture.supervisorToken);assert.ok(supervisor.attachments.some(a=>a.attachmentId===photoId));const download=await fetch(`${base}/api/v2/evidence/${photoId}`,{headers:{Authorization:'Bearer '+fixture.supervisorToken}});assert.equal(download.status,200);assert.deepEqual(Buffer.from(await download.arrayBuffer()),png);pass('Supervisor reads original photo');
 await context.unrouteAll({behavior:'ignoreErrors'});await context.close();
 assert.deepEqual(errors,[]);pass('No browser page errors');
 fs.writeFileSync(out+'/completed-detail.json',JSON.stringify(d,null,2));
}catch(e){results.push({name:e.message,status:'FAIL'});fs.writeFileSync(out+'/results.json',JSON.stringify(results,null,2));throw e;}finally{await browser.close();}







