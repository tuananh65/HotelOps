import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium, expect } from '@playwright/test';
const out='../artifacts/reception-access-ui';fs.mkdirSync(out,{recursive:true});
const profile={user:{fields:{userId:7,fullName:'Lễ tân kiểm thử'}},roles:[{code:'RECEPTION',branchId:1}],branches:[{branchId:1,name:'Bùi Viện'}],memberships:[],requiresPasswordChange:false,
 grants:['ROOM_VIEW','ROOM_CHECKIN','ROOM_CHECKOUT','ROOM_OCCUPANCY_MOVE','IR_VIEW','IR_CREATE','INCIDENT_VIEW','INCIDENT_CREATE','ATTENDANCE_RECORD'].map(code=>({code,branchId:1,dataScope:'BRANCH'}))};
let onShift=false;const requests=[],writes=[],errors=[];
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:950}});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await context.route('**/api/v2/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname.replace('/api/v2/','');requests.push(path+url.search);
  let json={items:[],total:0,page:1,pageSize:25},status=200;
  if(req.method()!=='GET'){
   writes.push(path);status=403;json={title:'RECEPTION_NOT_ON_SHIFT'};
  } else if(path==='auth/me')json=profile;
  else if(path==='rooms')json=['READY','PENDING_CLEANING','CLEANING_IN_PROGRESS','PENDING_INSPECTION','REWORK_REQUIRED'].map((readiness,i)=>({roomId:i+1,branchId:1,code:String(101+i),readiness,operationalStatus:i===0?'READY':'DIRTY',isOccupied:false,rowVersion:'version',roomTypeName:'Standard',capacity:2,canOperate:onShift}));
  else if(path.endsWith('/history'))json={events:[],blocks:[]};
  else if(path==='workspace/options')json={departments:[],rooms:[],priorities:[],severities:[]};
  await route.fulfill({status,json});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 const base=process.env.BASE_URL||'https://127.0.0.1:5082';
 await page.goto(base+'/#work?kind=INCIDENT');
 await expect(page.getByPlaceholder('Tên công việc…')).toBeVisible();
 await expect(page.getByRole('button',{name:'Sự cố',exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:/Đang xem/})).toHaveCount(0);
 assert(requests.some(p=>p.startsWith('work?')&&p.includes('kind=INTERNAL_REQUEST')&&p.includes('mine=true')));
 assert(!requests.some(p=>p.startsWith('work?')&&p.includes('kind=INCIDENT')));
 await page.goto(base+'/#rooms');
 await expect(page.locator('.room-tile')).toHaveCount(5);
 await expect(page.getByRole('button',{name:/Đang dọn dẹp/})).toHaveCount(0);
 await page.getByRole('button',{name:/Cần dọn dẹp 4/}).click();
 await expect(page.locator('.room-tile')).toHaveCount(4);
 await expect(page.locator('.room-tile').filter({hasText:'Cần dọn dẹp'})).toHaveCount(4);
 await page.getByRole('button',{name:/Tất cả 5/}).click();
 await page.locator('.room-tile').first().click();
 await expect(page.getByRole('link',{name:'Mở lịch làm việc'})).toBeVisible();
 await expect(page.getByRole('button',{name:'Đặt phòng',exact:true})).toHaveCount(0);
 onShift=true;await page.reload();await page.locator('.room-tile').first().click();
 await page.getByRole('button',{name:'Đặt phòng',exact:true}).click();
 await page.getByRole('button',{name:'Xác nhận',exact:true}).click();
 await expect(page.getByText('Bạn phải bắt đầu ca và đang trong giờ làm việc tại chi nhánh này để thay đổi trạng thái phòng.')).toBeVisible();
 await expect(page.getByText('Đã cập nhật phòng.')).toHaveCount(0);
 assert.deepEqual(writes,['rooms/1/actions/reserve']);
 await page.keyboard.press('Escape');
 for(const width of [1440,768,375]){
  await page.setViewportSize({width,height:950});
  const closeNav=page.getByRole("button",{name:"Đóng điều hướng"}); if(await closeNav.isVisible()) await closeNav.click();
  await page.waitForTimeout(250);
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.screenshot({path:`${out}/rooms-${width}.png`});
 }
 assert.deepEqual(errors,[]);
 fs.writeFileSync(`${out}/verification.json`,JSON.stringify({passed:true,mockedApi:true,checks:['reception excludes incident','forced personal IR','merged cleaning filter and badges','shift-required UI','expired shift API error without false success','responsive'],writes,errors},null,2));
 console.log('PASS reception personal requests, cleaning group, shift guard UI and responsive views');
}finally{await browser.close();}
