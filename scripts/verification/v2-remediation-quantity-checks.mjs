import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';

// The owning isolated fixture supplies a complete DB snapshot (work/events/blocks/documents/ledger),
// HTTP sender, and a current-version body. This module never starts a server or mutates fixture SQL.
export async function verifyMissingReportQuantity({send, snapshot, body}) {
  const before = await snapshot();
  const rejected = await send({...body, quantity:100, operationKey:randomUUID()});
  assert.equal(rejected.status,409);
  assert.equal(rejected.data.code??rejected.data.title,'ROOM_ISSUE_EXCEEDS_ASSESSED_MISSING');
  assert.deepEqual(await snapshot(),before,'Excessive report must not create work, events, blocks, or ledger entries');
  const accepted = await send({...body, quantity:1, operationKey:randomUUID()});
  assert.equal(accepted.status,200);
  return accepted.data;
}

export async function verifyRemediationQuantity({send, snapshot, body}) {
  const before = await snapshot();
  const rejected = await send({...body, quantity:100, operationKey:randomUUID()});
  assert.equal(rejected.status,409);
  assert.equal(rejected.data.code??rejected.data.title,'REMEDIATION_EXCEEDS_REPORTED_MISSING');
  assert.deepEqual(await snapshot(),before,'Excessive remediation must not create documents, approval, or stock movements');
  const accepted = await send({...body, quantity:1, operationKey:randomUUID()});
  assert.equal(accepted.status,200);
  return accepted.data;
}
