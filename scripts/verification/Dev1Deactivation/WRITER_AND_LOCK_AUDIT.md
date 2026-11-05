# Writer and lock audit

Search scope: all application C# (including controllers/services/security/raw SQL), current mappings/entities, release SQL and patches. No assignment writers are found for cleaning/rework, inspection/defects, operational tasks, internal requests, shifts/work-time or department/user manager responsibilities. No application hosted/background responsibility writer registration was found. Canonical seed inserts departments; verification scripts create fixtures and one trial cleanup changes department is_active. These are not runtime business APIs and do not prove deployed jobs/imports/direct SQL absent.

Reproduce the application reference inventory from repository root:

```powershell
rg -n 'CleaningAssignments|CleaningReworks|RoomInspections|InspectionDefects|OperationalTaskAssignments|InternalRequestAssignments|ShiftAssignments|ManagerUserId' src/HotelOps.Api --glob '!**/Entities/**' --glob '!**/Data/HotelOpsDbContext.cs' --glob '!**/obj/**' --glob '!**/bin/**'
rg -n 'new (CleaningAssignment|CleaningRework|RoomInspection|InspectionDefect|OperationalTaskAssignment|InternalRequestAssignment|ShiftAssignment|Department)|AddHostedService|BackgroundService|IHostedService' src/HotelOps.Api --glob '!**/obj/**' --glob '!**/bin/**'
```

## DEV1 writer inventory and seven-step protocol

The required sequence is caller-owned SQL transaction -> affected Users in increasing ID with UPDLOCK/HOLDLOCK -> ACTIVE/nondeleted and approved employment eligibility -> canonical feature locks -> mandatory re-read -> write -> retain User locks through commit/rollback.

| Source / writer or reader | Current behavior | Protocol verdict |
|---|---|---|
| `Security/Authorization/Resources/CleaningTaskAccessReader.cs:15`; `CleaningTaskResourceResolver.cs:12` | AsNoTracking projection and current-assignee permission evidence, no transaction/responsibility mutation. Resolver rejects multiple current assignees. | Read adapter implemented; **not a writer lock** and not a full deactivation predicate. All seven writer steps NOT IMPLEMENTED for cleaning mutations. |
| Cleaning/rework UC-028-036, inspection UC-041-047, operational UC-102-112, internal UC-114-123, shift UC-138/139, department manager writers | Schema/entities only; no business API/service mutation discovered. Future assign/reassign/accept/reopen/review/handover/manager-reactivation paths all require coverage. | **NOT IMPLEMENTED**, not PASS or not-applicable safety. External writer inventory pending. |
| `UserAccountActivationService.cs:24` | Own transaction; ordered actor/target Users; retained authority locks; SQL session and scope gates; conditional INACTIVE -> ACTIVE, token/audit, final gates. | Implemented activation mechanism; creates no duty and does not release last-admin capability. Existing evidence does not certify Deactivate. |
| `UserBranchAssignmentService.cs:24` | Own transaction; ordered Users; authority/membership/target-role range locks; active target; role-path/OM refusal; token/member/audit + final gates. | Implemented Add Membership; no duty reassignment/removal or global admin mutex. Its pattern is reusable but does not retrofit future responsibility writers. |
| `TemporaryCredentialService.cs:91` + `AdminCredentialResetWorkflow.cs:17` | Ordered actor/target User locks, retained authority and final proof/session checks, credential/session/audit transaction. | Existing reset race protection; can change login eligibility, but has **no last-admin invariant**. |
| `CredentialLockoutService.cs:28`; SQL sessions / login / reauthentication | Caller transaction, User before credential/session; row retained until caller commit. | Locks an individual principal, not the global capability set. No global administration serialization. |
| `UserAccountCreationService.cs:36` | Branch -> Department -> Role UPDLOCK/HOLDLOCK, then inserts new User/membership/initial role/credential. It does not assign an existing employee to business work. | **Reverse-order risk** relative to a future users-first global protocol; cannot bolt User locking on after those resources and claim canonical order. Review admin-mutex integration/identifier eligibility, even though initial roles are allowlisted nonadmin roles. |
| `UserProfileService.cs:27` | Own transaction, scope read, rowversion-CAS scalar update; no explicit ordered actor/target authority lock helper. Only fullName/email/phone change. | Not a responsibility writer. Email can affect login identifier eligibility; no last-admin serialization. Do not claim all existing DEV1 mutations already obey the proposed common protocol. |

