import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

// Called by the isolated P0 API harness. request(user, path, body, expected)
// must authenticate real-role accounts and reject non-fixture database writes.
export async function verifyP0WorkAuthorization({ request, branchId, omId, ownerId,
  maintenanceSupervisorId, hkStaffId, incidentId, internalRequestId, maintenanceId }) {
  const action = row => ({ rowVersion: row.rowVersion, operationKey: randomUUID(), reason: 'P0 isolated verification' });
  for (const kind of ['INCIDENT', 'INTERNAL_REQUEST']) {
    await request(omId, 'work', { branchId, kind, title: 'Forbidden OM mutation' }, 403);
    await request(omId, `work/candidates?branchId=${branchId}&kind=${kind}`, undefined, 403);
  }
  await request(ownerId, 'work', { branchId, kind: 'OPERATIONAL', title: 'Forbidden Owner mutation' }, 403);
  for (const id of [incidentId, internalRequestId]) {
    const detail = await request(omId, `work/${id}`);
    assert.equal(detail.capabilities.canCoordinateIncident, false);
    assert.equal(detail.capabilities.canCoordinateRequest, false);
    assert.equal(detail.capabilities.canHandleRequest, false);
    for (const command of ['assign', 'accept', 'start', 'complete', 'verify', 'reopen', 'cancel', 'progress', 'route', 'triage', 'escalate', 'handover-offer', 'handover-accept', 'handover-reject']) {
      await request(omId, `work/${id}/actions/${command}`, { ...action(detail.work.fields), userId: omId }, 403);
    }
  }
  let work = (await request(omId, 'work', { branchId, kind: 'OPERATIONAL', title: 'P0 OM self-execution verification' })).fields;
  for (const command of ['assign', 'accept', 'start', 'complete', 'verify']) {
    work = (await request(omId, `work/${work.workId}/actions/${command}`, { ...action(work), userId: omId })).fields;
  }
  assert.equal(work.status, 'CLOSED');
  assert.equal(work.verifiedBy, omId);
  work = (await request(omId, `work/${work.workId}/actions/reopen`, action(work))).fields;
  assert.equal(work.status, 'PENDING_ASSIGNMENT');
  const maintenance = (await request(maintenanceSupervisorId, `work/${maintenanceId}`)).work.fields;
  await request(maintenanceSupervisorId, `work/${maintenanceId}/actions/assign`, { ...action(maintenance), userId: hkStaffId }, 403);
  await request(maintenanceSupervisorId, `work/${maintenanceId}/actions/handover-offer`, { ...action(maintenance), userId: hkStaffId }, 403);
  await request(hkStaffId, `work/${maintenanceId}/actions/start`, action(maintenance), 403);
  const candidates = await request(maintenanceSupervisorId, `work/candidates?branchId=${branchId}&kind=MAINTENANCE`);
  assert(candidates.every(user => user.userId === maintenanceSupervisorId));
  return { passed: true, operationalWorkId: work.workId };
}
