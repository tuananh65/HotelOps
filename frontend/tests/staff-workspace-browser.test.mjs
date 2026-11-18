import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import { chromium } from '@playwright/test';
import { build } from 'esbuild';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {createAuthApi} from './src/auth/authApi';
import './src/styles.css'; import './src/styles/cacao.css';
import {TaskReadPage} from './src/operationalTasks/TaskReadPage'; import {InternalRequestIntakePage} from './src/internalRequests/IntakePage'; import {InternalRequestHistoryPage} from './src/internalRequests/HistoryPage';
const s=window.scenario, calls=[]; window.calls=calls;
const options={branches:[{id:1,name:'Chi nhánh Bùi Viện'}],types:[{id:1,name:'Hỗ trợ di chuyển đồ',materialNeed:false},{id:2,name:'Bổ sung vật dụng phòng',materialNeed:true}],departments:[{id:3,name:'Kỹ thuật'}],requesterDepartments:[{id:4,name:'Buồng phòng'}],priorities:[{id:6,name:'Bình thường'}],rooms:[{id:2,name:'201'}],areas:[{id:9,name:'Sảnh chính'}]};
window.fetch=async(url,o={})=>{
 calls.push({url,method:o.method??'GET',body:o.body});
 if(url==='/api/auth/session')return Response.json({user:{id:'6',username:'lan.dang',name:'Đặng Thị Lan'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
 if(url==='/api/auth/csrf')return Response.json({csrfToken:'test'});
 if(url.startsWith('/api/internal-requests/intake')){
  if(s.denied)return Response.json({title:'IR_CREATE_REQUIRED'},{status:403});
  if(s.unavailable)return Response.json({title:'IR_INTAKE_SCHEMA_REQUIRED'},{status:503});
  if(o.method==='POST')return Response.json({requestId:44,status:'PENDING_ACCEPTANCE',message:'MSG09'},{status:201});
  return Response.json(options);
 }
 if(url==='/api/operational-tasks/branches')return Response.json([{id:1,name:'Chi nhánh Bùi Viện',isPrimary:true,canCreate:false}]);
 if(url.startsWith('/api/branches/1/operational-tasks?'))return Response.json({items:[{id:7,code:'CV-007',branchId:1,departmentId:4,title:'Kiểm tra vật dụng phòng 201',status:'TODO',workflowStatus:'ASSIGNED',progressPercent:0,dueAt:'2026-11-18T07:00:00Z'}],totalCount:1,page:1,pageSize:25});
 if(url==='/api/internal-requests/options')return Response.json({statuses:['SUBMITTED'],types:[],priorities:[],departments:[]});
 if(url.startsWith('/api/internal-requests?'))return s.revoked?Response.json({}, {status:403}):Response.json({items:[{id:1,number:'IR-001',subject:'Hỗ trợ vận chuyển đồ',branchId:1,branchName:'Chi nhánh Bùi Viện',receivingDepartmentId:3,receivingDepartmentName:'Kỹ thuật',currentAssigneeId:null,currentAssigneeName:null,status:'SUBMITTED',deadline:'2026-11-18T07:00:00Z',overdue:false}],totalCount:1,page:1,pageSize:25,snapshotUtc:'2026-11-18T05:00:00Z'});
 return Response.json({}, {status:403});
};
const store=createAuthStore(createAuthApi(window.fetch));
const component=s.page==='tasks'?<TaskReadPage/>:s.page==='history'?<InternalRequestHistoryPage/>:<InternalRequestIntakePage/>;
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store}><div className="cacao-content">{component}</div></AuthProvider></BrowserRouter>);
` }, bundle: true, write: false, outfile: 'bundle.js', jsx: 'automatic', format: 'iife', define: {'process.env.NODE_ENV':'"production"', 'import.meta.env':'{}'} });
let browser;
before(async()=>{browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});mkdirSync('../artifacts/staff-workspace-fix/screenshots',{recursive:true});});
after(async()=>browser?.close());
async function run(scenario, check, width=1280){
 const page=await browser.newPage({viewport:{width,height:1000}}), errors=[];
 page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(6000);
 try{
  await page.route('**/*',r=>r.request().resourceType()==='document'?r.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>'}):r.abort());
  await page.goto('https://test.local/'+(scenario.page==='tasks'?'operational-tasks':scenario.page==='history'?'internal-requests':'internal-requests/new')+(scenario.query??''));
  await page.evaluate(s=>window.scenario=s,scenario);
  await page.addStyleTag({content:'body{margin:0;padding:24px;background:#faf6f1;font-family:system-ui}*{box-sizing:border-box}'});
  for(const f of bundle.outputFiles)if(f.path.endsWith('.css'))await page.addStyleTag({content:f.text});
  await page.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});
  await check(page);assert.deepEqual(errors,[]);
 }catch(e){console.log((await page.locator('body').innerText()).slice(0,3000));throw e;}finally{await page.close();}
}
test('tasks auto-load own branch without raw branch ID or unauthorized creation',()=>run({page:'tasks'},async p=>{
 await p.getByRole('button',{name:'CV-007: Kiểm tra vật dụng phòng 201',exact:true}).waitFor();
 assert.equal(await p.getByLabel('Mã chi nhánh').count(),0);
 assert.equal(await p.getByText('Tạo công việc',{exact:true}).count(),0);
 const calls=await p.evaluate(()=>window.calls);assert(calls.some(c=>c.url.startsWith('/api/branches/1/operational-tasks?')));assert(!calls.some(c=>c.method==='POST'));
 await p.screenshot({path:'../artifacts/staff-workspace-fix/screenshots/tasks.png',fullPage:true});
}));
test('tasks reject branch outside allowed options without reading that branch',()=>run({page:'tasks',query:'?branchId=999'},async p=>{
 await p.getByText('Chi nhánh Bùi Viện',{exact:true}).waitFor();
 assert(!(await p.evaluate(()=>window.calls)).some(c=>c.url.includes('/api/branches/999/')));
}));
test('history displays names, Vietnam time and clears rows after denied refresh',()=>run({page:'history'},async p=>{
 await p.getByRole('cell',{name:'Hỗ trợ vận chuyển đồ',exact:true}).waitFor();
 await p.getByRole('cell',{name:'Chi nhánh Bùi Viện',exact:true}).waitFor();await p.getByRole('cell',{name:'Kỹ thuật',exact:true}).waitFor();
 assert.match(await p.locator('tbody').innerText(),/14:00/);
 await p.screenshot({path:'../artifacts/staff-workspace-fix/screenshots/history.png',fullPage:true});
 await p.evaluate(()=>window.scenario.revoked=true);await p.getByRole('button',{name:'Tải lại lịch sử',exact:true}).click();
 await p.getByText('Bạn không có quyền xem lịch sử yêu cầu nội bộ.',{exact:false}).waitFor();assert.equal(await p.getByText('Hỗ trợ vận chuyển đồ',{exact:true}).count(),0);
}));
test('intake uses assigned branch and named room, sends correct IDs and UTC time',()=>run({},async p=>{
 await p.getByLabel('Loại yêu cầu',{exact:true}).selectOption('1');
 await p.getByLabel('Bộ phận tiếp nhận',{exact:true}).selectOption('3');await p.getByLabel('Độ ưu tiên',{exact:true}).selectOption('6');
 await p.getByLabel('Tiêu đề',{exact:true}).fill('Hỗ trợ di chuyển bàn');await p.getByLabel('Mô tả',{exact:true}).fill('Di chuyển bàn tại phòng 201.');
 await p.getByLabel('Thời hạn mong muốn (UTC+7)',{exact:true}).fill('2099-01-01T14:00');await p.getByLabel('Vị trí (không bắt buộc)').selectOption('room:2');
 await p.screenshot({path:'../artifacts/staff-workspace-fix/screenshots/intake.png',fullPage:true});
 await p.getByRole('button',{name:'Gửi yêu cầu',exact:true}).click();await p.getByText('MSG09: Đã tạo yêu cầu. Đang chờ tiếp nhận.',{exact:true}).waitFor();
 const posts=(await p.evaluate(()=>window.calls)).filter(c=>c.method==='POST');assert.equal(posts.length,1);const body=JSON.parse(posts[0].body);
 assert.equal(body.branchId,1);assert.equal(body.roomId,2);assert.equal(body.areaId,null);assert.equal(body.requesterDepartmentId,4);assert.equal(body.desiredDeadline,'2099-01-01T07:00:00.000Z');
}));
test('material request type routes to materials and prevents internal submit',()=>run({},async p=>{
 await p.getByLabel('Loại yêu cầu',{exact:true}).selectOption('2');await p.getByRole('link',{name:'Tạo yêu cầu vật tư',exact:true}).waitFor();assert(await p.getByRole('button',{name:'Gửi yêu cầu',exact:true}).isDisabled());
}));
test('denied intake distinguishes permission error from unavailable schema',async()=>{
 await run({denied:true},p=>p.getByText('Bạn chưa được cấp quyền tạo yêu cầu nội bộ trong chi nhánh này.',{exact:true}).waitFor());
 await run({unavailable:true},p=>p.getByText('Chức năng tạo yêu cầu chưa sẵn sàng do hệ thống chưa cập nhật dữ liệu cần thiết. Vui lòng liên hệ quản trị viên.',{exact:true}).waitFor());
});
test('mobile form fits viewport',()=>run({},async p=>{
 await p.getByLabel('Loại yêu cầu',{exact:true}).waitFor();assert(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 await p.screenshot({path:'../artifacts/staff-workspace-fix/screenshots/intake-mobile.png',fullPage:true});
},390));
