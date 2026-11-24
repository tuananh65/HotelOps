import {readFile} from 'node:fs/promises';
import {run} from './p0-runtime-runner.mjs';
import {verifyTarget,probe} from './p0-runtime-adapter.mjs';

const fields=x=>x.fields??x;
async function scenario(c){
  const fixture=JSON.parse(await readFile('artifacts/p0-workflow/legacy-fixture.json','utf8'));
  const {workId,assignmentId,branchId}=fixture;
  const staff='DVCT.hkPeer', supervisor='DVCT.supervisor';
  const detail=()=>c.request(supervisor,'GET',`/api/v2/work/${workId}`).then(r=>c.ok(r));
  const allocations=()=>c.request(staff,'GET',`/api/v2/supplies/allocations?branchId=${branchId}`).then(r=>c.ok(r));
  const configs=()=>c.request('DVCT.om','GET',`/api/v2/supplies/configuration?branchId=${branchId}`).then(r=>c.ok(r));
  const before=await detail(), work=fields(before.work);
  c.equal(work.title,'P0 TEST legacy recovery');c.equal(work.status,'ON_HOLD');
  c.check(!((await allocations()).some(a=>a.assignmentId===assignmentId)),'Legacy attendance must have no supply allocation');
  const route=`/api/v2/work/${workId}/reconcile-supplies`;
  const body={rowVersion:work.rowVersion,operationKey:c.uuid(),reason:'P0 TEST explicit recovery of simulated pre-migration cleaning'};
  for(const actor of [staff,'DVBV.supervisor','DVCT.owner','DVCT.admin'])c.denied(await c.request(actor,'POST',route,body));
  const config=(await configs()).find(x=>x.supplyGroup==='A'&&x.isActive);
  const stock=Number(await probe('stock',{...c.fixture,requests:{branchId,itemId:config.itemId}}));
  const changed=c.ok(await c.request('DVCT.om','PUT','/api/v2/supplies/configuration',{...config,initialQuantity:stock+1}));
  const inventory=await probe('inventory',c.fixture);
  try{
    const rejected=await c.request(supervisor,'POST',route,body);
    c.ok(rejected,409);c.check(rejected.body.code.includes('SUPPL')||rejected.body.code.includes('STOCK'),'Expected material shortage');
    c.equal(await probe('inventory',c.fixture),inventory,'Failed initialization must roll back every issue');
    c.check(!((await allocations()).some(a=>a.assignmentId===assignmentId)),'Failed initialization left allocation');
    c.equal(await detail(),before,'Failed recovery changed work history/state');
  }finally{c.ok(await c.request('DVCT.om','PUT','/api/v2/supplies/configuration',{...config,rowVersion:changed.rowVersion}));}
  c.ok(await c.request(supervisor,'POST',route,body));
  const after=await detail(), allocation=(await allocations()).find(a=>a.assignmentId===assignmentId);
  c.check(allocation,'Recovery must initialize a real current allocation');
  c.equal(fields(after.work).status,work.status);c.equal(fields(after.work).actualStartAt,work.actualStartAt);
  c.equal(after.assignments,before.assignments);c.equal(after.time,before.time);
  c.check(allocation.items.every(i=>i.initialIssued===i.initialTarget&&i.reserved>0&&i.used===0),'Recovery issues and reserves without inventing historical use');
  const ledgerPath=`/api/v2/supplies/allocations/${allocation.allocationId}/history`;
  const ledger=c.ok(await c.request(staff,'GET',ledgerPath));
  c.ok(await c.request(supervisor,'POST',route,body));
  c.equal(c.ok(await c.request(staff,'GET',ledgerPath)),ledger,'Retry duplicated allocation/reservation');
  c.equal((await allocations()).find(a=>a.assignmentId===assignmentId),allocation);
  const date=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
  const calendar=()=>c.request(staff,'GET',`/api/v2/workday?branchId=${branchId}&date=${date}`).then(r=>c.ok(r));
  const unfinished=(await calendar()).active;c.equal(unfinished.assignmentId,assignmentId);c.equal(unfinished.canEnd,false);
  c.ok(await c.request(staff,'POST',`/api/v2/workday/assignments/${assignmentId}/end`,{rowVersion:unfinished.rowVersion,operationKey:c.uuid()}),409);
  for(const action of ['resume','complete']){
    const current=fields((await detail()).work), payload={rowVersion:current.rowVersion,operationKey:c.uuid()};
    c.ok(await c.request(staff,'POST',`/api/v2/work/${workId}/actions/${action}`,payload));
    if(action==='complete')c.ok(await c.request(staff,'POST',`/api/v2/work/${workId}/actions/${action}`,payload));
  }
  const completed=(await allocations()).find(a=>a.assignmentId===assignmentId);
  c.check(completed.items.every(i=>i.used===2&&i.reserved===0),'Consume exactly capacity two after old shift end');
  const inspect={rowVersion:fields((await detail()).work).rowVersion,operationKey:c.uuid(),result:'PASS',note:'P0 TEST paper inspection of recovered job'};
  c.ok(await c.request(supervisor,'POST',`/api/v2/work/${workId}/inspection-result`,inspect));
  const active=(await calendar()).active;c.equal(active.canEnd,true);
  const end={rowVersion:active.rowVersion,operationKey:c.uuid()},endRoute=`/api/v2/workday/assignments/${assignmentId}/end`;
  c.ok(await c.request(staff,'POST',endRoute,end));c.ok(await c.request(staff,'POST',endRoute,end));
  const closed=(await allocations()).find(a=>a.assignmentId===assignmentId);
  c.equal(closed.status,'CLOSED');c.check(closed.items.every(i=>i.remaining===0&&i.reserved===0),'Return every unused item exactly once');
  c.reconcileSupply(await probe('balances',c.fixture));c.reconcileInventory(await probe('inventory',c.fixture));
}
process.env.P0_RUNTIME_CASES='E2E-14';
process.exitCode=await run(process.env.P0_RUNTIME_FIXTURE,{verifyTarget,scenarios:{'E2E-14':scenario}},'artifacts/p0-workflow/runtime-legacy-supplement.json');
