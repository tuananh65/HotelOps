# Task85 — event / source / permission integration matrix

Event names below are **business descriptions**, not approved notification type codes,
seed records or a claim of implemented outbox. Source S = cf8fb020362f8f64192e7f2f14a10547cebe5b91.
Paths/methods/lines and signatures are expanded in the companion producer inventory.

| Business event | Source identity → actual producer | Authoritative recipient evidence | Writer permission → readable endpoint/access | Transaction integration point → missing dependency |
|---|---|---|---|---|
| Cleaning assigned (UC029) | CleaningTaskId + new CleaningAssignmentId/assignment revision; S A.AssignCleaningAsync:32 | committed CleaningAssignment.AssigneeUserId; eligibility under actor+target locks | CLEANING_ASSIGN → current GET `/api/branches/{b}/cleaning-monitor/cleaning/{id}` needs CLEANING_VIEW; assigned staff access **not established** | A:51 audit→B:53 commit, same context; writer absent current, durable publisher/replay and safe recipient read route needed |
| Cleaning cancelled (UC037) | exact cleaning cycle/task+cancellation revision; **producer MISSING** | assignee if any at authoritative cancellation boundary; not latest assignment after event | cancel permission **UNRESOLVED**, don't promote CLEANING_VIEW; Monitor GET above is read only | missing cancellation/room recalculation/effective block/audit writer and atomic durable intent |
| Inspection assigned (UC041) | InspectionId + CleaningTaskId + VersionId, S A.AssignInspectionAsync:56 | persisted RoomInspection.InspectorUserId, active eligible supervisor, SoD | INSPECTION_PERFORM → S GET `/api/branches/{b}/inspections/{id}/execution` exact inspector only; GET absent current | A:85 audit→B:53 commit; current runtime restore+notification needed |
| Inspection approved (UC044) | InspectionId + exact cleaning/rework cycle + conclusion revision; **producer MISSING** | actual final cleaning/rework completer **UNPROVEN**, not latest assignment/progress | approval action binding **UNIMPLEMENTED**; S execution has INSPECTION_PERFORM but does not prove approve; current cleaning Monitor is not inspector conclusion route | full Passed/Completed/effective room lifecycle/audit+intent writer missing; authoritative final-completer mapping and recipient read permission required |
| Inspection rejected (UC045) | same exact cycle/inspection+FAILED conclusion; **producer MISSING** | actual final completer + authoritative eligible designated supervisor **UNRESOLVED** | rejection binding **UNIMPLEMENTED**; do not create permission; no live reject/detail action established | atomic Failed/ReworkRequired/defects/effective room state/audit/intent missing, supervisor selection decision and source evidence needed |
| Rework requested (UC046) | ReworkId + InspectionId + CleaningTaskId + selected defect IDs; S A.RequestCleaningReworkAsync:90 | persisted CleaningRework.AssigneeUserId, validated eligible housekeeper | INSPECTION_PERFORM → current GET `/api/branches/{b}/cleaning-monitor/rework/{id}` needs CLEANING_VIEW; own assignee access not guaranteed | A:112 audit→B:53 commit; historical writer absent current, delivery/retry/replay needed |
| Unscheduled Workday started (UC139 B1.2) | future WorkdayId + actor/branch/OperationId business-result identity; current StartAsync:25–29 fails closed | proposed unique active branch DH, no implemented approved resolver; no role broadcast | ATTENDANCE_RECORD own Start/Current/End; current GET `/api/branches/{b}/workdays/current` is **own-only**, DH cannot read another user's day through it even BRANCH/ALL | future business+audit+durable intent+operation replay before outer commit; publisher/replay/store/DH/action route contracts not integrated |
| End via Pause All (UC139) | actor/day/interval + exact module kind/item/revision; EndAsync:60 unavailable | no standalone notification recipient inferred; source event contracts need review | ATTENDANCE_RECORD does not confer CLEANING_EXECUTE/TASK_EXECUTE/maintenance source permission; Workday GET own-only | same outer transaction, sorted users→time→source/history; close while IN_PROGRESS→approved pause→End; DEV2 PM adapter/rework transition missing |

## Policy and readable action rules

Event producer authorization and recipient source-read authorization are separate checks.
An inbox deep link cannot confer the producer's permission or bypass current source authorization.
INSPECTION_PERFORM is actual S assignment/execution binding; no INSPECTION_ASSIGN code is invented.
Approvals/rejections lack actual writer bindings, therefore no permission guessed from UC titles.
Monitor GET currently only proves branch/global CLEANING_VIEW behavior; historical permission
matrix role grants alone do not prove an assigned recipient can read that route. No broad shared
authorization change is authorized by Task85. Delivery snapshots must keep exact source identity
and committed recipient; retry must not recompute recipient from latest assignment.

## SRS retry vs transaction decision that requires reconciliation

Historical SRS UC029 explicitly keeps assignment and queues notification retry when notification
cannot be created (MSG21); successful business transaction plus durable recoverable intent must
be distinguished from external delivery failure. B12 proposal requires atomic business/audit/intent
and rollback on staging failure. These are source requirements/proposals, not proof of a joint
decision on outbox staging failure, replay retention or delivery-terminal handling. Joint-approved
V1 semantics must resolve this before wiring production writers. No fake enqueue/delivery PASS.

## Product & Architecture Decisions V1.0 / Joint Review / Addendum V0.2

**MISSING from inspected local evidence.** Root and four registered worktree searches by filename
and content, plus available Git history filenames under docs/Coordination, did not find these
documents. External DEV2 checkpoint path unavailable. No remote fetch performed. Therefore their
exact content/hash/approval signatures and event registry cannot be compared or certified here.
User states Product & Architecture Decisions V1.0 is finalized: retain that business-approval
assertion, but do not reconstruct missing decision text or pretend Task79 draft is the final V1.

Known explicit approvals from task history cover A1 and B1.1 bounded runtime, P0 expiry fix and
Task84 one-line DI. They **do not approve** B1.2 shared DI, new notification/replay schema,
permission grants, recipient read scope changes or restoring historical common partials.
Business decisions being approved does not apply a shared-file diff. Supply exact V1.0/Joint/
Addendum paths or immutable blobs for final reconciliation; DEV2 can review source evidence now.
