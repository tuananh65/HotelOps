import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {randomUUID} from 'node:crypto';
import {writeFile} from 'node:fs/promises';

const require=createRequire(new URL('../../frontend/package.json',import.meta.url));
const {chromium,expect}=require('@playwright/test');
const roles=['manager','store','otherManager','otherStore'];
const fields=value=>value.fields??value;
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const scrub=value=>String(value).replace(/[a-f0-9]{64}/ig,'[REDACTED]');

// Called only by the owning transfer runner after its real positive fallback advice.
// No fixture setup, SQL notification insertion, server management or password login.
export async function verifyBusinessRealtime(fixture,suggestion,directory){
 const base='http://127.0.0.1:5779/api/v2',checks=[],actors=new Map(),errors=[];
 let browser,documentId,documentNo,primaryError,phase='opening streams';
 const readCounts=Object.fromEntries(roles.map(role=>[role,0]));
 const counters=()=>Object.fromEntries([...actors].map(([role,actor])=>[role,{frames:actor.frames.length,polls:actor.polls,heartbeats:actor.heartbeats,transportErrors:[...actor.transportErrors],lastSnapshot:actor.frames.at(-1)}]));
 const branch=role=>role.startsWith('other')?fixture.otherBranch:fixture.branch;
 async function request(role,path,method='GET',body){
  const response=await fetch(base+path,{method,headers:{Authorization:'Bearer '+fixture.sessions[role].token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(60000)});
  let data;try{data=await response.json();}catch{data={};}return {status:response.status,data};
 }
 async function ok(role,path,method='GET',body){const result=await request(role,path,method,body);assert.equal(result.status,200,`${method} ${path}: ${result.status} ${scrub(JSON.stringify(result.data))}`);return result.data;}
 async function inbox(role){
  const rows=[];
  for(let page=1;page<=100;page++){
   const result=await ok(role,`/notifications?branchId=${branch(role)}&page=${page}&pageSize=100`);
   rows.push(...result.items.map(fields));if(!result.hasMore)return rows;
  }
  throw new Error('Notification inbox exceeded bounded verification pages');
 }
 const notices=async role=>(await inbox(role)).filter(row=>row.documentId===documentId);
 const badge=actor=>actor.page.evaluate(()=>Number(document.querySelector('button[aria-label="Thông báo"] span[aria-label]')?.getAttribute('aria-label')?.split(' ')[0]||0));
 async function open(role){
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  const actor={context,frames:[],lists:[],page:null,initialBadge:0,polls:0,heartbeats:0,transportErrors:[],stopping:false};actors.set(role,actor);
  await context.route('**/api/**',async route=>{
   const url=new URL(route.request().url());url.hostname='127.0.0.1';url.port='5779';url.protocol='http:';
   try{
    const response=await route.fetch({url:url.toString(),timeout:120000});
    if(response.status()===200&&route.request().method()==='GET'){
     if(url.pathname==='/api/v2/realtime/notifications'){
      actor.polls++;
      for(const part of (await response.text()).split('\u001e')){
       try{const message=JSON.parse(part);if(message.type===2&&Array.isArray(message.item?.notificationIds))actor.frames.push(message.item);
        if(message.type===6)actor.heartbeats++;
        if(message.type===3||message.type===7)actor.transportErrors.push('Stream ended: '+scrub(JSON.stringify(message)));
       }catch{}
      }
     }else if(url.pathname==='/api/v2/documents')actor.lists.push(await response.json());
    }
    if(url.pathname.startsWith('/api/v2/realtime/')&&response.status()>=400&&!actor.stopping)actor.transportErrors.push('HTTP '+response.status());
    await route.fulfill({response});
   }catch(error){if(url.pathname.startsWith('/api/v2/realtime/')&&!actor.stopping)actor.transportErrors.push(scrub(error.message));await route.abort().catch(()=>{});}
  });
  await context.routeWebSocket('**/*',socket=>socket.close());
  await context.addInitScript(token=>{
   sessionStorage.setItem('hotelops.compact.session',token);
   window.__businessNotificationChanges=0;
   window.addEventListener('compact:notification-change',()=>window.__businessNotificationChanges++);
  },fixture.sessions[role].token);
  actor.page=await context.newPage();actor.page.on('pageerror',error=>errors.push({role,message:scrub(error.message)}));
  await actor.page.goto('http://127.0.0.1:5174/login#documents?transfer=1');
  await expect.poll(()=>actor.frames.length,{timeout:60000}).toBeGreaterThan(0);
  await expect.poll(()=>actor.page.evaluate(()=>window.__businessNotificationChanges),{timeout:30000}).toBeGreaterThan(0);
  if(!role.startsWith('other'))await actor.page.getByRole('button',{name:'Yêu cầu nhận được',exact:true}).click();
  await expect.poll(()=>actor.lists.length,{timeout:30000}).toBeGreaterThan(0);
  await expect.poll(()=>badge(actor),{timeout:30000}).toBe(actor.frames.at(-1).unreadCount);
  actor.initialBadge=await badge(actor);
 }
 async function verifyEvent(type,expectedCount,listCounts){
  phase='delivery '+type;
  const byRole={};
  for(const role of roles){
   const own=await notices(role),event=own.filter(row=>row.notificationType===type);
   assert.equal(event.length,1,role+' has exactly one persisted '+type);
   assert.equal(own.length,expectedCount,role+' persisted event count');
   const row=event[0];assert.equal(row.recipientUserId,fixture.sessions[role].user);
   assert.equal(row.branchId,fixture.branch,'Transfer notification retains linked document branch FK');
   assert.equal(row.readAt,null);byRole[role]=row.notificationId;
   const actor=actors.get(role);
   await expect.poll(()=>actor.frames.at(-1)?.notificationIds.includes(row.notificationId),{timeout:30000}).toBe(true);
   await expect.poll(()=>badge(actor),{timeout:30000}).toBe(actor.initialBadge+expectedCount-readCounts[role]);
   await expect.poll(()=>actor.lists.length,{timeout:30000}).toBeGreaterThan(listCounts[role]);
   const uiRow=actor.page.locator('tbody tr').filter({hasText:documentNo});
   await expect(uiRow).toHaveCount(1);
   await expect(uiRow).toContainText(type==='TRANSFER_SUBMIT'?'Chờ duyệt':'Đã duyệt');
   await expect(actor.page.getByText('Bạn có 1 thông báo mới. Mở Thông báo để xem chi tiết.',{exact:true})).toBeVisible();
   const popup=actor.page.locator('.alert-popup').filter({hasText:'Bạn có 1 thông báo mới.'});
   await popup.getByRole('button',{name:'Đóng thông báo',exact:true}).click();
   await expect(popup).toBeHidden();
  }
  assert.equal(new Set(Object.values(byRole)).size,4,'Each recipient owns a distinct persistent notification');
  for(const role of roles)for(const other of roles.filter(value=>value!==role))
   assert.ok(!actors.get(role).frames.some(frame=>frame.notificationIds.includes(byRole[other])),role+' must not receive '+other+' notification ID');
  checks.push({name:type+' persisted, delivered and rendered for four legitimate transfer actors',status:'PASS',notificationIds:byRole});
  return byRole;
 }
 async function staleRetry(role,path,body,ids){
  phase='stale retry '+role;
  const before={};for(const actor of roles)before[actor]=(await notices(actor)).map(row=>row.notificationId).sort((a,b)=>a-b);
  const frames=Object.fromEntries(roles.map(actor=>[actor,actors.get(actor).frames.length]));
  const retry=await request(role,path,'POST',body);assert.equal(retry.status,409);assert.equal(retry.data.title,'STALE_ROW_VERSION');
  checks.push({name:role+' stale action rejected with STALE_ROW_VERSION',status:'PASS',phase,counters:counters()});
  await pause(6500); // Negative assertion spans at least two existing stream polling intervals.
  for(const actor of roles){
   assert.deepEqual(actors.get(actor).transportErrors,[],actor+' transport stayed healthy');
   assert.deepEqual((await notices(actor)).map(row=>row.notificationId).sort((a,b)=>a-b),before[actor]);
   assert.equal(actors.get(actor).frames.length,frames[actor],actor+' stale retry emitted no new snapshot');
  }
  // Prove the SAME stream is alive with a real, authorized unread-state change.
  // Heartbeat timing is transport-specific and is not delivery evidence.
  phase='same-stream read positive control '+role;
  for(const recipient of roles){
   const actor=actors.get(recipient),id=ids[recipient],previous=actor.frames.at(-1);
   assert.ok(previous.notificationIds.includes(id));
   const other=roles[(roles.indexOf(recipient)+1)%roles.length];
   assert.equal((await request(other,`/notifications/${id}/read`,'POST')).status,404,'Foreign recipient cannot mark a notification read');
   assert.equal((await notices(recipient)).find(row=>row.notificationId===id).readAt,null);
   const read=fields(await ok(recipient,`/notifications/${id}/read`,'POST'));assert.ok(read.readAt);
   readCounts[recipient]++;
   await expect.poll(()=>actor.frames.length,{timeout:30000}).toBeGreaterThan(frames[recipient]);
   await expect.poll(()=>actor.frames.at(-1)?.notificationIds.includes(id),{timeout:30000}).toBe(false);
   assert.equal(actor.frames.at(-1).unreadCount,previous.unreadCount-1);
   await expect.poll(()=>badge(actor),{timeout:30000}).toBe(previous.unreadCount-1);
   assert.deepEqual(actor.transportErrors,[],recipient+' same stream stayed healthy');
   assert.deepEqual((await notices(recipient)).map(row=>row.notificationId).sort((a,b)=>a-b),before[recipient]);
  }
  checks.push({name:role+' stale retry emits no duplicate; same-stream authorized read proves delivery and ownership',status:'PASS',phase,counters:counters()});
 }
 try{
  assert.equal(suggestion.quantity,2);assert.equal(suggestion.sourceWarehouseId,fixture.warehouse);assert.equal(suggestion.destinationWarehouseId,fixture.otherWarehouse);
  browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
  const opened=await Promise.allSettled(roles.map(open));
  const failed=opened.find(result=>result.status==='rejected');if(failed)throw failed.reason;
  phase='create transfer request';
  const reason='Synthetic TEST human-reviewed statistical transfer suggestion';
  const created=fields(await ok('otherManager','/documents','POST',{branchId:fixture.branch,destinationBranchId:fixture.otherBranch,kind:'TRANSFER_REQUEST',sourceWarehouseId:suggestion.sourceWarehouseId,destinationWarehouseId:suggestion.destinationWarehouseId,reason}));
  documentId=created.documentId;documentNo=created.documentNo;
  const added=await ok('otherManager',`/documents/${documentId}/lines`,'POST',{rowVersion:created.rowVersion,itemId:suggestion.itemId,unitId:fixture.unit,quantity:suggestion.quantity});
  const submitBody={rowVersion:fields(added.document).rowVersion,operationKey:randomUUID(),reason};
  let counts=Object.fromEntries(roles.map(role=>[role,actors.get(role).lists.length]));
  phase='submit transfer request';
  const submitted=fields(await ok('otherManager',`/documents/${documentId}/actions/submit`,'POST',submitBody));assert.equal(submitted.status,'PENDING_APPROVAL');
  const submitIds=await verifyEvent('TRANSFER_SUBMIT',1,counts);
  await staleRetry('otherManager',`/documents/${documentId}/actions/submit`,submitBody,submitIds);
  counts=Object.fromEntries(roles.map(role=>[role,actors.get(role).lists.length]));
  const approveBody={rowVersion:submitted.rowVersion,operationKey:randomUUID(),reason};
  phase='approve transfer request';
  const approved=fields(await ok('manager',`/documents/${documentId}/actions/approve`,'POST',approveBody));assert.equal(approved.status,'APPROVED');
  const ids=await verifyEvent('TRANSFER_APPROVE',2,counts);
  await staleRetry('manager',`/documents/${documentId}/actions/approve`,approveBody,ids);
  for(const role of roles){
   await expect.poll(()=>badge(actors.get(role)),{timeout:30000}).toBe(actors.get(role).initialBadge);
   await actors.get(role).page.screenshot({path:directory+`/business-realtime-${role}.png`,fullPage:false});
  }
  checks.push({name:'Read ownership enforced and actual browser badge updates for every recipient',status:'PASS'});
  assert.deepEqual(errors,[]);
  return {documentId,checks};
 }catch(error){primaryError=error;checks.push({name:'business-triggered realtime verification',status:'FAIL',phase,counters:counters(),error:scrub(error.message)});throw error;}
 finally{
  for(const actor of actors.values())actor.stopping=true;
  let cleanupError;
  try{if(browser)await browser.close();}catch(error){cleanupError=scrub(error.message);checks.push({name:'browser cleanup',status:'FAIL',error:cleanupError});}
  finally{await writeFile(directory+'/business-realtime-results.json',JSON.stringify({at:new Date().toISOString(),documentId,phase,counters:counters(),checks,errors,cleanupError,transport:'Actual SignalR LongPolling through Edge route proxy to TEST API 5779',limits:'Existing fixture sessions; no password login. Advice reviewed in preceding browser helper; request/approval via actual HTTP. Four live pages observe automatic list/status, badge and popup refresh. All four actors are legitimate recipients; outsider roles are not covered. No SQL-injected notifications, no external AI provider, no physical transfer execution in this probe.'},null,2));}
  if(cleanupError&&!primaryError)throw new Error('Business realtime browser cleanup failed; see artifact');
 }
}
