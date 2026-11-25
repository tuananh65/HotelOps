import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyConfigurationDeltaBrowser(fixture,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const checks=[];
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias==='manager').token);
  const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#configuration?tab=requests');
  await page.getByRole('button',{name:'Thêm loại yêu cầu',exact:true}).click({timeout:60000});
  const requestDialog=page.getByRole('dialog',{name:'Thêm loại yêu cầu',exact:true});
  await expect(requestDialog.getByRole('group',{name:'Vai trò nhận',exact:true})).toBeVisible();
  await expect(requestDialog.locator('input[name="recipientUserId"]')).toHaveCount(0);
  await expect(requestDialog.locator('input[name="recipientDepartmentId"]')).toHaveCount(0);
  await expect.poll(()=>requestDialog.locator('input[name="recipientRoleId"]').count()).toBeGreaterThan(0);
  checks.push('configuration lists receiving roles and no employee/department checkboxes');
  await page.goto('http://127.0.0.1:5174/login#configuration?tab=room');
  await page.getByRole('button',{name:'Thêm loại báo cáo phòng',exact:true}).click({timeout:30000});
  const roomDialog=page.getByRole('dialog',{name:'Thêm loại báo cáo phòng',exact:true});
  const name='TEST browser report '+fixture.prefix;
  await roomDialog.getByLabel('Tên',{exact:true}).fill(name);
  await roomDialog.getByLabel('Quy trình xử lý').selectOption('MISSING');
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/work/room-issue-types'&&r.request().method()==='PUT');
  await roomDialog.getByRole('button',{name:'Lưu thay đổi',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(roomDialog).toHaveCount(0);await expect(page.getByRole('row').filter({hasText:name})).toHaveCount(1);
  checks.push('new room report type saved through browser and displayed after API reload');
  await page.goto('http://127.0.0.1:5174/login#requests');
  await page.getByRole('button',{name:'Yêu cầu thông thường',exact:true}).click({timeout:30000});
  await expect(page.getByRole('heading',{name:'Yêu cầu nội bộ',exact:true})).toHaveCount(1);
  await expect(page.getByRole('button',{name:'Đang xem trong phạm vi quyền',exact:true})).toHaveCount(0);
  await expect(page.getByRole('link',{name:'Vật tư nhân viên',exact:true})).toHaveCount(0);
  checks.push('ordinary requests have one page title and no scope or staff-supplies controls');
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:directory+'/requests-delta-mobile.png',fullPage:true});
  await page.goto('http://127.0.0.1:5174/login#reports');
  await expect(page.getByRole('combobox',{name:'Loại báo cáo',exact:true})).toHaveValue('operations-events',{timeout:30000});
  const shiftCells=page.locator('table.mobile-records tbody td[data-label="Nhóm sự việc"]').filter({hasText:/^Ca làm việc$/});
  await expect(shiftCells).toHaveCount(3,{timeout:30000});
  for(const cell of await shiftCells.all())await expect(cell).toBeVisible();
  const actions=page.locator('table.mobile-records tbody td[data-label="Diễn biến"]');
  await expect(actions.filter({hasText:/^Bắt đầu$/})).toHaveCount(1);
  await expect(actions.filter({hasText:/^Kết thúc ca$/})).toHaveCount(1);
  await expect(actions.filter({hasText:/^Tự kết thúc ca hết giờ$/})).toHaveCount(1);
  await page.screenshot({path:directory+'/operations-events-mobile.png',fullPage:true});
  checks.push('activity report is default and contains recorded shift events');
  assert.deepEqual(errors,[]);
  await writeFile(directory+'/configuration-delta-browser.json',JSON.stringify({status:'PASS',checks,limits:'Isolated TEST HTTP and Edge. No operational data or external email.'},null,2));
 }catch(e){for(const context of browser.contexts())for(const page of context.pages())await page.screenshot({path:directory+'/configuration-delta-failure.png',fullPage:true}).catch(()=>{});throw e;}
 finally{await browser.close();}
}
