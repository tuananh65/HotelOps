import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium, expect} from '@playwright/test';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[];let reads=0;
const state={weekStart:'2026-11-23',today:'2026-11-23',currentMinute:479,timezone:'Asia/Ho_Chi_Minh',canManage:false,
 shifts:[{shiftId:1,name:'Lễ tân · đêm',startMinute:0,endMinute:480,role:'Lễ tân',department:'Lễ tân',roleId:1,departmentId:1}],
 assignments:[{assignmentId:1,userId:1,shiftId:1,workDate:'2026-11-23',status:'WORKING',rowVersion:'fixture'}],
 active:{assignmentId:1,shiftId:1,name:'Lễ tân · đêm',workDate:'2026-11-23',startedAt:'2026-11-22T17:00:00Z',rowVersion:'fixture'}};
try{
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await page.clock.install({time:new Date('2026-11-23T00:59:00Z')});
 await page.route('**/api/v2/**',async route=>{
   assert.equal(route.request().method(),'GET');
   const path=new URL(route.request().url()).pathname;let json=[];
   if(path.endsWith('/auth/me'))json={user:{fields:{userId:1,fullName:'Lê Thu Hà'}},branches:[{branchId:1,name:'Bùi Viện'}],roles:[{code:'RECEPTION',branchId:1}],memberships:[],grants:[{code:'ATTENDANCE_RECORD',branchId:1,dataScope:'OWN'}]};
   else if(path.endsWith('/workday')){reads++;json=state;}
   else if(path.endsWith('/calendar'))json={markers:[]};
   else if(path.endsWith('/history'))json=[{eventId:-1,action:'AUTO_END',actor:'Hệ thống',occurredAt:'2026-11-23T01:00:00Z',reason:'Hệ thống tự động kết thúc ca khi hết giờ làm việc.'}];
   await route.fulfill({status:200,json});
 });
 await page.goto('http://127.0.0.1:5186/#workday');
 await expect(page.getByRole('button',{name:'Kết thúc ca',exact:true})).toBeVisible();
 state.active=null;state.currentMinute=480;state.assignments[0].status='ENDED';state.assignments[0].endedAt='2026-11-23T01:00:00Z';
 await page.clock.runFor(31000);
 await expect(page.getByRole('button',{name:'Kết thúc ca',exact:true})).toHaveCount(0);
 await expect(page.locator('.shift-list .badge')).toHaveText('Đã kết thúc');
 assert.ok(reads>=2);
 await page.getByRole('button',{name:'Chi tiết',exact:true}).click();
 await expect(page.getByRole('dialog')).toContainText('Tự động kết thúc ca');
 await expect(page.getByRole('dialog')).toContainText('Hệ thống');
 await page.keyboard.press('Escape');
 state.assignments[0].status='PLANNED';
 await page.clock.runFor(31000);
 await expect(page.locator('.shift-list .badge')).toHaveText('Đã phân ca');
 await expect(page.getByRole('button',{name:'Bắt đầu ca',exact:true})).toHaveCount(0);
 state.currentMinute=300;
 await page.clock.runFor(31000);
 await expect(page.getByRole('button',{name:'Bắt đầu ca',exact:true})).toBeVisible();
 fs.mkdirSync('../artifacts/shift-expiry',{recursive:true});
 await page.screenshot({path:'../artifacts/shift-expiry/personal-workday.png'});
 assert.deepEqual(errors,[]);
 fs.writeFileSync('../artifacts/shift-expiry/browser.json',JSON.stringify({passed:true,mockedApi:true,reads,errors,checks:['auto refresh','ended badge','no end button after expiry','Vietnamese system history','no start outside hours','start within hours']},null,2));
 console.log('PASS personal shift expiry and time window UI (mocked API)');
}finally{await browser.close();}
