import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFile} from 'node:fs/promises';

const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const fields=value=>value.fields??value;
const base='http://127.0.0.1:5779/api/v2';

// Uses the caller's live inventory fixture. Never creates notifications or stock.
export async function lowStockBrowser(fixture,expected,directory){
 const {kind,itemName,notificationId,available,minimum,missing}=expected;
 assert.ok(['consumable','reusable'].includes(kind));
 assert.ok(itemName&&notificationId);
 const prefix=directory+'/low-stock-'+kind;
 const read=async path=>{
  const response=await fetch(base+path,{headers:{Authorization:'Bearer '+fixture.sessions.store.token},signal:AbortSignal.timeout(15000)});
  assert.equal(response.status,200,'Snapshot GET '+path);return response.json();
 };
 const snapshot=async()=>({
  batches:await read(`/inventory-lifecycle/batches?branchId=${fixture.branch}&itemId=${fixture.items.fefo}`),
  reusable:await read(`/reusable-inventory?branchId=${fixture.branch}`),
  notifications:await read(`/notifications?branchId=${fixture.branch}&pageSize=100`),
 });
 const before=await snapshot();
 const alert=before.notifications.items.map(fields).find(row=>row.notificationId===notificationId);
 assert.ok(alert,'Expected real notification exists');
 assert.equal(alert.notificationType,'LOW_STOCK');
 assert.equal(alert.recipientUserId,fixture.sessions.store.user);
 assert.equal(alert.branchId,fixture.branch);
 const fragments=[itemName,`khả dụng ${available}`,`mức tối thiểu ${minimum}`,`thiếu ${missing}`];
 for(const fragment of fragments)assert.ok(alert.message.includes(fragment),'Real message must contain '+fragment);
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,timeout:20000});
 let page;
 const errors=[],blockedWrites=[],apiFailures=[],requests=[];
 try{
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  context.setDefaultTimeout(15000);context.setDefaultNavigationTimeout(20000);
  await context.route('**/api/**',async route=>{
   const request=route.request(),url=new URL(request.url());
   // Inbox rendering is the target; SignalR delivery is covered by its own probe.
   if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
   if(!['GET','HEAD','OPTIONS'].includes(request.method())){
    blockedWrites.push({method:request.method(),path:url.pathname});return route.abort();
   }
   url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{
    const response=await route.fetch({url:url.toString(),timeout:15000});
    requests.push({method:request.method(),path:url.pathname,status:response.status()});
    if(response.status()>=400)apiFailures.push({path:url.pathname,status:response.status()});
    await route.fulfill({response});
   }catch(error){apiFailures.push({path:url.pathname,error:error.name});await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.sessions.store.token);
  page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
  const inboxLoaded=page.waitForResponse(response=>{
   const url=new URL(response.url());
   return url.pathname==='/api/v2/notifications'&&Number(url.searchParams.get('branchId'))===fixture.branch&&response.status()===200;
  });
  await page.goto('http://127.0.0.1:5174/login#notifications');
  await inboxLoaded;
  const article=page.locator('.notification-list article').filter({has:page.getByRole('heading',{name:alert.title,exact:true})});
  await expect(article).toHaveCount(1);
  await expect(article.locator('h3')).toHaveText(alert.title);
  await expect(article.locator('p')).toHaveText(alert.message);
  for(const fragment of fragments)await expect(article.locator('p')).toContainText(fragment);
  await expect(article).toBeVisible();
  await page.screenshot({path:prefix+'-desktop.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  await expect(article).toBeVisible();
  await expect(article.locator('p')).toHaveText(alert.message);
  const layout=await article.evaluate(element=>{
   const rect=element.getBoundingClientRect(),text=element.querySelector('p');
   return {viewport:innerWidth,pageWidth:document.documentElement.scrollWidth,left:rect.left,right:rect.right,textWidth:text.clientWidth,textScrollWidth:text.scrollWidth};
  });
  assert.ok(layout.pageWidth<=layout.viewport+1,'Mobile page must not overflow horizontally');
  assert.ok(layout.left>=-1&&layout.right<=layout.viewport+1,'Notification must fit mobile viewport');
  assert.ok(layout.textScrollWidth<=layout.textWidth+1,'Notification text must wrap on mobile');
  await page.screenshot({path:prefix+'-mobile.png',fullPage:true});
  await context.close();
  const after=await snapshot();
  assert.deepEqual(after,before,'Read-only UI must preserve stock and notification state');
  assert.deepEqual(blockedWrites,[],'UI attempted no business/notification writes');
  assert.deepEqual(apiFailures,[]);assert.deepEqual(errors,[]);
  assert.ok(requests.some(r=>r.path==='/api/v2/notifications'&&r.status===200));
  const result={status:'PASS',at:new Date().toISOString(),kind,notificationId,itemName,available,minimum,missing,layout,requests,checks:['Real recipient inbox and exact current message','Desktop 1440 and mobile 390 screenshots','No horizontal overflow','Business and notification snapshots unchanged','No write requests'],limits:'Existing fixture session; actual API proxy 5779 and Vite 5174. No password login or SignalR proof. No mocked API, injected notification, delivery/read mutation.'};
  await writeFile(prefix+'-browser.json',JSON.stringify(result,null,2));return result;
 }catch(error){
  if(page&&!page.isClosed())await page.screenshot({path:prefix+'-failure.png',fullPage:true,timeout:5000}).catch(()=>{});
  await writeFile(prefix+'-browser.json',JSON.stringify({status:'FAIL',kind,error:String(error.message).replace(/[a-f0-9]{64}/ig,'[REDACTED]'),blockedWrites,apiFailures},null,2));throw error;
 }finally{await browser.close();}
}
