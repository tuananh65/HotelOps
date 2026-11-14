# UC012-only disposable acceptance

No production registration, schema installer, fixture writer or Task74 invocation.
Default execution is disabled. `HotelOpsDB`, shared/staging databases, unverified
certificates and missing owner/fixture/lease/audit contracts are rejected.

```powershell
dotnet run --project scripts/verification/UC012Acceptance -- --self-test
dotnet run --project scripts/verification/UC012Acceptance -- --prepare <NEW-directory>
dotnet run --project scripts/verification/UC012Acceptance -- --preflight <receipt> <SHA256> <NEW-report>
dotnet run --project scripts/verification/UC012Acceptance -- --execute <receipt> <SHA256> <NEW-report>
dotnet run --project scripts/verification/UC012Acceptance -- --report <report> <SHA256>
```

Prepare writes only an unapproved receipt template and 12-case manifest. It never
loads certificates, opens SQL or starts HTTPS. Preflight requires a signed-off receipt
and performs SELECT-only metadata/fixture queries and a read-only certificate-store
check. It does not log in: real cookie/session/CSRF readiness is checked by execute.
Failure never becomes runtime acceptance. Reports are create-new and sanitized.

## Required owner inputs

Exact server/database/GUID, database extended property `HotelOps.UC012.Trial` matching
the receipt isolation marker, owner approval reference, exclusive active lease,
restricted SQL principal (no sysadmin/db_owner/ALTER database/CONTROL SERVER), approved
schema artifact/hash, and dedicated active actor + branch-bound denied actor. Neither
database naming nor a file alone proves isolation; owner evidence must substantiate
the marker/receipt. The runner cannot provision schema or manufacture that approval.
The receipt must also bind `HostMachine` to the current process machine. SQL hostname
and certificate-file overrides are rejected; ordinary chain/hostname validation remains on.

Fixture JSON must bind `owner`, `databaseGuid`, `leaseId`, `actorId`, `deniedActorId`,
`codePrefix`, `approved:true`; its bytes must match the receipt SHA. There must be no
existing Branch with the leased prefix. The suite retains new Branch/audit records
as trial evidence and performs no DELETE/cleanup. A fresh run requires a new prefix
and approved fixture receipt. Session expiry affects only the dedicated denied
identity's session created by this run. The exclusive lease must prohibit other use
of both identities. No historical/imported session is activated.

`AuditFaultStatus` must be explicitly approved (500 or 503) and carry an approval
reference. Current BranchAdministrationService maps DbUpdateException to 503. The
runner injects an exception into the actual Branch AuditLog SaveChanges, after the
Branch UPDATE, without DDL; it checks unchanged version/name/audit after rollback.
An approved 500 expectation against a 503 implementation FAILS, never auto-adjusts.

Frontend verification JSON: `status:"PASS"`, `candidateAssemblySha256`,
`frontendContractsSha256`, `testsPassed` must bind the reviewed cutover version.
It is an owner-reviewed regression receipt, not fabricated by this tool. HTTP tests
also verify canonical quoted Base64/8-byte ETags and rejection of legacy 64-hex.

## Candidate closure and runtime

The runner refuses preflight/execute if BranchMaintenanceController still depends
on the legacy RoomTypeCreationService. Use an isolated PR51 candidate checkout and
the optional MSBuild `-p:UC012ApiProject=<absolute-HotelOps.Api.csproj>` to build against
that exact API project, without integrating its active cutover into production.
The loaded API assembly SHA must match the approved receipt. Never deploy this host.

Process-only environment: `UC012_TRIAL_SQL` (encrypted certificate-verified SQL),
`UC012_ACTOR_PASSWORD`, `UC012_DENIED_PASSWORD`; additionally `UC012_EXECUTE=APPROVED`
for execute. Never store credentials in receipt, Git or command arguments/logs.
The local host uses only Auth Login/Session/Logout, CSRF and the three actual PR51
BranchMaintenance actions; no BranchAdministrationController/deactivation discovery.
HTTPS is localhost-only, trusted development certificate required, separate DPAPI
cookie keys/purpose. Normal auth/session/CSRF/permission infrastructure is reused.
No certificate callback, trust bypass, retry strategy or automatic mutation replay.

Any SQL/HTTPS failure, lease expiry, missing fixture or unexpected status stops the
suite. Partial reports retain completed case IDs and cannot claim 12/12 acceptance.
PR51 remains HOLD even after runner source merge; live PASS + combined review is required.

## FASTTRACK-041R receipts and environment boundary

- Solution and runner build: PASS, zero final warnings/errors. Also compiled against
  exact PR51 `be6c7278e1f5749211e97b55c8c21c0727b31fba` API via the project override.
- 44 offline behavioral guards and 11 CLI tests PASS on both API compositions.
- Integration Branch API/browser + Auth HTTP/state: 181/181 PASS; frontend typecheck/build
  PASS, two inherited React Router use-client warnings. No frontend source changes.
- Read-only host observations: MEITA/maitu not Windows-elevated; named/default SQL services
  running; trusted localhost development certificate present. `sqlcmd -E -N` metadata
  query on the owner-confirmed named instance succeeded; current login is SQL sysadmin.
  This identity is explicitly unsuitable for this runner's application principal.
- No UC012 trial database matching the prefix was found. No approved compatible
  schema/fixture receipt, restricted trial principal, exclusive lease or signed-off
  audit-fault contract was supplied. No database/principal/schema was created.
- `--prepare` and blocked template `--preflight`/`--report` exercised without connecting
  SQL or starting HTTPS. SQL/HTTPS acceptance, races and injected rollback NOT_RUN.
- PR51 MERGE_HOLD; no production route cutover or business mutation activation.
