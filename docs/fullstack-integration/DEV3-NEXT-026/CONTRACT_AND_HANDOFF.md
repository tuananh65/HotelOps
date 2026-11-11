# UC138 V1 — secure preview plus new-plan Save/Assign source

Authority: DEV3-NEXT-026 + Issue56 comment6775703271 + supplemental full-UC directive.
Baseline8fd30f2f12e9dae102de2826e0be20e99e6a14ad. No SQL/HTTPS trial, deployment or activation.

## Permission mapping and narrow resource contract

scripts/database/release/03_HotelOps_RBAC_Catalog_Seed.sql:SHIFT_MANAGE is canonical; DEPT_MANAGER has BRANCH.
This is verified source mapping, not proof of a live database grant. Nothing is seeded or granted by this PR.
Use real PermissionService/EfPermissionDataSource and a local per-resource resolver, not role labels or SHIFT_VIEW.
Only effective SHIFT_MANAGE ALL/BRANCH satisfying real assignment/role expiry, active branch/access and actor scope
can pass. OWN never proves roster management: resolver returns HasOwnership=false, including actor's own roster.
Unbound ALL does not bypass resource membership. No modification of shared evaluator/DI/resource resolvers.

Narrow V1 actor: unrestricted active employed SQL-session user whose PrimaryBranchId and DepartmentId match the
ONE request scope, with active Branch, Department and current unrevoked Branch membership. Every employee must
independently meet the same active primary-Branch/Department/membership predicate. Every template is active and
belongs to Branch; schema has no template Department field, so no invented relationship. This is a restrictive V1
subset, not a redefinition of Department Head's broader branch-wide mandate or multi-department GBR031 support.
Mixed unauthorized/missing resources ->403 ENTIRE request, no partial rows or resource existence distinction.
No protected planning reads until all requested resources pass. Reauthorize session and every resource before response.
Cross-branch/department overlap facts are generic DataUnavailable with null times, not names/assignment IDs.
No raw SQL exception, ProblemDetails detail, credential or CSRF token enters public result.

## HTTP and DTO

POST /api/branches/{branchId:long}/departments/{departmentId:long}/shift-plans/preview
POST /api/branches/{branchId:long}/departments/{departmentId:long}/shift-plans  (Save)
Strict body:{drafts:[{employeeId:positive long,templateId:positive long,shiftDate:ISO DateOnly}]}.
Unknown nested/root fields rejected.1..500 drafts;96KiB body cap. Frontend requires positive JS-safe IDs and a real
calendar date. Branch/Department are selectors only, authorized by server. Cookie, HTTPS, CSRF, no-store.
Preview200: existing ShiftPlanPreview rows(index,startUtc,endUtc,issues numeric0..9),requiresServerRevalidation=true.
400 invalid input;401 invalid session;403 admission denied;413 body size;503 unavailable. No ETag/version claim.
Preview reads are non-atomic and have30second SQL budget; cancellation does not manufacture an HTTP status in client.
Core tests execute production PermissionService+resolver, not a mock granted permission result.
Controller remains NonController; wrapper not production mounted; no Program.cs or DI changes.

## Save V1 — actual SQL insert path with immutable release fuse

Save creates FUTURE plans only, not updates. Exact template/date group inside authorized Branch/Department must
not already exist. Group drafts into one WorkShift per(template,date); RequiredStaffCount=group employee count.
Existing model defaults supply documented PLANNED WorkShift and ASSIGNED ShiftAssignment vocabulary; no new states.
Recompute trusted times, employee eligibility, existing global overlaps and proposed overlaps before insertion.
Preview is never a write token. New-plan insert-only has no fabricated aggregate ETag or automatic overwrite/replay.
Status201:{createdShifts,createdAssignments,notificationCount}; assignment and notification counts equal draft count.
409 SHIFT_PLAN_CONFLICT;401/403/400 as admission;503 acceptance/unavailability. Unexpected failures500 sanitized.
Frontend accepts409 as definite rejection only with verified body status/title(and matching code if present).
Ambiguous mutation transport/5xx/malformed201 is OUTCOME_UNKNOWN, no retry or queue, flow permanently locks replay.

Own Serializable SQL transaction: global admin serialization FIRST -> retained sorted actor+employee User locks ->
Branch -> Department -> sorted template locks -> retained Branch/Department work-shift range -> validated scope reads.
Create Shift/Assignment entities with generated IDs; audit CREATE shift+employee membership; enqueue notices through
IShiftPlanNotificationParticipant in the SAME context/transaction. Stage must never send external notifications or commit.
Require exact staged recipient count/transaction ID, flush, clear tracking, final session/resource reauthorization,
final GLOBAL employee-overlap reread excluding only this transaction's inserted assignment IDs, then commit.
Any failed/exception path disposes uncommitted transaction. No old assignments/attendance/workdays rewritten.
Actual SQL range coverage/query plan, external writers, notification transaction integrity and rollback NOT verified offline.

