// Candidate child. Real UI/Cookies/CSRF; no route mocks, screenshots, traces or passwords in output.
import {createRequire} from 'node:module';
import readline from 'node:readline';
import {existsSync} from 'node:fs';
import {isBoundControl,BoundClose} from './protocol.mjs';
const require=createRequire(new URL('../../../../frontend/package.json',import.meta.url));
const {chromium}=require('@playwright/test');
const lines=readline.createInterface({input:process.stdin});
const iterator=lines[Symbol.asyncIterator]();
const first=await iterator.next();if(first.done)process.exit(1);
let input=JSON.parse(first.value);const session=input.session;
let browser,server,closeTreeAcknowledged;
let acknowledgeClose;const closeBarrier=new Promise(r=>{acknowledgeClose=r});
const emit=r=>process.stdout.write(JSON.stringify(r)+'\n');
const closer=new BoundClose(async()=>{if(browser?.isConnected()){emit({type:'pre-browser-close'});await Promise.race([closeBarrier,new Promise(r=>setTimeout(r,30000))]);await browser.close();}if(server)await server.close();});
const close=kind=>closer.close(kind);
const controls=(async()=>{for await(const line of { [Symbol.asyncIterator]:()=>iterator }){let message;try{message=JSON.parse(line)}catch{continue}if(!isBoundControl(session,message))continue;if(message.command==='CLOSE_TREE_CAPTURED'){closeTreeAcknowledged=true;acknowledgeClose();}if(message.command==='QUIESCE')void close('OwnerCancellation');}acknowledgeClose();if(!closer.requested)await close('ControllerLost');})();
const mappings=[[/^\/api\/incidents$/,'dbo.incidents','incident_id','incidentId'],[/^\/api\/incidents\/\d+\/maintenance-works$/,'dbo.maintenance_works','maintenance_work_id','maintenanceWorkId'],[/^\/api\/material-requests$/,'dbo.material_requests','request_id','requestId'],[/^\/api\/replenishments$/,'dbo.material_replenishments','replenishment_id','replenishmentId'],[/^\/api\/inventory\/receipts$/,'dbo.stock_documents','document_id','documentId']];
let pending=Promise.resolve();
try{
 const origin=new URL(input.frontend);if(origin.protocol!=='https:'||!['localhost','127.0.0.1','[::1]'].includes(origin.hostname))throw Error('Local HTTPS required');
 const edge='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
 server=await chromium.launchServer({host:'127.0.0.1',headless:input.mode!=='interactive-candidate',...(existsSync(edge)?{executablePath:edge}:{})});emit({type:'browser-process',pid:server.process().pid});browser=await chromium.connect(server.wsEndpoint());
 closer.ready();if(closer.requested){await closer.task;throw Error('Cooperative quiescence before login');}
 for(const user of input.users.slice(0,3)){
  if(closer.requested)throw Error('Cooperative quiescence');
  const context=await browser.newContext();
  context.on('response',response=>{const request=response.request();const method=request.method();const url=new URL(response.url());if(url.origin!==origin.origin||!url.pathname.startsWith('/api/')||!['POST','PUT','PATCH','DELETE'].includes(method))return;
   pending=pending.then(async()=>{const status=response.status(),roots=[];const mapping=method==='POST'&&status===201?mappings.find(([pattern])=>pattern.test(url.pathname)):null;
    if(mapping){const dto=await response.json();const id=dto[mapping[3]];if(!Number.isSafeInteger(id)||id<=0)throw Error('Created ID required');roots.push({table:mapping[1],pk:{[mapping[2]]:String(id)}})}
    emit({type:'action',actor:user.UserId,method,route:url.pathname,status,roots});
   }).catch(async()=>{await close('Failure')});
  });
  const page=await context.newPage();await page.goto(input.frontend+'/login');await page.locator('[name="username"]').fill(user.Username);await page.locator('[name="password"]').fill(input.password);await page.getByRole('button',{name:/sign in/i}).click();await page.waitForURL('**/dashboard');
  if(!(await context.cookies(input.frontend)).some(c=>c.name.includes('HotelOps')&&c.secure&&c.httpOnly))throw Error('Real Cookie missing');
  emit({type:'cookie-authenticated',actor:user.UserId,secureHttpOnlyCookie:true});
  await page.goto(input.frontend+'/operations/incidents');
  // Browser safety acceptance creates one real Incident through the existing UI,
  // exercising live returned-root and server-generated audit/event PK recording.
  if(input.mode==='browser-acceptance'&&user===input.users[0]){
   await page.goto(input.frontend+'/operations/incidents?action=A004A-021');
   const form=page.locator('form[data-operation="A004A-021"]');await form.waitFor();
   await form.locator('[name="branchId"]').fill(String(input.branchId));await form.locator('[name="areaId"]').fill(String(input.areaId));
   await form.locator('[name="incidentTitle"]').fill(session.Marker+' safety acceptance');await form.locator('[name="description"]').fill('Candidate safety journal acceptance');
   const discovered=form.locator('[name="discoveredAt"]');if(await discovered.count()){const local=new Date(Date.now()-60_000);await discovered.fill(new Date(local-local.getTimezoneOffset()*60_000).toISOString().slice(0,16));}
   const response=page.waitForResponse(r=>r.url().endsWith('/api/incidents')&&r.request().method()==='POST');await form.locator('button[type="submit"]').click();if((await response).status()!==201)throw Error('Incident create failed');await pending;
  }
 }
 input.password='';
 if(input.mode==='browser-acceptance')await close('NormalBrowserExit');
 else {const end=Date.now()+input.timeoutMinutes*60_000;while(browser.isConnected()&&!closer.requested&&Date.now()<end)await new Promise(r=>setTimeout(r,100));if(!closer.requested)await close(Date.now()>=end?'TimedOut':'NormalBrowserExit');}
 await pending;
}catch{closer.ready();await close('Failure');}
finally{input.password='';await close(closer.reason);emit({type:'terminal',kind:closer.reason,closeTreeAcknowledged:!!closeTreeAcknowledged});lines.close();if(closer.reason==='Failure')process.exitCode=1;}
// The EOF control loop cannot perform SQL cleanup and never invokes old runtime finally.
