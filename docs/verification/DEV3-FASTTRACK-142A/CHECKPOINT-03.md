# FASTTRACK-142A — checkpoint 03: FE05 evidence revision

Baseline: `c464b800de0a1a9d967285312534f9446b317d42`. Canonical branch: `integration/dev1-dev2-dev3-unblock-20261114`. Sprint remains active; this is not release acceptance.

## Implementation

- Selectively recovered housekeeping evidence amendment, postflight and runner/rollback support from reviewed FE05 candidate `d8d9195e13e0a5edb1edc79050ee56f24525209e`. Attachment insert/update/delete and evidence link/unlink now invalidate the parent inspection revision. Foreign inspection evidence associations remain rejected.
- Fixed a concrete integration defect: the existing evidence read guard recognized only the Windows CRLF package hash. It now accepts the two pinned, byte-verified LF/CRLF hashes of the same reviewed SQL. Unknown hashes and disabled triggers still fail closed. Deployment verification compares full trigger bodies, normalizing only line endings and the SQL Server DDL header.
- Added exact-instance/database/GUID/run-bound candidate prerequisite installer. Existing WorkTime, current-cycle and notification proposal schemas were installed only on the owned disposable target. No generated entities, permissions, application action fuses or shared database were changed.
- Added actual guard probe and updated schema probe to compare amended trigger definitions. The guard probe rolls back marker changes and trigger disabling, then verifies restoration.

## Direct execution evidence

| Check | Result | Evidence |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS, 0 warnings/errors | `checkpoint03-build.log.txt` |
| `dotnet test tests/HotelOps.Dev1.RegressionTests --filter 'FullyQualifiedName~Inspection\|FullyQualifiedName~Housekeeping'` | 3 PASS, 0 FAIL/SKIP | `checkpoint03-fe05-xunit.log.txt` |
| Inspection DomainTests | 45 PASS, in-memory only | `checkpoint03-inspection-domain.log.txt` |
| Inspection LoaderTests | 63 PASS, orchestration doubles | `checkpoint03-inspection-loader.log.txt` |
| `dotnet test HotelOps.slnx` | exit 0, no test discovery; not regression proof | `checkpoint03-solution-test.log.txt` |
| Candidate prerequisite installer | All 5 source steps exit 0 | `candidate-schema/candidate-schema.json` and logs |
| Actual EF zero-row SQL projections | 93/93 PASS; not full schema/business acceptance | `schema-projections-evidence.json` |
| Exact installed trigger source comparison | 22 PASS; checklist/housekeeping/progress guards PASS | `schema-projections-evidence.json` |
| SQL evidence revision trial | 6 PASS: attachment insert, link, unlink, soft deletion, foreign association denial, rollback cleanup | `evidence-revision-trial.sql`, `evidence-revision-trial.log.txt` |
| Actual application evidence guard | 4 PASS: LF, CRLF, unknown marker denial, disabled trigger denial; rollback restoration PASS | `evidence-guard.json` |
| Housekeeping PowerShell runner | AST parse PASS; historical-target runner not executed on sprint database | Direct command output |

New checkpoint logs are stored as UTF-8 with trailing display padding removed; assertion text and outcomes are unchanged. Earlier evidence files were not normalized again.

The initial SQL fixture failed because a required `users.created_at` value was omitted. The original failure is retained in `evidence-revision-trial-initial.log.txt`; fixture now supplies actual required timestamps. An initial new probe compile failed due to an extra parenthesis and was corrected before SQL execution. No failure is counted as PASS.

SQL target: `meita` / `HotelOps_DEV3_142A_20261114_cc78c13f`; database GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`; run marker `cc78c13ffc124efcad67b3d804baa84c`. Synthetic users/branches/rooms/tasks/inspections/attachments rolled back; schema and catalog deliberately retained for later trials. No file bytes were created. Shared `HotelOpsDB` and `HotelOps_ImportReview` were not mutated. SQL connections use encrypted local connections with trusted-server-certificate setting; server certificate chain acceptance is not claimed.

API remains stopped. Checkpoint 02 HTTPS auth checks are historical evidence within this sprint, not rerun here. Frontend is unchanged; npm/typecheck/build/AuthRouter evidence remains the accepted 141A baseline. Existing EF mapping warnings from runtime probes remain; the clean backend build does not erase those warnings.

## Acceptance and next work

`TASK_ID=DEV3-FASTTRACK-142A`

`CHECKPOINT=03_ACTIVE`; `INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`

`FE05_STATUS=PARTIAL; EVIDENCE_REVISION_SQL_VERIFIED`; `UC006_UC010_STATUS=WRITER_COORDINATION_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=NOTIFICATION_SCHEMA_ONLY; WORKFLOW_PENDING`; `FE15_STATUS=CANDIDATE_PENDING`.

`ISOLATED_SQL_STATUS=93_MODEL_PROJECTIONS_PASS;22_TRIGGER_DEFINITIONS_PASS;EVIDENCE_TRIAL_ROLLBACK_PASS`; `HTTPS_API_STATUS=CHECKPOINT02_AUTH_PASS;CURRENTLY_STOPPED`; `E2E_VERIFIED_WORKFLOWS=CHECKPOINT02_SYNTHETIC_API_LOGIN_SESSION_LOGOUT_ONLY`.

`UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`. Whole-UC classifications remain conservative; individual SQL checks do not establish full UC acceptance.

Critical remaining dependencies: FE05 exact reinspection binding, per-defect result persistence, atomic conclusion/room-status/audit/notification participants, replay and canonical route admission; UC006/010 physical writer coordination; missing FE11/13 workflows; FE14/15 scoped source integration and real business E2E.

Next three coding tasks: (1) reconcile and implement supported FE05 conclusion participants with SQL negative/concurrency tests; (2) reconcile UC006/010 user-first writer contracts and test real competing transactions; (3) implement supported FE11/13 API/service/UI gaps from confirmed SRS contracts. Keep all unproven actions fail closed.
