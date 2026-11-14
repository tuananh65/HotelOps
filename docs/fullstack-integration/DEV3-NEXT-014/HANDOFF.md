# DEV3-NEXT-014 — UC022 Deactivate / Reactivate Room candidate

UC022_SOURCE_STATUS: PARTIAL — full-stack candidate ready for source review, not a complete persistence implementation.
BUSINESS_ACTIVATION: BLOCKED. SQL_ACCEPTANCE: NOT_RUN. No COMPLETE_VERIFIED claim.
Branch feature/dev3-uc022-room-lifecycle; base425375e2450a6f78334a302f289f6e64d265451e.

## Requirements and existing implementation reused

UC022 in docs/requirements/srs_sections/02_use_case_specifications.md requires Active->Inactive after open cleaning/rework/inspection/incident/effective-block/asset checks, reason/confirmation and atomic status history/audit. Reactivate requires active floor/type and Pending Cleaning until inspection. GBR039/040/043/044 in05_requirement_appendix.md apply. These domain rules are the scope explicitly requested by the current task; historical permission matrix is not reapproved.

Existing active GET /api/rooms/{id}/edit?branchId reads CreatedRoomDto and quoted64hex SHA256 record/UpdatedAt/audit-revision ETag (RoomBatch2Service.EditRoomAsync). Reused frontend createHotelApi.read('roomEdit'), roomDto and strongTag. Do not use User16hex or Workday base64 rowversion. Existing RoomTypeCreationService.BatchAsync retains session/User-first Branch/catalog locks, MASTER_DATA_MANAGE scope and final session/authority gate in a local Serializable transaction. New partial class reuses this helper; no change to shared writer/order/schema.

Inspected Room, CleaningTask, RoomInspection, RoomBlock, Asset/Incident references and RoomStatusHistory mappings. No new dependency SQL predicate invented. GBR040 requires complete current-cycle semantics and all reference writers, not an entity-count guess.

## Actual implementation

RoomLifecycleContracts: strict unmapped-field rejection, reason/confirmation validation using existing structure convention10-1000/no control chars, pure server-fact domain intent for deactivate/reactivate. Duplicate state and invalid reactivation parents rejected. Plan explicitly requires status history and atomic audit. Domain Inactive/PendingCleaning are NOT SQL operational_status values, not authorization or release clearance.

RoomLifecycleCandidateService: new partial RoomTypeCreationService method; validates scoped IDs,64hex ETag, reason/confirmation, ticket/current actor/session/restriction and cancellation. Immutable source-level gates return Unavailable BEFORE SQL for BOTH operations. No config/test/caller-supplied clear result can enable them. Even if outer gates were changed by future reviewed source, the dormant BatchAsync callback still returns Unavailable; no mutation is implemented with an unapproved predicate/status engine. Direct service calls cannot write.

RoomLifecycleCandidateController: NonController, proposed POST /api/rooms/{id}/deactivate?branchId and /reactivate?branchId; strict reason/confirmation JSON, HTTPS/cookie/CSRF/no-store,16KiB request limit, missing428/invalid400/stale412/denied403/dependency409/unavailable503 mappings. No controller/DI/flag activation or production router edit. No actual200 success path currently exists.

Frontend: standalone session/branch/room-keyed RoomLifecycleCandidate, production graph excludes it; dispatch defaults OFF. Existing active read displays server room/active/status, missing certification warnings, reason->Review->Confirm and applicable operation. Typed client body only reason+confirmation, exact scoped POST/Room If-Match/cookie/CSRF, synchronous exclusion, identity/cancellation checks through body parsing, no queue/retry. Proposed200 DTO uses existing CreatedRoomDto; target/branch/expected active flag/fresh ETag must match, reactivation READY response cannot confirm. Does NOT implement the backend status engine; synthetic DIRTY/OUT_OF_SERVICE fixtures are example legacy payloads, not an approved mapping.401 explicit recovery,412 fresh read/review,409 sanitized dependency/state/parent conflict,503/network/malformed success unknown/no replay. Old branch/session reads and late mutations cannot restore protected data. No mock permissions/storage or raw error disclosure.

## Deliberately missing persistence boundary

Repository has no reusable authoritative Room status engine. SQL source scripts/database/release/01_HotelOps_Full_Schema.sql CK_rooms_operational_status and history constraints allow legacy AVAILABLE/OCCUPIED/DIRTY/CLEANING/INSPECTION/READY/OUT_OF_SERVICE, not SRS INACTIVE. Existing CreateRoom maps Pending Cleaning to DIRTY, but this does not approve a full Inactive/Blocked/current-cycle reactivation mapping. Do not silently use OUT_OF_SERVICE for every condition or edit schema.

Before writing room.is_active or operational_status, DEV3 with FE03/04/05 owners must integrate approved source/status recalculation plus old/new/source-event history and audit in the SAME transaction, validate actual trigger vocabulary/schema and final source rereads. This candidate does not claim that boolean obligation fields or test plans are atomic persistence. No room/history/audit SQL writes exist in this new service. This explicit gap, together with incomplete predicate/writer coverage, is why whole UC remains PARTIAL.

## Executed receipts

