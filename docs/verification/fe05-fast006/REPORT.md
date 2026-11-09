# FE05-FAST-006 — IMPLEMENTED_PENDING_DEPENDENCIES

**FE04_DEPENDENCY_STATUS: IMPLEMENTED_PENDING_VERIFICATION / NOT_READY for authoritative handoff.**
**UC045/046 successful authoritative committed mutation: NOT_RUN.**

## Exact source / scope

Worktree `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST006`, feature branch
`feature/dev1-fe05-authoritative-composition`, parent
`c3fe6bba1716ea54ace0f769b3b9bbf9e2ed234f`.
New source paths:

- `scripts/database/fe05-fast006/01_TrustedInspectionIssuer.sql`: candidate owner SQL procedure,
  no tables/grants/registry seed/producer activation and no domain COMMIT. Installer refuses overwrite
  and requires owned isolated FE05 trial. Not a shared deployment script.
- `Services/Housekeeping/InspectionTrustedIssuer.cs`: internal operation identity + command adapter
  through the existing scoped context/connection/SERIALIZABLE transaction, existing preflight,
  session/permission checks, assignee eligibility, same transaction postchecks and FE04 snapshot.
- `docs/verification/fe05-fast006/candidates/InspectionN01Composition.cs`: compile-only isolated
  coordinator candidate, NOT included in production API compilation. Depends on exact DEV2 source.
- `ReworkAuthorityReader.cs`: exact unmodified R03 dependency update; `InspectionAuthorityIntegration.cs`
  updates its pin metadata. UC044, public conclusion gates, controllers, Program, shared auth/mappings
  and notification source in production remain unchanged.
- New issuer unit/SQL harness and Program dispatch; exact source inventory/hash attached.

## FE04 pin

Fetched R03 `d37e99f42eaaf3089eb510c6298296a0f0743faa` from
`origin/feature/dev1-fe04-real-rework-authority`. This commit contains the two fixes beyond R01:
authority candidate SQL and explicit BIN2 issuer JSON comparison in reader.
The reader is imported verbatim and checked by Git blob; contract/projection files match the same pin.
The R03 SQL candidate is referenced by pin/blob, not deployed or copied into shared schema.
No claims that old R01 is final SQL-verified dependency. Owner source/trial tests are not counted as
FE05 different-transaction consumer verification.

## DEV2 package / Closure05 minimal dependencies

ZIP `HotelOps_DEV2_FE05_N01_IMPLEMENT06_HANDOFF.zip` verified SHA256
`45955b382a34ff31b2128a3365c5bf71a701de8332c6298c3896bc8a2b85ca7f`.
Package source identity `30bf39ba76e793bab9ad1777a5563d593b6f37239316b0877bcc011c50217392`;
source commit is `DEFERRED_BY_USER`, not a Git pin. Baseline Closure05 manifest identity
`e0d0ab3cd31f5d8ead780ef4ce13ab3f5d2c4fb1107983443d6174d5b3e75764`.

PL supplemental decision followed: none of the 14 N01 files applied to production, no wholesale
463-file source copy. Only 14 allowlisted files + 15 proposed directly required foundation files
were extracted under ignored TestResults for compatibility compilation. Each N01 changed-file hash
matched package allowlist. `closure05-proposed-dependency-allowlist.csv` records each exact path,
SHA256, owner/source identity, target existence and collision risk.

Foundation set for full 14-file overlay (not permission to integrate): NotificationContracts,
NotificationTransactionParticipant, NotificationSourcePolicy/SourceReference, Inbox/Delivery/
DeliveryFailures/DeliveryWorker/CleaningReadQuery, Fe05NotificationStaging, NotificationIntent entity,
HotelOpsDbContext.Notifications, NotificationSchemaValidator, IntegrationSqlGuard and
Security/Authorization/Resources/MaintenanceWorkAccessReader.

Concrete compatibility findings:

1. `NotificationCommitGuard` is embedded in changed `NotificationPublisher.cs`; target has only
   different FE04 candidate guards. Never substitute those guards or create a second notification graph.
