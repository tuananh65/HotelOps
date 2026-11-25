import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

async function supervisorPage(fixture,name,action){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias==='supervisor').token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const result=await action(page);assert.deepEqual(errors,[]);return result;
 }catch(error){if(page)await page.screenshot({path:`artifacts/restructure-v2/${name}-failure.png`,fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}

// This replaces the runner's one POST, rather than creating a second task.
export async function createCleaningAssignmentBrowser(fixture,room,title,expectedStatus){
 return supervisorPage(fixture,'assignment-create',async page=>{
  await page.goto('http://127.0.0.1:5174/login#work');
  await page.getByRole('button',{name:'+ Tạo mới',exact:true}).click({timeout:30000});
  const rooms=page.getByRole('region',{name:'Phòng cần tạo việc dọn',exact:true});
  await rooms.getByRole('textbox',{name:'Tìm phòng cần dọn',exact:true}).fill(room.code);
  const row=rooms.locator('tbody tr').filter({has:page.getByText('Phòng '+room.code,{exact:true})});
  await expect(row).toHaveCount(1);
  await row.getByRole('button',{name:'Tạo yêu cầu',exact:true}).click();
  const form=page.locator('form').filter({has:page.locator('input[name="title"]')});
  await form.locator('input[name="title"]').fill(title);
  await form.locator('select[name="assigneeUserId"]').selectOption(String(fixture.actors.find(a=>a.alias==='hk').user));
  await form.locator('input[name="durationMinutes"][value="30"]').check();
  const committed=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/work'&&r.request().method()==='POST');
  await form.getByRole('button',{name:'Tạo công việc',exact:true}).click();
  const response=await committed;assert.equal(response.status(),expectedStatus);const data=await response.json();
  if(expectedStatus===409){
   assert.equal(data.code??data.title,'STAFF_SUPPLIES_INSUFFICIENT');
   await expect(page.getByText('Nhân viên không đủ vật tư khả dụng để nhận phòng này. Kiểm tra vật tư đang giữ chỗ và gửi yêu cầu bổ sung.',{exact:true})).toBeVisible();
  }else await expect(form).toHaveCount(0);
  const name=expectedStatus===409?'assignment-insufficient':'assignment-second-room';
  await page.screenshot({path:`artifacts/restructure-v2/${name}.png`,fullPage:true});
  await writeFile(`artifacts/restructure-v2/${name}.json`,JSON.stringify({status:'PASS',roomId:room.roomId,httpStatus:response.status(),limits:'Actual TEST supervisor browser creation; caller checks reservation/ledger snapshots. No password login or SignalR.'},null,2));
  return {status:response.status(),data};
 });
}

// Cancellation remains the existing API operation: accepted cleaning UX hides
// its cancel button. Verify the resulting status without inventing a new action.
export async function verifyCancelledReservationBrowser(fixture,workId){
 await supervisorPage(fixture,'reservation-cancelled',async page=>{
  const loaded=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${workId}`&&r.status()===200);
  await page.goto(`http://127.0.0.1:5174/login#work?kind=CLEANING&id=${workId}`);
  const detail=await (await loaded).json();assert.equal(detail.work.fields.status,'CANCELLED');
  await expect(page.getByText('Đã hủy',{exact:true}).first()).toBeVisible();
  await page.screenshot({path:'artifacts/restructure-v2/reservation-cancelled.png',fullPage:true});
 });
}
