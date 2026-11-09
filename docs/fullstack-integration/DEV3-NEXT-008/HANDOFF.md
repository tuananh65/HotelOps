# DEV3-NEXT-008 — UC138 new-assignment plan preview

Status: IMPLEMENTED_SOURCE_ONLY (internal preview, not a public management API).
Branch: feature/dev3-uc138-plan-preview.
Stacked base: feature/dev3-uc138-shift-planning at 9d601f4f4eea3c21307659e79030de67e7ecd1dd, incorporating integration ec945e9070855baf2cebf3ecddd8f2229020d33a. PR37 supplies the planner. PR targets integration/fullstack-base. Until PR37 merges, the overall diff includes its five files plus this six-file delta; review the new delta against the parent SHA. Merge PR37 first so the integration diff automatically narrows; do not merge this preview into the parent branch as an integration shortcut.

## Implemented business slice

Batch diagnostic preview for new assignment drafts: employee, branch, department, template and roster date. Snapshot caller-owned input before awaiting reads. Technical batch bound 500. Invalid references never query. Read branch/department active-state and timezone facts, template identity/branch/active state, employee account/employment/deletion and current branch-access facts. Current User.DepartmentId is treated as a fact: mismatch yields EmployeeDepartmentUnconfirmed, not an invented multi-department authorization denial.

SQL reader uses AsNoTracking and CancellationToken, reads timezone from the existing Branch.Timezone column and UTC time from SQL. Missing/invalid timezone has no fallback. Reuse ShiftSchedulePlanner and the existing global employee ShiftAssignmentConflictReader; only CANCELLED filtering already defined by that reader is used. New drafts do not exclude an existing assignment or pretend to implement update/cancellation semantics.

Detect overlap against existing assignments and across all proposed rows for the same employee, including different branches. Adjacent intervals remain allowed by existing half-open range math. Missing/stale/unresolved data is a diagnostic issue, never a clear result. Output has only row index, UTC interval and sanitized issue enum; no employee identities/credentials/raw provider exception. RequiresServerRevalidation is always true, including when no issues are found. There is no CanSave, grant, ETag, version or mutation result.

The reader/service are trusted internal primitives. A future caller must authorize every resource before invocation. SQL reads are deliberately non-atomic previews, not a concurrency/authorization boundary. A future writer must re-read facts and overlaps inside the approved lock/audit/outbox transaction. An injected fake/clear reader can only create a diagnostic preview; it cannot approve persistence or release.

## Schema/source evidence

Entities/Branch.cs (Timezone, IsActive), Department.cs (BranchId, IsActive), ShiftTemplate.cs (wall times, active/branch), User.cs (status/deletion/current DepartmentId), UserBranchAccess.cs (grant/revoke), corresponding HotelOpsDbContext mappings; existing SqlWorkTimeTransactionParticipant.EligibleAsync provides the active employee/current branch-access fact convention. No entity/schema/mapping modified. UC138 SRS steps 2-4 and GBR141 motivate preview; no Department Head role-name or new scope policy added.

## Executed receipts

- dotnet build HotelOps.slnx: exit 0, zero warnings/errors.
- dotnet run --project scripts/verification/ShiftPlanPreview/ShiftPlanPreview.Verification.csproj: 42/42 checks PASS, exit 0, fake reader only.
- dotnet run --project scripts/verification/ShiftSchedulePlanning/ShiftSchedulePlanning.Verification.csproj: 24/24 checks PASS, exit 0.
- Scoped dotnet format whitespace for the three new backend files and verification Program.cs, --verify-no-changes --no-restore: exit 0.
- dotnet test HotelOps.slnx --no-build: exit 0 without discovered test cases; not counted as acceptance.
- Initial build exposed incorrect generated User navigation name; corrected to correlated db.UserBranchAccesses query without changing generated entities. Final build clean.
- Staged allowlist, whitespace and secret-pattern scan checked before publication.

Cases cover clear preview still requiring revalidation, invalid refs, missing scope/timezone/template/employee, inactive/wrong-branch template, absent access, unconfirmed department, invalid interval, existing/proposed/cross-branch overlap, adjacent/different employees, unavailable facts without raw errors, bounded/empty batch, cancellation and caller collection replacement.