2. `Fe05NotificationStaging.cs` is absent from target and not in 14 changed files; required by N01
   staging/registration. It is unchanged foundation dependency, SHA recorded.
3. `NotificationSourcePolicy.cs` requires `MaintenanceWorkSource(s)` from MaintenanceWorkAccessReader,
   another missing cross-module dependency. Initial candidate compile failed with three CS0246 errors;
   these two exact source files fixed compilation in isolated candidate only.
4. Target shared DbContext lacks `ConfigureNotifications` invocation. Candidate compilation does not
   prove rowversion/configuration model runtime readiness. Do not edit generated/shared context to force it.
5. `NotificationRegistration` requires delivery/inbox and fail-closed source defaults. Program/DI and
   AddDbContext interceptor attachment are not integrated. No worker/producer flags changed.
6. `IntegrationSqlGuard` / concrete N01 store / N01 DDL require exact KIENPC, database/runtime identity
   and central lease. Local SQL Express issuer trial is NOT that accepted substrate. No retarget/bypass.
7. Package uses EF 10.0.8 whereas current API baseline uses its existing pinned package versions.
   Candidate compiled with existing target project packages; no production package upgrade performed.

DEV2 confirmation requested via this handoff: approve/provide minimal Closure05 foundation package
and the 15 proposed dependencies, mapping hook/DI/guard composition, compatibility with existing
FS013 users and exact authorized SQL lane. This is not a request to rebuild N01. Until confirmed,
safe integration is limited to package hash/allowlist review and isolated compilation, not production staging.

## Trusted issuer implementation / transaction contract

`dbo.fe05_inspection_issuer_v1 WITH EXECUTE AS OWNER` actions:
`VALIDATE_REJECT`, `REJECT`, `REWORK`, `COMPLETE_REJECT`, `COMPLETE_REWORK`.
SQL validates caller SERIALIZABLE/XACT_STATE, user/session credential hash/created-at/revocation/
expiry, existing INSPECTION_PERFORM grant paths, branch/room/floor scope, assigned inspector on
Reject, source revision/current cycle, mandatory results and FAIL defects. Requires caller catalog lock.
No role/permission IDs invented. C# repeats existing authorization and eligible housekeeper checks.

Mutation actions require the existing N01-enhanced reservation row with exact actor/operation/
request fingerprint before mutation. Domain transitions preserve FAST003 conditional root/task and
Rework identity/defect linking semantics; owner SQL boundary necessarily repeats those predicates
inside module rather than trusting a client-loaded graph. Existing FAST003 primitives are unchanged.
No second ledger. Completion requires issuer/module hash registration and persisted N01 event graph,
then updates existing reservation result_json/completed, audit and FE04 typed proof.
Receipts follow FE04 `FE05_INSPECTION_CONCLUSION_V1` / `FE05_REWORK_AUTHORITY_V1`, issuers v1,
FE05_COMMITTED_V1, writer transaction ID and persisted source/revision/digests.

**Candidate limits:** no completed module/graph security acceptance. Direct SQL REWORK assignee
eligibility and canonical request recomputation rely on caller adapter today; SQL must still independently
close those authority checks before granting EXECUTE. Completion digests come from exact FE04 C# reader;
trusted module-to-reader binding, prior conclusion receipt's full issuer/audit/projection/replay proof,
recipient policy and unknown-commit handling are not accepted/verified. Do not register or grant this
candidate as a production issuer. SQL source exists and compiles; it is NOT a fully reviewed authoritative writer.

Isolated coordinator candidate uses the exact N01 Reserve/Stage APIs and guard, reserves before domain
write, takes Replay before rerunning source/domain writer, requires N01 completed authority validation,
checks stored domain contract/identity/fingerprint, stages event, completes owner receipt/proof, invokes
FE04 projection on Reject, rereads evidence digest on Rework, and verifies guard before returning.
It never owns Begin/Commit/Rollback. Caller must prelock users/recipients/assignee in agreed ordering,
rollback on every participant error, use guarded EF CommitAsync only and preserve unknown outcomes.
No permissive coordination/recipient authority implementation was added; candidate cannot unblock public APIs.

