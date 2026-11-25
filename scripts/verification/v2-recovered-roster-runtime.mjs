import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {spawn} from 'node:child_process';
import {randomUUID} from 'node:crypto';
import {createRequire} from 'node:module';

const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const directory='artifacts/restructure-v2',runId=randomUUID();
const report={at:new Date().toISOString(),database:'HotelOpsCompactWorkdayTestV22611250010',checks:[],limits:'Read-only API/Edge roster verification using an owned temporary administrator session. No recovered-user password login, password reset, role mutation, email delivery or operational database writes.'};
let fixture,browser;
const readJson=async path=>JSON.parse((await readFile(path,'utf8')).replace(/^\uFEFF/,''));
const run=action=>new Promise((resolve,reject)=>{
 const child=spawn('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File','scripts/verification/v2-invitation-fixture.ps1','-Action',action],{windowsHide:true,env:{...process.env,HOTELOPS_V2_RUN_ID:runId}});
 let out='',error='';child.stdout.on('data',d=>out+=d);child.stderr.on('data',d=>error+=d);child.on('error',reject);
 child.on('exit',code=>{if(code)return reject(new Error(action+': '+error.slice(0,1200)));try{resolve(JSON.parse(out.trim().replace(/^\uFEFF/,'')));}catch{reject(new Error('Invalid fixture response'));}});
});
async function api(path){const response=await fetch('http://127.0.0.1:5778/api/v2'+path,{headers:{Authorization:'Bearer '+fixture.token},signal:AbortSignal.timeout(60000)});assert.equal(response.status,200,path);return response.json();}
const passed=(name,data)=>report.checks.push({name,status:'PASS',...data});
try{
 try{await run('Setup');}finally{try{const candidate=await readJson(directory+'/invitation-private/fixture.json');if(candidate.runId===runId)fixture=candidate;}catch{}}
 assert.ok(fixture,'Owned fixture required');await run('Start');
 const provenance=await readJson(directory+'/role-provenance.json');
 const original=provenance.sets[0];assert.equal(original.length,10);assert.equal(new Set(original.map(x=>x.user_id)).size,10);
 const comparison=await readJson(directory+'/role-recovery-current.json');
 const credentialRows=comparison.sets.find(rows=>rows.length&&Object.hasOwn(rows[0],'credentials_equal_source'));assert.ok(credentialRows);
 assert.equal(credentialRows.reduce((sum,row)=>sum+row.credentials_equal_source,0),10);
 assert.ok(credentialRows.every(row=>row.active_maintenance_members===row.recovered_users&&row.account_status_equal_source===row.recovered_users));
 passed('Existing read-only SQL comparison confirms ten preserved credentials and recovered memberships',{evidence:'role-recovery-current.json',comparisonAt:comparison.at});
 const rolePage=await api('/catalog/roles?pageSize=100');assert.ok(rolePage.total<=100,'Role catalog must not be silently truncated');
 const roles=rolePage.items.map(x=>x.fields),maintenance=roles.find(x=>x.code==='MAINTENANCE_STAFF'&&x.isActive),manager=roles.find(x=>x.code==='OPERATION_MANAGER'&&x.isActive);
 assert.ok(maintenance);assert.ok(manager);assert.ok(!roles.some(x=>['DEPARTMENT_HEAD','DEPT_MANAGER'].includes(x.code)&&x.isActive));
 passed('Actual role catalog retains active Maintenance Staff and one OM role without active DH');
 const users=(await api('/users')).map(x=>x.fields),recovered=original.map(old=>{
  const user=users.find(u=>u.userId===old.user_id);assert.ok(user);assert.equal(user.accountStatus,'ACTIVE');
  assert.ok(user.roles.some(r=>r.roleId===maintenance.roleId&&r.branchId===old.branch_id));return {...old,user};
 });
 assert.equal(new Set(recovered.map(x=>x.branch_id)).size,2);
 passed('Ten original recovered users have active Maintenance Staff in their provenance branch',{count:recovered.length,branchCounts:[...new Set(recovered.map(x=>x.branch_id))].map(branchId=>({branchId,count:recovered.filter(x=>x.branch_id===branchId).length}))});
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1000}});
 await context.addInitScript(token=>sessionStorage.setItem('hotelops.compact.session',token),fixture.token);
 await context.route('**/api/**',async route=>{
  const url=new URL(route.request().url());if(url.pathname.startsWith('/api/v2/realtime/'))return route.abort();
  url.hostname='127.0.0.1';url.port='5778';url.protocol='http:';
  try{await route.fulfill({response:await route.fetch({url:url.toString()})});}catch{await route.abort().catch(()=>{});}
 });
 await context.routeWebSocket('**/*',socket=>socket.close());
 const page=await context.newPage(),pageErrors=[];page.on('pageerror',error=>pageErrors.push(error.message));
 await page.goto('http://127.0.0.1:5174/login#users');
 await expect(page.getByRole('heading',{name:'Nhân sự & truy cập',exact:true})).toBeVisible({timeout:30000});
 const photographed=new Set();
 for(const entry of recovered){
  await page.getByPlaceholder('Tên, mã nhân viên, username hoặc email').fill(entry.user.username);
  const row=page.locator('tbody tr').filter({hasText:entry.user.username});await expect(row).toHaveCount(1);
  await expect(row).toContainText(maintenance.name);
  if(!photographed.has(entry.branch_id)){await page.screenshot({path:`${directory}/recovered-roster-branch-${entry.branch_id}.png`,fullPage:true});photographed.add(entry.branch_id);}
 }
 passed('Admin directory renders the Maintenance Staff role for all ten recovered users',{screenshots:[...photographed].map(id=>`recovered-roster-branch-${id}.png`)});
 const om=users.find(u=>u.accountStatus==='ACTIVE'&&u.roles.some(r=>r.roleId===manager.roleId));assert.ok(om);
 await page.getByPlaceholder('Tên, mã nhân viên, username hoặc email').fill(om.username);
 await expect(page.locator('tbody tr').filter({hasText:om.username})).toContainText(manager.name);
 await page.screenshot({path:directory+'/om-consolidated-roster.png',fullPage:true});
 passed('Existing active OM is visible in the administrator directory');
 assert.deepEqual(pageErrors,[]);
 const after=(await api('/users')).map(x=>x.fields);
 for(const entry of recovered)assert.deepEqual(after.find(u=>u.userId===entry.user.userId),entry.user);
 passed('Read-only roster browsing preserves all ten account, membership and role records');
 report.status='PASS';
}catch(error){report.status='FAIL';report.error=String(error.message).replace(/[a-f\d]{64}/gi,'[redacted]');process.exitCode=1;}
finally{
 if(browser)await browser.close();
 if(fixture){try{await run('Stop');report.cleanup=await run('Cleanup');}catch(error){report.status='FAIL';report.cleanupError=String(error.message).replace(/[a-f\d]{64}/gi,'[redacted]');process.exitCode=1;}}
 await writeFile(directory+'/recovered-roster-runtime-results.json',JSON.stringify(report,null,2));
 console.log(JSON.stringify({status:report.status,checks:report.checks.length,error:report.error,cleanup:report.cleanup}));
}
