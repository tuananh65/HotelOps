import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium,expect} from '@playwright/test';
const origin=process.env.HK_UI_ORIGIN||'http://127.0.0.1:5174';
assert(['127.0.0.1','localhost'].includes(new URL(origin).hostname));
const out='../artifacts/incident-configuration';fs.mkdirSync(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});const checks=[],errors=[];
try{for(const width of [1440,390]){
 const context=await browser.newContext({viewport:{width,height:1000},ignoreHTTPSErrors:true});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','incident-ui-fixture'));
 let role='OPERATION_MANAGER',reported=false,status='IN_PROGRESS';const posted=[];
 const work=()=>({workId:7,kind:'CLEANING',status,title:'Dọn phòng 101',roomId:1,rowVersion:'AAAA',branchId:1});
 await context.route('**/api/v2/**',async route=>{
  const request=route.request(),url=new URL(request.url()),path=url.pathname.replace('/api/v2/','');let json={};
  if(request.method()!=='GET')posted.push({path,body:request.postDataJSON()});
  const perms=['IR_VIEW','IR_CREATE','IR_PROCESS','ROOM_SUPPLY_CONFIG','SHIFT_MANAGE','CLEANING_VIEW','CLEANING_EXECUTE','CLEANING_ASSIGN','INSPECTION_PERFORM','INCIDENT_VIEW','DASHBOARD_VIEW'];
  if(path==='auth/me')json={user:{fields:{userId:1,fullName:'Kiểm thử cấu hình'}},branches:[{branchId:1,name:'Bùi Viện',timezone:'Asia/Ho_Chi_Minh'}],roles:[{code:role,branchId:1}],memberships:[],grants:perms.map(code=>({code,branchId:1,dataScope:'BRANCH'}))};
  else if(path==='workspace/options')json={departments:[{departmentId:1,name:'Buồng phòng'},{departmentId:2,name:'Kỹ thuật'}],rooms:[],references:[],warehouses:[],items:[]};
  else if(path==='work/internal-request-types')json=request.method()==='GET'?{configured:false,items:[],roles:[{roleId:1,name:'Nhân viên buồng phòng',code:'HK_STAFF'}],recipients:[{userId:2,fullName:'Giám sát thử',employeeCode:'TEST'}]}:{referenceId:1};
  else if(path==='workday/configuration')json={shifts:[],departments:[{departmentId:1,name:'Buồng phòng'}],roles:[{roleId:1,name:'Nhân viên buồng phòng'}],departmentRoles:[{departmentId:1,roleId:1}]};
  else if(path==='work/room-issue-types')json=[{referenceId:1,name:'Hỏng đồ vật',kind:'BROKEN',isActive:true,requireNote:true,impacts:[{code:'READINESS',name:'Ảnh hưởng phục vụ',isActive:true}]}];
  else if(path==='work/room-issue-objects')json={code:'101',rowVersion:'AAAA',assets:[{assetId:1,name:'Điều hòa'}],items:[]};
  else if(path==='work/room-issues'){if(request.method()==='POST'){reported=true;json={fields:{workId:9}};}else json=[];}
  else if(path==='work/7')json={work:{fields:work()},assignments:[{fields:{assigneeUserId:1,assignmentStatus:'ACTIVE',acceptedAt:'2026-11-24T10:00:00Z'}}],assignmentDetails:[],context:{room:'101'},capabilities:{canReportRoomIssue:status==='IN_PROGRESS',canConfirmRoomIssue:role==='HOUSEKEEPING_SUPERVISOR'},hasOpenRoomIssues:reported,roomIssues:reported?[{workId:9,title:'Hỏng điều hòa',kind:'BROKEN',stage:'ROOM_ISSUE_REPORTED'}]:[],events:[],reviews:[],attachments:[]};
  else if(path==='work/cleaning-queue')json={items:[{...work(),room:'101',hasRoomIssue:reported}],counts:[{status:reported?'INCIDENT_INSPECTION':status,count:1}],total:1};
  else if(path==='work')json={items:[{fields:work()}],total:1};
  else if(path==='work/7/related'||path==='insights/time')json=[];
  else if(path==='work/cleaning-creation-options')json={canCreate:true,candidates:[]};
  else if(path==='notifications')json={items:[],total:0};
  await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(json)});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 await page.goto(origin+'/login#configuration?tab=requests');
 await page.getByRole('button',{name:'Thêm loại yêu cầu',exact:true}).click();
 await page.locator('input[name=name]').fill('Yêu cầu hỗ trợ');await page.locator('textarea[name=description]').fill('Nội dung hỗ trợ');
 await page.getByLabel('Kỹ thuật',{exact:true}).check();await page.getByLabel('Giám sát thử · TEST',{exact:true}).check();
 await page.screenshot({path:`${out}/request-configuration-${width}.png`});
 await page.getByRole('button',{name:'Lưu thay đổi',exact:true}).click();await expect.poll(()=>posted.some(p=>p.path==='work/internal-request-types')).toBe(true);const saved=posted.find(p=>p.path==='work/internal-request-types');assert.deepEqual(saved.body.senderRoleIds,[1]);assert.deepEqual(saved.body.recipientDepartmentIds,[2]);assert.deepEqual(saved.body.recipientUserIds,[2]);
 await page.goto(origin+'/login#configuration?tab=shifts');await page.getByRole('button',{name:'Tạo ca',exact:true}).click();
 await expect(page.getByRole('combobox',{name:'Bắt đầu — giờ',exact:true})).toBeVisible();
 await page.getByRole('combobox',{name:'Kết thúc — giờ',exact:true}).selectOption('24');
 await expect(page.getByRole('combobox',{name:'Kết thúc — phút',exact:true})).toBeDisabled();assert.equal(await page.locator('input[name=end]').inputValue(),'24:00');
 await page.screenshot({path:`${out}/shift-time-${width}.png`});
 role='HK_STAFF';await page.goto(origin+'/login#room-issues?room=1&create=1&work=7');await page.reload();
 await expect(page.getByLabel('Đồ vật thuộc phòng')).toHaveCount(0);
 await page.getByLabel('Loại báo cáo').selectOption('1');await expect(page.getByLabel('Đồ vật thuộc phòng')).toBeVisible();
 await page.getByLabel('Đồ vật thuộc phòng').selectOption('asset:1');await expect(page.getByLabel('Mức ảnh hưởng')).toHaveCount(0);await expect(page.getByLabel('Ghi chú')).toHaveCount(0);
 await page.screenshot({path:`${out}/incident-report-${width}.png`});
 await page.getByRole('button',{name:'Gửi giám sát buồng phòng'}).click();await expect(page).toHaveURL(/#cleaning\?kind=CLEANING&id=7/);await expect(page.getByText('Sự cố đã phát hiện.',{exact:false})).toBeVisible();
 role='HOUSEKEEPING_SUPERVISOR';status='PENDING_INSPECTION';await page.goto(origin+'/login#work?kind=CLEANING&id=7');await page.reload();
 await page.getByRole('button',{name:'Xác nhận sự cố',exact:true}).click();await page.getByLabel('Kết quả kiểm tra').fill('Đã kiểm tra điều hòa không hoạt động');
 await page.getByRole('button',{name:'Xác nhận',exact:true}).click();await expect.poll(()=>posted.some(p=>p.path==='work/7/confirm-room-issue'&&p.body.relatedId===9)).toBe(true);
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+2));checks.push({width,requestCheckboxes:true,timePicker:true,progressiveForm:true,returnToWork:true,confirmIncident:true});await context.close();
 }
 assert.deepEqual(errors,[]);fs.writeFileSync(`${out}/browser.json`,JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({checks,errors}));
}finally{await browser.close();}
