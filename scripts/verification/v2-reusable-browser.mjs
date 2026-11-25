import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const labels={READY:'Sẵn sàng',IN_USE:'Tại phòng',DIRTY:'Cần vệ sinh',LAUNDRY:'Giặt là',DAMAGED:'Hư hỏng'};

export async function reusableMoveBrowser(fixture,source,status,quantity,location,directory){
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
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.store.token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#stock');
  await page.getByRole('button',{name:'Vật tư tái sử dụng',exact:true}).click();
  const row=page.locator('tbody tr').filter({has:page.getByRole('cell',{name:labels[source.status],exact:true})}).filter({hasText:source.name});
  await expect(row).toHaveCount(1);await row.getByRole('button',{name:'Luân chuyển',exact:true}).click();
  const dialog=page.getByRole('dialog',{name:'Luân chuyển '+source.name,exact:true});
  await dialog.getByLabel('Số lượng',{exact:true}).fill(String(quantity));
  await dialog.getByRole('combobox',{name:'Chuyển sang',exact:true}).selectOption(status);
  if(['DIRTY','DAMAGED'].includes(status))await dialog.getByRole('combobox',{name:'Nơi nhận',exact:true}).selectOption(location);
  await dialog.locator('select[name="location"]').selectOption(String(location==='room'?fixture.room:fixture.warehouse));
  await dialog.getByLabel('Lý do / Kết quả kiểm tra',{exact:true}).fill('Synthetic TEST reusable laundry cycle');
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/reusable-inventory/positions/${source.positionId}/move`&&r.request().method()==='POST');
  await dialog.getByRole('button',{name:'Xác nhận',exact:true}).click();
  const response=await saved;assert.equal(response.status(),200);const result=await response.json();await expect(dialog).toHaveCount(0);
  await expect(page.locator('tbody tr').filter({has:page.getByRole('cell',{name:labels[status],exact:true})}).filter({hasText:source.name})).toHaveCount(1);
  await page.screenshot({path:directory+`/reusable-${source.status.toLowerCase()}-${status.toLowerCase()}.png`,fullPage:false});
  assert.deepEqual(errors,[]);
  await writeFile(directory+`/reusable-${source.status.toLowerCase()}-${status.toLowerCase()}-browser.json`,JSON.stringify({status:'PASS',from:source.status,to:status,quantity,checks:['Storekeeper UI move succeeds','Destination state visible'],limits:'Synthetic reusable item equivalent to blanket/towel; ownership verified separately via API/SQL. No password login/SignalR.'},null,2));
  return {status:response.status(),data:result,requestBody:response.request().postDataJSON()};
 }catch(error){if(page)await page.screenshot({path:directory+'/reusable-browser-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
