import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';

// Caller owns the isolated TEST fixture and supplies an OPEN, unassigned legacy incident
// with a room asset and no request workflow/repair child. Never call against operational data.
export async function verifyLegacyIncidentRecovery(request,{incidentId,assetId,branchId,cleaningWorkId=null}) {
  const get=async(alias,path)=>{
    const response=await request(alias,path);
    assert.equal(response.status,200,path);return response.data;
  };
  const original=await get('supervisor',`/work/room-issues/${incidentId}`);
  assert.equal(original.legacy,true);assert.equal(original.canRecover,true);
  const fields=original.work.fields;
  const originalWork=await get('supervisor',`/work/${incidentId}`);
  const listed=await get('supervisor',`/work/room-issues?branchId=${branchId}`);
  assert.ok(listed.some(w=>w.workId===incidentId&&w.legacy===true),'Legacy incident must be discoverable before recovery');
  const body={rowVersion:fields.rowVersion,roomRowVersion:original.room.rowVersion,
    operationKey:randomUUID(),assetId,note:'Synthetic TEST physical verification of legacy damage'};
  const route=`/work/room-issues/${incidentId}/recover`;
  assert.equal((await request('om',route,'POST',body)).status,403);
  assert.equal((await request('hk',route,'POST',body)).status,403);
  assert.equal((await request('supervisor',route,'POST',{...body,assetId:-1})).status,409);
  assert.equal((await request('supervisor',route,'POST',{...body,note:' '})).status,400);
  const unchanged=await get('supervisor',`/work/room-issues/${incidentId}`);
  assert.equal(unchanged.work.fields.rowVersion,fields.rowVersion);
  assert.equal(unchanged.room.rowVersion,original.room.rowVersion);
  assert.equal(unchanged.legacy,true);
  assert.equal((await request('supervisor',route,'POST',body)).status,200);
  const recovered=await get('supervisor',`/work/room-issues/${incidentId}`);
  assert.equal(recovered.stage,'ROOM_ISSUE_REPAIR_REQUESTED');
  assert.equal(recovered.work.fields.createdBy,fields.createdBy);
  assert.equal(recovered.work.fields.createdAt,fields.createdAt);
  assert.equal(recovered.work.fields.status,'IN_PROGRESS');
  assert.equal(recovered.room.rowVersion,original.room.rowVersion,'Recovery must not change readiness or release the room');
  assert.equal(recovered.info.kind,'BROKEN');
  assert.equal(recovered.info.cleaningWorkId,cleaningWorkId,
    'Recovery must preserve the recorded CLEANING/REWORK context, never substitute the room current task');
  assert.equal(recovered.work.fields.parentWorkId,fields.parentWorkId,'Recovery must not rewrite ancestry');
  assert.equal(recovered.work.fields.turnoverNo,fields.turnoverNo,'Recovery must not move the incident to a new cycle');
  const recoveredWork=await get('supervisor',`/work/${incidentId}`);
  for(const link of originalWork.links){
    assert.ok(recoveredWork.links.some(l=>JSON.stringify(l)===JSON.stringify(link)),
      'Recovery must preserve every existing link');
  }
  const originalSource=originalWork.links.filter(l=>l.fields.relation==='SOURCE'&&l.fields.targetWorkId===cleaningWorkId);
  const expectedAddition=cleaningWorkId!=null&&originalSource.length===0?1:0;
  assert.equal(recoveredWork.links.length,originalWork.links.length+expectedAddition);
  if(cleaningWorkId!=null){
    const sources=recoveredWork.links.filter(l=>l.fields.relation==='SOURCE'&&l.fields.targetWorkId===cleaningWorkId);
    assert.equal(sources.length,1,'Validated parent ancestry must be visible to cleaning inspection queues');
    const cleaning=await get('supervisor',`/work/${cleaningWorkId}`);
    assert.ok(cleaning.roomIssues.some(i=>i.workId===incidentId),
      'Recovered incident must appear in the associated cleaning details');
  }
  for(const event of originalWork.events){
    assert.ok(recoveredWork.events.some(e=>JSON.stringify(e)===JSON.stringify(event)),
      'Recovery must retain every existing event');
  }
  assert.equal(recovered.events.filter(e=>e.eventType==='LEGACY_ROOM_ISSUE_RECOVERED').length,1);
  assert.equal(recovered.events.filter(e=>e.eventType==='ROOM_ISSUE_REPORTED').length,0);
  assert.equal((await request('supervisor',route,'POST',body)).status,409);
  const afterReplay=await get('supervisor',`/work/room-issues/${incidentId}`);
  assert.equal(afterReplay.work.fields.rowVersion,recovered.work.fields.rowVersion);
  assert.equal(afterReplay.info.cleaningWorkId,cleaningWorkId);
  assert.equal(afterReplay.events.filter(e=>e.eventType==='LEGACY_ROOM_ISSUE_RECOVERED').length,1);
  const inbox=await get('lead',`/work/maintenance-inbox?branchId=${branchId}&queue=waiting&page=1`);
  assert.ok(inbox.items.some(w=>w.workId===incidentId));
  const lead=await get('lead',`/work/room-issues/${incidentId}`);
  assert.equal(lead.canCreateRepair,true);
  return recovered;
}

// Run after the caller completes repair, technical review and one HKS inspection-pass.
// Use two isolated cases: current cycle pending inspection, and historical context with
// a different current cleaning cycle. The caller captures currentBefore before that PASS.
export async function verifyRecoveredCleaningCompletion(request,{
  incidentId,cleaningWorkId,currentCleaningWorkId,currentBefore,currentCycle,
}) {
  const read=async path=>{
    const response=await request('supervisor',path);
    assert.equal(response.status,200,path);return response.data;
  };
  const issue=await read(`/work/room-issues/${incidentId}`);
  assert.equal(issue.info.cleaningWorkId,cleaningWorkId);
  assert.equal(issue.stage,'ROOM_ISSUE_INSPECTION_PASSED');
  const detail=await read(`/work/${currentCleaningWorkId}`);
  const fields=detail.work.fields;
  if(currentCycle){
    assert.equal(fields.status,'COMPLETED','One remediation PASS must complete the current finished cleaning cycle');
    assert.equal(detail.capabilities.canRecordPaperInspection,false,'No redundant inspection step should remain');
  }else{
    assert.equal(fields.rowVersion,currentBefore.rowVersion,'Historical remediation must not modify the newer cleaning cycle');
    assert.equal(fields.status,currentBefore.status);
    assert.equal(fields.turnoverNo,currentBefore.turnoverNo);
  }
}