- dotnet build HotelOps.slnx:exit0,0warnings/errors.
- dotnet run --project scripts/verification/RoomLifecycleCandidate/RoomLifecycleCandidate.Verification.csproj:29/29 checks PASS exit0. Domain valid/duplicate/parent/confirmation/reason rules, overposted status rejected, actual MVC discovery exclusion, cookie/HTTPS/Room-vs-User ETag preflight, immutable direct-call release fuse/no SQL connection, concurrent opposite operations stay blocked. These do NOT prove one-success SQL concurrency.
- dotnet test HotelOps.slnx --no-build:exit0, no framework cases reported; not counted as acceptance.
- node --test tests/room-lifecycle-api.test.mjs tests/room-lifecycle-browser.test.mjs tests/hotel-api.test.mjs tests/auth-http.test.mjs tests/auth-state.test.mjs:210/210 PASS exit0 (46 new Room,70 Hotel API,94 Auth).
- targeted hotel-router-integration --test-name-pattern="actual entry|branch change|new entry|dashboard|UC023":8/8 PASS exit0.
- npm.cmd run typecheck/build and scoped Prettier:exit0. Build retains two inherited React Router use-client warnings.
- scoped C# whitespace verification:exit0. Allowlist10files, whitespace/secret scan before commit.
- Initial browser run exit1: synthetic fixture incorrectly used16hex User ETag. Corrected fixture to64hex Room ETag; production validator not loosened. Final counts above refer to corrected fixture/source.

Backend harness uses empty SqlConnection that remains closed. Browser external network is blocked and fetch responses synthetic. Real login/HTTPS/SQL mutation/history/audit, status-engine behavior, placement-vs-deactivation, dependency creation/reopening, concurrent same/opposite operations, stale ETag and provider/commit/audit rollback all NOT_RUN without approved trial.

## Dependency decisions / owner exit criteria

- DEV1 FE03/04/05: exact cleaning/rework/inspection current-cycle and effective RoomBlock rules; accepted status engine/SQL vocabulary/history trigger mapping and readiness denominator behavior. Existing IsActive flag is preserved; no readiness policy changed here.
- DEV2 Incident/Asset: accepted current placement/open incident readers and complete create/move/reopen/reassign writer census with retained eligibility/ancestry locks. PR25 remains separate; no source copied or changed.
- DEV3: integrate accepted predicates, final branch/room/parent/version/session/authority rereads, status calculation/history/audit transaction and fault handling. No empty/unresolved result clears release.
- PL + named trial owner: exact disposable server/database/ownership/schema prerequisites/fixtures/scripts/lease; then live concurrency, rollback, atomic audit/history and HTTPS acceptance. Deadlock1205 FAIL.

## UC010 coordination

Issue44 and comments read directly. Posted focused owner deliverables at https://github.com/tuananh65/HotelOps/issues/44#issuecomment-6765619166 : predicate/writer census by DEV1/DEV2, DEV3 final transactional rereads + atomic role/User/session/audit + LastAdmin, and named approved disposable trial. Reuse Task44/45 foundations; no full audit requested. No business approval claimed, no release fuse removed and PR45 not reopened.

## Preservation / next operation

Exactly10NEW files, existing tracked bytes unchanged: no Program.cs/DbContext/entity/schema/migration/permission or DEV1/DEV2 writer edit. FE01/FE02/FE12 production routes/flags preserved. Workday mutations OFF/NonController; UC008 Update version-blocked. No merge/deploy/shared SQL.
Next autonomous business target: UC026 Department Deactivation, after short contract/source check; keep its mutation closed if affected-account/task/request predicates or writer contracts remain unresolved. No duplicate UC022/UC010 helper PR or COMPLETE credit.

## Source SHA256

970A372D1E08BF2DCF69C57EE395AD03262D2DD2B76968EB6B220A25E57A935B  src/HotelOps.Api/Controllers/RoomLifecycleCandidateController.cs
490A5DE05E47722B88FFAC3103769A6F7D56D35779E19FCA9B2CDCE31ED95D7F  src/HotelOps.Api/DTOs/HotelStructure/RoomLifecycleContracts.cs
08AFEFD3A9C488703555E42B6DB872C0C0C8DE2EED4525C4E5888BE6421AEEB7  src/HotelOps.Api/Services/HotelStructure/RoomLifecycleCandidateService.cs
5602EC39BB54002DC7963B94A4EB2B14A1F9BFB20A5A871CC05AE20117E67A79  frontend/src/hotel/roomLifecycleApi.ts
156F34D346B1AACEB0EBCEFA2E44847A989B4192CD3FFF3F5AC9887A866E3C62  frontend/src/hotel/RoomLifecycleCandidate.tsx
D68E7FB312BC28BD6906A6239112CD434EEBCD865FD6A2D59A00062293F127E5  frontend/tests/room-lifecycle-api.test.mjs
E583418231A56CA1514C290320BDF6BE804853E405D85EAED8476E057EBB0B42  frontend/tests/room-lifecycle-browser.test.mjs
B1F84A845087C6A0B337C2616E4FFEB3072FF9BD013436F67759100C23794012  scripts/verification/RoomLifecycleCandidate/Program.cs
720478BD353C02A24462E2FE4F00BD713CC9ABD45A42AE42F904BDF140B13A50  scripts/verification/RoomLifecycleCandidate/RoomLifecycleCandidate.Verification.csproj
