import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium,expect} from '@playwright/test';
const origin=process.env.STOCK_UI_ORIGIN || 'http://127.0.0.1:5174';
assert(['localhost','127.0.0.1'].includes(new URL(origin).hostname));
const out='../artifacts/warehouse-validation'; fs.mkdirSync(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const errors=[],checks=[];
try {
 for(const restricted of [false,true]) {
  const context=await browser.newContext({viewport:{width:1440,height:1000},ignoreHTTPSErrors:true});
  await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','browser-fixture'));
  let minimum=50,fail=false;
  const branches=restricted?[1]:[1,2],permissions=restricted?['INVENTORY_VIEW']:['INVENTORY_VIEW','ROOM_SUPPLY_CONFIG','MR_VIEW'];
  await context.route('**/api/v2/**',async route=>{
   const url=new URL(route.request().url()),path=url.pathname.split('/api/v2/')[1],branch=Number(url.searchParams.get('branchId')||1);
   const warehouse=branch===1?'Kho Bùi Viện':'Kho Cao Thắng'; let json={}; let status=200;
   if(path==='auth/me') json={user:{fields:{userId:1,fullName:'OM Kiểm thử'}},roles:branches.map(branchId=>({code:'OPERATION_MANAGER',branchId})),branches:branches.map(branchId=>({branchId,name:branchId===1?'Bùi Viện':'Cao Thắng'})),grants:branches.flatMap(branchId=>permissions.map(code=>({code,branchId,dataScope:'BRANCH'}))),memberships:[],requiresPasswordChange:false};
   else if(path==='catalog/warehouses') json={items:[{fields:{warehouseId:branch,name:warehouse,isActive:true}}],total:1};
   else if(path==='insights/stock') json=[{warehouseId:branch,itemId:1,item:'Bàn chải đánh răng',code:'HIDDEN-CODE',warehouse,baseQty:40,minimumQty:minimum,unit:'Cái',isActive:true},{warehouseId:branch,itemId:2,item:'Khăn mặt',code:'HIDDEN-2',warehouse,baseQty:50,minimumQty:50,unit:'Cái',isActive:true}];
   else if(path==='supplies/minimum') {
    if(route.request().method()==='PUT') {
     if(fail){status=409;json={title:'STALE_ROW_VERSION'};}
     else {minimum=route.request().postDataJSON().minimumStock;json={minimumStock:minimum,rowVersion:'new',configurationRowVersion:'new'};}
    }else json={minimumStock:minimum,rowVersion:'old',configurationRowVersion:'old'};
   }
   else if(path==='workspace/options') json={departments:[],items:[],warehouses:[],references:[]};
   else if(path==='documents'||path==='insights/ledger'||path.startsWith('notifications')) json={items:[],total:0};
   await route.fulfill({status,json});
  });
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await page.goto(origin+'/#stock');
  await expect(page.locator('.stock-context strong')).toHaveText('Kho Bùi Viện');
  await expect(page.locator('thead')).toContainText('STT');
  for(const text of ['Mã vật tư','Biến động cuối']) await expect(page.locator('thead')).not.toContainText(text);
  await expect(page.locator('.stock-below-minimum')).toHaveCount(1);
  assert.equal(await page.locator('.stock-below-minimum .numeric').evaluate(el=>getComputedStyle(el).color),'rgb(180, 35, 24)');
  await expect(page.getByText('HIDDEN-CODE')).toHaveCount(0);
  if(!restricted){
   await page.getByRole('button',{name:'Sửa tối thiểu Bàn chải đánh răng'}).click();
   await page.getByRole('spinbutton',{name:'Tồn tối thiểu',exact:true}).fill('30');
   fail=true;await page.getByRole('button',{name:'Lưu tối thiểu',exact:true}).click();
   await expect(page.getByRole('dialog')).toBeVisible();await expect(page.getByText('Cấu hình đã thay đổi. Đóng biểu mẫu và tải lại trước khi sửa.')).toBeVisible();
   await page.getByRole('button',{name:'Hủy',exact:true}).click();fail=false;
   await page.getByRole('button',{name:'Sửa tối thiểu Bàn chải đánh răng'}).click();
   await page.getByRole('spinbutton',{name:'Tồn tối thiểu',exact:true}).fill('30');await page.getByRole('button',{name:'Lưu tối thiểu',exact:true}).click();
   await expect(page.getByRole('dialog')).toHaveCount(0);await expect(page.locator('.stock-below-minimum')).toHaveCount(0);
   await page.getByRole('button',{name:'Xem kho chi nhánh khác',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:'Cao Thắng',exact:true}).click();
   await expect(page.locator('.stock-context strong')).toHaveText('Kho Cao Thắng');
   await page.goto(origin+'/#documents');await expect(page.locator('.topbar h1')).toHaveText('Lịch sử yêu cầu vật tư');
   await expect(page.getByRole('button',{name:'Bộ lọc',exact:true})).toBeVisible();
   await page.goto(origin+'/#stock');
  }else{
   await expect(page.getByRole('button',{name:/Sửa tối thiểu/})).toHaveCount(0);
   await page.getByRole('button',{name:'Xem kho chi nhánh khác',exact:true}).click();
   await expect(page.getByText('Tài khoản hiện chỉ được xem kho của chi nhánh này.')).toBeVisible();await page.keyboard.press('Escape');
  }
  for(const width of [1440,390,320]) {
   await page.setViewportSize({width,height:950});
   if(width<600)await expect.poll(()=>page.locator('.sidebar').evaluate(el=>el.getBoundingClientRect().right)).toBeLessThanOrEqual(1);
   await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
   await page.screenshot({path:`${out}/stock-${restricted?'restricted':'om'}-${width}.png`,fullPage:true});
  }
  checks.push(restricted?'Restricted role cannot edit minimum or switch unauthorized branch':'OM: warehouse context, columns, red threshold, error persistence, save, branch switch, history navigation, mobile');
  await context.close();
 }
 assert.deepEqual(errors,[]);
 fs.writeFileSync(`${out}/browser-results.json`,JSON.stringify({passed:true,origin,checks},null,2));
 console.log('PASS warehouse UI fixtures at 1440/390/320px');
}finally{await browser.close();}
