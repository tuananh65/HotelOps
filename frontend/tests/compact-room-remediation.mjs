import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[],writes=[];let role='HOUSEKEEPING_SUPERVISOR',stage='ROOM_ISSUE_REPORTED',kind='BROKEN';
const grants=['INCIDENT_VIEW','INCIDENT_CREATE','INCIDENT_TRIAGE','INSPECTION_PERFORM','MAINTENANCE_ASSIGN','MAINTENANCE_VIEW','MR_FULFILL','MR_CREATE','MR_VIEW'];
const work={workId:5,title:'Hỏng đồ vật · Tivi · Phòng 104',rowVersion:'v1',status:'OPEN'};
const types=[{referenceId:1,kind:'BROKEN',name:'Hỏng đồ vật',isActive:true,rowVersion:'t1'},{referenceId:2,kind:'MISSING',name:'Thiếu đồ vật',isActive:true,rowVersion:'t2'}];
try{
 const context=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:375,height:812}});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await context.route('**/api/v2/**',async route=>{
  const req=route.request(),path=new URL(req.url()).pathname.replace('/api/v2/','');let json={};
  if(req.method()!=='GET'){
   const body=req.postDataJSON();writes.push({path,body});json={fields:work};
   if(path==='work/room-issues')stage='ROOM_ISSUE_REPORTED';
   else if(path.endsWith('/approve'))stage='ROOM_ISSUE_APPROVED';
   else if(path.endsWith('/send-repair'))stage='ROOM_ISSUE_REPAIR_REQUESTED';
   else if(path.endsWith('/create-repair'))stage='ROOM_ISSUE_REPAIR_CREATED';
   else if(path.endsWith('/report-remedy'))stage='ROOM_ISSUE_REMEDY_REPORTED';
   else if(path.endsWith('/request-inspection'))stage='ROOM_ISSUE_INSPECTION_REQUESTED';
   else if(path.endsWith('/inspection-pass'))stage='ROOM_ISSUE_INSPECTION_PASSED';
   else if(path.endsWith('/request-material'))stage='ROOM_ISSUE_MATERIAL_REQUESTED';
  }else if(path==='auth/me')json={user:{fields:{userId:1,fullName:'Kiểm thử vai trò'}},branches:[{branchId:1,name:'Bùi Viện'}],roles:[{code:role,name:role,branchId:1}],memberships:[{branchId:1,departmentId:1,kind:'EMPLOYMENT'}],grants:grants.map(code=>({code,branchId:1,dataScope:'BRANCH'}))};
  else if(path==='work/room-issue-types')json=types;
  else if(path==='work/room-issue-objects')json={roomId:4,code:'104',rowVersion:'r1',assets:[{assetId:8,name:'Tivi'}],items:[]};
  else if(path==='work/room-issues')json=[{...work,stage}];
  else if(path==='work/room-issues/5')json={work:{fields:work},stage,canCreateRepair:stage==="ROOM_ISSUE_REPAIR_REQUESTED",info:{kind,objectName:'Tivi'},room:{code:'104',rowVersion:'r2'},head:role==='DEPT_MANAGER',lead:role==='MAINTENANCE_LEAD',store:role==='STOREKEEPER',supervisor:role==='HOUSEKEEPING_SUPERVISOR',maintenance:[],materials:kind==='MISSING'?[{documentId:9,status:'FULLY_ISSUED'}]:[],events:[{eventType:stage,occurredAt:'2026-11-23T10:00:00Z'}]};
  else if(path==='workspace/options')json={departments:[{departmentId:1,name:'Buồng phòng'}],warehouses:[{warehouseId:2,name:'Kho chính'}],items:[{itemId:3,name:'Tivi thay thế',baseUnitId:4}],references:[{referenceId:4,kind:'UOM',name:'Cái'}]};
  else if(path==='notifications')json={items:[]};
  await route.fulfill({json});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 const base=process.env.UX_BASE||'https://127.0.0.1:5082';
 async function open(actor,hash='#room-issues?id=5'){role=actor;await page.goto(`${base}/${hash}`);await page.reload();await expect(page.locator('dialog').last()).toBeVisible();}
 await open('HOUSEKEEPING_SUPERVISOR','#room-issues?room=4&create=1');
 await page.getByLabel('Loại báo cáo').selectOption('1');await page.getByLabel('Đồ vật thuộc phòng').selectOption('asset:8');
 for(const width of [320,375,430]){await page.setViewportSize({width,height:812});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`page overflow ${width}`);assert.ok(await page.locator('dialog').last().evaluate(e=>e.scrollWidth<=e.clientWidth),`dialog overflow ${width}`);}
 await expect(page.getByLabel('Bộ phận xử lý')).toHaveCount(0);await expect(page.locator('input[type=datetime-local]')).toHaveCount(0);
 await page.getByRole('button',{name:'Gửi trưởng bộ phận',exact:true}).click();
 assert.equal(writes.at(-1).body.roomId,4);assert.equal(writes.at(-1).body.assetId,8);assert.equal(writes.at(-1).body.roomRowVersion,'r1');
 await open('DEPT_MANAGER');await page.getByRole('button',{name:'Duyệt sự cố',exact:true}).click();await page.getByRole('button',{name:'Gửi trưởng bảo trì khắc phục'}).click();
 await open('MAINTENANCE_LEAD');await page.getByRole('button',{name:'Tạo công việc để phân công bảo trì'}).click();await page.getByLabel('Kết quả sửa chữa / bổ sung thực tế').fill('Đã sửa chữa và nghiệm thu hoàn tất');await page.getByRole('button',{name:'Gửi trưởng bộ phận xác nhận'}).click();
 await open('DEPT_MANAGER');await page.getByRole('button',{name:'Xác nhận và yêu cầu giám sát kiểm tra phòng'}).click();
 await open('HOUSEKEEPING_SUPERVISOR');await page.getByRole('button',{name:'Gửi kết quả kiểm tra'}).click();await expect(page.getByText('Đã kiểm tra đạt, chuyển cần dọn lại').first()).toBeVisible();
 kind='MISSING';stage='ROOM_ISSUE_APPROVED';await open('DEPT_MANAGER');
 await page.getByLabel('Bộ phận yêu cầu').selectOption('1');await page.getByLabel('Kho cấp').selectOption('2');await page.getByLabel('Vật tư bổ sung').selectOption('3');await page.getByLabel('Đơn vị').selectOption('4');await page.getByRole('button',{name:'Tạo và gửi yêu cầu vật tư'}).click();
 assert.equal(writes.at(-1).body.warehouseId,2);
 await open('STOREKEEPER');await page.getByLabel('Phiếu đã cấp đủ').selectOption('9');await page.getByLabel('Kết quả sửa chữa / bổ sung thực tế').fill('Đã bổ sung đồ vật vào đúng phòng');await page.getByRole('button',{name:'Gửi trưởng bộ phận xác nhận'}).click();
 assert.equal(writes.at(-1).body.documentId,9);assert.deepEqual(errors,[]);
 console.log('PASS: all four role screens, room-bound report, repair/material routing, physical inspection, 320/375/430 mobile, no console errors');
}finally{await browser.close();}
