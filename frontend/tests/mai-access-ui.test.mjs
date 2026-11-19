import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import { build } from 'esbuild';
import { chromium } from '@playwright/test';

const bundle = await build({ stdin: { resolveDir: process.cwd(), loader: 'tsx', contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter,Routes,Route} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider';
import {PendingInspectionsPage} from './src/features/inspectionExecution/PendingInspectionsPage';
import {InitialInspectionAssignmentPage} from './src/features/inspectionExecution/InitialInspectionAssignmentPage';
import {HistoryWorkspace} from './src/features/inspectionHistory/HistoryWorkspace';
import {TaskReadPage} from './src/operationalTasks/TaskReadPage';
import CleaningAssignment from './src/screens/housekeeping/CleaningAssignmentScreen';
import './src/styles.css'; import './src/styles/cacao.css'; import './src/styles/design-system.css';
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider><main className="cacao-content"><Routes>
<Route path="/inspections/assignment" element={<PendingInspectionsPage/>}/>
<Route path="/inspections/cleaning-tasks/:taskId/assign" element={<InitialInspectionAssignmentPage/>}/>
<Route path="/history" element={<HistoryWorkspace branch={1} onDenied={()=>{}}/>}/>
<Route path="/operational-tasks" element={<TaskReadPage/>}/>
<Route path="/cleaning/assignment" element={<CleaningAssignment/>}/>
</Routes></main></AuthProvider></BrowserRouter>);` }, bundle: true, write: false, outfile: 'mai.js', format: 'iife', jsx: 'automatic', define: { 'process.env.NODE_ENV': '"development"', 'import.meta.env': '{}' } });
let browser;
before(async () => { browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true }); mkdirSync('../artifacts/mai-tran-access-review/screenshots', {recursive:true}); });
after(async () => browser?.close());
const tag='"0000000000000001"';
async function fixture(path, options, check) {
 const p=await browser.newPage({viewport:{width:options.mobile?390:1280,height:900}});
 const calls=[],errors=[];p.on('pageerror',e=>errors.push(e.message));p.setDefaultTimeout(10000);
 const row=(id,inspector)=>({id,number:`INS-${id}`,roomId:id,roomNumber:`10${id}`,branchId:1,cleaningTaskId:id,versionId:1,versionNumber:1,inspectorId:inspector,inspectorName:inspector===2?'Trần Thị Mai':'Người kiểm tra khác',status:'IN_PROGRESS',result:null,at:'2026-11-19T10:00:00Z',summary:null,reworkCount:0});
 await p.route('https://mai.test/**',route=>{
  const u=new URL(route.request().url()),path=u.pathname;calls.push(path);
  const json=(body,status=200,headers={})=>route.fulfill({status,contentType:'application/json',headers,body:JSON.stringify(body)});
  if(route.request().resourceType()==='document')return route.fulfill({contentType:'text/html',body:'<html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><div id="root"></div></html>'});
  if(path==='/api/auth/session')return json({user:{id:'2',username:'mai.tran',name:'Trần Thị Mai'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
  if(path==='/api/auth/navigation/operation-grants')return json({grants:(options.noPerform?['INSPECTION_VIEW']:['INSPECTION_VIEW','INSPECTION_PERFORM','CLEANING_VIEW','CLEANING_ASSIGN','TASK_VIEW','TASK_EXECUTE']).map(permission=>({permission,branchId:1,scope:permission==='TASK_EXECUTE'?'OWN':'BRANCH'}))});
  if(path==='/api/inspections/assignment-branches')return json(options.denied?[]:[{id:1,name:'Bùi Viện',isPrimary:true}]);
  if(path==='/api/branches/1/inspections/pending-assignment')return json({items:[{taskId:9,roomId:1,roomNumber:'101',createdAt:'2026-11-19T10:00:00Z'}],totalCount:1,page:1,pageSize:25});
  if(path==='/api/branches/1/cleaning-tasks/9/initial-inspection-options')return json({taskId:9,branchId:1,roomId:1,roomNumber:'101',cycleETag:tag,taskETag:tag,checklistVersionId:1,checklistName:'Tiêu chuẩn phòng',versionNo:1,existingInspectorId:options.assign?null:options.mine?2:8,existingInspectionId:options.assign?null:7,inspectors:options.assign?[{id:2,name:'Trần Thị Mai'}]:[],canAssign:!!options.assign,unavailableReason:options.assign?null:'ALREADY_ASSIGNED'},200,{etag:tag});
  if(path==='/api/inspection-history')return json({items:[row(1,2),row(2,8)],total:2,filters:{branchId:1,page:1,pageSize:20,roomId:null,inspectorId:null,housekeeperId:null,from:null,to:null,result:null,search:null}});
  if(path==='/api/operational-tasks/branches')return json([{id:1,name:'Bùi Viện',isPrimary:true,canCreate:false,canViewWorkload:false,canManageSeries:false}]);
  if(path==='/api/branches/1/operational-tasks')return json({items:u.searchParams.get('status')==='COMPLETED'?[]:[{id:3,code:'OT-3',branchId:1,departmentId:1,title:'Kiểm tra cuối ca',status:'TODO',progressPercent:0,dueAt:null,workflowStatus:null}],totalCount:u.searchParams.get('status')==='COMPLETED'?0:1,page:1,pageSize:25});
  if(path==='/api/branches/1/operational-tasks/3')return json({task:{id:3,code:'OT-3',branchId:1,departmentId:1,title:'Kiểm tra cuối ca',status:'TODO',progressPercent:0,dueAt:null},description:null,completionNote:null,progress:[],progressTotalCount:0,canExecute:false});
  if(path==='/api/rooms/workspace-options')return json([{id:1,name:'Bùi Viện',isPrimary:true,canViewRooms:true,canMonitor:true,canCreate:!options.denied,canReadOwn:true}]);
  if(path.endsWith('/cleaning-monitor/options'))return json({floors:[],assignees:[],priorities:[],statuses:['IN_PROGRESS']});
  if(path.endsWith('/cleaning-monitor'))return json({items:[{kind:'cleaning',id:9,cleaningTaskId:9,taskNumber:'VS-009',roomId:1,roomNumber:'101',floorId:1,floorName:'Tầng 1',recordedStatus:'IN_PROGRESS',workflowState:null,priorityId:null,priorityCode:null,assigneeId:7,assigneeName:'Nhân viên vệ sinh',dueAtUtc:null,isOverdue:null,progressPercent:0,blockerCode:null,blockerNote:null,description:null}],total:1,page:1,pageSize:20,fromDate:'2026-11-19',toDate:'2026-11-19',timezone:'Asia/Ho_Chi_Minh',asOfUtc:'2026-11-19T05:00:00Z'});
  return json({},403);
 });
 try {await p.goto('https://mai.test'+path);for(const f of bundle.outputFiles.filter(f=>f.path.endsWith('.css')))await p.addStyleTag({content:f.text});await p.addScriptTag({content:bundle.outputFiles.find(f=>f.path.endsWith('.js')).text});await check(p,calls);assert.deepEqual(errors,[]);}finally{await p.close();}
}
test('inspection menu entry selects authorized branch and reaches existing assignment screen',async()=>fixture('/inspections/assignment',{mobile:true},async(p,calls)=>{
 await p.getByRole('button',{name:'Phân công kiểm tra',exact:true}).waitFor(); assert.ok(calls.includes('/api/inspections/assignment-branches'));
 assert.equal(new URL(p.url()).searchParams.get('branchId'),'1');
 assert.ok(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
 await p.screenshot({path:'../artifacts/mai-tran-access-review/screenshots/inspection-mobile.png',fullPage:true});
}));
test('denied assignment entry has no task links or queue request',async()=>fixture('/inspections/assignment',{denied:true},async(p,calls)=>{
 await p.getByText('Chưa có chi nhánh được cấp quyền sử dụng chức năng này.').waitFor();
 assert.equal(calls.some(x=>x.includes('pending-assignment')),false);assert.equal(await p.getByRole('button',{name:'Phân công kiểm tra',exact:true}).count(),0);
}));
for(const mine of [false,true])test(`inspection execution link respects assigned inspector: mine=${mine}`,async()=>fixture('/inspections/cleaning-tasks/9/assign?branchId=1',{mine},async(p)=>{
 await p.getByRole('heading',{name:'Phòng 101'}).waitFor();await p.waitForFunction(()=>document.body.innerText.includes('Đã có phân công kiểm tra'));
 if(mine)await p.getByRole('link',{name:'Mở kiểm tra',exact:true}).waitFor();
 assert.equal(await p.getByRole('link',{name:'Mở kiểm tra',exact:true}).count(),mine?1:0);
}));
test('history offers execution only for own open inspection',async()=>fixture('/history',{},async(p)=>{
 await p.getByRole('button',{name:'INS-2',exact:true}).waitFor();await p.getByRole('link',{name:'Thực hiện kiểm tra'}).waitFor();
 assert.equal(await p.getByRole('link',{name:'Thực hiện kiểm tra'}).count(),1);assert.equal(await p.getByRole('link',{name:'Thực hiện kiểm tra'}).getAttribute('href'),'/inspections/1/1/execution');
}));
test('inspection viewer without perform permission gets no execution link',async()=>fixture('/history',{noPerform:true},async(p)=>{
 await p.getByRole('button',{name:'INS-1',exact:true}).waitFor();assert.equal(await p.getByRole('link',{name:'Thực hiện kiểm tra'}).count(),0);
}));
test('task execution OWN does not expose editor for somebody else task',async()=>fixture('/operational-tasks?branchId=1&taskId=3',{},async(p,calls)=>{
 await p.getByRole('heading',{name:'Kiểm tra cuối ca'}).waitFor();await p.getByText('Bạn đang xem và theo dõi công việc.',{exact:false}).waitFor();
 assert.equal(await p.getByRole('heading',{name:'Cập nhật tiến độ',exact:true}).count(),0);assert.equal(calls.some(x=>x.includes('/progress')),false);
 assert.equal(await p.getByRole('button',{name:'Xác minh công việc'}).count(),0);
}));
for(const denied of [false,true])test(`cleaning assignment menu starts with named task selection; denied=${denied}`,async()=>fixture('/cleaning/assignment',{denied},async(p)=>{
 await p.getByRole('link',{name:'VS-009',exact:true}).waitFor();assert.equal(await p.getByLabel('Công việc (ID)',{exact:true}).count(),0);
 assert.equal(await p.getByRole('link',{name:'Phân công / phân công lại',exact:true}).count(),denied?0:1);
}));

test('queue assigns inside popup and close stays on queue',()=>fixture('/inspections/assignment',{assign:true},async p=>{await p.getByRole('button',{name:'Phân công kiểm tra',exact:true}).click();const d=p.getByRole('dialog');await d.getByLabel('Người kiểm tra',{exact:true}).waitFor();await d.getByRole('button',{name:'Đóng',exact:true}).click();assert.equal(new URL(p.url()).pathname,'/inspections/assignment');await p.getByRole('button',{name:'Phân công kiểm tra',exact:true}).waitFor();}));
test('recorded task status filters legacy rows even without workflow projection',()=>fixture('/operational-tasks',{},async p=>{
 await p.getByRole('listitem').getByText('Chưa bắt đầu',{exact:true}).waitFor();await p.getByRole('button',{name:'Bộ lọc',exact:true}).click();await p.getByLabel('Trạng thái công việc',{exact:true}).selectOption('TODO');
 const sent=p.waitForRequest(r=>new URL(r.url()).searchParams.get('status')==='TODO');await p.getByRole('button',{name:'Áp dụng trạng thái',exact:true}).click();const url=new URL((await sent).url());assert.equal(url.searchParams.has('workflowStatus'),false);await p.getByRole('listitem').getByText('Chưa bắt đầu',{exact:true}).waitFor();
}));
