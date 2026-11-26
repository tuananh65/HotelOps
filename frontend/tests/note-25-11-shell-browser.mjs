import {chromium,expect} from '@playwright/test';
import {mkdir,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const accounts=JSON.parse(process.env.HOTELOPS_BROWSER_ACCOUNTS||'{}');
if(!Object.keys(accounts).length)throw new Error('Run the isolated HTTP runner with -Browser.');
const output=fileURLToPath(new URL('../../.artifacts/note-25-11-shell/',import.meta.url));
await mkdir(output,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const results=[];
try{
 for(const [role,account] of Object.entries(accounts)){
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  await page.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),account.Token);
  await page.route('**/api/v2/**',async route=>{
   try{const url=new URL(route.request().url());url.host='127.0.0.1:5092';url.protocol='http:';const response=await route.fetch({url:url.href,timeout:30000});await route.fulfill({response});}
   catch{await route.abort().catch(()=>{});}
  });
  try{
   await page.goto('http://127.0.0.1:5173/');
   await expect(page.getByRole('button',{name:'Thông tin cá nhân',exact:true})).toBeVisible({timeout:30000});
   const nav=page.getByRole('navigation',{name:'Điều hướng chính'});
   await expect(nav).toBeVisible();
   const apiRead=async path=>{const response=await page.request.get(`http://127.0.0.1:5092/api/v2/${path}`,{headers:{Authorization:`Bearer ${account.Token}`}});expect(response.status(),path).toBe(200);return response.json();};
   const pass=name=>results.push({name:role+' '+name,result:'PASS'});
   await expect(nav.getByRole('button',{name:'Checklist',exact:true})).toHaveCount(0);
   if(role==='HOUSEKEEPING_SUPERVISOR'){
    const data=await apiRead(`insights/housekeeping?branchId=${account.Branch}`);
    const chart=page.getByRole('region',{name:'Khối lượng dọn phòng (công việc)'});
    await expect(chart).toBeVisible();
    const total=data.workload.reduce((sum,r)=>sum+r.count,0);
    if(total){
     await expect(chart.locator('.chart-body.bars')).toBeVisible();
     await expect(chart.locator('.chart-track')).toHaveCount(data.workload.length);
     expect(await chart.locator('.chart-values strong').allTextContents()).toEqual(data.workload.map(r=>r.count.toLocaleString('vi-VN')));
     pass('workload horizontal bars show actual API counts');
    }else{
     await expect(chart).toContainText('Chưa có dữ liệu để thống kê.');
     results.push({name:role+' live workload horizontal bars',result:'NOT_RUN',reason:'Existing clone has no active cleaning work; empty state verified'});
    }
   }
   if(role==='OPERATION_MANAGER'){
    await expect(nav.getByRole('button',{name:'Nhân sự',exact:true})).toBeVisible();
    await page.goto('http://127.0.0.1:5173/#work');
    const personnel=await apiRead(`workday/personnel?branchId=${account.Branch}`);
    expect(personnel.length).toBeGreaterThan(0);
    await expect(page.getByRole('columnheader',{name:'Liên hệ',exact:true})).toBeVisible();
    for(const employee of personnel.slice(0,10)){
     const row=page.locator('#main-content tbody tr').filter({hasText:employee.fullName});
     await expect(row).toContainText(employee.phone||'Chưa có số điện thoại');
     await expect(row).toContainText(employee.email||'Chưa có email');
    }
    pass('personnel contacts rendered from API');
    await expect(page.locator('#main-content')).not.toContainText('Công việc vận hành');
    await expect(page.getByRole('button',{name:'Lịch làm việc tuần',exact:true})).toHaveCount(0);
   }
   if(role==='STOREKEEPER'){
    await expect(page.locator('#main-content a[href*="create"]')).toHaveCount(0);
    await expect(page.locator('#main-content a[href="#catalog"]')).toHaveCount(0);
    pass('dashboard has no forbidden catalog/create shortcuts');
    await expect(nav).not.toContainText('Bổ sung vật tư');await expect(nav).not.toContainText('Lịch sử & Chứng từ');
    await page.goto('http://127.0.0.1:5173/#stock');
    await expect(page.locator('#main-content')).not.toContainText('Xem kho chi nhánh khác');
    const stock=await apiRead(`insights/stock?branchId=${account.Branch}`);
    const warehouses=await apiRead(`catalog/warehouses?branchId=${account.Branch}&pageSize=100`);
    const active=new Set(warehouses.items.filter(w=>w.fields.isActive).map(w=>w.fields.warehouseId));
    const low=stock.filter(r=>active.has(r.warehouseId)&&r.minimumQty!=null&&(r.availableQty??r.baseQty)<r.minimumQty).sort((a,b)=>String(a.item).localeCompare(String(b.item),'vi'));
    await page.getByRole('button',{name:'Bộ lọc',exact:true}).click();
    await page.getByLabel('Dưới định mức').check();
    await expect(page.locator('#main-content tbody tr')).toHaveCount(Math.min(25,low.length));
    for(const [index,row] of low.slice(0,25).entries())await expect(page.locator('#main-content tbody tr').nth(index)).toContainText(row.item);
    pass('low-stock filter matches available quantity and minimum');
   }
   if(role==='OPERATION_MANAGER'){
    await page.goto('http://127.0.0.1:5173/#assets');
    const assets=await apiRead(`assets?branchId=${account.Branch}`);
    const tree=page.getByRole('region',{name:'Tài sản theo tầng và phòng'});
    await expect(tree).toBeVisible();
    expect(assets.length).toBeGreaterThan(0);
    await expect(tree.locator('details[open]')).toHaveCount(0);
    await expect(tree.locator('tbody tr')).toHaveCount(assets.length);
    if(assets.some(a=>a.floorId==null))await expect(tree).toContainText('Khu vực chung / chưa gán tầng');
    pass('asset tree collapsed and all API assets retained');
    await page.goto('http://127.0.0.1:5173/#stock');
    await expect(page.getByRole('link',{name:'Duyệt vật tư tiêu hao',exact:true})).toHaveAttribute('href','#requests?tab=inbox');
    await page.getByRole('button',{name:'Xem kho chi nhánh khác',exact:true}).click();
    await expect(page.getByText('Chỉ xem tồn khả dụng để đề nghị điều chuyển.',{exact:false})).toBeVisible();
    await expect(page.getByLabel('Kho cung cấp')).toBeVisible();
    await page.keyboard.press('Escape');
    await page.getByRole('button',{name:'Vật tư tái sử dụng',exact:true}).click();
    await expect(page.getByRole('button',{name:'Tổng hợp',exact:true})).toBeVisible();
    await expect(page.getByRole('button',{name:'Đề nghị ghi giảm',exact:true})).toHaveCount(0);
    await expect(page.getByRole('columnheader',{name:'Vật tư / Vị trí',exact:true})).toHaveCount(0);
    pass('stock narrow cross-branch view, approval link and reusable summary');
    await page.goto('http://127.0.0.1:5173/#configuration?tab=checklists');
    await expect(page.getByRole('link',{name:'Checklist',exact:true})).toHaveCount(0);
    await expect(page.getByText('Bạn chưa có quyền truy cập nhóm cấu hình này.',{exact:true})).toBeVisible();
    pass('checklist configuration denied to OM without manage permission');
   }
   if(role==='OWNER'){
    await expect(page.getByRole('button',{name:'Lịch làm việc tuần',exact:true})).toHaveCount(0);
    await expect(page.locator('#main-content')).not.toContainText('Công việc cần chú ý');
   }
   if(!['OWNER','OPERATION_MANAGER'].includes(role)){
    await page.getByRole('button',{name:'Lịch làm việc tuần',exact:true}).click();
    await expect(page.locator('.personal-week-calendar > section')).toHaveCount(7);
    await page.keyboard.press('Escape');
   }
   if(role==='HOUSEKEEPING_SUPERVISOR'){
    const profile=await apiRead('auth/me');
    expect(profile.grants.some(g=>g.code==='CHECKLIST_MANAGE')).toBe(true);
    await page.goto('http://127.0.0.1:5173/#configuration?tab=checklists');
    await expect(page.getByRole('link',{name:'Checklist',exact:true})).toBeVisible();
    pass('authorized checklist configuration route available without standalone nav');
   }
   const badge=page.locator('.notification-bell span');
   if(await badge.count()){
    const style=await badge.evaluate(e=>({position:getComputedStyle(e).position,color:getComputedStyle(e).color,background:getComputedStyle(e).backgroundColor}));
    expect(style.position).toBe('absolute');expect(style.color).toBe('rgb(255, 255, 255)');
    results.push({name:role+' unread badge styles',result:'PASS',background:style.background});
   }else results.push({name:role+' unread badge styles',result:'NOT_RUN',reason:'No visible unread notifications in existing clone data'});
   await page.getByRole('button',{name:'Thông báo',exact:true}).click();
   await expect(page).toHaveURL(/#notifications/);
   await page.screenshot({path:output+role+'-desktop.png'});
   await page.setViewportSize({width:320,height:800});
   await expect(page.getByRole('button',{name:'Mở điều hướng',exact:true})).toBeVisible();
   await page.getByRole('button',{name:'Mở điều hướng',exact:true}).click();await expect(nav).toBeVisible();
   await expect.poll(async()=>await nav.evaluate(e=>e.closest('aside').getBoundingClientRect().left)).toBeGreaterThanOrEqual(0);
   await page.screenshot({path:output+role+'-mobile.png'});
   results.push({name:role+' real API shell/navigation/calendar/mobile',result:'PASS'});
  }catch(error){results.push({name:role+' real API shell',result:'FAIL',error:error.message});await page.screenshot({path:output+role+'-failure.png'}).catch(()=>{});}
  finally{await page.close();}
 }
 await writeFile(output+'results.json',JSON.stringify(results,null,2));
 console.log(JSON.stringify({suite:'Actual Shell and isolated API, no mocked payloads',results},null,2));
 if(results.some(r=>r.result==='FAIL'))process.exitCode=1;
}finally{await browser.close();}
