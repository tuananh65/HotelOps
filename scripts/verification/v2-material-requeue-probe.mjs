import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';

// Caller owns an isolated TEST fixture: missing quantity=2, first request=2,
// ISSUE=1 followed by close-remainder. No operational documents may be used.
// The existing quantity guards must reject 2 and permit only the outstanding 1.
export async function verifyPartialMaterialRequeue(request,{
  incidentId,documentId,itemId,unitId,warehouseId,departmentId,snapshot,
}) {
  const path=`/work/room-issues/${incidentId}`;
  const read=async()=>{
    const response=await request('supervisor',path);
    assert.equal(response.status,200);return response.data;
  };
  const before=await read();
  assert.equal(before.stage,'ROOM_ISSUE_MATERIAL_REQUESTED');
  assert.equal(before.canReplaceMaterial,true);
  assert.ok(before.materials.some(d=>d.documentId===documentId&&d.status==='REMAINDER_CLOSED'));
  const body={rowVersion:before.work.fields.rowVersion,operationKey:randomUUID(),
    itemId,unitId,warehouseId,departmentId,quantity:1};
  assert.equal(typeof snapshot,'function','Complete fixture snapshot is required');
  const stateBefore=await snapshot();
  for(const alias of ['hk','store','manager']){
    assert.equal((await request(alias,path+'/request-material','POST',body)).status,403,
      `${alias} must not reclaim the supervisor queue`);
  }
  const over=await request('supervisor',path+'/request-material','POST',{
    ...body,quantity:2,operationKey:randomUUID(),
  });
  assert.equal(over.status,409,'Posted one of two must remain committed');
  assert.equal(over.data.code??over.data.title,'REMEDIATION_EXCEEDS_REPORTED_MISSING');
  assert.deepEqual(await snapshot(),stateBefore,'Rejected roles and excess request must roll back workflow, work, documents, stock and audit');
  const unchanged=await read();
  assert.equal(unchanged.work.fields.rowVersion,before.work.fields.rowVersion,
    'Rejected over-request must roll back the queue handoff and work mutation');
  assert.equal(unchanged.materials.length,before.materials.length);
  const result=await request('supervisor',path+'/request-material','POST',body);
  assert.equal(result.status,200,'HKS must reclaim the closed warehouse request and request the missing one');
  const after=await read();
  assert.equal(after.materials.length,before.materials.length+1);
  assert.equal(after.stage,'ROOM_ISSUE_MATERIAL_REQUESTED');
  assert.equal(after.canReplaceMaterial,false,'New approved request must prevent a second replacement');
  const committed=await snapshot();
  assert.equal((await request('supervisor',path+'/request-material','POST',body)).status,200);
  assert.deepEqual(await snapshot(),committed,'Exact retry must not mutate workflow, documents, stock or audit');
  assert.equal((await read()).materials.length,after.materials.length,'Retry must not create another request');
  return after;
}
