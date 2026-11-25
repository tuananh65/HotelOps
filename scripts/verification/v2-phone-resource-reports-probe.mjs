import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const fields=x=>x.fields??x;

// Called only inside the guarded, backup-checked TEST fixture runner.
// All domain mutations go through HTTP; browser responses are real proxied API responses.
export async function verifyPhoneAndResourceReports({fixture,foreignBranch,ok,request,directory}){
 assert.match(fixture.prefix,/^V2HK-/);
 const checks=[],reason='Synthetic TEST Nov25 resource report '+fixture.prefix;
 let phase='candidate API',browser,page;
 const errors=[];
 try{
 const tomorrow=new Date(Date.now()+86400000);
 const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(tomorrow);
 const week=await ok('manager','/workday?branchId='+fixture.branch+'&date='+date);
 const today=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
 const current=await ok('manager','/workday?branchId='+fixture.branch+'&date='+today);
 const hk=fixture.actors.find(x=>x.alias==='hk'),peer=fixture.actors.find(x=>x.alias==='peer');
 const assignment=current.assignments.find(x=>x.assignmentId===hk.assignment);assert.ok(assignment);
 const shift=week.shifts.find(x=>x.shiftId===assignment.shiftId);assert.ok(shift);
 const candidates=await ok('manager','/workday/candidates?shiftId='+shift.shiftId+'&date='+date);
 assert.equal(candidates.find(x=>x.userId===hk.user)?.phone,'0900000001');
 assert.equal(candidates.find(x=>x.userId===peer.user)?.phone,null);
 checks.push('actual candidates endpoint returns synthetic stored phone and null unchanged');

 phase='resource mutations and report APIs';
 const fill=fixture.items.find(x=>x.code.endsWith('-fill'));
 await ok('manager','/inventory-lifecycle/profiles','PUT',{branchId:fixture.branch,itemId:fill.itemId,classification:'CONSUMABLE',expiryPolicy:'NONE',reason});
 await ok('manager','/supplies/configuration','PUT',{branchId:fixture.branch,itemId:fill.itemId,supplyGroup:'A',warehouseId:fixture.warehouse,unitId:fixture.unit,initialQuantity:1,minimumStock:0,perGuestQuantity:1,isActive:true});
 const document=fields(await ok('store','/documents','POST',{branchId:fixture.branch,kind:'RECEIPT',destinationWarehouseId:fixture.warehouse,reason,supplierName:'TEST report supplier'}));
 const line=await ok('store','/documents/'+document.documentId+'/lines','POST',{rowVersion:document.rowVersion,itemId:fill.itemId,unitId:fixture.unit,quantity:3});
 await ok('store','/documents/'+document.documentId+'/actions/post','POST',{rowVersion:fields(line.document).rowVersion,operationKey:randomUUID(),reason});
 const assetId=fixture.assets.at(-1).assetId;
 const original=fields((await ok('manager','/assets/'+assetId)).asset);assert.equal(original.conditionCode,'GOOD');
 await ok('manager','/assets/'+assetId+'/actions/condition','POST',{rowVersion:original.rowVersion,operationKey:randomUUID(),condition:'FAIR',reason});
 const endpoint=kind=>'/insights/reports/'+kind+'?branchId='+fixture.branch;
 const events=await ok('manager',endpoint('operations-events'));
 assert.ok(events.some(x=>x.category==='Kho'&&x.subject.includes(document.documentNo)&&x.action==='Ghi sổ'));
 assert.ok(events.some(x=>x.category==='Tài sản'&&x.subject.includes(original.assetCode)&&x.action==='Cập nhật tình trạng'&&x.detail===reason));
 const stock=await ok('manager',endpoint('stock'));
 assert.ok(stock.map(fields).some(x=>x.itemId===fill.itemId&&Number(x.baseQty)===3));
 const assets=await ok('manager',endpoint('assets'));
 assert.ok(assets.map(fields).some(x=>x.assetCode===original.assetCode&&x.conditionCode==='FAIR'));
 for(const kind of ['operations-events','stock','assets'])assert.equal((await request('manager','/insights/reports/'+kind+'?branchId='+foreignBranch)).status,403);
 checks.push('posted receipt and asset condition appear in persisted OM events/current reports; foreign branch denied');
 const before={document:await ok('store','/documents/'+document.documentId),asset:await ok('manager','/assets/'+assetId),stock};
 phase='open staffing browser';
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   assert.equal(route.request().method(),'GET','Read-only browser must not mutate domain data');
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   await route.fulfill({response:await route.fetch({url:url.toString()})});
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(x=>x.alias==='manager').token);
  page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  const apiLoaded=(path,query={})=>page.waitForResponse(response=>{
   const url=new URL(response.url());
   return url.pathname==='/api/v2/'+path&&Object.entries(query).every(([key,value])=>url.searchParams.get(key)===String(value))&&response.request().method()==='GET';
  });
  await page.goto('http://127.0.0.1:5174/login#work?kind=staff');
  phase='staffing date and shift filters';
  const weekLoaded=apiLoaded('workday',{branchId:fixture.branch,date});
  await page.getByLabel('Ngày phân ca',{exact:true}).fill(date);
  assert.equal((await weekLoaded).status(),200);
  // The implicit label also contains option text. Match its accessible prefix,
  // then wait for the requested date's choices instead of a transient form.
  const shiftSelect=page.getByRole('combobox',{name:/^Ca cần bố trí/});
  await expect(shiftSelect.locator(`option[value="${shift.shiftId}"]`)).toHaveCount(1);
  if(await shiftSelect.inputValue()!==String(shift.shiftId)){
   const candidatesLoaded=apiLoaded('workday/candidates',{shiftId:shift.shiftId,date});
   await shiftSelect.selectOption(String(shift.shiftId));
   assert.equal((await candidatesLoaded).status(),200);
  }
  await expect(page.getByRole('columnheader',{name:'Số điện thoại',exact:true})).toBeVisible();
  await expect(page.getByRole('columnheader',{name:'Chi nhánh',exact:true})).toHaveCount(0);
  await expect(page.getByRole('row').filter({hasText:'TEST hk'}).getByRole('cell',{name:'0900000001',exact:true})).toBeVisible();
  await expect(page.getByRole('row').filter({hasText:'TEST peer'}).getByRole('cell',{name:'Chưa cập nhật',exact:true})).toBeVisible();
  await page.screenshot({path:directory+'/staff-phone-live.png',fullPage:true});
  checks.push('Edge displays actual phone and missing-phone fallback for the selected shift/date');
  phase='OM activity report browser';
  const eventsLoaded=apiLoaded('insights/reports/operations-events',{branchId:fixture.branch});
  await page.goto('http://127.0.0.1:5174/login#reports');
  assert.equal((await eventsLoaded).status(),200);
  await expect(page.getByRole('row').filter({hasText:document.documentNo}).filter({hasText:'Ghi sổ'})).toHaveCount(1);
  await expect(page.getByRole('row').filter({hasText:original.assetCode}).filter({hasText:'Cập nhật tình trạng'})).toHaveCount(1);
  await page.screenshot({path:directory+'/om-resource-events-live.png',fullPage:true});
  phase='OM stock report browser';
  const stockLoaded=apiLoaded('insights/reports/stock',{branchId:fixture.branch});
  await page.getByRole('combobox',{name:/^Loại báo cáo/}).selectOption('stock');
  assert.equal((await stockLoaded).status(),200);
  await expect(page.locator('tbody')).toContainText(String(fill.itemId));
  phase='OM assets report browser';
  const assetsLoaded=apiLoaded('insights/reports/assets',{branchId:fixture.branch});
  await page.getByRole('combobox',{name:/^Loại báo cáo/}).selectOption('assets');
  assert.equal((await assetsLoaded).status(),200);
  await expect(page.locator('tbody')).toContainText(original.assetCode);
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:directory+'/om-assets-live-mobile.png',fullPage:true});
  assert.deepEqual(errors,[]);
  assert.deepEqual({document:await ok('store','/documents/'+document.documentId),asset:await ok('manager','/assets/'+assetId),stock:await ok('manager',endpoint('stock'))},before);
  checks.push('Edge displays actual phone, missing-phone fallback, warehouse/asset events and current reports without domain mutations');
  await writeFile(directory+'/phone-resource-reports-live.json',JSON.stringify({at:new Date().toISOString(),status:'PASS',database:'HotelOpsCompactWorkdayTestV22611250010',checks,limits:'Synthetic TEST only. Sessions injected; password login and realtime not part of this probe.'},null,2));
 }catch(error){
  if(page&&!page.isClosed())await page.screenshot({path:directory+'/phone-resource-reports-failure.png',fullPage:false,timeout:5000}).catch(()=>{});
  const scrub=value=>String(value).replace(/[a-f0-9]{64}/ig,'[REDACTED]');
  await writeFile(directory+'/phone-resource-reports-live.json',JSON.stringify({at:new Date().toISOString(),status:'FAIL',phase,checks,error:scrub(error.message),browserErrors:errors.map(scrub),limits:'Only listed checks completed; no overall PASS. Synthetic TEST only.'},null,2));
  throw error;
 }finally{if(browser)await browser.close();}
}
