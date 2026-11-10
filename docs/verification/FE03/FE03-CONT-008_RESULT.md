# FE03-CONT-008 RESULT

- CURRENT_DEVELOP_SHA / baseline: 4c748a70fe7e5db2ef363edc55c96d872a20c6f4 (PR41 merge verified ancestor of origin/develop).
- NEW_BRANCH: feature/dev1-fe03-uc027-completion, created from latest origin/develop in the existing clean FE03 worktree. Prior implementation branch unchanged.
- IMPLEMENTATION_COMPLETED: persisted status-history attribution. GET /api/rooms/{roomId}/history adds nullable changedByUserId from room_status_history.changed_by; frontend displays recorded ID or Chưa ghi nhận. No username/profile/role lookup, current-user fallback or inferred SYSTEM actor.
- SOURCE_REUSED: existing ROOM_VIEW/session/branch-room authorization, serializable read transaction, async AsNoTracking/CancellationToken, history/source validation, deterministic ChangedAt/HistoryId ordering and existing board.
- Source references: SRS UC027 View Status History requires old/new status, source event, actor and time; RoomStatusHistory.ChangedBy and HotelOpsDbContext mapping to changed_by/FK_room_status_history_changed_by; RoomCreationService already records ChangedBy=actor for INIT events. Current task explicitly prioritizes this read-side capability. No mutation permission is inferred from historical actor lists.

## Targeted classification

| Capability | Status | Scope |
| --- | --- | --- |
| Existing persisted operational codes and read filters | IMPLEMENTED_REUSABLE | Existing exact stored-code contract; not an approved transition matrix. |
| Branch/room read authorization | IMPLEMENTED_REUSABLE | Existing ROOM_VIEW + same-branch resource checks remain unchanged. |
| History actor/provenance | APPROVED_IMPLEMENTABLE -> implemented | Direct nullable changed_by attribution on authorized room history; source-event verification retained. |
| Allowed transitions / actor-permission matrix / authoritative status writer | BLOCKED_CONTRACT | Change Notes §2 and UC027 review annotation leave Reception/mutation rules pending; no new endpoint/writer. |
| Concurrency/version, audit atomicity and mutation transaction | BLOCKED_CONTRACT | SRS GBR-005/007/043 describe generic expectations; no accepted concrete command/version protocol. Room generated entity has no room rowversion contract. |
| RoomBlock business constraints | BLOCKED_CONTRACT | DEV2 confirmed persistence only; no accepted active-block/release/readiness policy. Existing unreleased-facts display unchanged. |
| Effective Readiness and FE05 projection | BLOCKED_DEPENDENCY / BLOCKED_CONTRACT | No accepted authoritative projection; no derivation from Cleaning/Inspection/Rework. |

## Validation

- dotnet build HotelOps.slnx: PASS, 0 warnings, 0 errors.
- dotnet test HotelOps.slnx: exit 0; solution contains API only, no tests executed by this command.
- Explicit HotelOps.Dev1.RegressionTests filters RoomHistoryAttributionTests, RoomOperationalReadTests, CleaningLifecycleContractTests, CleaningClosureBoundaryTests, CleaningMonitorRouteTests: PASS 66/66, 0 skipped.
- New backend tests: same-room isolation and empty scope; descending timestamp/ID tie-break; recorded/null actor preserved; SQL Server translation reads changed_by without JOIN/users; DTO nullable serialization. SQL test uses ToQueryString only, no connection.
- First focused run: 65 PASS, 1 FAIL because test assumed literal TOP(1); EF parameterizes TOP. Assertion corrected; final 66 PASS. No application error was found in that run.
- Frontend typecheck/build: PASS. Existing two React Router use client bundle warnings retained.
- Frontend unit: PASS 60/60; actor/null accepted, missing/zero/negative/string/unsafe ID rejected.
- Playwright FE03: PASS 14/14, intercepted fixtures; includes actor/null display and existing scope/stale-response regressions. Existing FORCE_COLOR/NO_COLOR warning retained.
- New API routes/methods unchanged. No mutations added. Source review confirms actor projection only occurs after existing room authorization; tests verify projection same-room filtering and no user-profile join.
- SQL/HTTPS live for this new application version: NOT_RUN. Prior PR41 live evidence applies only to application SHA6a0c924. No runtime replacement, SQL business write or trial request performed.
- Backend DTO field is additive. New frontend requires explicit changedByUserId number/null and rejects missing/malformed data; deploy it with the corresponding backend version. Missing persisted attribution is shown as unavailable, never inferred.

## Required Project Leader decisions (no requests sent to other developers)

| Decision and existing reference | Alternatives | Recommendation / rationale | Impact while pending |
| --- | --- | --- | --- |
| Authoritative status command owner; SRS UC027 POST01/GBR043 says system-derived, Change Notes §2 leaves Reception lifecycle duties pending | Keep status changes inside source workflows; or explicitly approve a separate Reception command with bounded transitions | Confirm one authoritative source-workflow writer and list any Reception exception commands explicitly, avoiding competing writers | No operational-status mutation endpoint |
| Final transition/permission matrix; Change Notes §2/§4 and UC027 review annotation | Explicit per-command role/permission/branch conditions; or no additional mutation grants | Approve a complete from/to/source/actor/scope table rather than granting all transitions to a role; existing ROOM_VIEW stays unchanged | Cannot implement or test allowed/denied mutation cases |
| Concurrency/version and atomic receipt; GBR005/007/043, Room.cs, RoomStatusHistory.cs | Adopt the accepted writer's version/idempotency contract; or approve a new room-specific version/schema contract separately | Reuse the authoritative writer's protocol and atomically persist source state/history/actor/audit; confirm conflict/retry behavior before code | No mutation concurrency/audit/transaction foundation implemented |
| RoomBlock lifecycle semantics; DEV2 PERSISTENCE_PRESENT/BLOCKED_CONTRACT, RoomBlock.cs | Define scheduling/release semantics and source of truth; or retain recorded unreleased facts only | Retain current facts-only UI until active predicate, release policy and transition constraints are explicitly accepted | No effective-block enforcement/readiness mapping |
| Authoritative readiness projection; TurnoverCycleReadService coverage and prior FE05 dependency review | Accept a versioned projection with precedence/freshness/source identity; or keep readiness unavailable | Integrate one accepted projection instead of deriving from Cleaning/Inspection labels | Readiness/related evidence and next actions remain incomplete |

## Files changed (10)

- src/HotelOps.Api/DTOs/Rooms/RoomDirectoryContracts.cs
- src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs
- src/HotelOps.Api/Services/Rooms/RoomOperationalReadQueries.cs
- tests/HotelOps.Dev1.RegressionTests/RoomHistoryAttributionTests.cs (new)
- frontend/src/api/dev1/roomBoard.ts
- frontend/src/screens/rooms/RoomStatusBoardScreen.tsx
- frontend/tests/room-status-fixtures.mjs
- frontend/tests/room-status.test.mjs
- frontend/tests/room-status.spec.ts
- docs/verification/FE03/FE03-CONT-008_RESULT.md (new)

MUTATION_STATUS / READINESS_STATUS: BLOCKED_CONTRACT. No shared Program.cs, Authentication, EF entities/mappings, schema/grants or other worktrees changed. No push/PR/merge.
TASK_COMPLETION_PERCENT / UC027_VERIFIED_PERCENT: CHƯA ĐỦ DỮ LIỆU. UC027 NOT ACCEPTED.
NEXT_CODING_TASK: verify history attribution against an authorized runtime carrying this new backend/frontend version, then address approved downstream actions/projection contracts. No claim that the new source is live-verified.
