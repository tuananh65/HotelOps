// Destructive actions are limited to explicitly provisioned synthetic TEST branches.
export function supplyScenarios({probe,branch,fields,allocations}) {
 const api='/api/v2';
 const actor=(suffix,alias)=>`${suffix}.${alias}`;
 const getAllocation=async(c,suffix='DVBV',alias='hk')=>(await allocations(c,suffix,alias)).find(a=>a.assignmentId===c.fixture.actors[actor(suffix,alias)].assignmentId);
 async function week(c,suffix='DVBV',alias='hk') {
  const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  return c.ok(await c.request(actor(suffix,alias),'GET',`${api}/workday?branchId=${branch(c.fixture,suffix).branchId}&date=${date}`));
 }
 async function start(c,suffix='DVBV',alias='hk') {
  const a=c.fixture.actors[actor(suffix,alias)],row=(await week(c,suffix,alias)).assignments.find(x=>x.assignmentId===a.assignmentId);
  c.equal(row.status,'PLANNED','Fresh fixture required; lifecycle scenarios must not replay against previously mutated data');
  const payload={rowVersion:row.rowVersion,operationKey:c.uuid()},route=`${api}/workday/assignments/${a.assignmentId}/start`;
  const result=fields(c.ok(await c.request(actor(suffix,alias),'POST',route,payload)));
  return {result,payload,route};
 }
 async function createCleaning(c,suffix='DVBV',alias='hk',code='P0_102') {
  const b=branch(c.fixture,suffix),rooms=c.ok(await c.request(actor(suffix,'supervisor'),'GET',`${api}/work/cleaning-rooms?branchId=${b.branchId}`));
  const room=rooms.items.find(r=>r.code===code);c.check(room,'Requested fixture room must need cleaning');
  return c.request(actor(suffix,'supervisor'),'POST',`${api}/work`,{branchId:b.branchId,kind:'CLEANING',title:`P0 TEST ${code}`,roomId:room.roomId,roomRowVersion:room.rowVersion,assigneeUserId:c.fixture.actors[actor(suffix,alias)].userId,durationMinutes:15});
 }
 async function workAction(c,suffix,alias,id,action,extra={}) {
  const detail=c.ok(await c.request(actor(suffix,alias),'GET',`${api}/work/${id}`));
  return c.request(actor(suffix,alias),'POST',`${api}/work/${id}/actions/${action}`,{rowVersion:fields(detail.work).rowVersion,operationKey:c.uuid(),...extra});
 }
 async function end(c,suffix,alias) {
  const active=(await week(c,suffix,alias)).active;c.check(active,'Active shift required');
  const payload={rowVersion:active.rowVersion,operationKey:c.uuid()},route=`${api}/workday/assignments/${active.assignmentId}/end`;
  return {response:await c.request(actor(suffix,alias),'POST',route,payload),payload,route};
 }
 async function stock(c,suffix,itemId) {return probe('stock',{...c.fixture,requests:{branchId:branch(c.fixture,suffix).branchId,itemId}});}
 return {
  'E2E-02':async c=>{
   const get=s=>c.request(actor(s,'om'),'GET',`${api}/supplies/configuration?branchId=${branch(c.fixture,s).branchId}`);
   const first=c.ok(await get('DVBV')),other=c.ok(await get('DVCT'));
   c.equal(first.filter(x=>x.supplyGroup==='A'&&x.isActive).length,8);c.equal(other.filter(x=>x.supplyGroup==='A'&&x.isActive).length,8);
   const original=first.find(x=>x.supplyGroup==='A');
   let changed;
   try {
    changed=c.ok(await c.request('DVBV.om','PUT',`${api}/supplies/configuration`,{...original,initialQuantity:original.initialQuantity+1,minimumStock:original.minimumStock+1}));
    c.equal(changed.initialQuantity,original.initialQuantity+1);c.equal(changed.minimumStock,original.minimumStock+1);
    c.equal(c.ok(await get('DVCT')),other,'Branch configuration leaked into other branch');
    c.denied(await c.request('DVCT.om','PUT',`${api}/supplies/configuration`,changed));
   }finally{if(changed)c.ok(await c.request('DVBV.om','PUT',`${api}/supplies/configuration`,{...original,rowVersion:changed.rowVersion}));}
  },
  'E2E-03':async c=>{
   for(const [suffix,alias] of [['DVBV','hk'],['DVCT','hk'],['DVCT','hkPeer']]){
    const items=c.fixture.items.filter(x=>x.code.startsWith('P0_FILL_'));
    const before=await Promise.all(items.map(i=>stock(c,suffix,i.itemId)));
    const started=await start(c,suffix,alias);c.equal(started.result.status,'WORKING');
    const allocation=await getAllocation(c,suffix,alias);c.equal(allocation.status,'OPEN');c.equal(allocation.items.length,8);
    for(let n=0;n<items.length;n++){
     const balance=allocation.items.find(x=>x.itemId===items[n].itemId);
     c.equal(balance.initialIssued,balance.initialTarget);c.equal(balance.available,balance.initialTarget);
     c.equal(await stock(c,suffix,items[n].itemId),before[n]-balance.initialIssued);
    }
    c.ok(await c.request(actor(suffix,alias),'POST',started.route,started.payload));
    c.equal(await getAllocation(c,suffix,alias),allocation,'Start retry changed allocation or balances');
   }
  },
  'E2E-04':async c=>{
   const b=branch(c.fixture),configs=c.ok(await c.request('DVBV.om','GET',`${api}/supplies/configuration?branchId=${b.branchId}`)),original=configs.find(x=>x.supplyGroup==='A');
   const currentStock=await stock(c,'DVBV',original.itemId);
   const changed=c.ok(await c.request('DVBV.om','PUT',`${api}/supplies/configuration`,{...original,initialQuantity:currentStock+1}));
   const before=await probe('inventory',c.fixture);
   try {
    await start(c,'DVBV','hkPeer');
    const a=await getAllocation(c,'DVBV','hkPeer');c.equal(a.status,'SHORT');c.check(!!a.shortageReason,'Shortage reason missing');
    c.check(a.items.every(i=>i.initialIssued===0&&i.remaining===0),'Short start issued a partial allocation');
    c.equal(await probe('inventory',c.fixture),before,'Short start moved stock');
    const response=await createCleaning(c,'DVBV','hkPeer','P0_103');c.equal(response.status,409);
   }finally{c.ok(await c.request('DVBV.om','PUT',`${api}/supplies/configuration`,{...original,rowVersion:changed.rowVersion}));}
  },
  'E2E-05':async c=>{
   const before=await getAllocation(c),work=fields(c.ok(await createCleaning(c)));
   const reserved=await getAllocation(c);c.check(reserved.items.every((x,n)=>x.reserved===before.items[n].reserved+2),'Capacity-two room must reserve two of every fill');
   let executing=fields(c.ok(await workAction(c,'DVBV','hk',work.workId,'start')));c.equal(executing.status,'IN_PROGRESS');
   const payload={rowVersion:executing.rowVersion,operationKey:c.uuid()};
   const route=`${api}/work/${work.workId}/actions/complete`;
   c.ok(await c.request('DVBV.hk','POST',route,payload));
   const consumed=await getAllocation(c);
   for(const row of consumed.items){const previous=before.items.find(x=>x.itemId===row.itemId);c.equal(row.used,previous.used+2);c.equal(row.reserved,previous.reserved);}
   c.ok(await c.request('DVBV.hk','POST',route,payload));c.equal(await getAllocation(c),consumed,'Completion retry consumed twice');
   const detail=c.ok(await c.request('DVBV.supervisor','GET',`${api}/work/${work.workId}`));
   const inspect={rowVersion:fields(detail.work).rowVersion,operationKey:c.uuid(),result:'PASS',note:'P0 TEST paper inspection'};
   c.ok(await c.request('DVBV.supervisor','POST',`${api}/work/${work.workId}/inspection-result`,inspect));
   c.ok(await c.request('DVBV.supervisor','POST',`${api}/work/${work.workId}/inspection-result`,inspect));
   let room=c.ok(await c.request('DVBV.reception','GET',`${api}/rooms?branchId=${branch(c.fixture).branchId}`)).find(x=>x.roomId===work.roomId);
   c.equal(room.readiness,'READY');
   for(const action of ['reserve','check-in','check-out']){
    c.ok(await c.request('DVBV.reception','POST',`${api}/rooms/${room.roomId}/actions/${action}`,{rowVersion:room.rowVersion,operationKey:c.uuid()}));
    room=c.ok(await c.request('DVBV.reception','GET',`${api}/rooms?branchId=${branch(c.fixture).branchId}`)).find(x=>x.roomId===work.roomId);
   }
   c.equal(room.readiness,'PENDING_CLEANING');
  },
  'E2E-06':async c=>{
   const before=await getAllocation(c,'DVBV','hkPeer');c.equal(before.status,'SHORT');
   const response=await createCleaning(c,'DVBV','hkPeer','P0_103');c.equal(response.status,409);
   c.check(['STAFF_SUPPLIES_INSUFFICIENT','STAFF_SUPPLY_SHORTAGE','SUPPLY_ALLOCATION_SHORT','STAFF_SUPPLIES_NOT_READY'].includes(response.body?.code??response.body?.error?.code)||String(response.body?.code??response.body?.error?.code).includes('SUPPL'),'Expected supply-specific business error');
   c.equal(await getAllocation(c,'DVBV','hkPeer'),before,'Rejected assignment changed balances');
  },
  'E2E-08':async c=>{
   const before=await getAllocation(c,'DVCT'),peerBefore=await getAllocation(c,'DVCT','hkPeer');
   const work=fields(c.ok(await createCleaning(c,'DVCT')));
   c.ok(await workAction(c,'DVCT','supervisor',work.workId,'assign',{userId:c.fixture.actors['DVCT.hkPeer'].userId,durationMinutes:15}));
   const old=await getAllocation(c,'DVCT'),peer=await getAllocation(c,'DVCT','hkPeer');
   c.equal(old.items.map(i=>i.reserved),before.items.map(i=>i.reserved));
   for(const row of peer.items)c.equal(row.reserved,peerBefore.items.find(x=>x.itemId===row.itemId).reserved+2);
   c.ok(await workAction(c,'DVCT','supervisor',work.workId,'cancel',{reason:'P0 TEST cancellation releases reservation'}));
   c.equal((await getAllocation(c,'DVCT','hkPeer')).items.map(i=>i.reserved),peerBefore.items.map(i=>i.reserved));
   c.equal(old.items.map(i=>i.remaining),before.items.map(i=>i.remaining));
  },
  'E2E-13':async c=>{
   const before=await getAllocation(c,'DVCT','hkPeer');c.equal(before.status,'OPEN');
   const stocks=await Promise.all(before.items.map(i=>stock(c,'DVCT',i.itemId)));
   const result=await end(c,'DVCT','hkPeer');c.ok(result.response);
   const closed=await getAllocation(c,'DVCT','hkPeer');c.equal(closed.status,'CLOSED');
   for(let n=0;n<closed.items.length;n++){
    const row=closed.items[n];c.equal(row.remaining,0);c.equal(row.reserved,0);
    c.equal(row.returned,before.items[n].returned+before.items[n].remaining);
    c.equal(await stock(c,'DVCT',row.itemId),stocks[n]+before.items[n].remaining);
   }
   c.ok(await c.request('DVCT.hkPeer','POST',result.route,result.payload));c.equal(await getAllocation(c,'DVCT','hkPeer'),closed);
  },
  'E2E-14':async c=>{
   const work=fields(c.ok(await createCleaning(c,'DVBV','hk','P0_103')));
   c.equal((await week(c)).active.canEnd,false);c.equal((await end(c,'DVBV','hk')).response.status,409);
   c.ok(await workAction(c,'DVBV','supervisor',work.workId,'cancel',{reason:'P0 TEST close work blocker'}));
   const a=await getAllocation(c),item=a.items[0];
   const request=c.ok(await c.request('DVBV.hk','POST',`${api}/requests/refills`,{allocationId:a.allocationId,items:[{itemId:item.itemId,quantity:1}],reason:'P0 TEST pending refill blocks closure',operationKey:c.uuid()}));
   c.equal((await week(c)).active.canEnd,false);c.equal((await end(c,'DVBV','hk')).response.status,409);
   c.ok(await c.request('DVBV.supervisor','POST',`${api}/requests/${request.request.workId}/actions/reject`,{rowVersion:request.request.rowVersion,operationKey:c.uuid(),reason:'P0 TEST close pending refill'}));
   c.equal((await week(c)).active.canEnd,true);
  },
 };
}
