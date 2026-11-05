# Task77 — One bounded approval request (A1)

**UPDATE: User đã APPROVED A1 WITH STRICT SCOPE trong lượt continuation03/11/2026. Không xin duyệt lại các thay đổi này. Gói đã được áp dụng và kiểm chứng trên disposable trials; current status/evidence ở [Task77 report](TASK77_WORKDAY_FOUNDATION.md) và TASK77_A1_VALIDATION.json. Nội dung bên dưới là proposal gốc được user duyệt, giữ để trace scope; “not approved” là trạng thái lịch sử trước quyết định.**

**Not approved and not applied.** The current task authorizes proposals and independent DEV1 code, not shared schema/DbContext/DI changes. AGENTS.md, “Codex Task Scope”: “If a task requires modifications to shared components, explain the impact and request approval before making those changes.”

## Exact requested scope

Approve **Task77 A1 schema/model/transaction-participant foundation on newly created disposable SQL trials only**, with these files and conditions:

| Candidate | Exact target / change | Ownership and impact |
|---|---|---|
| [01_WorkTime.forward.sql](proposal/01_WorkTime.forward.sql) | Execute only after approval + guarded fresh trial identity; create dbo.workdays, dbo.work_intervals, dbo.time_correction_requests, keys/FKs/CHECKs/indexes and one package marker | Shared DB contract;3 new tables, no change to82 existing tables, seed/catalog/grants/ShiftAttendance |
| [02_WorkTime.rollback.sql](proposal/02_WorkTime.rollback.sql) | Empty-owned-trial rollback only; refuses recorded history | Proposal for release/rollback acceptance; no auto cleanup/drop of any existing DB |
| [WorkTime.shared.patch](proposal/WorkTime.shared.patch) | Add Entities/Workday.cs, WorkInterval.cs, TimeCorrectionRequest.cs; add Data/HotelOpsDbContext.WorkTime.cs; insert **one** ConfigureWorkTime(modelBuilder) call in existing Data/HotelOpsDbContext.cs; add Services/WorkTime/IWorkTimeTransactionParticipant.cs | Shared EF model + reusable time interface; no old generated entity edit, no second OnModelCreatingPartial implementation, no new project/EF migration |
| [Program.di.patch](proposal/Program.di.patch) | Insert **one** scoped IWorkTimeTransactionParticipant→SqlWorkTimeTransactionParticipant registration after existing CleaningMonitor registration | Shared DI only; apply **after** DEV1 SQL participant exists, compiles, DI resolves without cycles and uses the same scoped DbContext. No other registration/middleware/auth/config change |

The five new source files in WorkTime.shared.patch have exact counterparts `*.cs.proposed` in proposal/. No source target exists yet. The DbContext insertion and Program insertion were checked against current local files; existing patches are preserved. `git apply --check` is **not** model compilation, SQL DDL execution or DI resolution.

SqlWorkTimeTransactionParticipant is **not implemented** in this submission; the Program patch is a conditional activation diff, not independently deployable. After A1 approval, implementing that DEV1-owned class and its trial acceptance is the next authorized task step; DI must remain unapplied if dependencies/contract checks fail.

## Design decisions included in A1 for explicit review

- One WORKING row/user and one open interval/user enforced by filtered unique indexes globally across branches/modules; states WORKING/ENDED and correction PENDING/APPLIED/REJECTED follow SRS.
- Open/Closed interval is derived from immutable server `closed_at` NULL/not NULL, with CHECK consistency; no duplicate status column.
- Separate workday/interval; nullable ShiftAssignment link supports unscheduled days. Proposed Unscheduled reason bound10–1000 characters is a review decision (SRS requires a reason; the10-character minimum follows the manual-override convention GBR012). Work_date uses scheduled ShiftDate or server date in SYS-PAR10 for an unscheduled day; do not hard-code a new branch timezone.
- UTC datetime2(7), immutable original server open/close, separate effective corrected times, rowversion. Capture a **single SQL SYSUTCDATETIME() value after lock waits**, assign equal initial original/effective values, and reuse for audit. Client timestamps are permitted only as proposed correction values, never original event time.
- Polymorphic proposed item discriminators CLEANING/REWORK/INCIDENT/MAINTENANCE_OCCURRENCE/TASK + positive source ID. This has no SQL FK to the source; trusted module adapters must verify source/branch/current owner and revision under retained locks. PM identity unresolved → that adapter remains disabled. This is an explicit integrity tradeoff, not a proved cross-module FK.
- Composite FK makes interval workday/user/branch agree. ShiftAssignment.user/branch/date, Department.branch, active eligibility, no overlap, containment within workday and correction owner/SoD require the service protocol, not just table CHECKs. All owner time edits serialize under existing retained User lock before feature locks.
- Correction proposal initially accepts **closed intervals only** with original snapshot + rowversion; stale requests cannot overwrite a newer correction. Server original values remain unchanged. SRS does not explicitly settle correction of currently open intervals; that capability remains deferred rather than silently inferred. Multiple pending requests may coexist; only an unchanged original revision can apply.
- Keep WORKDAY_CONTRACT and every other deactivation gate unchanged. Working day blocks future deactivation even with no interval; Ended does not release assignments. Integrating positive time predicates/releasing gates is outside A1.

## Explicit boundaries

A1 does not approve production/shared database deployment; no HotelOpsDB access, DEV2 edits, notification/outbox implementation, PM identity/schema changes, Pause All adapters or permission/grant changes. It does not complete UC111/138/139.

No permission binding is silently inferred from ATTENDANCE_RECORD or SHIFT_MANAGE. Correction permission `work_interval.correct` has no approved mapping in current62-permission catalog. Public Workday/time/correction endpoints require reviewed UC-to-existing-permission binding and notification/Pause All acceptance; they are outside this initial A1 schema/participant activation. No direct public OpenInterval API is proposed.

Next action after A1 approval: apply the exact model/interface patch; build the DEV1 SQL participant; create a fresh guarded trial; execute the approved DDL there; prove uniqueness/locking/atomic audit/rollback and model compatibility; add the one DI line only after successful compilation/DI validation. Report foundation acceptance separately from complete UC acceptance. If the exact reviewed diff drifts, request review of the changed lines instead of expanding approval.

**Approval question:** Do you approve A1 exactly as scoped above: the two trial-only SQL candidates, six-file model/interface patch, and conditional one-line DI patch, while keeping business permission bindings and all other exclusions unchanged?
