# Task48 — Account Deactivation V1 PROVISIONAL

Recorded from the DEV1 owner's explicit Task48 instructions, 2026-10-29 Asia/Saigon.
These are provisional business rules; PO may revise them. They do not authorize a new
schema, state vocabulary, role/permission assignment, workflow or HTTP Deactivate route.
Task47 technical approval remains the basis of executable predicates listed below.

| Responsibility | V1 business rule | Executable support / remaining decision |
|---|---|---|
| Cleaning assignment | Block a current nonterminal assignment; contradictions fail closed | ACTIVE + ended_at NULL + parent PENDING/IN_PROGRESS blocks; ACTIVE with COMPLETED parent remains unresolved pending final-completion/Pending Inspection mapping; inconsistent markers/unknown parent unresolved |
| Rework / Inspection / Defect | Block target's current designated responsibility | PENDING/IN_PROGRESS with explicit assignee/inspector; OPEN/IN_PROGRESS with defect assigned_to. Historical creator/finished inspector/approver alone is not current ownership. Conflicting completion evidence unresolved |
| Shift | Block open shift responsibility; clock alone cannot release it | UNSUPPORTED exact predicate: approved stored statuses, timezone, end/attendance/cancellation semantics absent |
| Workday | Block only direct current responsibility | UNSUPPORTED: no approved Workday representation, including unscheduled workday; do not infer from attendance alone |
| Internal Request | Block actor required to perform the next step | UNRESOLVED: mandatory actor/state mapping absent; requester FK does not prove a next action |
| Handover acknowledgment | Block unfulfilled mandatory transfer/acknowledgment action | UNSUPPORTED: authoritative designated receiver/acknowledgment representation absent for internal requests; do not infer from event actor |
| Operational Task | Block assignee of nonterminal task | UNRESOLVED exact predicate: task review/verification/reopen/assignment-end semantics differ between SRS and schema |
| Active Department / Line Manager | Block current management responsibility | Active departments.manager_user_id = target implemented; inactive department is not manager-specific blocker. users.manager_user_id requires exact current subordinate/management semantics |

Unsupported/unresolved means Deactivate remains blocked, not that no responsibility exists.
All dependency checks must include responsibilities outside the target's primary branch;
an API must authorize target scope separately and avoid exposing out-of-scope details.
No automatic cancellation, reassignment, manager replacement or data repair is allowed.

Future mutation needs global-admin serialization, retained Users locks in ascending order
(existing global guard first retains the Users universe), feature locks, authoritative
session/authority/state reread, version check, full responsibility review, atomic account
change/session revocation/audit and LastAdmin poststate. Existing helpers do not supply
these guarantees. Writer source, both race orders and regressions must be verified before
any endpoint enablement in a separately authorized task.