Candidate request fingerprints distinguish UC045 from UC046 and bind actor/branch/inspection/expected
revision/reason/confirmation or assignee/due/ordered defect set. This request hash is not a second evidence
authority digest. HTTP UC046 Idempotency-Key transport remains unchanged pending accepted canonical contract.

## Actual tests / receipts

| Check | Result |
|---|---|
| Production API solution build | PASS, 0 warnings/errors |
| New issuer canonical-request unit checks | 11/11 PASS; not replay acceptance |
| Issuer SQL installation | PASS, execute_as_principal_id=-2; no grants/issuer registration |
| Issuer SQL focused cases | 12/12 PASS; all explicitly rolled back, persistence checked |
| Isolated Closure05 + 14-file overlay compatibility build | PASS after two missing dependencies added to candidate |
| Isolated N01 coordinator compilation | PASS, 0 warnings/errors; compile-only, no runtime claim |
| `dotnet test HotelOps.slnx --no-build --no-restore` | exit 0; API-only solution, not a test count |
| Successful REJECT/REWORK domain mutation through new module | NOT_RUN: required N01 reservation/substrate absent |
| Duplicate/conflicting operations, completed replay, partial-participant rollback | NOT_RUN against N01 SQL |
| Real issued proof/receipt acceptance, projection/block, before-commit guard SQL | NOT_RUN |
| Different-transaction FE04 consumer / authoritative public commit | NOT_RUN |

New isolated trial `HotelOps_Canonical_20261109_040343_02dc2f7b_Fresh`, local
`DESKTOP-JI8U50T\SQLEXPRESS`, SQL16.0.1000.6, UUID owner marker
`66916e64-68e7-40fd-8862-e5086e83c137`. Identity/marker verified before mutations; synthetic fixtures,
0 users initially. No shared settings/secrets used. Existing databases not reset/dropped.
Inherited DDL owner was an unresolved Windows account: first owner-module invocation failed SQL15404
before any test PASS. Approval review temporarily unavailable (usage limit), action not executed then.
After user continued and review service recovered, only this verified disposable trial's database owner
was repaired to local SQL principal SID0x01. EXECUTE AS OWNER retained; no API role grants added.
Harness refuses overwrite and verifies exact installed definition on rerun. Second run: 12/12 PASS.

Cases: valid real assigned Reject source; missing N01 reservation blocks mutation; revoked session;
branch permission isolation; wrong assigned inspector; D4 stale evidence revision; missing cycle;
false/NULL confirmation; missing source; missing FAIL defect; missing catalog lock.
Every case rollback compares root/task/cycle snapshot, result/defect/evidence state and session/audit counts.
These checks do not establish least-privileged runtime/module security or successful atomic completion.

## Remaining blockers by owner / progress

- DEV2 + PL: confirm minimal Closure05 allowlist, mandatory recipient/source-read policy, N01 graph/schema
  acceptance and exact authorized runtime lane. No second queue/ledger/publisher permitted.
- FE05: independent SQL authorization/canonical-request completion, full trusted module-to-snapshot binding,
  production coordinator/replay/unknown-commit completion, UC046 key transport after accepted contract.
- FE04 + schema owner: activate/pin exact reviewed final issuer only after full composition tests; R03 SQL
  substrate/real proof/different-transaction consumer/projection/Blocked precedence verification in FE05 lane.
- Shared runtime: immutable private file storage; digest reread cannot prevent file mutation after checking.

FAST006 task estimate **55%**, UC044 **45%** unchanged, UC045 **75%**, UC046 **75%**.
New code and unit/source-validation checks delivered; production full-stack acceptance unchanged.
No AUTHORITATIVE_INSPECTION_HANDOFF_READY, no UC ACCEPTED. Feature-only commit/push after scope/hash checks;
no shared merge/deploy, shared SQL, producer activation, authentication/router or generated entity edits.
