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
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias===role).token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await run(page);assert.deepEqual(errors,[]);
 }catch(error){if(page)await page.screenshot({path:`artifacts/restructure-v2/cleaning-${role}-browser-failure.png`,fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}

export async function verifyCleaningAssessmentBrowser(fixture,workId){
 await session(fixture,'hk',async page=>{
  await page.goto(`http://127.0.0.1:5174/login#cleaning?kind=CLEANING&id=${workId}`);
  // Accepted UI combines receiving the assignment and starting it in one action.
  for(const [command,title] of [['start','Bắt đầu']]){
   await page.getByRole('button',{name:title,exact:true}).click({timeout:30000});
   const dialog=page.getByRole('dialog',{name:title,exact:true});
   const applied=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${workId}/actions/${command}`&&r.request().method()==='POST');
   await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
   assert.equal((await applied).status(),200);await expect(dialog).toHaveCount(0);
  }
  // Starting cleaning opens the existing completion dialog; dismiss it before assessment.
  const completion=page.getByRole('dialog',{name:'Gửi hoàn thành',exact:true});
  await expect(completion).toBeVisible();await completion.getByRole('button',{name:'Hủy',exact:true}).click();
  const form=page.locator('form').filter({has:page.getByRole('button',{name:'Lưu kiểm tra vật tư',exact:true})});
  for(const [prefix,value] of [['current','3'],['usable','3'],['unusable','0'],['expired','0'],['replenished','1']])await form.locator(`input[name^="${prefix}-"]`).fill(value);
  await expect(form.getByText(/Cần bổ sung: 1/)).toBeVisible();
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/supplies/room-assessments/${workId}`&&r.request().method()==='POST');
  await form.getByRole('button',{name:'Lưu kiểm tra vật tư',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(form.getByText(/Đã kiểm tra/)).toBeVisible();
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false);
  await page.screenshot({path:'artifacts/restructure-v2/cleaning-assessment-mobile.png',fullPage:true});
 });
}

export async function verifyPaperInspectionBrowser(fixture,workId){
 await session(fixture,'supervisor',async page=>{
  await page.goto(`http://127.0.0.1:5174/login#work?kind=CLEANING&id=${workId}`);
  await page.getByRole('button',{name:'Xác nhận hoàn thành',exact:true}).click({timeout:30000});
  const dialog=page.getByRole('dialog',{name:'Ghi kết quả kiểm tra phòng',exact:true});
  await dialog.getByRole('combobox',{name:'Kết quả',exact:true}).selectOption('PASS');
  await expect(dialog.locator('textarea[name="note"]')).toHaveValue('');
  await expect(dialog.getByRole('checkbox')).toHaveCount(0);
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${workId}/inspection-result`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Lưu kết quả',exact:true}).click();assert.equal((await saved).status(),200);
  await expect(dialog).toHaveCount(0);
  await page.screenshot({path:'artifacts/restructure-v2/cleaning-inspected.png',fullPage:true});
 });
}

export async function verifyCleaningCompletionBrowser(fixture,workId){
 let body;
 await session(fixture,'hk',async page=>{
  await page.goto(`http://127.0.0.1:5174/login#cleaning?kind=CLEANING&id=${workId}`);
  const items=page.locator('.check-item select');await expect(items.first()).toBeEnabled({timeout:30000});
  const count=await items.count();assert.ok(count>0);
  for(let i=0;i<count;i++){
   const saved=page.waitForResponse(r=>new URL(r.url()).pathname.startsWith(`/api/v2/work/${workId}/checklist/`)&&r.request().method()==='PUT');
   await items.nth(i).selectOption('PASS');assert.equal((await saved).status(),200);await expect(items.nth(i)).toHaveValue('PASS');
  }
  await page.getByRole('button',{name:'Gửi hoàn thành',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Gửi hoàn thành',exact:true});
  const completed=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/${workId}/actions/complete`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await completed;assert.equal(response.status(),200);body=response.request().postDataJSON();
  await expect(dialog).toHaveCount(0);
  await page.screenshot({path:'artifacts/restructure-v2/cleaning-completed-browser.png',fullPage:false});
 });
 return body;
}

export async function verifyShiftBrowser(fixture,command){
 const assignment=fixture.actors.find(a=>a.alias==='hk').assignment;
 const route=`/workday/assignments/${assignment}/${command}`;let body,result;
 await session(fixture,'hk',async page=>{
  await page.goto('http://127.0.0.1:5174/login#workday');
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2'+route&&r.request().method()==='POST');
  await page.getByRole('button',{name:command==='start'?'Bắt đầu ca':'Kết thúc ca',exact:true}).click({timeout:30000});
  if(command==='end')await page.getByRole('dialog',{name:'Kết thúc ca',exact:true}).getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);body=response.request().postDataJSON();result=await response.json();
  if(command==='start')await expect(page.locator('.active-shift')).toBeVisible();else await expect(page.locator('.active-shift')).toHaveCount(0);
  await page.screenshot({path:`artifacts/restructure-v2/housekeeping-shift-${command}.png`,fullPage:false});
 });
 return {route,body,result};
}
