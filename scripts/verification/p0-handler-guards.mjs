import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';

const service = 'src/HotelOps.Api/Compact/Services/';
const authority = readFileSync(`${service}WorkdayAuthority.cs`, 'utf8');
const requests = readFileSync(`${service}RequestWorkflowService.Actions.cs`, 'utf8');
const routing = readFileSync(`${service}RequestWorkflowService.cs`, 'utf8');
const domain = readFileSync(`${service}RequestWorkflowService.Domain.cs`, 'utf8');
assert.match(authority, /a\.Status=="WORKING".*a\.StartedAt!=null.*a\.EndedAt==null/);
assert.match(authority, /s\.BranchId==branch.*r\.Code==role/);
assert.match(authority, /m\.DepartmentId==s\.DepartmentId/);
assert.match(authority, /now<TimeZoneInfo\.ConvertTimeToUtc/);
assert.match(routing, /s\.RoleId==request\.QueueRoleId/);
assert.match(requests, /await WorkdayAuthority\.Require\(db,access,actor,clock,flow\.BranchId,role,ct\)/);
assert.match(domain, /flow\.HandlerUserId!=null\)return/);
assert.match(domain, /oldQueue!=await Role\(queueRole,ct\)/);
console.log('Handler source contracts passed; runtime scenarios require isolated authenticated fixture.');

// The integration suite supplies real HTTP clients, separate on-duty/off-duty users, and
// fresh requests already routed to the corresponding roles. This does not create fake stock.
export async function verifyHandlerRuntime({ offDutyCases, wrongRoleShiftCases, contested, readState }) {
  const denied = response => assert.ok([403, 409].includes(response.status), JSON.stringify(response));
  for (const test of [...offDutyCases, ...wrongRoleShiftCases]) {
    const before = await readState(test.id);
    denied(await test.client('POST', test.path, { ...test.body, operationKey: randomUUID() }));
    assert.deepEqual(await readState(test.id), before, 'Denied request changed persisted state');
  }
  const first = await contested.first('POST', `/api/v2/requests/${contested.id}/actions/claim`, {
    rowVersion: contested.rowVersion, operationKey: randomUUID(),
  });
  assert.equal(first.status, 200, JSON.stringify(first));
  const before = await readState(contested.id);
  denied(await contested.second('POST', contested.domainActionPath, {
    ...contested.domainActionBody, operationKey: randomUUID(),
  }));
  assert.deepEqual(await readState(contested.id), before, 'Another handler changed the claimed request');
}