## Canonical writer contract still to integrate

1. Discover participant/resource IDs read-only; discovery does not authorize a write. Begin/retain the caller transaction. Lock all affected old/new users in numeric order before feature locks. If participant IDs change on re-read, restart or conflict; do not acquire a newly discovered lower-ID User after feature locks.
2. Recheck account ACTIVE/nondeleted plus feature-specific employment/branch/department eligibility under held locks. Current login policy does not require employment_status ACTIVE; Material Request does. Do not copy a universal employment rule without approval.
3. Lock feature resources in a reviewed order, re-read lifecycle, version and current participants, then mutate responsibility and audit atomically. Include reopen/reinspection/manager or parent reactivation: those can restore responsibility without changing assignee FK.
4. Deactivation must use the same User boundary, authoritative dependency predicates and relevant rows/key ranges, update status/token, revoke sessions and audit in one transaction. A later writer must observe INACTIVE and reject; a writer-first schedule must force Deactivate to observe the new duty and refuse.
5. Agree table/row/applock ordering and deadlock retry behavior before integration. Relevant FK/index presence is not a protocol: FK shared locks validate existence and can still allow assignment after deactivation commits. Avoid scans/range-lock escalation assumptions; inspect query plans/index coverage on a trial before claiming serialization.

No helper was added or advertised as integrated in Task 40. The existing locks protect their current workflows only. Required real SQL tests per future writer: writer-first and deactivate-first schedules with DMV waits; concurrent reassign in both directions; reopened duty; stale discovered participant IDs; eligibility changes; audit/commit rollback; no deadlock cycle across feature locks or global admin guard. **NOT RUN: no approved deactivation/predicate writer implementation yet.**

## DEV2 RC-D1 integration boundary

The following is the Task 40 supplied current reference, checked against available source where possible; it is not a substitute for the two missing DEV2 documents:

| Area | Current reference and observed writer | Outstanding integration |
|---|---|---|
| Maintenance | `MaintenanceWorkService.Commands.AssignWorkAsync:10` writes assignee_id at line 18; RequireUserAsync:115 checks existence only. CreateWork:44 sets assignee NULL. No Reopen method is present in this snapshot; RC-D1 says Create/Reopen do not retain assignee. | Do not incorrectly label Create/Reopen as current assignee writers. DEV2 must deliver retained User eligibility locks for AssignWork and review lifecycle-restoring paths. Commands lock incident/resource before validating participant; order retrofit needs review. |
| Material Request | Requester in DRAFT/NEEDS_INFORMATION and current pending approver are blockers per supplied RC-D1. `MaterialRequestService.Participant:40`, `Approver:46`, `Cycle:113`; Commands Save/submit/resubmit/current decisions. Participant checks ACTIVE/nondeleted/employment and branch relation via ordinary reads. | Exact current revision/requester/cycle writer audit and retained participant locks pending DEV2. A request/catalog applock alone does not serialize user deactivation. |
| Warehouse | Active warehouse manager is blocker per RC-D1. `InventoryService.Masters.SaveWarehouse:64`, ordinary manager ACTIVE/nondeleted check:75; warehouse lock/resource read precedes this check. | Retained affected User locks and canonical order need DEV2 retrofit; do not acquire Users late under warehouse/catalog locks. |
| Asset | Pending handover receiver is blocker per RC-D1. `AssetService.Handover.CreateHandoverAsync:48`, `FinalizeHandoverAsync:85`/accept update assets.custodian_user_id:114; target check:38 calls existence convention. `AssetService.Validation.ApplyMaster:89` does not write custodian; Create/Update call it, so they are not identified as custodian writers in this snapshot. | Create/finalize currently take asset/resource locks before participant checks; need DEV2 order retrofit and current-custodian lifecycle audit. Do not assume accepted history alone proves or clears current custody. |

DEV2 source is not edited. RC-D2 retrofit and SQL race evidence remain pending; no DEV2 approval is claimed.
