import {run} from './p0-runtime-runner.mjs';
import {verifyTarget,probe,branch} from './p0-runtime-adapter.mjs';
const fields=x=>x.fields??x;
async function scenario(c){
  for(const suffix of ['DVBV','DVCT']){
    const b=branch(c.fixture,suffix),store=`${suffix}.store`,om=`${suffix}.om`;
    const item=c.fixture.items.find(x=>x.code==='P0_DURABLE_KETTLE');
    const stock=Number(await probe('stock',{...c.fixture,requests:{branchId:b.branchId,itemId:item.itemId}}));
    let d=fields(c.ok(await c.request(store,'POST','/api/v2/documents',{branchId:b.branchId,kind:'STOCKTAKE',sourceWarehouseId:b.warehouseId,reason:'P0 TEST simulated count discrepancy; no stock adjustment',operationKey:c.uuid()})));
    c.ok(await c.request(store,'POST',`/api/v2/documents/${d.documentId}/lines`,{rowVersion:d.rowVersion,itemId:item.itemId,unitId:item.unitId,quantity:1,countedQty:stock+1}));
    d=fields(c.ok(await c.request(store,'GET',`/api/v2/documents/${d.documentId}`)).document);
    c.ok(await c.request(store,'POST',`/api/v2/documents/${d.documentId}/actions/count`,{rowVersion:d.rowVersion,operationKey:c.uuid()}));
    const report=c.ok(await c.request(om,'GET',`/api/v2/insights/reports/stocktake-differences?branchId=${b.branchId}`));
    const row=report.find(x=>x.documentId===d.documentId);c.check(row,'Actual stocktake report row required');
    c.equal(row.differenceBaseQty,1);c.equal(row.needsAdjustment,true);
    const dash=c.ok(await c.request(om,'GET',`/api/v2/insights/dashboard?branchId=${b.branchId}`));
    c.check(dash.actionCenter.groups.find(g=>g.code==='STOCKTAKE_DIFFERENCES').items.some(x=>x.id===d.documentId),'Action center must link counted discrepancy');
    c.equal(Number(await probe('stock',{...c.fixture,requests:{branchId:b.branchId,itemId:item.itemId}})),stock,'Counting cannot adjust inventory');
    c.denied(await c.request(suffix==='DVBV'?'DVCT.om':'DVBV.om','GET',`/api/v2/insights/reports/stocktake-differences?branchId=${b.branchId}`));
    const transfers=c.ok(await c.request(om,'GET',`/api/v2/insights/reports/transfers?branchId=${b.branchId}`));
    c.check(transfers.length>0&&transfers.some(x=>x.lines.length>0),'Transfer report needs actual transfer lines');
  }
  const b=branch(c.fixture,'DVCT'),actor='DVCT.supervisor';
  const config=c.ok(await c.request('DVCT.om','GET',`/api/v2/supplies/configuration?branchId=${b.branchId}`)).find(x=>x.supplyGroup==='A'&&x.isActive);
  const allocation=c.ok(await c.request('DVCT.hk','GET',`/api/v2/supplies/allocations?branchId=${b.branchId}`)).find(a=>a.assignmentId===c.fixture.actors['DVCT.hk'].assignmentId);
  const rooms=c.ok(await c.request(actor,'GET',`/api/v2/work/cleaning-rooms?branchId=${b.branchId}`));
  const room=rooms.items.find(r=>r.code.startsWith('P0_')&&r.code!=='P0_LEGACY');c.check(room,'Available synthetic room required');
  let changed,work;
  try{
    const results=await Promise.all([
      c.request('DVCT.om','PUT','/api/v2/supplies/configuration',{...config,perGuestQuantity:config.perGuestQuantity+1}),
      c.request(actor,'POST','/api/v2/work',{branchId:b.branchId,kind:'CLEANING',title:'P0 TEST configuration assignment race',roomId:room.roomId,roomRowVersion:room.rowVersion,assigneeUserId:c.fixture.actors['DVCT.hk'].userId,durationMinutes:15})]);
    changed=c.ok(results[0]);work=fields(c.ok(results[1]));
    const after=c.ok(await c.request('DVCT.hk','GET',`/api/v2/supplies/allocations?branchId=${b.branchId}`)).find(a=>a.allocationId===allocation.allocationId);
    const prior=allocation.items.find(x=>x.itemId===config.itemId),balance=after.items.find(x=>x.itemId===config.itemId);
    c.equal(balance.initialTarget,prior.initialTarget,'Existing allocation initial target must not change');
    c.equal(balance.reserved-prior.reserved,2,'Capacity-two fixture must reserve its allocation snapshot of one per guest, not newly edited configuration');
  }finally{
    if(work){const d=fields(c.ok(await c.request(actor,'GET',`/api/v2/work/${work.workId}`)).work);c.ok(await c.request(actor,'POST',`/api/v2/work/${work.workId}/actions/cancel`,{rowVersion:d.rowVersion,operationKey:c.uuid(),reason:'P0 TEST close configuration race task'}));}
    if(changed)c.ok(await c.request('DVCT.om','PUT','/api/v2/supplies/configuration',{...config,rowVersion:changed.rowVersion}));
  }
  c.reconcileSupply(await probe('balances',c.fixture));c.reconcileInventory(await probe('inventory',c.fixture));
}
process.env.P0_RUNTIME_CASES='E2E-28';
process.exitCode=await run(process.env.P0_RUNTIME_FIXTURE,{verifyTarget,scenarios:{'E2E-28':scenario}},'artifacts/p0-workflow/runtime-report-snapshot.json');
