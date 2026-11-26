import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage();let started=false,reads=0;const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/v2/supplies/room-assessments/690',route=>{
  reads++;return route.fulfill({json:started?[{reservationId:57,itemId:213,name:'Vật tư kiểm thử',requiredQuantity:2,missingQuantity:2,rowVersion:'fixture'}]:[]});
 });
 await page.route('**/api/v2/work/690/execution-checklists',route=>route.fulfill({json:[]}));
 await page.goto('http://127.0.0.1:5173/tests/cleaning-assessment-harness.html');
 await expect(page.getByText('Chưa có dữ liệu giữ chỗ vật tư cho công việc này.')).toBeVisible();
 await expect(page.getByText('Công việc chưa có checklist.',{exact:false})).toBeVisible();
 await expect(page.locator('select[name="template"]')).toHaveCount(0);
 started=true;await page.evaluate(()=>window.showCleaningFixture('IN_PROGRESS'));
 await expect(page.getByText('Vật tư kiểm thử · Định mức 2')).toBeVisible();
 await expect(page.getByText('Chưa có dữ liệu giữ chỗ vật tư cho công việc này.')).toHaveCount(0);
 assert.ok(reads>=2);assert.deepEqual(errors,[]);
 await page.getByRole('button',{name:'Thêm checklist (tùy chọn)'}).click();
 await expect(page.getByText('Chưa có checklist phù hợp được phát hành. Bạn vẫn có thể hoàn thành công việc mà không gắn checklist.')).toBeVisible();
 console.log('PASS: assessment refresh after start and optional cleaning checklist UI (mock API, real browser/components).');
}finally{await browser.close();}
