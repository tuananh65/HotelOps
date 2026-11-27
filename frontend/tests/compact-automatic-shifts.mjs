import assert from 'node:assert/strict';
import fs from 'node:fs';
import {chromium, expect} from '@playwright/test';

// Uses real HTTP responses from an isolated, backed-up SQL clone; no API mocks.
const fixturePath=process.env.HOTELOPS_AUTO_SHIFT_BROWSER_SESSION;
assert.ok(fixturePath, 'Set HOTELOPS_AUTO_SHIFT_BROWSER_SESSION to the isolated test-session JSON.');
const fixture=JSON.parse(fs.readFileSync(fixturePath,'utf8'));
const api=process.env.HOTELOPS_AUTO_SHIFT_API || 'http://127.0.0.1:5082';
const frontend=process.env.HOTELOPS_AUTO_SHIFT_FRONTEND || 'http://127.0.0.1:5173';
async function get(path,token){
  const result=await fetch(`${api}/api/v2/${path}`,{headers:{Authorization:`Bearer ${token}`}});
  assert.equal(result.status,200,`${path}: ${result.status}`);
  return result.json();
}
const access=await get(`workday/command-access?branchId=${fixture.branchId}`,fixture.staffToken);
assert.equal(access.canOperate,true);
const options=await get(`work/cleaning-creation-options?branchId=${fixture.branchId}`,fixture.supervisorToken);
assert.equal(options.canCreate,true);
assert.ok(options.candidates.some(c=>c.userId===fixture.staffId));
const week=await get(`workday?branchId=${fixture.branchId}&date=${fixture.today}`,fixture.staffToken);
assert.equal(week.active.status,'WORKING');
assert.equal(week.pendingReconciliations.length,0);
const browser=await chromium.launch({executablePath:process.env.HOTELOPS_BROWSER_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const errors=[];const writes=[];
try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.addInitScript(({token})=>sessionStorage.setItem('hotelops.compact.session',token),{token:fixture.staffToken});
  await context.route('**/api/v2/**',async route=>{
    const request=route.request();
    if(request.method()!=='GET')writes.push(new URL(request.url()).pathname);
    const url=new URL(request.url());
    const response=await route.fetch({url:api+url.pathname+url.search});
    await route.fulfill({response});
  });
  const page=await context.newPage();
  page.on('pageerror',error=>errors.push(error.message));
  await page.goto(`${frontend}/#workday`);
  await expect(page.getByRole('heading',{name:'Lịch làm việc',exact:true})).toBeVisible();
  await expect(page.locator('.active-shift').filter({hasText:'Ca đang hoạt động'})).toBeVisible();
  await expect(page.locator('.active-shift').filter({hasText:'chờ đối soát'})).toHaveCount(0);
  await expect(page.getByRole('button',{name:'Bắt đầu ca',exact:true})).toHaveCount(0);
  await page.getByRole('button',{name:'Chi tiết',exact:true}).last().click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog').getByRole('button',{name:'Bắt đầu ca',exact:true})).toHaveCount(0);
  await page.keyboard.press('Escape');
  fs.mkdirSync('../artifacts/automatic-shifts',{recursive:true});
  for(const width of [1440,390]){
    await page.setViewportSize({width,height:1000});
    await page.waitForTimeout(350);
    assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`horizontal overflow at ${width}`);
    await page.screenshot({path:`../artifacts/automatic-shifts/workday-${width}.png`,fullPage:true});
  }
  assert.deepEqual(errors,[]);
  assert.ok(!writes.some(p=>/workday\/assignments\/\d+\/start$/.test(p)));
  // Dispose long-poll routes before closing their request context.
  await context.unrouteAll({behavior:'ignoreErrors'});
  console.log('PASS: real HTTP auto-activation, supervisor candidates, current/pending shifts, no start button/request, desktop/mobile, no page errors.');
}finally{await browser.close();}
