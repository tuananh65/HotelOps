import { createRequire } from 'node:module';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const require=createRequire(resolve(root,'frontend/package.json'));
const { chromium, expect }=require('@playwright/test');
const base=process.env.FE03_LIVE_BASE || 'https://localhost:7039';
const source=process.env.FE03_LIVE_SHA || '6a0c9248008203c245c347d9d6831279835ee544';
const lease=process.env.FE03_LIVE_LEASE || '2026-11-11T03:05:21.0616355Z';
const attribution=source==='717715fa0f9fa51ba467592f1dd01ea06e8ce568';
if(!['6a0c9248008203c245c347d9d6831279835ee544','717715fa0f9fa51ba467592f1dd01ea06e8ce568'].includes(source) || new URL(base).protocol!=='https:' || new URL(base).hostname!=='localhost') throw Error('Unverified browser runtime configuration');
const checks=[];
const browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({ignoreHTTPSErrors:false,serviceWorkers:'block'});
let csrf,logged=false,completed=false;
function check(name,ok){checks.push({name,pass:!!ok});if(!ok)throw Error(name);console.log(name+": PASS");}
const apiErrors=[];
const page=await context.newPage();
page.on('response',res=>{if(new URL(res.url()).pathname.startsWith('/api/')&&res.status()>=500)apiErrors.push(res.status());});
// Serve only built frontend static bytes; every /api request reaches the real TLS backend.
await page.route(`${base}/**`,async route=>{
 const url=new URL(route.request().url());
 if(url.pathname.startsWith('/api/'))return route.continue();
 if(url.pathname.startsWith('/assets/')&&/^\/assets\/[a-zA-Z0-9_.-]+$/.test(url.pathname)){
  const path=resolve(root,'frontend/dist'+url.pathname);
  return route.fulfill({status:200,body:readFileSync(path),contentType:path.endsWith('.js')?'application/javascript':'text/css'});
 }
 if(url.pathname==='/rooms/status')return route.fulfill({status:200,body:readFileSync(resolve(root,'frontend/dist/index.html')),contentType:'text/html'});
 return route.continue();
});
await page.goto(`${base}/api/health/database`);
const request={
 get: async url=>browserRequest(url,'GET'),
 post: async (url,options)=>browserRequest(url,'POST',options),
};
async function browserRequest(url,method,options={}){
 const result=await page.evaluate(async ({url,method,options})=>{
  const res=await fetch(url,{method,credentials:'include',headers:{'Content-Type':'application/json',...options.headers},body:options.data?JSON.stringify(options.data):undefined});
  return {status:res.status,body:await res.text()};
 },{url,method,options});
 return {status:()=>result.status,json:async()=>JSON.parse(result.body)};
}
try{
 const username=process.env.FE03_LIVE_USER,password=process.env.FE03_LIVE_PASSWORD;
 delete process.env.FE03_LIVE_USER;delete process.env.FE03_LIVE_PASSWORD;
 check('Lease still active',Date.now()<Date.parse(lease));
 const boot=await request.get(`${base}/api/auth/csrf`);check('Live CSRF',boot.status()===200);
 const token=(await boot.json()).csrfToken;
 const login=await request.post(`${base}/api/auth/login`,{headers:{Origin:base,'X-CSRF-TOKEN':token},data:{username,password}});
 check('Live allowed login',login.status()===200);logged=true;csrf=(await login.json()).csrfToken;
 await page.goto(`${base}/rooms/status?branchId=1`);
 await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toBeVisible();
 await expect(page.getByLabel('Bộ đếm trạng thái')).toContainText('DIRTY: 1');
 await expect(page.getByLabel('Bộ đếm trạng thái')).toContainText('READY: 1');
 await expect(page.getByLabel('Bộ đếm trạng thái')).toContainText('OUT_OF_SERVICE: 1');
 check('Frontend real room board and counters',true);
 await page.locator('select[name=status]').selectOption('DIRTY');await page.getByRole('button',{name:'Lọc phòng'}).click();
 await expect(page.getByRole('link',{name:'ENVFE03-102',exact:true})).toHaveCount(0);
 await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toBeVisible();check('Live frontend status filter',true);
 await page.getByRole('link',{name:'ENVFE03-101',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Phòng ENVFE03-101',exact:true})).toBeVisible();
 await expect(page.getByRole('region',{name:'Lịch sử trạng thái',exact:true})).toContainText('DIRTY');
 await expect(page.getByRole('region',{name:'Chi tiết trạng thái phòng'})).toContainText('RoomBlock');check('Live detail history block UI',true);
 if(attribution){
  const api=await request.get(`${base}/api/rooms/1/history?branchId=1`);const data=await api.json();
  check('Browser API actor attribution and ordering',api.status()===200&&data.items.length===2&&data.items[0].id===2&&data.items[0].changedByUserId===1&&data.items[1].id===1&&data.items[1].changedByUserId===null);
  const history=page.getByRole('region',{name:'Lịch sử trạng thái',exact:true});
  await expect(history.locator('li')).toHaveCount(2);
  await expect(history.locator('li').nth(0)).toContainText('Người thay đổi (ID): 1');
  await expect(history.locator('li').nth(1)).toContainText('Người thay đổi (ID): Chưa ghi nhận');
  await expect(history.locator('time').nth(0)).toHaveText(data.items[0].changedAtUtc);
  await expect(history.locator('time').nth(1)).toHaveText(data.items[1].changedAtUtc);
  await expect(history).not.toContainText('envfe03001.allowed');
  check('UI persisted actor1 null actor and history ordering',true);
 }
 await expect(page.getByRole('note')).toContainText('Chưa có nguồn được xác nhận');check('Readiness explicitly unavailable',true);
 // Delay genuine response delivery only, never substitute API status/body.
 let delay=false,release,arrived;
 let pending,seen;
 const reset=()=>{pending=new Promise(r=>release=r);seen=new Promise(r=>arrived=r);};reset();
 await page.route('**/api/rooms?**',async route=>{
  if(delay){arrived();await pending;}
  await route.continue();
 });
 delay=true;await page.goBack();await seen;
 await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toHaveCount(0);
 await expect(page.getByRole('status')).toBeVisible();release();delay=false;
 await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toBeVisible();check('Browser Back hides cache while real response pending',true);
 reset();delay=true;
 await page.getByLabel('Chi nhánh (ID)',{exact:true}).fill('2');await page.getByRole('button',{name:'Lọc phòng'}).click();await seen;
 await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toHaveCount(0);release();delay=false;
 await expect(page.getByRole('alert')).toBeVisible();await expect(page.getByRole('link',{name:'ENVFE03-201',exact:true})).toHaveCount(0);check('Live branch switch 403 with no stale or foreign room',true);
 await page.getByLabel('Chi nhánh (ID)',{exact:true}).fill('1');await page.getByRole('button',{name:'Lọc phòng'}).click();await expect(page.getByRole('link',{name:'ENVFE03-101',exact:true})).toBeVisible();check('Allowed scope recovers',true);
 if(attribution){
  await page.goto(`${base}/rooms/status?branchId=1&roomId=2`);
  const history=page.getByRole('region',{name:'Lịch sử trạng thái',exact:true});
  await expect(history.locator('li')).toHaveCount(1);
  await expect(history).toContainText('Người thay đổi (ID): 1');
  await expect(history).not.toContainText('Chưa ghi nhận');
  check('Room2 UI isolates history from room1',true);
 }
 check('No unexpected server 500 errors',apiErrors.length===0);completed=true;
}finally{
 if(logged){const logout=await request.post(`${base}/api/auth/logout`,{headers:{Origin:base,'X-CSRF-TOKEN':csrf}});check('Browser session logout',logout.status()===204);const after=await request.get(`${base}/api/rooms?branchId=1`);check('Browser session revoked',after.status()===401);}
 mkdirSync(resolve(root,attribution?'obj/fe03-live-012':'obj/fe03-independent-004'),{recursive:true});
 writeFileSync(resolve(root,attribution?'obj/fe03-live-012/frontend-acceptance.json':'obj/fe03-independent-004/frontend-acceptance.json'),JSON.stringify({status:completed&&checks.every(c=>c.pass)?'PASS':'FAIL',utc:new Date().toISOString(),source_commit:source,backend:base,frontend_static_delivery:'Local built assets served by browser harness; API bodies/statuses all real HTTPS; selected requests delayed without replacement',tls_validation:true,checks},null,2));
 await context.close();await browser.close();
}
console.log('LIVE_FRONTEND_PASS checks='+checks.length);
