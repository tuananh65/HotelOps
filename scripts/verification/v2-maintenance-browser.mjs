import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';
const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');

export async function verifyIncidentAdvisoryBrowser(fixture,issueId,role,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try{
  const context=await browser.newContext({viewport:{width:390,height:844}});
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.actors.find(a=>a.alias===role).token);
  const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#room-issues?id='+issueId);
  const responsePromise=page.waitForResponse(r=>new URL(r.url()).pathname==='/api/v2/advisory'&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Xem phân tích',exact:true}).click();
  const response=await responsePromise;assert.equal(response.status(),200);const result=await response.json();
  assert.equal(result.method,'STATISTICAL_RULES');assert.equal(result.externalStatus,'BLOCKED_EXTERNAL_CONFIGURATION');assert.equal(result.requiresHumanDecision,true);
  await expect(page.getByRole('heading',{name:'Thống kê và quy tắc nghiệp vụ — không phải kết quả AI',exact:true})).toBeVisible();
  await expect(page.getByText(/Không suy ra người gây sự cố từ người báo cáo/)).toBeVisible();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.getByRole('heading',{name:'Hỗ trợ phân tích',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:directory+`/advisory-incident-${role}-mobile.png`,fullPage:false});assert.deepEqual(errors,[]);
  await writeFile(directory+`/advisory-incident-${role}-browser.json`,JSON.stringify({status:'PASS',role,checks:['Mobile incident analysis uses statistical/rules fallback','External configuration absence explicit','Human decision required; reporter is not presumed responsible','No viewport overflow'],limits:'Owned TEST session; no external AI calls or password login/SignalR.'},null,2));
  return result;
 }finally{await browser.close();}
}

export async function verifyMaintenanceInspection(fixture,issueId,directory){
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 let page;
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  // Static frontend only; all API traffic goes to the isolated TEST server.
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname.startsWith('/api/v2/realtime/')){await route.abort();return;}
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{await route.fulfill({response:await route.fetch({url:url.toString()})});}
   catch{await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  const supervisor=fixture.actors.find(a=>a.alias==='supervisor'),peer=fixture.actors.find(a=>a.alias==='peer');
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),supervisor.token);
  page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:5174/login#room-issues?id='+issueId);
  await expect(page.getByRole('combobox',{name:/^Nguyên nhân/})).toBeVisible({timeout:30000});
  await page.getByRole('combobox',{name:/^Nguyên nhân/}).selectOption('EMPLOYEE');
  await page.getByRole('combobox',{name:/^Người có liên quan/}).selectOption(String(peer.user));
  await page.getByLabel('Căn cứ xác minh',{exact:true}).fill('Synthetic TEST related-person verification');
  const saved=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/room-issues/${issueId}/investigation`&&r.request().method()==='POST');
  await page.getByRole('button',{name:'Lưu kết quả xác minh',exact:true}).click();
  assert.equal((await saved).status(),200);
  const detail=await context.request.get(`http://127.0.0.1:5779/api/v2/work/room-issues/${issueId}`,{headers:{Authorization:'Bearer '+supervisor.token}});
  assert.equal(detail.status(),200);const data=await detail.json();
  assert.ok(data.investigation.some(e=>e.details.Cause==='EMPLOYEE'&&e.details.RelatedUserId===peer.user));
  assert.equal(data.reporterUserId,fixture.actors.find(a=>a.alias==='hk').user);
  assert.equal(data.info.detectedByUserId,peer.user);
  await expect(page.getByRole('button',{name:'Gửi kết quả kiểm tra',exact:true})).toBeEnabled();
  await page.screenshot({path:directory+'/maintenance-inspection-desktop.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1),false,'mobile horizontal overflow');
  await page.screenshot({path:directory+'/maintenance-inspection-mobile.png',fullPage:true});
  await page.getByRole('button',{name:'Gửi kết quả kiểm tra',exact:true}).click();
  const inspection=page.getByRole('dialog',{name:'Gửi kết quả kiểm tra phòng',exact:true});
  await expect(inspection).toBeVisible();
  await inspection.locator('select[name="result"]').selectOption('FAIL');
  await expect(inspection.getByLabel('Ghi chú (bắt buộc khi không đạt)',{exact:true})).toHaveAttribute('required','');
  await inspection.locator('select[name="result"]').selectOption('PASS');
  await expect(inspection.getByLabel('Ghi chú (bắt buộc khi không đạt)',{exact:true})).toHaveValue('');
  const inspected=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/v2/work/room-issues/${issueId}/inspection-pass`&&r.request().method()==='POST');
  await inspection.getByRole('button',{name:'Xác nhận',exact:true}).click();
  assert.equal((await inspected).status(),200);
  await expect(page.getByText('Đã kiểm tra đạt',{exact:true}).first()).toBeVisible();
  assert.deepEqual(errors,[]);
  await writeFile(directory+'/maintenance-inspection-browser.json',JSON.stringify({status:'PASS',checks:['related person selection and persisted cause','reporter/discoverer preserved','mobile width','HKS PASS without note through UI','no page errors'],limits:'Synthetic TEST session. SignalR transport and password login not covered.'},null,2));
 }catch(error){if(page)await page.screenshot({path:directory+'/maintenance-browser-failure.png',fullPage:true}).catch(()=>{});throw error;}
 finally{await browser.close();}
}
