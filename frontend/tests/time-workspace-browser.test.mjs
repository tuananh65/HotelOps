import {test,before,after} from 'node:test';
import assert from 'node:assert/strict';
import {mkdirSync} from 'node:fs';
import {build} from 'esbuild';
import {chromium} from '@playwright/test';
const bundle=await build({stdin:{resolveDir:process.cwd(),loader:'tsx',contents:`
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import './src/styles.css';import './src/styles/cacao.css';
import {AuthProvider} from './src/auth/AuthProvider';import {createAuthStore} from './src/auth/authStore';import {createAuthApi} from './src/auth/authApi';
import {WorkdayRoute} from './src/workday/WorkdayRoute';import {MyTimeLogPage} from './src/workTime/MyTimeLogPage';import {TimeCorrectionsPage} from './src/workTime/TimeCorrectionsPage';
const s=window.scenario,calls=[];window.calls=calls;
const correction={correctionId:5,branchId:1,departmentId:2,intervalId:7,employeeId:8,kind:'MAINTENANCE',workItemId:14,status:'PENDING',requestedAt:'2026-11-18T03:00:00Z',originalStart:'2026-11-18T01:00:00Z',originalEnd:'2026-11-18T02:00:00Z',proposedStart:'2026-11-18T01:10:00Z',proposedEnd:'2026-11-18T02:00:00Z',reason:'Quên dừng thời gian công việc',appliedStart:null,appliedEnd:null,decidedBy:null,decidedAt:null,decisionReason:null,version:'AAAAAAAAAAE=',canDecide:!!s.manager};
window.fetch=async(url,o={})=>{
 calls.push({url,method:o.method??'GET',body:o.body,headers:o.headers});
 const u=new URL(url,location.origin);
 if(url==='/api/auth/session')return Response.json({user:{id:'8',username:'thinh.ngo',name:'Ngô Đức Thịnh'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
 if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'test'});
 if(url==='/api/workdays/workspace-options')return s.scopeError?Response.json({}, {status:503}):Response.json(s.noScope?[]:[{branchId:1,name:'Khách sạn Đức Vượng - Chi nhánh Bùi Viện',isPrimary:true,canReadShifts:!s.noShifts,canReadOwnTime:true,correctionDepartments:s.manager?[{id:2,name:'Kỹ thuật - Bảo trì'}]:[]}]);
 if(url.endsWith('/own-shifts'))return Response.json([{shiftAssignmentId:10,shiftId:5,branchId:1,departmentId:2,shiftDate:'2026-11-18',scheduledStartAtUtc:'2026-11-17T23:00:00Z',scheduledEndAtUtc:'2026-11-18T07:00:00Z'}]);
 if(url.endsWith('/current'))return s.working?Response.json({workdayId:20,branchId:1,departmentId:2,workDate:'2026-11-18',shiftAssignmentId:10,isUnscheduled:false,startedAtUtc:'2026-11-18T01:00:00Z',rowVersion:'AAAAAAAAAAE=',openInterval:null}):new Response(null,{status:204});
 if(url.includes('/own-time-log?'))return s.denied?Response.json({}, {status:403}):Response.json({branchId:1,date:u.searchParams.get('date'),asOf:'2026-11-18T05:00:00Z',page:Number(u.searchParams.get('page')),pageSize:50,total:s.empty?0:1,workdayClosedSeconds:0,workdayProvisionalSeconds:14400,workClosedSeconds:3600,workProvisionalSeconds:0,items:s.empty?[]:[{intervalId:7,kind:'MAINTENANCE',workItemId:14,start:'2026-11-18T01:00:00Z',end:s.open?null:'2026-11-18T02:00:00Z',closedSeconds:3600,provisionalSeconds:0,version:'AAAAAAAAAAE='}]});
 if(u.pathname==='/api/branches/1/time-corrections')return Response.json({branchId:1,departmentId:u.searchParams.has('departmentId')?Number(u.searchParams.get('departmentId')):null,page:1,pageSize:50,total:s.empty?0:1,items:s.empty?[]:[correction]});
 if(u.pathname==='/api/branches/1/time-corrections/5')return Response.json({correction,otherIntervals:[]},{headers:{ETag:'"AAAAAAAAAAE="'}});
 if(o.method==='POST'&&url.includes('/time-corrections/'))return s.stale?Response.json({title:'TIME_STALE'},{status:412}):Response.json({correction:{...correction,status:url.endsWith('/decision')?'APPLIED':'PENDING'}});
 return Response.json({}, {status:403});
};
const store=createAuthStore(createAuthApi(window.fetch));window.store=store;
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store}><div className="cacao-content">{s.page==='workday'?<WorkdayRoute/>:s.page==='log'?<MyTimeLogPage/>:<TimeCorrectionsPage/>}</div></AuthProvider></BrowserRouter>);
`},bundle:true,write:false,outfile:'bundle.js',format:'iife',jsx:'automatic',define:{'process.env.NODE_ENV':'"production"','import.meta.env':'{}'}});
let browser;
before(async()=>{browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});mkdirSync('../artifacts/time-workspace-fix/screenshots',{recursive:true});});
after(async()=>browser?.close());
async function run(scenario,check,width=1200){const page=await browser.newPage({viewport:{width,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(6000);
 try{await page.route('**/*',r=>r.request().resourceType()==='document'?r.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>'}):r.abort());
 await page.goto('https://local.test/'+(scenario.page==='workday'?'workday':scenario.page==='log'?'my-time-log':'time-corrections')+(scenario.query??''));await page.evaluate(s=>window.scenario=s,scenario);
 for(const f of bundle.outputFiles)if(f.path.endsWith('.css'))await page.addStyleTag({content:f.text});await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});
 await check(page);assert.deepEqual(errors,[]);
 }catch(e){console.log((await page.locator('body').innerText()).slice(0,2200));throw e;}finally{await page.close();}}
const posts=p=>p.evaluate(()=>window.calls.filter(c=>c.method==='POST'));
test('workday automatically loads own branch, formats shifts in Vietnam time and never mutates',()=>run({page:'workday',working:true},async p=>{
 await p.getByText('Đang làm việc',{exact:true}).waitFor();assert.match(await p.locator('body').innerText(),/06:00/);assert.equal(await p.getByLabel('Mã chi nhánh').count(),0);assert.equal((await posts(p)).length,0);
 await p.screenshot({path:'../artifacts/time-workspace-fix/screenshots/workday.png',fullPage:true});
}));
test('time log auto-loads own data, date changes reload and denied reads clear results',()=>run({page:'log'},async p=>{
 await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).waitFor();await p.getByRole('button',{name:/^Bộ lọc/}).click(); await p.getByLabel('Ngày làm việc',{exact:true}).fill('2026-11-17');
 await p.waitForFunction(()=>window.calls.some(c=>c.url.includes('date=2026-11-17')));await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).waitFor();
 await p.screenshot({path:'../artifacts/time-workspace-fix/screenshots/time-log.png',fullPage:true});await p.evaluate(()=>window.scenario.denied=true);await p.getByRole('button',{name:'Tải lại nhật ký',exact:true}).click();await p.getByText('Bạn không còn quyền xem nhật ký thời gian chi nhánh này.',{exact:true}).waitFor();assert.equal(await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).count(),0);
}));
test('employee sees only own corrections and no department ID or decision action',()=>run({},async p=>{
 await p.getByRole('button',{name:'Xem đề nghị điều chỉnh',exact:true}).click();await p.getByRole('heading',{name:'Chi tiết đề nghị điều chỉnh',exact:true}).waitFor();assert.equal(await p.getByLabel('Phạm vi xem').count(),0);assert.equal(await p.getByText('TIME_CORRECTION_DECIDE').count(),0);assert.equal(await p.getByRole('button',{name:'Áp dụng điều chỉnh'}).count(),0);
 await p.screenshot({path:'../artifacts/time-workspace-fix/screenshots/correction-detail.png',fullPage:true});
}));
test('authorized manager uses named department and stale decision sends once',()=>run({manager:true,stale:true},async p=>{
 await p.getByRole('button',{name:/^Bộ lọc/}).click(); await p.getByLabel('Phạm vi xem',{exact:true}).selectOption('2');await p.getByRole('button',{name:'Xem đề nghị điều chỉnh',exact:true}).click();await p.getByRole('button',{name:'Xét duyệt đề nghị điều chỉnh',exact:true}).click();await p.getByRole('button',{name:'Áp dụng điều chỉnh',exact:true}).click();
 await p.getByRole('dialog').getByText('Dữ liệu đã thay đổi. Tải lại trước khi gửi đề nghị hoặc xét duyệt.',{exact:true}).waitFor();assert(await p.getByRole('button',{name:'Áp dụng điều chỉnh',exact:true}).isDisabled());const sent=await posts(p);assert.equal(sent.length,1);assert.equal(sent[0].headers['If-Match'],'"AAAAAAAAAAE="');assert.equal(JSON.parse(sent[0].body).start,'2026-11-18T01:10:00.000Z');
}));
test('closed interval correction sends correct UTC, CSRF and version',()=>run({page:'log'},async p=>{
 await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).click();await p.getByLabel('Lý do',{exact:true}).fill('Quên dừng bộ đếm khi kết thúc');await p.getByRole('button',{name:'Gửi điều chỉnh',exact:true}).click();
 await p.getByText('Đã gửi đề nghị điều chỉnh.',{exact:false}).waitFor();const sent=await posts(p);assert.equal(sent.length,1);assert.equal(sent[0].headers['X-CSRF-TOKEN'],'test');assert.equal(JSON.parse(sent[0].body).start,'2026-11-18T01:00:00.000Z');
}));
test('open interval cannot submit correction',()=>run({page:'log',open:true},async p=>{
 await p.getByText('Có thể đề nghị sau khi kết thúc',{exact:true}).waitFor();assert.equal(await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).count(),0);
}));
test('foreign branch URL falls back to allowed branch and never reads foreign correction',()=>run({query:'?branchId=999&correctionId=5'},async p=>{
 await p.getByRole('button',{name:'Xem đề nghị điều chỉnh',exact:true}).waitFor();const calls=await p.evaluate(()=>window.calls);assert(!calls.some(c=>c.url.includes('/branches/999/')));assert(!calls.some(c=>c.url.endsWith('/time-corrections/5')));
}));
test('empty corrections guide employee to the time log; mobile does not overflow',()=>run({empty:true},async p=>{
 await p.getByText('Chưa có đề nghị điều chỉnh trong phạm vi này.',{exact:true}).waitFor();assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await p.screenshot({path:'../artifacts/time-workspace-fix/screenshots/corrections-mobile.png',fullPage:true});
},390));
test('scope unavailable offers retry without loading work data',()=>run({page:'log',scopeError:true},async p=>{
 await p.getByRole('button',{name:'Thử lại',exact:true}).waitFor();assert(!(await p.evaluate(()=>window.calls)).some(c=>c.url.includes('own-time-log')));await p.evaluate(()=>window.scenario.scopeError=false);await p.getByRole('button',{name:'Thử lại',exact:true}).click();await p.getByRole('button',{name:'Đề nghị điều chỉnh',exact:true}).waitFor();
}));
test('no shift permission hides shift section without issuing its request',()=>run({page:'workday',noShifts:true},async p=>{
 await p.getByText('Bạn chưa bắt đầu ca hoặc đã kết thúc ca gần nhất.',{exact:true}).waitFor();assert(!(await p.evaluate(()=>window.calls)).some(c=>c.url.endsWith('/own-shifts')));
}));
test('correction list opens creation popup with eligible intervals without navigating away',()=>run({},async p=>{
 await p.getByRole('button',{name:'Tạo đề nghị điều chỉnh',exact:true}).click();
 const dialog=p.getByRole('dialog');await dialog.waitFor();
 await dialog.getByLabel('Khoảng thời gian đã kết thúc',{exact:true}).selectOption('7');
 await dialog.getByLabel('Lý do',{exact:true}).fill('Điều chỉnh thời gian bị ghi sai');
 assert.equal(await p.getByRole('dialog').count(),1);
 await dialog.getByRole('button',{name:'Gửi điều chỉnh',exact:true}).click();
 await dialog.waitFor({state:'hidden'});assert.equal((await posts(p)).length,1);
}));
test('empty correction creation remains visible and can change date without sending',()=>run({empty:true},async p=>{
 await p.getByRole('button',{name:'Tạo đề nghị điều chỉnh',exact:true}).click();
 const dialog=p.getByRole('dialog');await dialog.getByText('Chưa có khoảng thời gian đã kết thúc trong ngày này. Chọn ngày khác để tạo đề nghị.',{exact:true}).waitFor();
 await dialog.getByLabel('Ngày có thời gian cần điều chỉnh').fill('2026-11-17');
 assert.equal(await dialog.getByRole('button',{name:'Gửi điều chỉnh',exact:true}).count(),0);assert.equal((await posts(p)).length,0);
 const box=await dialog.boundingBox();assert(box.x>=0 && box.y>=0 && box.x+box.width<=390);
},390));
