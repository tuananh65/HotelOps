# FASTTRACK-142A checkpoint111 — UC120 closure transaction

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=6f2af1100f4ee16373732c49abf14934963d83db
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
PARTIAL=120/144
SHARED_DB_MUTATION=NO

The UC120 service now performs a Serializable closure transaction: validate current session, independent IR_VIEW, receiving DEPT_MANAGER/IR_PROCESS mapping, request version, IN_PROGRESS and one current assignment; inspect known linked-work/handover prerequisites; record result, COMPLETE event and audit; close Open escalation; end the current assignment as COMPLETED while preserving the historical handler; set request CLOSED/completed_at/closed_at; and persist one requester notification intent. Final session/read/process authority is checked before commit. The service is not yet registered as a public API or UI action.

The guarded disposable-only SQL script adds closure receipts, completion outbox, internal-request escalations and explicit material links. Thirteen restrictive FKs preserve event/audit/request/assignment provenance. Required result and escalation state CHECKs plus the one-Open-escalation filtered unique index enforce the approved lifecycle. No existing generated entities or status CHECKs were changed. Existing request row_version remains authoritative; no second request state/version is introduced. Material links require an explicit mandatory flag; this batch creates no linking API or new target authorization.

Mandatory material always blocks pending the requested business decision mapping Fully Issued and valid remainder closure to GBR-152. Optional material links cannot bypass branch integrity. This is a bounded gap, not permission to ignore materials. Referenced audit must remain protected by the approved archive-before-purge policy; no cascade, disabled FK, receipt deletion or retention activation is introduced.

[Trial2](checkpoint111-trial2-runtime.json) success=true verifies direct service -> SQL with an actual SQL session, stale version412, revoked department mapping403, injected outbox failure rolling back event/audit/receipt/assignment/escalation/request version, concurrent closure200/412, persisted trimmed result, closed escalation and exactly one requester intent. Schema installation is executed twice against the same verified clone. This is not public HTTP/UI closure E2E and does not prove delivered notifications.

Trial1 passed closure assertions but failed the parent logout check because the independently created service-test session remained active. Cleanup now removes only that fixture-owned session; browser logout assertions were retained. Both clone and backup cleanup receipts confirm removal. The failed trial remains recorded. C110 prerequisite SQL evidence is historical and reused because that code is unchanged.

Backend solution build PASS with zero warnings/errors. Actual InternalRequest regression suite72 PASS. Solution-level test exits0 without discovering a test project and is not counted as coverage. ModelProbe retains three existing EF1002 credential-fixture warnings. No frontend/dependency changes; frontend build/tests and npm ci not rerun.

WORKFLOWS_COMPLETED=[]
UI_API_SQL_E2E_VERIFIED=[]
EXACT_REMAINING_GAPS=[material terminal-state mapping; recipient-revalidated completion notification delivery/source; authorized completion read/API/UI with protected blocker display; CSRF/HTTP/browser closure acceptance]
NEW_BLOCKERS=[mandatory material completion mapping awaiting explicit decision]
NEXT_BATCH=[completion delivery/source; canonical read/Complete controller and UI; public HTTPS/browser closure with rollback/concurrency and material gate]

The zero-new-UC checkpoint delivers the actual closure transaction after C110 prerequisites, rather than additional peripheral coverage. UC120 stays PARTIAL until the remaining public workflow and notification acceptance is verified. The full sprint goal and original deadline remain active.
