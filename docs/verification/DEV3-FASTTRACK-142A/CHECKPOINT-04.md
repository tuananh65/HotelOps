# FASTTRACK-142A checkpoint 04 — UC044 domain participant

Baseline `2984582e2ad4afd580331fb74b7b8f392aa46819`; canonical integration branch unchanged. This checkpoint is implementation progress, not full UC044 acceptance.

Selectively recovered approval preparation and domain writer from `d8d9195e13e0a5edb1edc79050ee56f24525209e`, excluding unsupported rework preparation dependencies. SRS section 5.6 requires mandatory PASS, assigned inspector, separation of duties, inspection Passed and cleaning Completed with audit in the same transaction. The internal participant now performs those domain writes using existing schema, caller-owned SERIALIZABLE transaction, locked source loading, real session/permission checks, current-cycle validation and rowversion predicates. No endpoint or canonical action was opened. Room projection, notification staging and durable replay must still be composed before API acceptance.

Changed application files: `Services/Housekeeping/InspectionConclusionPreparation.cs`, `InspectionApproveDomainWriter.cs`. Added focused transaction-boundary regression in LoaderTests. Added `SprintApproveProbe.cs`, `SprintApproveFixture.sql` and ModelProbe dispatch. No generated entities, mappings, permission catalog, authentication security, main/develop or shared database changed.

## New direct evidence

- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors (`checkpoint04-build.log.txt`). First sandboxed attempt could not read local NuGet.Config; authorized elevated build succeeded.
- `dotnet test HotelOps.slnx`: exit 0, no test discovery (`checkpoint04-solution-test.log.txt`); not counted as a regression suite.
- Focused Inspection/Housekeeping xUnit: 3 PASS (`checkpoint04-fe05-xunit.log.txt`).
- Inspection DomainTests: 45 PASS (`checkpoint04-domain.log.txt`).
- Inspection LoaderTests: final 64 PASS (`checkpoint04-loader-final.log.txt`), including actual adapter rejection before opening SQL when no caller transaction exists.
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe -- --sprint-approve <new-report>`: final **10 SQL cases PASS**, each with verified rollback (`approve-domain-extended-verified.json`). Actual credential proof, SQL session, existing HK_SUPERVISOR branch grant and permission evaluator are used, not authorization doubles.

SQL cases: valid domain transition and exactly one audit; repeated request with original revision denied without another audit; mandatory FAIL; stale revision; wrong branch; revoked role grant; revoked session; cleaner/inspector conflict; invalid lifecycle; injected audit trigger failure. Final case rolls back inspection/task writes and the temporary trigger. Room status remains INSPECTION because this participant does not own the room projection. This is neither durable successful commit evidence nor HTTP/browser acceptance; no concurrency race has yet been tested.

Preserved first failure `approve-domain-first.json`: fixture left NOCOUNT ON, so session revocation's EF affected-row result was unsuitable. Fixture now restores NOCOUNT OFF; no production guard was weakened. `approve-domain-extended.json` retains a fixture failure using ASSIGNED for a cleaning assignment. Inspected existing CK_cleaning_assignments_status and corrected fixture to ACTIVE; final run passes. These failed attempts are not counted as successful cases.

All SQL writes target verified `meita` / `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, run marker `cc78c13ffc124efcad67b3d804baa84c`. Each fixture checks identity and empty synthetic baseline before INSERT. Every case rolls back and checks users, branches, inspections, sessions and audits are empty; injected trigger absence is checked. No real users or file bytes. The API remains stopped. New text logs are UTF-8 with only trailing display padding removed.

## Receipt

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=04_ACTIVE`; `INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`; `ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114`.

`SOURCE_IMPLEMENTED=UC044_INTERNAL_APPROVAL_PREFLIGHT_AND_DOMAIN_WRITER`; `FE05_STATUS=PARTIAL; DOMAIN_SQL_10_PASS`; `UC006_UC010_STATUS=WRITER_COORDINATION_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=CANDIDATE_PENDING`; `FE15_STATUS=CANDIDATE_PENDING`.

`ISOLATED_SQL_STATUS=APPROVAL_DOMAIN_NEGATIVES_AND_ROLLBACK_VERIFIED`; `HTTPS_API_STATUS=CHECKPOINT02_AUTH_PASS;STOPPED`; `E2E_VERIFIED_WORKFLOWS=CHECKPOINT02_SYNTHETIC_API_LOGIN_SESSION_LOGOUT_ONLY`.

`UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`.

`CRITICAL_BLOCKERS=FE05_ROOM_PROJECTION_NOTIFICATION_REPLAY_COMPOSITION;REINSPECTION_BINDING_AND_RESULT_STORAGE;CANONICAL_ROUTE_ACCEPTANCE;OTHER_WORKSTREAM_GAPS`.

`NEXT_IMPLEMENTATION=Compose FE05 room projection and notifications/replay against confirmed SRS; prove UC006/010 SQL writer coordination; complete supported FE11/13 and FE14/15 workflows`. Frontend baseline results remain historical 141A evidence, not rerun in this checkpoint.
