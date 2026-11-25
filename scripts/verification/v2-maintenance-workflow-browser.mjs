import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

async function session(fixture,role,run){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias===role).token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const result=await run(page);assert.deepEqual(errors,[]);return result;
 }catch(error){if(page)await page.screenshot({path:`artifacts/restructure-v2/maintenance-${role}-workflow-failure.png`,fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
const responseFor=(page,route,method='POST')=>page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2'+route&&r.request().method()===method);

export async function verifyLegacyIssueSearchBrowser(fixture,title,incidentId){
 return session(fixture,'supervisor',async page=>{
  await page.goto('http://127.0.0.1:5174/login#room-issues');
  const search=page.getByRole('searchbox',{name:'Tìm phòng hoặc sự cố',exact:true});
  const loaded=page.waitForResponse(r=>{const url=new URL(r.url());return url.pathname==='/api/v2/work/room-issues'&&url.searchParams.get('search')===title;});
  await search.fill(title);const response=await loaded;assert.equal(response.status(),200);
  const result=await response.json();assert.equal(result.total,1);assert.equal(result.items[0].workId,incidentId);
  const card=page.locator('.room-issue-card').filter({hasText:title});await expect(card).toHaveCount(1);
  await card.click();const dialog=page.getByRole('dialog',{name:title,exact:true});
  await expect(dialog.getByRole('heading',{name:'Xác minh và chuyển sửa chữa',exact:true})).toBeVisible();
  await expect(dialog.getByRole('button',{name:'Chuyển giám sát bảo trì',exact:true})).toBeEnabled();
  await page.screenshot({path:'artifacts/restructure-v2/maintenance-legacy-search.png',fullPage:false});
 });
}

export async function replacementDecisionBrowser(fixture,workId,role,command,assetId){
 return session(fixture,role,async page=>{
  const route=role==='manager'?`assets?replacement=${workId}`:`work?kind=MAINTENANCE&id=${workId}`;
  const loaded=responseFor(page,`/work/${workId}/replacement`,'GET');
  await page.goto('http://127.0.0.1:5174/login#'+route);
  const data=await loaded;assert.equal(data.status(),200,await data.text());
  const scope=role==='manager'?page.getByRole('dialog',{name:'Xử lý đề xuất thay tài sản',exact:true}):page;
  const panel=scope.getByRole('heading',{name:'Đề xuất thay mới thiết bị',exact:true}).locator('..');
  await expect(panel).toHaveCount(1);
  await expect(panel).toBeVisible();
  const reasonLabel=command==='propose'?'Kết luận không thể sửa / lý do đề xuất':'Kết quả xử lý / lý do';
  await panel.getByRole('textbox',{name:reasonLabel,exact:true}).fill('TEST verified replacement '+command);
  if(command==='propose')await panel.getByRole('textbox',{name:'Phương án thay mới',exact:true}).fill('TEST replace with an inspected asset and reconcile both physical locations');
  if(command==='install')await panel.getByRole('combobox',{name:'Thiết bị thay',exact:true}).selectOption(String(assetId));
  const names={propose:'Gửi giám sát xác nhận',confirm:'Xác nhận, gửi OM',approve:'Đồng ý thay mới',reject:'Từ chối',install:'Ghi nhận đã thay thực tế'};
  const applied=responseFor(page,`/work/${workId}/replacement/${command}`);
  await panel.getByRole('button',{name:names[command],exact:true}).click();
  const response=await applied;assert.equal(response.status(),200,await response.text());
  const body=response.request().postDataJSON(),result=await response.json();
  await expect(panel.getByRole('button',{name:names[command],exact:true})).toHaveCount(0);
  await page.screenshot({path:`artifacts/restructure-v2/replacement-${role}-${command}.png`,fullPage:false});
  return {body,result};
 });
}

export async function reportDamageBrowser(fixture,workId,roomId,assetId,typeId){
 return session(fixture,'hk',async page=>{
  const typesReady=responseFor(page,'/work/room-issue-types','GET');
  await page.goto(`http://127.0.0.1:5174/login#room-issues?room=${roomId}&create=1&work=${workId}&workKind=CLEANING`);
  assert.equal((await typesReady).status(),200);
  const dialog=page.getByRole('dialog',{name:'Báo cáo sự cố phòng',exact:true});
  await expect(dialog.getByRole('combobox',{name:'Loại báo cáo',exact:true})).toBeVisible({timeout:30000});
  await expect(dialog.getByRole('combobox',{name:'Đồ vật thuộc phòng',exact:true})).toHaveCount(0);
  await dialog.getByRole('combobox',{name:'Loại báo cáo',exact:true}).selectOption(String(typeId));
  await dialog.getByRole('combobox',{name:'Đồ vật thuộc phòng',exact:true}).selectOption('asset:'+assetId);
  await expect(dialog.locator('textarea')).toHaveCount(0);
  await expect(dialog.getByRole('combobox',{name:'Mức ảnh hưởng',exact:true})).toHaveCount(0);
  const saved=responseFor(page,'/work/room-issues');
  await dialog.getByRole('button',{name:'Gửi giám sát buồng phòng',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);
  await expect(page).toHaveURL(new RegExp(`#cleaning\\?kind=CLEANING&id=${workId}$`));
  await expect(dialog).toHaveCount(0);
  await page.screenshot({path:'artifacts/restructure-v2/maintenance-hk-reported.png',fullPage:false});
  return response.json();
 });
}

export async function confirmDamageBrowser(fixture,workId){
 return session(fixture,'supervisor',async page=>{
  await page.goto(`http://127.0.0.1:5174/login#work?kind=CLEANING&id=${workId}`);
  await page.getByRole('button',{name:'Xác nhận sự cố',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Xác nhận sự cố sau kiểm tra phòng',exact:true});
  await dialog.getByLabel('Kết quả kiểm tra',{exact:true}).fill('Synthetic TEST confirmed damaged equipment');
  const saved=responseFor(page,`/work/${workId}/confirm-room-issue`);
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();assert.equal((await saved).status(),200);await expect(dialog).toHaveCount(0);
 });
}

export async function inspectRepairBrowser(fixture,issueId,pass){
 return session(fixture,'supervisor',async page=>{
  await page.goto('http://127.0.0.1:5174/login#room-issues?id='+issueId);
  await page.getByRole('button',{name:'Gửi kết quả kiểm tra',exact:true}).click();
  const inspection=page.getByRole('dialog',{name:'Gửi kết quả kiểm tra phòng',exact:true});
  await inspection.locator('select[name="result"]').selectOption(pass?'PASS':'FAIL');
  const note=inspection.getByLabel('Ghi chú (bắt buộc khi không đạt)',{exact:true});
  if(pass)await expect(note).toHaveValue('');else await note.fill('Leak');
  const saved=responseFor(page,`/work/room-issues/${issueId}/inspection-${pass?'pass':'fail'}`);
  await inspection.getByRole('button',{name:'Xác nhận',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(page.getByRole('button',{name:'Gửi kết quả kiểm tra',exact:true})).toHaveCount(0);
  await page.screenshot({path:`artifacts/restructure-v2/maintenance-hks-${pass?'pass':'fail'}.png`,fullPage:false});
 });
}

export async function createRepairBrowser(fixture,issueId,external){
 return session(fixture,'lead',async page=>{
  await page.goto('http://127.0.0.1:5174/login#room-issues?id='+issueId);
  await page.getByRole('combobox',{name:'Hình thức',exact:true}).selectOption(external?'external':'internal');
  if(!external)await page.getByRole('combobox',{name:'Nhân viên bảo trì',exact:true}).selectOption(String(fixture.actors.find(a=>a.alias==='technician').user));
  await expect(page.getByLabel('Hướng dẫn xử lý',{exact:true})).toHaveValue('');
  const saved=responseFor(page,`/work/room-issues/${issueId}/create-repair`);
  await page.getByRole('button',{name:'Tiếp nhận và giao sửa chữa',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);
  await expect(page.getByRole('heading',{name:'Tiếp nhận và phân công sửa chữa',exact:true})).toHaveCount(0);
  await page.screenshot({path:`artifacts/restructure-v2/maintenance-created-${external?'external':'internal'}.png`,fullPage:false});
  return response.request().postDataJSON();
 });
}

async function command(page,workId,action,label){
 await page.getByRole('button',{name:label,exact:true}).click();
 const dialog=page.getByRole('dialog',{name:label,exact:true});
 const note=dialog.locator('textarea[name="reason"]');
 if(await note.count())await note.fill('Synthetic TEST maintenance action');
 const saved=responseFor(page,`/work/${workId}/actions/${action}`);
 await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
 assert.equal((await saved).status(),200);await expect(dialog).toHaveCount(0);
}

export async function prepareRepairBrowser(fixture,workId,external){
 return session(fixture,external?'lead':'technician',async page=>{
  await page.goto(`http://127.0.0.1:5174/login#work?kind=MAINTENANCE&id=${workId}`);
  await page.getByRole('combobox',{name:'Mẫu đã phát hành',exact:true}).selectOption(String(fixture.repairChecklist));
  const attached=responseFor(page,`/work/${workId}/execution-checklist`);
  await page.getByRole('button',{name:'Gắn checklist vào công việc',exact:true}).click();assert.equal((await attached).status(),200);
  if(!external){await command(page,workId,'accept','Nhận việc');await command(page,workId,'start','Bắt đầu');}
  await expect(page.locator('.check-item select').first()).toBeEnabled();
  await page.screenshot({path:`artifacts/restructure-v2/maintenance-started-${external?'external':'internal'}.png`,fullPage:false});
 });
}

export async function completeRepairBrowser(fixture,workId,external){
 return session(fixture,external?'lead':'technician',async page=>{
  const loaded=responseFor(page,`/work/${workId}`,'GET');
  await page.goto(`http://127.0.0.1:5174/login#work?kind=MAINTENANCE&id=${workId}`);
  const response=await loaded;assert.equal(response.status(),200,await response.text());
  assert.ok((await response.json()).checklist.length>0,'Repair detail must contain the attached checklist');
  const items=page.locator('.check-item select');await expect(items.first()).toBeEnabled();
  const count=await items.count();assert.ok(count>0);
  for(let i=0;i<count;i++){
   const saved=page.waitForResponse(r=>new URL(r.url()).pathname.startsWith(`/api/v2/work/${workId}/checklist/`)&&r.request().method()==='PUT');
   await items.nth(i).selectOption('PASS');assert.equal((await saved).status(),200);await expect(items.nth(i)).toHaveValue('PASS');
  }
  if(!external)await command(page,workId,'complete','Gửi hoàn thành');
  await page.screenshot({path:`artifacts/restructure-v2/maintenance-checklist-${external?'external':'internal'}.png`,fullPage:false});
 });
}

export async function reviewRepairBrowser(fixture,issueId,external){
 return session(fixture,'lead',async page=>{
  await page.goto('http://127.0.0.1:5174/login#room-issues?id='+issueId);
  await page.getByLabel('Kết quả sửa chữa / bổ sung thực tế',{exact:true}).fill('Synthetic TEST technical review passed');
  const saved=responseFor(page,`/work/room-issues/${issueId}/report-remedy`);
  await page.getByRole('button',{name:'Gửi giám sát buồng phòng kiểm tra',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(page.getByRole('button',{name:'Gửi giám sát buồng phòng kiểm tra',exact:true})).toHaveCount(0);
  await page.screenshot({path:`artifacts/restructure-v2/maintenance-reviewed-${external?'external':'internal'}.png`,fullPage:false});
 });
}