PendingReleaseGates is immutable and enforced BEFORE SQL even for direct calls:
- ROSTER_USER_SCOPE_WRITER_COORDINATION
- NOTIFICATION_TRANSACTION_PARTICIPANT_ACCEPTANCE
- SQL_OVERLAP_RACE_AUDIT_ROLLBACK_HTTPS
No boolean configuration/empty reader/preview receipt removes them. No live successful writer is enabled.

## Notification boundary — implementation still missing

The existing model has Notification/NotificationRecipient but repository has no reviewed durable scheduling producer.
This PR implements the consumer contract/call site and exact recipient notices, not a producer or delivery service.
Notification owner must provide an accepted IShiftPlanNotificationParticipant that stages the approved event type,
recipient, payload/action URL/dedup/delivery policy atomically using the supplied context/transaction. No direct writes
into DEV2/FE14-owned notification tables and no invented event type. Absent provider fails before mutation.
This is NOT merely awaiting SQL: the concrete adapter is still missing implementation and cross-owner review.

## Frontend

Reuse ShiftPlanPreviewPanel with typed scoped HTTP adapter and SecureShiftPlanWorkspace, not duplicate screen.
Snapshot bounded drafts before CSRF await; no route/body authority inference, storage, bearer token or raw error output.
Explicit Preview -> Review Save -> Confirm Save, synchronous client exclusion, immutable Save snapshot, default Save OFF.
Changing a draft during preview aborts/invalidates old response (existing behavior preserved); edits lock during Save.
401 clears protected preview and draft snapshot, blocks new preview until explicit session revalidation/remount.
Late identity/unmount cannot publish results. Save ambiguity clears preview/controls and provides no replay button.
Workspace is not imported into production routes; FE01/FE02/Workday flags unchanged.

## Verification receipts

- dotnet build HotelOps.slnx:exit0,0warnings/errors.
- SecureShiftPreview verification:58/58 exit0; production auth/privacy/session/bounds, immutable Save pre-SQL gates,
  five OFFLINE EF SQL-translation assertions. No positive SQL Save/transaction or notification producer test claimed.
- ShiftPlanPreview verification:57 PASS exit0, existing fake-reader/offline EF cases.
- ShiftSchedulePlanning verification:24 PASS exit0, existing planner/conflict/cancellation cases.
- dotnet test HotelOps.slnx:exit0,restore-only output; no discovered test count claimed.
- frontend combined new API/browser +existing Preview/scheduled Workday/read/Auth HTTP-state:330/330 exit0.
- typecheck/build exit0; two inherited ReactRouter use-client warnings.
- Scoped formatting, staged allowlist/whitespace and secret scan are publication gates.
Initial combined330 had one regression: pending preview input was incorrectly disabled. Fixed source, assertions retained.
Next run had a fixture race resolving Save before actual dispatch; added wait for actual POST, no assertion relaxation.
Final330 all passed. No full historical Auth/RBAC/Task44/45 rerun. No live/shared SQL or HTTP requests.

## Completion split, remaining whole-UC work

CODED: secure Preview authorization/API/SQL scope reader/typed FE adapter; Review-Confirm; insert-only Save/Assign SQL
orchestration, initial/final conflict validation, audit writes and notification participation boundary.
WAITING SQL/HTTPS ACCEPTANCE: real grant/data admission, privacy/non-atomic reads, SQL timezone/overlap translation,
range locks and exactly-one-winner races, stale scope/session replacement, audit/staging faults+rollback and receipt integrity.
STILL MISSING IMPLEMENTATION: notification producer/delivery adapter; coordinated participation of every roster,
employment/department/assignment/cancel/reopen/external writer; broader existing-plan update/versioning, eligible future
assignment removal and template administration needed by whole UC138. No model migrations or fake ETags substituted.
Owners:DEV3 roster orchestration; DEV1/DEV2 affected resource writers/notification owner; runtime owner disposable
schema/fixtures/TLS/GUID/exclusive lease; PL independent source/release review. Primary HotelOpsDB is not a trial.
UC138=PARTIAL/IMPLEMENTED_SOURCE_ONLY, COMPLETE_VERIFIED=NO, credit delta0. No source-only merge/deploy implied.
NEXT: integrate accepted notification participant and writer matrix, then guarded disposable acceptance; continue remaining
UC138 lifecycle/version operations only with durable storage/version contract, not another isolated helper PR.