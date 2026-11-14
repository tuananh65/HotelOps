# FASTTRACK-142A — checkpoint 02, SQL/HTTPS integration

Baseline `588cba574d0db4a18ff3efbc210bb6a73c82edfa`, clean canonical integration branch. Sprint remains active.

## Implemented and executed

- Fixed real UC017 deployment defect: explicit required SET options before filtered-index creation. The prior sqlcmd execution failed with error 1934/QUOTED_IDENTIFIER; the entire UC017 transaction rolled back (branch/minutes columns and markers absent). Fixed source installed successfully, and existing `06_UC017_Verify.sql` passed against the trial.
- Added exact-target prerequisite runner and real HTTPS auth/schema probe modes to existing ModelProbe project. No application authorization, generated entities, mappings, credentials, release fuses or permission semantics changed.
- Applied existing user-rowversion and RBAC catalog contracts only to the owned GUID-bound sprint database. Added the four DEV2 patches, UC017, checklist, housekeeping and progress schema packages. Existing deployment target checks were rebound to exact sprint ownership; schema/business checks preserved. Each package used an outer transaction, with failure evidence retained.
- Checklist/housekeeping/progress module guards now pass; all 21 installed trigger definitions match repository source after line-ending normalization. Real zero-row EF SQL projections: **87 PASS / 6 FAIL** out of 93. This is NOT full schema acceptance.

## Direct evidence and commands

| Command | Result | Evidence |
|---|---|---|
| `dotnet run --project src/HotelOps.Api --no-build --no-launch-profile --urls https://localhost:7142` with process-local isolated connection | HTTPS listener started; normal trusted localhost certificate | Direct tool output; no shared configuration/User Secrets changed |
| `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe -p:BuildProjectReferences=false -- --sprint-auth <report>` | Final 11 checks PASS; synthetic cleanup PASS | `https-auth-verified.json`, `https-auth-after-schema.json` |
| Same auth mode, first execution | HTTP/SQL assertions passed but cleanup FAILED | `https-auth-first.json`; preserved |
| `sqlcmd ... -i first-auth-cleanup.sql` | Exactly two owned subject audit rows removed; zero audit rows remain | `first-auth-cleanup.log.txt` |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verification/Install-Dev3SprintPrerequisites.ps1 -EvidenceDirectory <output> [-StartAt 4/6]` | Final prerequisites installed; original failures retained | `schema-prerequisites.json`, `prerequisites-retry/`, `prerequisites-final/` with source/derived hashes |
| ModelProbe `--sprint-schema <report>` | Initial 62/93 projections PASS; final 87/93 PASS, 6 FAIL; 21 trigger comparisons PASS | `schema-projections-initial.json`, `schema-projections-verified.json` |
| `sqlcmd -S localhost -E -C -b -d HotelOps_DEV3_142A_20261114_cc78c13f -i scripts/database/release/uc017/06_UC017_Verify.sql` | exit 0, read-only postflight PASS | `uc017-postflight.log.txt` (successful script emits no rows) |
| `dotnet build HotelOps.slnx` | Final PASS, 0 warnings/errors | `checkpoint02-build-final.log.txt` |
| `dotnet test HotelOps.slnx` | exit 0, no test discovery | `checkpoint02-solution-test.log.txt`; not regression proof |

Auth trial exercises real anonymous session 401, users 401, missing login CSRF 400, real login 200, persisted SQL session, authenticated session 200, users 403 with no grants, missing logout CSRF 400, valid logout 204, session 401 afterward, persisted revocation and audit. Random password/hash/cookies/CSRF remain in memory and never enter reports. No browser workflow or privileged user-directory success is claimed. No full UC is COMPLETE_VERIFIED.

First auth cleanup matched actor only, while existing AuthenticationAudit records session creation/logout by subject with null actor. Corrected harness cleans only its synthetic user subject/actor rows. Two leftover records (IDs 1/3, subject users/1) were explicitly verified and removed with exact database/run/GUID guards. Final reruns confirm users, sessions and audits empty. Catalog/schema persist intentionally for the next trial.

First prerequisite runner omitted the checklist runner's postflight/hash marker. Housekeeping correctly refused to install. Checklist postflight verified actual schema before publishing its exact source hash, then housekeeping/progress succeeded. Final exact trigger comparisons prevent treating object counts alone as compatibility proof. This was a runner defect, not a reason to bypass module guards.

Initial required build failed because our live API process 34452 locked apphost.exe. Verified executable path and loopback listener ownership before stopping that process; final build passed. `checkpoint02-build.log.txt` retains the failure. API is stopped at this checkpoint; no background deployment remains.

Runtime warnings retained: three existing EF RoomTypeBranchV1 index-mapping warnings and an unordered Take query warning. No test claims suppress these warnings. SQL probe connections use encrypted local SQL with trusted-server-certificate setting; HTTPS uses default certificate validation. No claim of SQL server-certificate-chain acceptance.

## Remaining work

Six failed model projections: Workday, WorkInterval, TimeCorrectionRequest, NotificationIntentCandidate, NotificationOperationCandidate, NotificationPublicationCandidate. Inspect existing WorkTime and notification candidate schema contracts before installation. FE05 additionally still needs current-cycle/evidence/binding/result-storage and atomic conclusion participants; housekeeping schema alone does not solve these dependencies.

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=02_ACTIVE`; `FE05_STATUS=PARTIAL/OFFLINE_PASS`; `UC006_UC010_STATUS=RUNTIME_COORDINATION_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=CANDIDATE_PENDING`; `FE15_STATUS=CANDIDATE_PENDING`.

`ISOLATED_SQL_STATUS=AUTH_PERSISTENCE_AND_CLEANUP_PASS; MODEL_GAPS_6`; `HTTPS_API_STATUS=AUTH_TRIAL_PASS; STOPPED_AFTER_TEST`; `E2E_VERIFIED_WORKFLOWS=SYNTHETIC_API_LOGIN_SESSION_LOGOUT_ONLY`; `UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0` (same conservative whole-UC classification as checkpoint 01).

Next: finish WorkTime/notification/cycle schema compatibility on this target, integrate FE05 conclusion against real prerequisites, then prove UC006/010 writer coordination and implement remaining FE11/13/14/15 work. No shared HotelOpsDB or HotelOps_ImportReview mutation occurred.
