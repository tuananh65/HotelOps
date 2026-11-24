import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
const base=process.env.UX_BASE||'https://127.0.0.1:5082';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const profile={user:{fields:{userId:1,fullName:'Admin'}},branches:[{branchId:1,name:'Bùi Viện',isActive:true},{branchId:2,name:'Cao Thắng',isActive:true}],roles:[{code:'SYS_ADMIN',name:'Quản trị hệ thống',branchId:null}],memberships:[],grants:['MASTER_DATA_MANAGE','ROLE_MANAGE','USER_VIEW','USER_MANAGE','ROOM_VIEW'].map(code=>({code,dataScope:'ALL',branchId:null}))};
const rows={departments:[{departmentId:10,code:'HK',name:'Buồng phòng',description:'Mô tả bộ phận',isActive:true,rowVersion:'v1'}],'room-types':[{roomTypeId:20,code:'STD',name:'Tiêu chuẩn',capacity:2,description:'Mô tả phòng',isActive:true,rowVersion:'v1'}],permissions:[{permissionId:30,code:'ROOM_VIEW',description:'Xem phòng',isActive:true,rowVersion:'v1'}]};
const users=[{userId:1,fullName:'Admin',username:'admin',employeeCode:'A',accountStatus:'ACTIVE',rowVersion:'v1'},{userId:2,fullName:'Nhân viên',username:'staff',employeeCode:'B',accountStatus:'ACTIVE',rowVersion:'v1'}];
const writes=[],errors=[];let failNext=false;
const pageOf=items=>({items:items.map(fields=>({fields})),total:items.length,page:1,pageSize:100});
try{
 const context=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:900}});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await context.route('**/api/v2/**',async route=>{
  const req=route.request(),path=new URL(req.url()).pathname.replace('/api/v2/','');let json={};
  if(req.method()!=='GET'){
   const body=req.postDataJSON();writes.push({path,body});
   if(failNext||path.startsWith('catalog/permissions/')){failNext=false;return route.fulfill({status:409,json:{title:path.startsWith('catalog/permissions/')?'PERMISSION_USED_BY_ACTIVE_USER':'STALE_ROW_VERSION'}});}
   if(path.startsWith('catalog/')){const resource=path.split('/')[1],row=rows[resource][0];Object.assign(row,body.fields,{rowVersion:`v${writes.length+1}`});json={fields:row};}
   else if(path==='users/2/deactivate'){users[1].accountStatus='INACTIVE';json={fields:users[1]};}
  }else if(path==='auth/me')json=profile;
  else if(path==='catalog/branches')json=pageOf(profile.branches);
  else if(path.startsWith('catalog/'))json=pageOf(rows[path.split('/')[1]]||[]);
  else if(path==='users')json=users.map(fields=>({fields}));
  else if(path==='users/2')json={user:{fields:users[1]},roles:[],memberships:[]};
  else if(path==='notifications')json=pageOf([]);
  await route.fulfill({json});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 for(const resource of ['departments','room-types']){
  await page.goto(`${base}/#settings?resource=${resource}`);
  await expect(page.locator('.branch-context')).toHaveCount(0);
  await expect(page.getByRole('columnheader',{name:'Mô tả',exact:true})).toBeVisible();
  await expect(page.getByRole('columnheader',{name:'Trạng thái',exact:true})).toBeVisible();
  const toggle=page.getByRole('switch');await expect(toggle).toHaveAttribute('aria-checked','true');
  assert.equal(await toggle.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(35, 122, 80)');
  await toggle.click();await expect(toggle).toHaveAttribute('aria-checked','false');
  assert.equal(await toggle.evaluate(e=>getComputedStyle(e).backgroundColor),'rgb(183, 53, 61)');
  assert.deepEqual(writes.at(-1).body.fields,{isActive:false});assert.equal(writes.at(-1).body.rowVersion,'v1');
  await page.getByRole('button',{name:'Chỉnh sửa',exact:true}).click();
  await expect(page.locator('dialog input[type=checkbox]')).toHaveCount(0);
  await page.locator('dialog input[name=description]').fill('Mô tả đã sửa');
  await page.locator('dialog button.primary').click();await expect(page.locator('dialog')).toHaveCount(0);
  assert(!Object.hasOwn(writes.at(-1).body.fields,'isActive'));
  await expect(toggle).toHaveAttribute('aria-checked','false');await expect(page.locator('tbody')).toContainText('Mô tả đã sửa');
  failNext=true;await toggle.click();await expect(page.getByRole('alert')).toBeVisible();await expect(toggle).toHaveAttribute('aria-checked','false');
  await toggle.click();await expect(toggle).toHaveAttribute('aria-checked','true');
 }
 await page.goto(`${base}/#settings?resource=permissions`);await page.getByRole('switch').click();
 await expect(page.getByRole('alert')).toBeVisible();await expect(page.getByRole('switch')).toHaveAttribute('aria-checked','true');
 await expect(page.getByRole('button',{name:'Chỉnh sửa',exact:true})).toHaveCount(0);
 await page.goto(`${base}/#users`);await expect(page.getByRole('switch')).toHaveCount(2);
 await expect(page.getByRole('switch',{name:/Admin/})).toBeDisabled();
 await page.getByRole('switch',{name:/Nhân viên/}).click();await expect(page.getByRole('heading',{name:'Vô hiệu hóa tài khoản'})).toBeVisible();
 await expect(page.locator('.branch-context')).toHaveCount(0);
 await page.locator('dialog textarea[name=reason]').fill('Ngừng sử dụng theo yêu cầu quản trị');
 await page.locator('dialog form button[type=submit],dialog form button.primary').last().click();
 assert(writes.some(w=>w.path==='users/2/deactivate'&&w.body.reason));
 profile.roles=[{code:'DEPT_MANAGER',name:'Trưởng bộ phận',branchId:1}];await page.goto(`${base}/#settings?resource=departments`);await page.reload();
 await expect(page.locator('.branch-context').first()).toBeVisible();
 await expect(page.getByRole('button',{name:'Đổi khách sạn',exact:true}).first()).toBeVisible();
 assert.deepEqual(errors,[]);
 console.log('PASS: Admin green/red switches, no checkboxes, description columns/edit, status preserved, stale/dependency rollback, account confirmation, Admin-only branch hiding');
}finally{await browser.close();}
