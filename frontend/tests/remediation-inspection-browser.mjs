import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium,expect} from '@playwright/test';
const origin=process.env.HK_UI_ORIGIN||'http://127.0.0.1:5174';
assert(['localhost','127.0.0.1'].includes(new URL(origin).hostname));
const out='../artifacts/remediation-inspection/inspection-ui';fs.mkdirSync(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const checks=[],errors=[];
try {
 for(const role of ['HOUSEKEEPING_SUPERVISOR']) for(const width of [1440,390]) {
  let status='ASSIGNED',incident=false,reworked=false,submitted=null,stage='ROOM_ISSUE_MATERIAL_REQUESTED';const requests=[];
  const context=await browser.newContext({viewport:{width,height:1000},ignoreHTTPSErrors:true});
  await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','housekeeping-ui-fixture'));
  const work=()=>({workId:7,kind:'CLEANING',title:'Dọn phòng 101',description:'Dọn phòng 101',status,roomId:1,branchId:1,createdBy:9,rowVersion:'AAAA',dueAt:'2026-11-24T13:00:00Z'});
  const supervisor=role==='HOUSEKEEPING_SUPERVISOR';
  await context.route('**/api/v2/**',async route=>{
   const url=new URL(route.request().url()),path=url.pathname.replace('/api/v2/','');requests.push({path,query:url.search});
   let json={};
   const perms=['CLEANING_VIEW','IR_CREATE','IR_VIEW','TASK_VIEW','STAFF_SUPPLY_VIEW',...(supervisor?['CLEANING_ASSIGN','INSPECTION_VIEW','INSPECTION_PERFORM','IR_PROCESS','DEFECT_RESOLVE']:['CLEANING_EXECUTE'])];
   if(path==='auth/me')json={user:{fields:{userId:1,fullName:'Kiểm thử vệ sinh'}},branches:[{branchId:1,name:'Bùi Viện',timezone:'Asia/Ho_Chi_Minh'}],roles:[{code:role,branchId:1}],memberships:[],grants:perms.map(code=>({code,branchId:1,dataScope:supervisor?'BRANCH':'OWN'}))};
   else if(path==='work/cleaning-queue'){const room={isRoom:true,workId:null,roomId:2,room:'102',roomType:'Tiêu chuẩn',capacity:2,rowVersion:'BBBB',status:'PENDING_CLEANING'};const q=url.searchParams.get('queue');json={items:q==='rooms'?[room]:q==='all'?[room,{...work(),room:'101',assignee:'Nhân viên thử'}]:[{...work(),room:'101',assignee:'Nhân viên thử'}],counts:[{status:'ASSIGNED',count:1},{status:'PENDING_CLEANING',count:1}],total:q==='all'?2:1};}
   else if(path==='work')json={items:[{fields:work()}],total:1};
   else if(path==='work/7')json={inspectionVariant:incident?'SC':reworked?'DL':null,hasOpenRoomIssues:incident,roomIssues:incident?[{workId:8,title:'Thiếu ấm đun',kind:'MISSING',status:'IN_PROGRESS',stage,readyForInspection:stage==='ROOM_ISSUE_MATERIAL_REQUESTED'}]:[],work:{fields:work()},assignments:[{fields:{assignmentId:1,assigneeUserId:supervisor?2:1,assignmentStatus:'ACTIVE',acceptedAt:status==='ASSIGNED'?null:'2026-11-24T12:01:00Z'}}],assignmentDetails:[{assignmentId:1,assignedBy:'Giám sát thử',assignee:'Nhân viên thử',assignedAt:'2026-11-24T12:00:00Z',acceptedAt:status==='ASSIGNED'?null:'2026-11-24T12:01:00Z',assignmentStatus:'ACTIVE'}],context:{branch:'Bùi Viện',floor:'Tầng 1',room:'101'},capabilities:{canConfirmRoomIssue:incident,canReportRoomIssue:supervisor?status==='PENDING_INSPECTION':status==='IN_PROGRESS',canRecordPaperInspection:supervisor&&(status==='PENDING_INSPECTION'||reworked)},events:[],reviews:reworked?[{result:'FAIL',verifiedAt:'2026-11-23T12:00:00Z',findings:[{status:'OPEN',description:'Bẩn'}]}]:[],attachments:[]};
   else if(path==='work/room-issues/8')json={work:{fields:{workId:8,kind:'INCIDENT',title:'Thiếu ấm đun',rowVersion:'v',status:'IN_PROGRESS'}},info:{kind:'MISSING'},room:{code:'101',rowVersion:'v'},stage,supervisor:true,canRequestInspection:stage==='ROOM_ISSUE_MATERIAL_REQUESTED',materials:[],maintenance:[],events:[]};
   else if(path==='requests/8')json={actions:stage==='ROOM_ISSUE_MATERIAL_REQUESTED'?[]:['claim']};
   else if(path==='work/room-issues/8/request-inspection'){stage='ROOM_ISSUE_INSPECTION_REQUESTED';json={};}
   else if(path==='work/7/inspection-result'){submitted=route.request().postDataJSON();json={};}
   else if(path==='work/7/related')json=reworked?[{fields:{workId:11,kind:'REWORK',status:'PENDING_REINSPECTION',completedAt:'2026-11-24T12:00:00Z'}}]:[];
   else if(path==='insights/time')json=[];
   else if(path==='work/candidates')json=[{userId:2,fullName:'Nhân viên thử'}];
   else if(path==='work/cleaning-creation-options')json={canCreate:true,candidates:[{userId:2,fullName:'Nhân viên thử',employeeCode:'TEST'}]};
   else if(path==='work/housekeeping-history')json={date:'2026-11-23',endDate:'2026-11-29',timezone:'Asia/Ho_Chi_Minh',items:[{...work(),status:'COMPLETED',performer:'Nhân viên thử',completedAt:'2026-11-24T12:00:00Z',room:'101'}],total:1};
   else if(path==='workspace/options')json={rooms:[{roomId:1,code:'101'}],departments:[],references:[]};
   else if(path.includes('notifications'))json=[];
   await route.fulfill({json});
  });
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  const root=supervisor?'work':'cleaning';
  await page.goto(`${origin}/login#${root}`);
  await expect(page.getByRole('link',{name:'Công việc hiện tại',exact:true})).toBeVisible();
  await expect(page.getByRole('link',{name:'Tình trạng phòng',exact:true})).toHaveCount(0);
  if(supervisor){
   const tabs=page.getByRole('group',{name:'Lọc hàng đợi'});
   await expect(tabs.getByRole('button').first()).toContainText('Tất cả');
   await expect(tabs.getByRole('button').nth(1)).toContainText('Phòng cần dọn');
   await expect(tabs.getByRole('button',{name:/Chưa phân công/})).toHaveCount(0);
   await expect(page.getByText('Phòng 102',{exact:true})).toBeVisible();
   await expect(page.getByRole('button',{name:'Tạo yêu cầu',exact:true})).toBeVisible();
   await tabs.getByRole('button',{name:/Đã giao/}).click();
   await expect.poll(()=>requests.some(r=>r.query.includes('queue=assigned'))).toBe(true);
   await expect(page.getByText('Đang xem trong phạm vi quyền',{exact:true})).toHaveCount(0);
  }else {
   assert(requests.some(r=>r.query.includes('cleaningCurrent=true')));
   for(const name of ['Công việc khác','Thông báo','Vật tư của tôi','Sự cố phòng','Lịch sử công việc'])await expect(page.locator('nav').getByRole('link',{name,exact:true})).toHaveCount(0);
   status='PENDING_INSPECTION';await page.reload();
   const badge=page.locator('.badge').filter({hasText:'Chờ kiểm tra'}).first();await expect(badge).toBeVisible();
   assert.equal(await badge.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(255, 228, 154)');status='ASSIGNED';
  }
  await page.screenshot({path:`${out}/${role}-${width}-current.png`,fullPage:true});
  if(supervisor){for(const name of ['Sự cố & Thông báo','Vật tư nhân viên','Lịch sử vệ sinh'])await expect(page.locator('.sidebar').getByRole('link',{name,exact:true})).toHaveCount(0);}
  if(supervisor){await page.goto(`${origin}/login#rooms`);await page.reload();await expect(page.getByRole('heading',{name:'Công việc hiện tại',exact:true})).toBeVisible();assert(!requests.some(r=>r.path==='rooms'));}
  await page.goto(`${origin}/login#${root}?kind=CLEANING&id=7`);await page.reload();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('link',{name:'Báo sự cố',exact:true})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Hủy công việc',exact:true})).toHaveCount(0);
  await expect(page.getByRole('heading',{name:'Minh chứng',exact:true})).toHaveCount(0);
  await expect(page.getByText(/Người nhận: Nhân viên thử/)).toBeVisible();
  if(supervisor){
   status='REWORK_REQUIRED';await page.reload();
   await page.getByRole('button',{name:'Tạo việc dọn lại',exact:true}).click();
   const dialog=page.getByRole('dialog').last();
   await expect(dialog.getByRole('radio')).toHaveCount(3);
   await expect(dialog.getByLabel('Nhân viên dọn phòng')).toBeVisible();
   await dialog.getByRole('button',{name:/Đóng/}).click();
   await expect(page.getByRole('heading',{name:'Công việc hiện tại',exact:true})).toBeVisible();
   status='PENDING_INSPECTION';await page.reload();
   await expect(page.getByRole('button',{name:'Xác nhận hoàn thành',exact:true})).toBeVisible();
  }else {await expect(page.getByRole('dialog').getByText('Chờ nhận',{exact:true})).toBeVisible();status='IN_PROGRESS';await page.reload();await expect(page.getByRole('button',{name:'Tạm dừng',exact:true})).toHaveCount(0);}
  reworked=true;status='REWORK_REQUIRED';await page.reload();
  const dl=page.getByRole('dialog').locator('.badge').filter({hasText:'Chờ kiểm tra - dl'});await expect(dl).toBeVisible();assert.equal(await dl.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(238, 224, 214)');
  await page.getByRole('button',{name:'Xác nhận hoàn thành',exact:true}).click();const form=page.getByRole('dialog').last();await expect(form.getByRole('checkbox')).toHaveCount(0);await expect(form.getByLabel(/Ghi chú/)).not.toHaveAttribute('required');await form.getByRole('button',{name:'Lưu kết quả',exact:true}).click();await expect.poll(()=>submitted?.result).toBe('PASS');assert.equal(submitted.note,null);assert.equal(submitted.resolveFindings,undefined);reworked=false;status='PENDING_INSPECTION';await page.reload();
  const report=page.getByRole('link',{name:'Báo sự cố',exact:true});await expect(report).toBeVisible();
  assert.equal(await report.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(182, 74, 7)');
  incident=true;await page.reload();const sc=page.getByRole('dialog').locator('.badge').filter({hasText:'Chờ kiểm tra - sc'});await expect(sc).toBeVisible();assert.equal(await sc.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(255, 240, 217)');await page.getByRole('button',{name:'Kiểm tra sau khắc phục',exact:true}).click();await page.getByRole('button',{name:'Tiếp nhận kiểm tra phòng',exact:true}).click();await page.getByRole('dialog').last().getByRole('button',{name:'Xác nhận',exact:true}).click();await expect(page.getByRole('button',{name:'Gửi kết quả kiểm tra',exact:true})).toBeEnabled();assert(requests.some(r=>r.path==='work/room-issues/8/request-inspection'));await page.screenshot({path:out+'/inspection-'+width+'.png',fullPage:true});
  await page.goto(`${origin}/login#${root}?section=history`);await page.reload();
  await page.getByRole('button',{name:'Bộ lọc',exact:true}).click();
  await page.getByLabel('Xem theo').selectOption('week');
  await expect.poll(()=>requests.some(r=>r.path==='work/housekeeping-history'&&r.query.includes('period=week'))).toBe(true);
  assert(requests.some(r=>r.path==='work/housekeeping-history'&&r.query.includes(`mine=${!supervisor}`)));
  await page.screenshot({path:`${out}/${role}-${width}-history.png`,fullPage:true});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'page overflow');
  checks.push(`${role} ${width}: tabs, scope, duration, rework close, stage, details, history`);
  await context.close();
 }
 assert.deepEqual(errors,[]);fs.writeFileSync(`${out}/browser.json`,JSON.stringify({origin,checks,errors},null,2));console.log(JSON.stringify({checks,errors}));
} finally {await browser.close();}