## Not implemented / remaining gates

SQL adapter compiles but was NOT executed against SQL; query translation, live eligibility and transaction consistency are NOT verified. No HTTP endpoint, controller, DI registration or frontend is introduced: public preview admission/resource authorization and DTO/query contract must be approved before exposing employee facts. No authorization test is claimed from fake readers. Roster/template persistence, assignment cancellation, versioning, atomic audit/notification and SQL race/rollback acceptance remain pending. No timezone/version/auth/notification policy invented. Preview is not UC138 completion.

No shared SQL, migration, deployment, shared authorization or DEV1/DEV2 source changed. WorkdaysController NonController, candidate unmounted, mutation dispatch OFF. UC008 Update remains blocked. Source hashes below apply only to this delta; parent planner files preserved.

## SHA-256
src/HotelOps.Api/DTOs/WorkTime/ShiftPlanPreview.cs: 522F210D91FDC1744C0F2FC49DC41EC6584A8786A28A951ABC5C777BEBCD3ED4
src/HotelOps.Api/Services/WorkTime/ShiftPlanPreviewService.cs: 853368396CC4BED4830EACE9A1B757BAE4E8844ACB090F4358C631D75F7ECDD4
src/HotelOps.Api/Services/WorkTime/SqlShiftPlanPreviewReader.cs: CAA217F8DFCE15B44885D56B381ACD0303437F6AC803807A624A547EF1E09A5D
scripts/verification/ShiftPlanPreview/Program.cs: E1A4653B5835B93282A5BCC0320B6CCF034F720C290521C602D6DEAF722D7F52
scripts/verification/ShiftPlanPreview/ShiftPlanPreview.Verification.csproj: D5DB2CEC2BFB132082C19FC51669858F454CCB2AFD6D9FE87A6790D8AA96E8F0

## DEV3-NEXT-009 synchronized receipts and decisions

Merged integration 4ee4e0d3e5afd8e3a6aaaa888128bc24c3eab4e3 without conflict. PR37 is merged: integration diff is now exactly the six preview files, not eleven. Backend build exit 0 / zero warnings/errors. Preview verification 52/52 PASS (including timeout fail-closed and eight actual EF query translation checks), planner 24/24 PASS. Scoped formatting verifies exit 0. Production read paths now delegate to private query builders so offline ToQueryString checks exercise actual scope/template/employee/clock expressions instead of test copies. No query filtering or authorization policy changed.

Translation uses a parameterless SqlConnection with empty connection string, guarded connection-opening interceptor, and confirms Closed state/no tracked entities. Generated SQL maps departments/branches/timezone, shift_templates/wall times, users and EXISTS current branch access; clock remains SYSUTCDATETIME. No connection opened or query executed. Physical schema/data, provider command timeout behavior and database cancellation are not verified. SQL_ADAPTER_ACCEPTANCE: NOT_RUN. Fake timeout is sanitized DataUnavailable; cancellation propagates. No automatic retry added.

Decision register (all PENDING, not implied approval):
- Preview/management authority: Project Leader + Security, DEV3 implementer. Approve admission, branch/global/department resource checks and employee visibility before public exposure. Internal reader is not authorization.
- HTTP contract: Project Leader/TL + DEV3. Freeze request/response/error/query limits and safe exposure of diagnostics before controller/UI.
- Version/concurrency: TL/Security + DEV3. Approve roster/template aggregate version acquisition, shared writer lock order and final authority; no synthetic ETag/User ETag reuse.
- Audit/notification: DEV3 + notification-module owner, approved by TL. Define atomic audit/outbox/enqueue and failure/rollback contract; no direct notification send introduced.
- SQL trial: Project Leader + named trial owner (not yet designated here). Approve exact disposable server/database, ownership/prerequisites/scripts and lease before any live execution; shared database remains prohibited.

Next operation moved to UC016 read-only dependency-review UI, using the existing active GET contract; no UC138 public API/writer is opened to bypass these decisions.