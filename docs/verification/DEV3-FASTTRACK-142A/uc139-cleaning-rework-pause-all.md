# UC139 Cleaning / Rework Pause All source

Status: PARTIAL_IMPLEMENTATION / NOT_TESTED for the complete UC. No new COMPLETE_VERIFIED UC is claimed.

## Approved contract and implementation

The Project Leader approved Rework ON_HOLD in the existing workflow_status column while preserving physical status IN_PROGRESS. Cleaning retains its existing ON_HOLD workflow state. Neither adapter completes, cancels or reassigns work. END_OF_SHIFT is recorded by the server; Pause All does not ask for a fabricated blocker reason or note.

WorkdayService now dispatches TASK, CLEANING and REWORK inside its existing serializable, current-session-authorized transaction with user/time locks and workday ETag. The owning service validates its source and current executor, closes the real open interval, writes the canonical held state with row-version comparison, appends a reason receipt and audit, and rechecks authority. Workday End remains in that same transaction and cannot commit with an open interval. Failure in any step rolls back all writes. Unsupported sources remain fail-closed.

Cleaning validates current room/cycle/assignment and refuses unresolved, linked or unproven blockers. The existing Resume endpoint recognizes only a receipt matching the held task revision and closed interval. Manual blocker Resume retains its existing confirmation/note and blocker checks. The original ActualStartAt is preserved; resumed execution opens a new interval.

Rework validates current HK_STAFF eligibility, CLEANING_EXECUTE and the existing trusted FE05 authority reader, which independently requires CLEANING_VIEW and verifies committed producer, audit, source/evidence digests and current failed inspection/cycle. Duplicate open reworks and inspector-as-executor are denied. Resume reuses POST /branches/{branch}/reworks/{id}/start and its existing CSRF, strong ETag, operation key, audit replay, current assignee, shift/workday and authority checks. Only an END_OF_SHIFT receipt bound to the current ON_HOLD revision permits this transition. Its response remains the existing Start/IN_PROGRESS receipt contract; audit additionally identifies resumedFrom=END_OF_SHIFT. The UI displays Resume rework only for the server capability RESUME_REWORK. Legacy provisional execution does not gain a new transition.

Effective room readiness treats held/accepted Rework as outstanding. No room is made usable by pausing work. Generated EF entities and mappings are unchanged.

## SQL installation and rollback

- scripts/verification/CanonicalDatabase/CleaningEndOfShift.sql adds an immutable Cleaning hold receipt.
- scripts/verification/CanonicalDatabase/ReworkEndOfShift.sql adds an immutable Rework hold receipt and extends the existing trusted CHECK with only ON_HOLD + IN_PROGRESS. It preserves the prior CHECK definition, including any independently installed ACCEPTED/FAILED states.
- Both require exact server/database/nonzero GUID and disposable database naming, use a transactional migration lock, and support ApplyChanges=0 rollback. They create no users, grants or role assignments.
- Rework runtime additionally requires the existing committed FE05 authority and Assigned Rework Execution installations. These scripts do not install or impersonate those prerequisites.
- No persistent migration was applied in this increment. No shared/production database was modified. Post-install removal after real receipts exist is not authorized by these scripts; immutable history must be preserved.

## Direct evidence, 2026-11-16

- dotnet build HotelOps.slnx --no-restore: PASS, zero warnings/errors.
- npm run build: TypeScript and Vite PASS. Existing React Router directive warnings and >500 kB bundle warning remain.
- Focused regression classes ReworkLifecycleTests, WorkdayControllerAdmissionTests and CleaningClosureBoundaryTests: 80 PASS. These do not prove the new SQL lifecycle end-to-end.
- Rework transport: 9 PASS, including ON_HOLD/Resume capability validation. Cleaning transport in the preceding uncommitted increment: 15 PASS.
- Browser with mocked API: 4 PASS (Cleaning/Rework Pause All review and unknown-source denial for scheduled/unscheduled workdays). This is not HTTPS/SQL evidence.
- dotnet test HotelOps.slnx --no-restore: exit 0 without discovered tests; not counted as coverage.
- Read-only disposable identity verified, followed by two Rework migration installations in an outer rollback transaction: PASS. Two trusted FKs and enabled immutable trigger verified. A temporary table using the installed CHECK accepted ON_HOLD/IN_PROGRESS and rejected ON_HOLD/COMPLETED. Rollback restored the exact original CHECK and removed receipt store/markers. Local transcript script: artifacts/uc139-rework-end-of-shift/migration-rollback.sql (ignored artifact).
- Cleaning migration previously ran twice with outer rollback, trusted FK/trigger metadata and removal checks: PASS. These DDL checks do not prove receipt DML immutability or application transaction behavior.

## Rework Resume UI correction — 2026-11-16

