// Execute through the verified TEST runner only. No test is executed by importing this module.
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {fileURLToPath} from 'node:url';
const execute=promisify(execFile);
export async function migrationAudit(mode='migration',values={}) {
 const args=['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',fileURLToPath(new URL('./p0-runtime-migration-audit.ps1',import.meta.url)),'-Mode',mode];
 for(const [name,value] of Object.entries(values)) {
  if(!['BranchId','ItemId','WorkId'].includes(name)||!Number.isSafeInteger(value)||value<=0)throw new Error('Invalid read-only audit argument');
  args.push(`-${name}`,String(value));
 }
 try {const {stdout}=await execute('powershell.exe',args,{windowsHide:true,maxBuffer:1024*1024});return JSON.parse(stdout.trim().replace(/^\uFEFF/,''));}
 catch {throw new Error(`Read-only TEST ${mode} audit failed; inspect helper without printing secrets`);}
}
export function concurrencyScenarios({probe,branch,fields,allocations}) {
 const api='/api/v2';
 const allocation=async(c,s='DVBV',alias='hk')=>(await allocations(c,s,alias)).find(a=>a.assignmentId===c.fixture.actors[`${s}.${alias}`].assignmentId);
 const stock=(c,s,itemId)=>probe('stock',{...c.fixture,requests:{branchId:branch(c.fixture,s).branchId,itemId}});
 const fresh=(c,s,id)=>c.request(`${s}.hk`,'GET',`${api}/requests/${id}`).then(r=>c.ok(r));
 const act=(c,s,alias,d,action,extra={})=>c.request(`${s}.${alias}`,'POST',`${api}/requests/${d.request.workId}/actions/${action}`,{rowVersion:d.request.rowVersion,operationKey:c.uuid(),...extra});
 async function refill(c,s,items,alias='hk') {
  const a=await allocation(c,s,alias);c.check(a&&a.status!=='CLOSED','Open fixture allocation required');
  return c.ok(await c.request(`${s}.${alias}`,'POST',`${api}/requests/refills`,{allocationId:a.allocationId,items,reason:'P0 TEST supply concurrency',operationKey:c.uuid()}));
 }
 const approve=async(c,s,d)=>c.ok(await act(c,s,'supervisor',d,'approve'));
 async function issue(c,s,items,alias='hk') {return c.ok(await act(c,s,'store',await approve(c,s,await refill(c,s,items,alias)),'issue'));}
 async function receipt(c,s,itemId,quantity) {
  c.check(c.fixture.allowSyntheticTestReceipt===true,'Explicit isolated synthetic receipt authorization required');
  const b=branch(c.fixture,s);
  let d=c.ok(await c.request(`${s}.store`,'POST',`${api}/requests/replenishments`,{branchId:b.branchId,warehouseId:b.warehouseId,items:[{itemId,quantity}],reason:'P0 TEST physical receipt fixture restoration',operationKey:c.uuid()}));
  d=c.ok(await act(c,s,'om',d,'approve'));
  return c.ok(await c.request(`${s}.store`,'POST',`${api}/requests/${d.request.workId}/receive`,{rowVersion:d.request.rowVersion,operationKey:c.uuid(),items:[{itemId,quantity}]}));
 }
 async function workAct(c,s,alias,id,action,extra={}) {
  const d=c.ok(await c.request(`${s}.${alias}`,'GET',`${api}/work/${id}`));
  return c.request(`${s}.${alias}`,'POST',`${api}/work/${id}/actions/${action}`,{rowVersion:fields(d.work).rowVersion,operationKey:c.uuid(),...extra});
 }
 async function cleaningRooms(c,s) {return c.ok(await c.request(`${s}.supervisor`,'GET',`${api}/work/cleaning-rooms?branchId=${branch(c.fixture,s).branchId}`)).items;}
 const createCleaning=(c,s,room)=>c.request(`${s}.supervisor`,'POST',`${api}/work`,{branchId:branch(c.fixture,s).branchId,kind:'CLEANING',title:`P0 TEST reservation race ${room.code}`,roomId:room.roomId,roomRowVersion:room.rowVersion,assigneeUserId:c.fixture.actors[`${s}.hk`].userId,durationMinutes:15});
 async function consumeRoom(c,s,room) {
  const w=fields(c.ok(await createCleaning(c,s,room)));
  c.ok(await workAct(c,s,'hk',w.workId,'start'));
  c.ok(await workAct(c,s,'hk',w.workId,'complete'));
  const d=c.ok(await c.request(`${s}.supervisor`,'GET',`${api}/work/${w.workId}`));
  c.ok(await c.request(`${s}.supervisor`,'POST',`${api}/work/${w.workId}/inspection-result`,{rowVersion:fields(d.work).rowVersion,operationKey:c.uuid(),result:'PASS',note:'P0 TEST paper inspection'}));
  for(const action of ['reserve','check-in','check-out']) {
   const r=c.ok(await c.request(`${s}.reception`,'GET',`${api}/rooms?branchId=${branch(c.fixture,s).branchId}`)).find(x=>x.roomId===room.roomId);
   c.ok(await c.request(`${s}.reception`,'POST',`${api}/rooms/${room.roomId}/actions/${action}`,{rowVersion:r.rowVersion,operationKey:c.uuid()}));
  }
 }
 return {
  'E2E-01':async c=>{
   const a=await migrationAudit();c.equal(a.migrationCount,1);c.equal(a.activeRoleCount,8);c.check(a.historicalAssignmentCount>0,'Migration must contain a before snapshot');
   for(const key of ['activeLegacyAssignments','missingHistoricalAssignmentsOrUsers','missingHistoricalMemberships','missingHistoricalSchedules','unrevokedPreMigrationSessions','activeAssignmentsWithoutMembership'])c.equal(a[key],0,key);
   for(const suffix of ['DVBV','DVCT'])for(const alias of ['admin','owner','om','supervisor','hk','maintenance','reception','store']) {
    const me=c.ok(await c.request(`${suffix}.${alias}`,'GET',`${api}/auth/me`));c.check(me.roles.some(r=>r.code===c.fixture.actors[`${suffix}.${alias}`].roleCode),'Migrated eight-actor authentication mismatch');
   }
   return {liveDatabaseProbe:true,passwordPreservation:'Requires pre-migration baseline comparison; this audit never reads password hashes'};
  },
  'E2E-07':async c=>{
   const s='DVBV',b=branch(c.fixture,s);
   const cancelRaceWork=async id=>{
    c.check(b.code==='P0_TEST_DVBV','Recovery is restricted to the synthetic TEST branch');
    const d=c.ok(await c.request(`${s}.supervisor`,'GET',`${api}/work/${id}`)),w=fields(d.work);
    const room=b.rooms.find(r=>r.roomId===w.roomId&&['P0_102','P0_103'].includes(r.code));
    c.check(room&&w.branchId===b.branchId&&w.kind==='CLEANING'&&w.title===`P0 TEST reservation race ${room.code}`,
     'Refusing cancellation of a task outside the reservation race fixture');
    c.equal(w.createdBy,c.fixture.actors[`${s}.supervisor`].userId,'Refusing non-fixture creator');
    c.equal(w.status,'ASSIGNED','Started/completed work requires separate recovery review');
    c.check(d.assignments.map(fields).some(a=>a.assignmentStatus==='ACTIVE'&&a.assigneeUserId===c.fixture.actors[`${s}.hk`].userId),
     'Refusing cancellation of a task assigned outside this TEST actor');
    c.ok(await workAct(c,s,'supervisor',id,'cancel',{reason:'P0 TEST release race reservation; preserve history'}));
   };
   // Opt-in only: parent must identify the interrupted winner from the recorded TEST run/API detail.
   if(process.env.P0_RECOVER_RESERVATION_WORK_ID){
    const id=Number(process.env.P0_RECOVER_RESERVATION_WORK_ID);
    c.check(Number.isSafeInteger(id)&&id>0,'Explicit valid TEST recovery work ID required');
    await cancelRaceWork(id);
   }
   const before=await allocation(c,s);c.check(before.status==='OPEN','Open allocation required');
   c.check(before.items.every(i=>i.reserved===0&&i.available>=2),'Run reservation race without unrelated open cleaning');
   let a=before;
   // Consume real kits through the complete/inspection/checkout workflow until exactly one room fits.
   for(let n=0;Math.min(...a.items.map(i=>i.available))>=4&&n<20;n++){
    const room=(await cleaningRooms(c,s)).find(r=>r.code==='P0_102');c.check(room,'Reusable TEST room missing');
    await consumeRoom(c,s,room);a=await allocation(c,s);
   }
   c.check(Math.min(...a.items.map(i=>i.available))>=2&&Math.min(...a.items.map(i=>i.available))<4,'Exactly one capacity-two kit required');
   const rooms=(await cleaningRooms(c,s)).filter(r=>['P0_102','P0_103'].includes(r.code));c.equal(rooms.length,2);
   const results=await Promise.all(rooms.map(room=>createCleaning(c,s,room)));
   const winners=results.filter(r=>r.status===200).map(r=>fields(r.body));
   try {
    c.equal(results.map(r=>r.status).sort(),[200,409],'Concurrent winners/losers differ');
    const loserIndex=results.findIndex(r=>r.status===409),loser=results[loserIndex];
    const code=loser.body?.code??loser.body?.error?.code;
    c.check(['STAFF_SUPPLIES_INSUFFICIENT','TRANSACTION_RETRY_REQUIRED'].includes(code),'Unexpected reservation conflict');
    const reserved=await allocation(c,s);
    for(const row of reserved.items){const old=a.items.find(i=>i.itemId===row.itemId);c.equal(row.reserved,old.reserved+2);c.check(row.available>=0,'Concurrent assignments over-reserved supplies');}
    const history=()=>c.request(`${s}.hk`,'GET',`${api}/supplies/allocations/${a.allocationId}/history`).then(r=>c.ok(r));
    const ledgerBefore=await history();
    const retryRoom=(await cleaningRooms(c,s)).find(r=>r.roomId===rooms[loserIndex].roomId);
    c.check(retryRoom,'Rolled-back loser must leave the room available for assignment');
    const retried=await createCleaning(c,s,retryRoom);c.equal(retried.status,409);
    c.equal(retried.body?.code??retried.body?.error?.code,'STAFF_SUPPLIES_INSUFFICIENT');
    c.equal((await allocation(c,s)).items,reserved.items,'Rejected retry changed staff balances');
    c.equal(await history(),ledgerBefore,'Rejected retry wrote supply ledger entries');
   }finally{
    for(const winner of winners)await cancelRaceWork(winner.workId);
   }
   const released=await allocation(c,s);c.equal(released.items.map(i=>i.reserved),before.items.map(i=>i.reserved));
   const restore=before.items.map(i=>({itemId:i.itemId,quantity:i.remaining-released.items.find(r=>r.itemId===i.itemId).remaining})).filter(i=>i.quantity>0);
   if(restore.length)await issue(c,s,restore);
   c.reconcileSupply(await probe('balances',c.fixture));
  },
  'E2E-11':async c=>{
   const s='DVBV',a=await allocation(c,s),itemId=a.items[0].itemId,before=await stock(c,s,itemId);
   let d=await approve(c,s,await refill(c,s,[{itemId,quantity:before+1}]));
   const denied=await act(c,s,'store',d,'issue');c.equal(denied.status,409);c.equal(denied.body?.code??denied.body?.error?.code,'STOCK_INSUFFICIENT');
   c.equal(await stock(c,s,itemId),before);c.equal((await allocation(c,s)).items,a.items,'Failed issue changed staff balance');
   d=await fresh(c,s,d.request.workId);c.equal(d.request.status,'PENDING_STOREKEEPER');
   c.ok(await act(c,s,'store',d,'reject'),400);
   const rejected=c.ok(await act(c,s,'store',d,'reject',{reason:'Kho TEST không đủ hàng'}));c.equal(rejected.request.status,'REJECTED');c.equal(await stock(c,s,itemId),before);
  },
  'E2E-12':async c=>{
   const s='DVBV',itemId=(await allocation(c,s)).items[0].itemId,before=await stock(c,s,itemId);c.check(before>=2,'Positive actual stock required');
   const quantity=Math.floor(before/2)+1;
   const first=await approve(c,s,await refill(c,s,[{itemId,quantity}]));
   const second=await approve(c,s,await refill(c,s,[{itemId,quantity}],'hkPeer'));
   const balanceBefore=await probe('balances',c.fixture);
   const responses=await c.race([()=>act(c,s,'store',first,'issue'),()=>act(c,s,'storePeer',second,'issue')],[200,409]);
   const failed=responses.find(r=>r.status===409);c.equal(failed.body?.code??failed.body?.error?.code,'STOCK_INSUFFICIENT');
   c.equal(await stock(c,s,itemId),before-quantity);
   const balanceAfter=await probe('balances',c.fixture);
   c.equal(balanceAfter.reduce((sum,b)=>sum+b.additionalIssued,0)-balanceBefore.reduce((sum,b)=>sum+b.additionalIssued,0),quantity,'Stock race issued more than one quantity');
   const failedDetail=responses[0].status===409?first:second;
   const owner=responses[0].status===409?'hk':'hkPeer';
   const current=c.ok(await c.request(`${s}.${owner}`,'GET',`${api}/requests/${failedDetail.request.workId}`));
   c.equal(current.request.status,'PENDING_STOREKEEPER');c.ok(await act(c,s,'store',current,'reject',{reason:'P0 TEST stock race loser'}));
   await receipt(c,s,itemId,quantity);c.equal(await stock(c,s,itemId),before);
   c.reconcileSupply(balanceAfter);
  },
  'E2E-15':async c=>{
   const s='DVBV',b=branch(c.fixture,s),itemId=(await allocation(c,s)).items[1].itemId;
   const configs=c.ok(await c.request(`${s}.om`,'GET',`${api}/supplies/configuration?branchId=${b.branchId}`)),original=configs.find(x=>x.itemId===itemId);
   let changed=c.ok(await c.request(`${s}.om`,'PUT',`${api}/supplies/configuration`,{...original,minimumStock:50}));
   const initialStock=await stock(c,s,itemId);
   try {
    if(initialStock<50)await receipt(c,s,itemId,50-initialStock);
    if(initialStock>50)await issue(c,s,[{itemId,quantity:initialStock-50}]);
    c.equal(await stock(c,s,itemId),50);
    const read=()=>migrationAudit('alerts',{BranchId:b.branchId,ItemId:itemId});
    const at50=await read();c.equal(at50.activeNotifications,0);c.check(!at50.episode,'Alert must resolve at threshold');
    await issue(c,s,[{itemId,quantity:1}]);c.equal(await stock(c,s,itemId),49);
    const at49=await read();c.check(!!at49.episode&&at49.activeRecipients>=2,'Storekeeper peers must receive alert');c.equal(at49.activeNotifications,at49.activeRecipients,'Duplicate alert recipient');
    changed=c.ok(await c.request(`${s}.om`,'PUT',`${api}/supplies/configuration`,changed));
    const repeated=await read();c.equal(repeated.episode,at49.episode);c.equal(repeated.activeNotifications,at49.activeNotifications);
    await receipt(c,s,itemId,1);const resolved=await read();c.check(!resolved.episode);c.equal(resolved.activeNotifications,0);c.check(resolved.resolvedNotifications>=at49.activeNotifications);
    if(initialStock>50)await receipt(c,s,itemId,initialStock-50);
   }finally{c.ok(await c.request(`${s}.om`,'PUT',`${api}/supplies/configuration`,{...original,rowVersion:changed.rowVersion}));}
  },
  'E2E-25':async c=>{
   const s='DVBV',b=branch(c.fixture,s),itemId=(await allocation(c,s)).items[0].itemId;
   const hkTypes=c.ok(await c.request(`${s}.hk`,'GET',`${api}/requests/types?branchId=${b.branchId}`));c.check(hkTypes.some(t=>t.code==='CONSUMABLE_REFILL_REQUEST'));
   const omTypes=c.ok(await c.request(`${s}.om`,'GET',`${api}/requests/types?branchId=${b.branchId}`));c.check(!omTypes.some(t=>t.code==='CONSUMABLE_REFILL_REQUEST'));
   const created=await refill(c,s,[{itemId,quantity:1}]);
   c.denied(await c.request(`${s}.hkPeer`,'GET',`${api}/requests/${created.request.workId}`));c.denied(await c.request('DVCT.om','GET',`${api}/requests/${created.request.workId}`));
   const races=await c.race([()=>act(c,s,'supervisor',created,'approve'),()=>act(c,s,'supervisor',created,'reject',{reason:'P0 TEST competing review'})],[200,409]);
   let d=races.find(r=>r.status===200).body;
   c.equal(d.history.filter(h=>['approve','reject'].includes(h.action)).length,1,'Approval/rejection race recorded two decisions');
   if(d.request.status==='PENDING_STOREKEEPER') {
    const claim=c.ok(await act(c,s,'store',d,'claim'));c.equal(claim.request.handlerUserId,c.fixture.actors[`${s}.store`].userId);
    c.denied(await act(c,s,'storePeer',claim,'issue'),[403]);
    d=c.ok(await act(c,s,'store',claim,'reject',{reason:'P0 TEST finish handler ownership probe'}));
   }
   const reread=await fresh(c,s,d.request.workId);c.equal(reread.history,d.history,'History lost after independent read');c.equal(reread.request.status,'REJECTED');
   c.check(reread.history.every(h=>h.correlationId===reread.request.correlationId),'Request history correlation mismatch');
   const evidence=await migrationAudit('workflow',{WorkId:d.request.workId});c.check(evidence.notifications>0,'No persisted workflow notification');
   c.check(evidence.linkedChildRequests>0,'Parent-child remediation request evidence missing; run room scenarios first');
  },
 };
}
