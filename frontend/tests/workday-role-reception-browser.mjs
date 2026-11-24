import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium,expect} from '@playwright/test';
const origin=process.env.WORKDAY_UI_ORIGIN||'http://127.0.0.1:5174';
assert(['127.0.0.1','localhost'].includes(new URL(origin).hostname));
const browser=await chromium.launch({channel:'msedge',headless:true});
const errors=[],checks=[];
try{
 for(const role of ['OPERATION_MANAGER','RECEPTION'])for(const width of [1440,390]){
  let working=false;
  const context=await browser.newContext({viewport:{width,height:900},ignoreHTTPSErrors:true});
  await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','workday-ui-fixture'));
  await context.route('**/api/v2/**',async route=>{
   const url=new URL(route.request().url()),path=url.pathname.replace('/api/v2/','');let json={},status=200;
   assert.equal(route.request().method(),'GET');
   if(path==='auth/me')json={user:{fields:{userId:1,fullName:'Kiểm thử'}},branches:[{branchId:1,name:'Bùi Viện'}],roles:[{code:role,branchId:1}],memberships:[],requiresPasswordChange:false,grants:(role==='RECEPTION'?['ROOM_VIEW','IR_CREATE','IR_VIEW']:['SHIFT_MANAGE']).map(code=>({code,branchId:1,dataScope:'BRANCH'}))};
   else if(path==='workday')json={weekStart:'2026-11-23',today:'2026-11-24',currentMinute:1080,canManage:true,timezone:'Asia/Ho_Chi_Minh',assignments:[],active:null,shifts:[
    {shiftId:1,roleId:1,departmentId:1,role:'Giám sát bảo trì',department:'Kỹ thuật',name:'Bảo trì sáng',startMinute:480,endMinute:690,capacity:2,isActive:true},
    {shiftId:2,roleId:2,departmentId:1,role:'Trưởng kỹ thuật cũ',department:'Kỹ thuật',name:'Ca kỹ thuật cũ',startMinute:480,endMinute:690,capacity:2,isActive:false}]};
   else if(path==='workday/calendar')json={year:2026,markers:[],notice:''};
   else if(path==='workday/candidates/batch'){assert(!url.searchParams.getAll('dates').includes('2026-11-24'));json={users:[{userId:2,fullName:'Giám sát bảo trì thử',employeeCode:'TEST',hourWarnings:[]}],slots:[{workDate:'2026-11-25',remaining:2}]};}
   else if(path==='rooms')json=[{roomId:1,branchId:1,code:'101',roomTypeId:1,rowVersion:'AAAAAAAB',operationalStatus:'READY',readiness:'READY',canOperate:working,isOccupied:false}];
   else if(path==='rooms/1/history')json={events:[],blocks:[]};
   else if(path==='work/room-issues'||path==='work/room-issue-types')json=[];
   else if(path==='work/room-issue-objects'){status=403;json={title:'RECEPTION_NOT_ON_SHIFT',status};}
   else if(path.startsWith('catalog/'))json={items:[],total:0};
   else if(path.startsWith('notifications'))json={items:[],total:0};
   await route.fulfill({status,json});
  });
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  if(role==='OPERATION_MANAGER'){
   await page.goto(origin+'/#workday');
   await expect(page.getByRole('combobox').first().locator('option')).toHaveCount(1);
   await expect(page.getByRole('button',{name:'Phân Bảo trì sáng ngày 2026-11-24',exact:true})).toHaveCount(0);
   await page.getByRole('button',{name:'Phân Bảo trì sáng ngày 2026-11-25',exact:true}).click();
   await expect(page.getByRole('dialog')).toContainText('Giám sát bảo trì thử');
   await page.getByRole('combobox',{name:'Phạm vi phân ca'}).selectOption('week');
   await expect(page.getByRole('group',{name:'Ngày phân ca'})).not.toContainText('24/11');
   await page.getByRole('dialog').getByRole('button',{name:'Đóng',exact:true}).click();
   await page.getByRole('checkbox',{name:'Hiện lịch sử ca đã ngừng sử dụng'}).check();
   await expect(page.getByRole('combobox').first().locator('option')).toHaveCount(2);
   await page.getByRole('combobox').first().selectOption('1:2');
   await expect(page.getByText('Đã ngừng sử dụng · chỉ xem lịch sử')).toBeVisible();
   await expect(page.locator('.add-shift')).toHaveCount(0);
   checks.push(`OM ${width}: history separate, expired slot hidden, next day candidate shown`);
  }else{
   await page.goto(origin+'/#rooms');
   await page.getByRole('button',{name:/^Phòng 101 /}).click();
   await expect(page.getByRole('dialog')).toBeVisible();
   await expect(page.getByRole('link',{name:'Báo sự cố',exact:true})).toHaveCount(0);
   working=true;await page.reload();
   await page.getByRole('button',{name:/^Phòng 101 /}).click();
   await expect(page.getByRole('link',{name:'Báo sự cố',exact:true})).toBeVisible();
   working=false;await page.goto(origin+'/#room-issues?room=1&create=1');
   await expect(page.getByRole('dialog')).toContainText('Bạn phải bắt đầu ca');
   await expect(page.getByRole('button',{name:'Gửi giám sát buồng phòng'})).toHaveCount(0);
   checks.push(`Reception ${width}: outside hidden, working shown, direct URL denied`);
  }
  fs.mkdirSync('../artifacts/workday-role-validation',{recursive:true});
  await page.screenshot({path:`../artifacts/workday-role-validation/${role}-${width}.png`});await context.close();
 }
 assert.deepEqual(errors,[]);fs.writeFileSync('../artifacts/workday-role-validation/browser-results.json',JSON.stringify({origin,checks,errors},null,2));console.log('PASS '+checks.join('; '));
}finally{await browser.close();}