The existing backend exposes ON_HOLD with RESUME_REWORK only for a revision-bound end-of-shift receipt. The canonical AssignedReworkRead screen previously rejected both values. It now displays the held task and offers Resume only for that exact capability/state pair, using the existing Start endpoint, current CSRF, ETag, idempotency key and session-bound request. READ_ONLY holds cannot Resume; submission remains limited to IN_PROGRESS. No generated entities or database were changed by this correction.

Direct validation: frontend production build (including TypeScript) PASS; assigned-rework-route browser suite 6/6 PASS, including held read-only denial and authorized Resume through the Start route. Browser HTTP responses are mocked, not live SQL/HTTPS evidence. Existing Vite React Router directive and large-chunk warnings remain. UC139 is not promoted to COMPLETE_VERIFIED by this change.

## Remaining UC139 acceptance

Follow-up direct SQL evidence: `ModelProbe --rework-workday-boundary` executed the production EF `ReworkWorkdayBoundary.IsStoppedAsync` method on the identity-guarded disposable database. Synthetic Rework without an interval was rejected for IN_PROGRESS and NULL workflow, allowed for ON_HOLD and PENDING_REINSPECTION, and did not block an unrelated actor. The ON_HOLD installer and all fixture rows ran within an owning serializable rollback transaction; post-rollback synthetic user/branch absence was verified. This is query-level evidence, not full Workday HTTP authorization/rollback or concurrency acceptance. ModelProbe build emitted three existing EF1002 warnings in SprintCredentialScenario.cs, outside this increment.

Current-source correction: Maintenance now participates through MaintenanceEndOfShift and the MAINTENANCE interval bridge; the historical paragraph below describes the earlier implementation. Live full-workflow acceptance is still pending.

End Workday additionally rejects physically IN_PROGRESS Rework when its canonical workflow is running, missing or unknown, even if a legacy source has no open interval. ON_HOLD and PENDING_REINSPECTION remain valid stopped workflow states; no physical status is changed. The check runs under the existing serializable workday transaction and user/time locks, across branches. A missing workflow column fails closed. This correction does not fabricate or close an interval for legacy data.

Validation of this boundary increment: backend solution build PASS (zero warnings/errors); 39 existing ReworkLifecycleTests/WorkdayControllerAdmissionTests PASS. Solution test command exited zero without discovering tests. These tests do not exercise the new SQL query; live SQL/HTTPS rollback and legacy-data reconciliation remain pending. No database mutation was performed.

Actual UI -> HTTPS -> SQL Hold/Resume, stale versions, revoked permissions, concurrent ownership/authority changes, receipt retry and rollback behavior require runtime QA. Incident/Maintenance does not yet participate in the work_intervals bridge (the participant supports CLEANING/REWORK/TASK); its existing Hold/Resume only changes maintenance state and work logs. End now explicitly denies while the employee owns IN_PROGRESS Maintenance, even when there is no work interval, with PAUSE_ALL_MAINTENANCE_BRIDGE_REQUIRED. This serializable read prevents an empty interval list being mistaken for stopped Maintenance. The remaining Incident bridge and full UC139 runtime acceptance are the next implementation/verification work.

## Task/Cleaning legacy execution boundary (2026-11-16)

End Workday now also inspects currently active, unended Task/Cleaning assignments across all branches after Pause All and before End. A physical IN_PROGRESS source is not assumed stopped because its interval is absent. Task requires its existing task_progress_versions state PAUSED or PENDING_VERIFICATION with no physical completion timestamp. Cleaning requires ON_HOLD with no actual end, or PENDING_INSPECTION/REWORK_REQUIRED with an actual end from completed cleaning. Rework execution remains independently checked, so a rejected cleaning cycle does not make its original cleaner permanently running or bypass an active rework.

Missing workflow storage, missing/null/unknown workflow state or inconsistent physical completion remains fail-closed with PAUSE_ALL_TASK_RECONCILIATION_REQUIRED or PAUSE_ALL_CLEANING_RECONCILIATION_REQUIRED. No synthetic interval, inferred duration, automatic task completion or database repair is performed. The checks share the existing Serializable transaction and employee/time locks. A rejection after a Pause All participant causes the owning transaction to roll back instead of committing a partially ended workday.

Changed source: AssignedWorkdayBoundary.cs (new), WorkdayService.cs. Generated entities, schema, frontend and grants unchanged. Backend solution build PASS (0 warnings/errors); 80 existing focused WorkdayControllerAdmission/ReworkLifecycle/CleaningClosureBoundary tests PASS. Solution test exits zero without discovery. These existing tests do not execute the new SQL predicates; actual predicate translation/execution, legacy fixtures and whole-workflow rollback/concurrency are NOT_RUN and remain runtime QA work. No database mutation. UC139 stays PARTIAL_IMPLEMENTATION; source-complete counters unchanged.
