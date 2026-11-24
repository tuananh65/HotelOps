import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
const base=process.env.UX_BASE||'https://127.0.0.1:5082';
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[],writes=[];let role='DEPT_MANAGER',fail=false;
const types=[{referenceId:11,name:'Hỗ trợ khách',description:'Hỗ trợ trong chi nhánh',defaultDepartmentId:10,isActive:true,rowVersion:'v1'}];
const rooms=[{referenceId:21,kind:'BROKEN',name:'Hỏng đồ vật',description:'Báo đồ hỏng',isActive:true,rowVersion:'r1'},{referenceId:22,kind:'MISSING',name:'Thiếu đồ vật',isActive:true,rowVersion:'r2'}];
try{
 const context=await browser.newContext({ignoreHTTPSErrors:true,viewport:{width:1440,height:900}});
 await context.addInitScript(()=>sessionStorage.setItem('hotelops.compact.session','fixture'));
 await context.route('**/api/v2/**',async route=>{
  const req=route.request(),path=new URL(req.url()).pathname.replace('/api/v2/','');let json={};
  if(req.method()!=='GET'){
   const body=req.postDataJSON();writes.push({path,body});
   if(fail){fail=false;return route.fulfill({status:409,json:{title:'STALE_ROW_VERSION'}});}
   if(path==='work/room-issue-types')Object.assign(rooms.find(r=>r.kind===body.kind),body,{rowVersion:'updated'});
   else if(path.startsWith('work/internal-request-types')){const id=Number(path.split('/')[2]),row=types.find(r=>r.referenceId===id);if(row)Object.assign(row,body,{rowVersion:'updated'});else types.push({...body,referenceId:12,rowVersion:'new'});}
   else if(path==='work')json={fields:{workId:5,kind:'INTERNAL_REQUEST',title:body.title,status:'DRAFT',rowVersion:'w1'}};
  }else if(path==='auth/me')json={user:{fields:{userId:1,fullName:'Trưởng bộ phận'}},branches:[{branchId:1,name:'Bùi Viện',isActive:true}],roles:[{code:role,branchId:1}],memberships:[{branchId:1,departmentId:10,kind:'EMPLOYMENT'}],grants:['IR_VIEW','IR_CREATE','IR_PROCESS','INCIDENT_VIEW','INCIDENT_TRIAGE'].map(code=>({code,dataScope:role==='DEPT_MANAGER'?'BRANCH':'OWN',branchId:1}))};
  else if(path==='work/internal-request-types')json={configured:true,items:types};
  else if(path==='work/room-issue-types')json=rooms;
  else if(path==='work/room-issues')json=[];
  else if(path==='workspace/options')json={departments:[{departmentId:10,name:'Lễ tân'},{departmentId:20,name:'Kỹ thuật'}],rooms:[],references:[],checklists:[]};
  else if(path==='work'||path==='notifications')json={items:[],total:0,page:1};
  await route.fulfill({json});
 });
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
 await page.goto(`${base}/#internal-settings`);
 await expect(page.getByRole('heading',{name:'Cấu hình nội bộ',exact:true})).toBeVisible();
 await expect(page.getByRole('columnheader',{name:'Bộ phận nhận mặc định'})).toBeVisible();
 await page.getByRole('switch',{name:/Hỗ trợ khách/}).click();await expect(page.getByRole('switch',{name:/Hỗ trợ khách/})).toHaveAttribute('aria-checked','false');
 fail=true;await page.getByRole('switch',{name:/Hỗ trợ khách/}).click();await expect(page.getByRole('alert')).toBeVisible();await expect(page.getByRole('switch',{name:/Hỗ trợ khách/})).toHaveAttribute('aria-checked','false');
 await page.getByRole('button',{name:'Chỉnh sửa',exact:true}).click();
 await expect(page.locator('dialog input[type=checkbox]')).toHaveCount(0);
 await page.getByLabel('Mô tả',{exact:true}).fill('Mô tả đã sửa');await page.getByLabel('Bộ phận nhận mặc định',{exact:true}).selectOption('20');
 await page.getByRole('button',{name:'Lưu thay đổi'}).click();await expect(page.locator('dialog')).toHaveCount(0);
 assert.equal(writes.at(-1).body.defaultDepartmentId,20);assert.equal(writes.at(-1).body.isActive,false);
 await page.getByRole('switch',{name:/Hỗ trợ khách/}).click();
 await page.getByRole('button',{name:'Báo cáo phòng',exact:true}).click();
 await expect(page.getByRole('switch')).toHaveCount(2);await expect(page.locator('input[type=checkbox]')).toHaveCount(0);
 await page.getByRole('row').filter({hasText:'Hỏng đồ vật'}).getByRole('button',{name:'Chỉnh sửa'}).click();
 await page.getByLabel('Mô tả',{exact:true}).fill('Kiểm tra bằng giấy');await page.getByRole('button',{name:'Lưu thay đổi'}).click();
 assert.equal(writes.at(-1).body.description,'Kiểm tra bằng giấy');
 for(const width of [320,375,430]){await page.setViewportSize({width,height:812});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`No overflow ${width}`);await expect(page.getByRole('switch').first()).toBeVisible();}
 await page.setViewportSize({width:1440,height:900});
 await page.goto(`${base}/#room-issues`);await page.getByRole('link',{name:'Cấu hình loại báo cáo'}).click();await expect(page.getByRole('heading',{name:'Loại báo cáo phòng'})).toBeVisible();await expect(page.locator('dialog')).toHaveCount(0);
 await page.goto(`${base}/#requests?create=1`);
 await page.locator('select[name=categoryId]').selectOption('11');await expect(page.locator('select[name=departmentId]')).toHaveValue('20');
 await page.locator('input[name=title]').fill('Cần hỗ trợ');await page.locator('select[name=requesterDepartmentId]').selectOption('10');
 await page.locator('dialog form button[type=submit],dialog form button.primary').last().click();
 assert.equal(writes.at(-1).body.categoryId,11);assert.equal(writes.at(-1).body.departmentId,20);
 await page.goto(`${base}/#internal-settings`);await expect(page.locator('nav button[aria-current=page]')).toHaveAttribute('aria-label','Cấu hình nội bộ');await page.screenshot({path:'../artifacts/internal-settings-desktop.png'});
 role='HK_STAFF';await page.reload();await expect(page.getByRole('heading',{name:'Cấu hình nội bộ',exact:true})).toHaveCount(0);
 assert.deepEqual(errors,[]);console.log('PASS internal settings: table, toggle, concurrency error, edit, room tab, 320/375/430, request defaults, role navigation');
}catch(e){for(const c of browser.contexts())for(const p of c.pages()){console.log('ERRORS',errors);console.log((await p.locator('body').innerText()).slice(-6000));await p.screenshot({path:'../artifacts/internal-settings-failure.png'});}throw e;}finally{await browser.close();}
