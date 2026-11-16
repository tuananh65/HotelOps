import { chromium } from '@playwright/test';
import assert from 'node:assert/strict';
let input='';for await(const chunk of process.stdin)input+=chunk;const f=JSON.parse(input);input='';
let browser,stage='login';const checks=[];
try {
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const page=await browser.newPage();page.setDefaultTimeout(15000);
 await page.goto('https://localhost:7142/');await page.getByLabel('Username or email').fill(f.username);await page.getByLabel('Password',{exact:true}).fill(f.password);f.password='';
 await page.getByRole('button',{name:'Sign in',exact:true}).click();await page.waitForURL('**/dashboard');await page.goto('https://localhost:7142/');await page.getByRole('link',{name:'Inspection checklists',exact:true}).click();
 stage='branch admission';await page.getByLabel('Branch ID',{exact:true}).fill(String(f.branch));await page.getByRole('button',{name:'Load checklists'}).click();
 await page.getByRole('button',{name:'Tạo checklist',exact:true}).click();checks.push(stage);
 stage='create Draft';const form=page.getByRole('form',{name:'Tạo checklist',exact:true});
 await form.getByLabel('Mã checklist',{exact:true}).fill('BROWSER-'+Date.now());await form.getByLabel('Tên checklist',{exact:true}).fill('Browser inspection standard');
 await form.getByLabel('Mã loại phòng áp dụng (phân cách bằng dấu phẩy)',{exact:true}).fill(String(f.type));await form.getByLabel('Nội dung',{exact:true}).fill('Clean surfaces');await form.getByLabel('Bắt buộc',{exact:true}).check();
 await form.getByRole('button',{name:'Lưu Draft',exact:true}).click();
 const detail=page.getByRole('region',{name:'Chi tiết checklist',exact:true});await detail.getByRole('heading',{name:'Phiên bản 1 — DRAFT',exact:true}).waitFor();checks.push(stage);
 async function transition(action){await detail.getByRole('button',{name:action,exact:true}).click();const confirm=detail.getByRole('form',{name:'Xác nhận phiên bản'});if(action==='Ngừng sử dụng bản công bố')await confirm.getByText(/^MSG33:/).waitFor();await confirm.getByLabel('Lý do',{exact:true}).fill('Reviewed browser inspection standard');assert.equal(await confirm.getByRole('button',{name:'Xác nhận',exact:true}).isEnabled(),false);await confirm.getByLabel('Tôi xác nhận thao tác',{exact:true}).check();await confirm.getByRole('button',{name:'Xác nhận',exact:true}).click();}
 stage='publish first version';await transition('Công bố Draft');await detail.getByRole('heading',{name:'Phiên bản 1 — PUBLISHED',exact:true}).waitFor();checks.push(stage);
 stage='create next Draft';await detail.getByRole('button',{name:'Tạo Draft kế tiếp',exact:true}).click();const edit=detail.getByRole('form',{name:'Chỉnh sửa tiêu chí',exact:true});await edit.getByLabel('Nội dung',{exact:true}).fill('Clean surfaces and fixtures');await edit.getByLabel('Lý do cập nhật',{exact:true}).fill('Extend inspection coverage');await edit.getByRole('button',{name:'Lưu Draft',exact:true}).click();await detail.getByRole('heading',{name:'Phiên bản 2 — DRAFT',exact:true}).waitFor();checks.push(stage);
 stage='replace Published version';await transition('Công bố Draft');await detail.getByRole('heading',{name:'Phiên bản 1 — RETIRED',exact:true}).waitFor();await detail.getByRole('heading',{name:'Phiên bản 2 — PUBLISHED',exact:true}).waitFor();checks.push(stage);
 stage='retire current version';await transition('Ngừng sử dụng bản công bố');await detail.getByRole('heading',{name:'Phiên bản 2 — RETIRED',exact:true}).waitFor();await detail.getByText('MSG08: Checklist đã được lưu thành công.',{exact:true}).waitFor();checks.push(stage);
 stage='logout';await page.goto('https://localhost:7142/profile');await page.getByRole('button',{name:'Logout',exact:true}).click();await page.waitForURL('**/login');checks.push(stage);
 console.log(JSON.stringify({success:true,stage,checks}));
}catch(error){console.log(JSON.stringify({success:false,stage,checks,error:String(error.message).slice(0,700)}));process.exitCode=1;}finally{await browser?.close();}
