# UC123 source completion

2026-11-16: IMPLEMENTED_PENDING_QA, not COMPLETE_VERIFIED.

Source reconciliation against UC123 normal flow, withdrawal alternative, MSG10 and the approved IR_VIEW/Sent/Received decisions:

| Requirement | Current implementation |
| --- | --- |
| Canonical entry and scoped list | `/internal-requests`, `HistoryPage.tsx`, `InternalRequestHistoryController`, `InternalRequestReadService` and `InternalRequestReadAccess`; current session, IR_VIEW and branch/related-record scope |
| Status, receiving department, handler, deadline, overdue and deterministic ordering | `InternalRequestHistoryQueries.ListAsync`; deadline then request ID; corresponding list columns |
| Sent/Received, status, type, priority, department, overdue, keyword | `InternalRequestDirection`, history query/filter options and UI controls; Received uses current handler or explicit current management authority, intersected with read scope |
| Routing, decisions, progress, result, handover, escalation and assignment history | Paged events and assignments, existing progress/handover/escalation panels; Complete persists its result in the COMPLETE event note |
| Authorized linked work | Task, cleaning, incident and material panels; independent target authorization. Legacy task source references and RelatedIncidentId remain visible without inventing completion requirements. Material read uses MR_VIEW |
| Withdraw before acceptance | Withdrawal panel/controller/service, requester and PENDING_ACCEPTANCE checks, strong version/CSRF, one transaction for cancellation/event/audit/outbox, current mapped manager delivery and notification source reauthorization |
| No matches and load failure | Explicit MSG10 with retained filters and clear/change guidance; new Reload history action reruns the authorized request after a load failure |

The previous matrix mixed two independent concerns: legacy completion classification belongs to UC120; UC123 must display authorized legacy references, which it already does. Material links and creation handoff are also present in current source. These are no longer missing UC123 source components. UC118/120 and the incomplete Reopen/Escalate mutations remain separate gaps; no completion claim is made for those UCs.

Direct validation this batch: frontend TypeScript/production build PASS; 5 history decoder/transport checks PASS; 20 existing backend direction/read-access/withdrawal checks PASS (0 skipped). The frontend build retains the existing React Router directive and large-bundle warnings. No new SQL, HTTPS or browser acceptance run, database changes, grants or worker activation. Earlier C105/C107 withdrawal/direction evidence remains historical and does not prove the current full UC.

Human QA: use a verified disposable database with the existing IR schemas, normal authenticated sessions and explicit grants. Verify all filters/pagination, scoped legacy and material targets, Complete result history, withdrawal versus concurrent Accept/Route, rollback/outbox delivery, revoked permissions and cross-branch denial. Missing schema/grants must remain fail-closed. No automatic legacy classification or broad target access is authorized by this reconciliation.
