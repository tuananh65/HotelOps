import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[],writes=[];
const work={workId:7,kind:'CLEANING',branchId:1,roomId:1,title:'Dọn phòng 104',description:'Dọn phòng',status:'ASSIGNED',rowVersion:'v1',createdBy:9,dueAt:'2026-11-23T10:30:00Z'};
const assignment={assignmentId:1,assigneeUserId:2,assignmentStatus:'ACTIVE',acceptedAt:null};
try{
 const context=await browser.newContext({viewport:{width:375,height:812},timezoneId:'Asia/Ho_Chi_Minh'});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await context.route('**/api/v2/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname.replace('/api/v2/','');let json={};
  if(req.method()!=='GET'){
   writes.push({path,body:req.postDataJSON()});
   assert.ok(['work/7/actions/start','work/7/actions/complete'].includes(path));
   if(path.endsWith('/start')){work.status='IN_PROGRESS';work.actualStartAt='2026-11-23T10:00:00Z';assignment.acceptedAt=work.actualStartAt;work.rowVersion='v2';}
   else{work.status='PENDING_INSPECTION';work.rowVersion='v3';}
   json={fields:work};
  }else if(path==='auth/me')json={user:{fields:{userId:2,fullName:'Nhân viên kiểm thử'}},branches:[{branchId:1,name:'Bùi Viện'}],roles:[{code:'HK_STAFF',name:'Nhân viên buồng phòng',branchId:1}],memberships:[],grants:['CLEANING_VIEW','CLEANING_EXECUTE','ATTENDANCE_RECORD','INCIDENT_CREATE','INCIDENT_VIEW'].map(code=>({code,branchId:1,dataScope:'OWN'}))};
  else if(path==='work/7')json={work:{fields:work},assignments:[{fields:assignment}],assignmentDetails:[],events:[],time:[],attachments:[],checklist:[],context:{room:'104',floor:'Tầng 1',branch:'Bùi Viện'}};
  else if(path==='work/7/related')json=[];
  else if(path==='work')json={items:[{fields:work}],total:1,pageSize:50};
  else if(path==='workspace/options')json={rooms:[{roomId:1,code:'104'}],departments:[],references:[],checklists:[]};
  else if(path==='notifications')json={items:[]};
  else if(path==='work/candidates')json=[];
  await route.fulfill({json});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`${process.env.UX_BASE||'https://127.0.0.1:5082'}/#cleaning`);
 await expect(page.getByRole('button',{name:/Mở Dọn phòng 104/})).toBeVisible();
 await expect(page.getByRole('columnheader',{name:'Thao tác'})).toHaveCount(0);
 for(const width of [320,375,430,768]){
  await page.setViewportSize({width,height:812});await page.waitForTimeout(250);
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`list overflow at ${width}`);
 }
 await page.setViewportSize({width:375,height:812});
 await page.getByRole('button',{name:/Mở Dọn phòng 104/}).click();
 const dialog=page.locator('dialog.room-drawer');await expect(dialog).toBeVisible();
 await expect(dialog.getByText('Minh chứng',{exact:true})).toHaveCount(0);
 await expect(dialog.getByRole('link',{name:'Báo sự cố'})).toHaveCount(0);
 await dialog.getByRole('button',{name:'Bắt đầu',exact:true}).click();
 await dialog.getByRole('button',{name:'Bắt đầu',exact:true}).last().click();
 await expect(dialog.getByRole('button',{name:'Báo cáo hoàn thành',exact:true})).toBeVisible();
 await expect(dialog.getByRole('button',{name:'Đề nghị bàn giao'})).toHaveCount(0);
 assert.ok(await dialog.evaluate(e=>e.scrollWidth<=e.clientWidth),'drawer must not overflow horizontally');
 await dialog.getByRole('button',{name:'Báo cáo hoàn thành',exact:true}).click();
 await expect(dialog.getByText('Chờ kiểm tra',{exact:true}).first()).toBeVisible();
 assert.equal(writes.length,2);assert.equal(writes[0].body.reason,null);assert.equal(writes[1].body.rowVersion,'v2');
 assert.deepEqual(errors,[]);console.log('PASS: mobile row navigation, no horizontal overflow, start→complete, no incident/evidence/handover, current version');
}finally{await browser.close();}
