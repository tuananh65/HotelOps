# P0 isolated fixture preparation

Status: PARTIAL. No database mutation or API call executed by this worker.

Created:
- `scripts/database/compact-v2/p0-fixture.sql`: exact TEST database and schema 3.0.0 guards, two new synthetic branches, twenty identities (eight actors plus HK/store peer per branch), one warehouse each, currently scheduled eight-hour shifts, rooms, eight fill items, durable item and room equipment. No direct stock movements. Existing users/history untouched. Existing fixture fails safely rather than resetting.
- `scripts/verification/p0-fixture-prepare.ps1`: parameterized SQL execution; private artifact directory ACL; independent listener PID and SQL session database check before runtime; invokes real API bootstrap.
- `scripts/verification/p0-fixture-bootstrap.mjs`: twenty real logins, branch configurations, posted synthetic TEST receipts, independent MR approval and issue to room, non-HK shift starts. HK shifts remain planned for automatic allocation scenarios. Private checkpoint preserves completed steps; no false runtime PASS before all requests succeed.

Validation: `node --check scripts/verification/p0-fixture-bootstrap.mjs` passed. SQL/runtime and PowerShell execution NOT_TESTED because the integrated schema/API was not yet available.

## Integration prerequisite and approval block

Automatic privacy review rejected writing `scripts/verification/p0-fixture-credentials.mjs`, a random-password/Identity-V3 hash generator, because its filename may contain sensitive data. The tool explicitly requires approval before retrying that exact file. It was not retried through another tool or renamed. Therefore no credential generator or private credential file was created. Parent must resolve the privacy approval or provide an existing authorized private fixture input. This is a tooling block, not missing user business authorization.

Input private JSON: `{ "actors": [{ "alias", "suffix", "roleCode", "departmentCode", "username", "password", "passwordHash" }] }` with suffix DVBV/DVCT and aliases admin, owner, om, supervisor, hk, maintenance, reception, store, hkPeer, storePeer. Role codes correspond to the eight approved actors; departments OPS, OPS, OPS, HK, HK, ENG, FO, STORE, HK, STORE. Username must be unique and <=30 characters. Hash uses actual ASP.NET Identity V3, not plaintext. Never print contents.

Execute after migration/API readiness:
`& scripts/verification/p0-fixture-prepare.ps1 -PrivateActorsFile <private-json> -Endpoint http://127.0.0.1:<test-port> -ApiProcessId <pid> -AllowSyntheticTestReceipt`

Output `artifacts/p0-workflow/private/runtime-fixture.json` includes tokens keyed `DVBV.hk` etc., branch resources and item IDs. Parent adapter maps these aliases to the runtime-runner fixture shape. Receipt provenance is explicitly synthetic TEST only. Potential trigger/API incompatibilities must be resolved through actual execution; no claim of full fixture verification.

Unresolved: privacy approval for blocked generator; integrated TEST schema/API and runtime validation.
