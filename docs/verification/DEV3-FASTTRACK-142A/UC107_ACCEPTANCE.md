# UC107 — Accept Assigned Task: COMPLETE_VERIFIED (isolated)

Checkpoint67, 2026-11-15. Newly completed: **UC107 only**. Source: SRS 11.6, GBR001/002/005/007/008/010/129 and decision67. [Final runtime](checkpoint67-decline-final.json): success=true, **91 records**, including 33 entry assertions, 41 decline assertions and three browser results with **21 stages**. These counts are not 91 independent automated tests. Cleanup is independently proven below; the runtime deliberately retains immutable synthetic criteria until the owned clone is dropped.

| Acceptance criterion | Direct evidence |
| --- | --- |
| Logged-in executor with current assignment | Anonymous Decline with its own valid CSRF returns401; manager declining another assignee's task403; foreign branch403; revoked executor grant403 |
| All three SRS executor actors | HK_STAFF full Accept/Decline browser/API path; MAINTENANCE_STAFF and STOREKEEPER each Accept/Start and Decline using actual catalog grants |
| Open own assigned task and review details | Canonical scoped list/detail; browser reads Assigned execution state, readonly criterion, description, priority/deadline fields; task state comes from workflow authority, not inferred TODO |
| Accept and success feedback | Assigned→Accepted, fresh ETag, MSG08; reload retains Accepted; browser continues through Start, progress, Pause/Resume and history |
| Accept preserves time semantics | No workday/interval needed for Accept; it does not set actual start; an Assigned task with an existing first start can Accept without erasing it |
| Required Decline reason | Empty/whitespace/control-character/4001-character values rejected400 without writes; a short nonblank reason succeeds (no invented ten-character minimum); UI disables blank/whitespace submission |
| Pending Assignment and assignee removal | Decline commits physical TODO plus authoritative PENDING_ASSIGNMENT; unique active assignment ends as CANCELLED with ended_at; original assignment note/history preserved |
| Atomic audit, reason and intent | Audit contains exact progress/assignment/reason/recipient evidence. Audit-write failure rolls back all business changes. Injected permission loss after assignment cancellation reaches the final gate, returns403 and rolls back both decline and revocation |
| Current state, concurrency and replay | Missing ETag428, missing CSRF400, stale412, Accepted cannot Decline409. Two Declines produce one200/one403 after ownership loss. Accept versus Decline commits exactly one response; losing request403/412 |
| Ended ownership does not restore authority | Former assignee receives404 for detail and403 for progress; its scoped list excludes declined tasks. The final permission proof is transaction-local, not a historical-read bypass |
| Work-time integrity | Decline does not open/close unrelated intervals; existing first start and two prior task intervals remain intact. UC108 workday/shift guards are unchanged |
| Department Head notification | Recipient intent contains only eligible manager in the task branch. Revocation blocks delivery; delivery fault rolls back notice/receipt while keeping intent; concurrent retries yield one notification/recipient |
| Source authorization and manager handoff | Authorized manager source opening200; revoked manager404. Browser reads Pending Assignment, persisted reason/history, and opens the canonical task from inbox |
| Actual hosted recovery | [Worker receipt](checkpoint67-decline-worker-final.json): success=true, singleDelivery=true, sourceUnchanged=true, hostStopped=true |
| Workflow filtering | Canonical PENDING_ASSIGNMENT filter applies before pagination/count; OWN list excludes ended assignments; invented workflow TODO400; missing capability503 |
| Session lifecycle | Both Decline browsers log out; API logout leaves no active executor session in SQL |
| Schema and cleanup | Only existing workflow CHECK extended; no new table/generated EF change. [SQL cleanup](checkpoint67-task-cleanup-final.txt) proves original source constraint and five clone absences; [files/hosts](checkpoint67-files-host-cleanup.json) proves zero backups/private files/listeners |

Task creation, assignment and Working workday are synthetic preconditions. Criterion definitions are seeded during their permitted fixture phase, then the owned fixture is prepared as Assigned; no trigger is disabled. Independent role/browser cases may reset or create synthetic preconditions. These preparations are not evidence that UC102/104/106 writers exist. Existing permission catalog may authorize other assigned executor roles; no new actor restriction is inferred from UC135.

Failed receipts are retained: [trial1](checkpoint67-decline-trial1.json) attempted criterion creation in Assigned and was correctly rejected by the existing trigger; [trial2](checkpoint67-decline-trial2.json) placed permission-loss injection on an EF OUTPUT target and failed before the final gate, so injection moved to the raw assignment UPDATE; [trial3](checkpoint67-decline-trial3.json) found a missing accessible name on the workflow select, fixed in the UI; [trial4](checkpoint67-decline-trial4.json) passed business/browser flows but omitted browser logout, correctly failing the existing SQL session assertion. Final trial5 includes real browser logout and passes unchanged assertions. Audit-write fault assertions intentionally include SQL rejecting that write; they do not claim a particular trigger body ran.

UC105 linked-source requester binding and UC106 processing-cycle boundary remain separate gaps. This is isolated workflow acceptance, not full FE11 or production deployment certification.
