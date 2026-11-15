# UC116 acceptance — checkpoint93

Status: COMPLETE_VERIFIED on the identity-guarded disposable environment. This closes UC116, not UC114 creation, all UC115 routing/reassignment, or FE13 as a whole. A synthetic Pending Acceptance request is the documented precondition. No shared/production database mutation or deployment.

Requirement source: `docs/requirements/srs_sections/02_use_case_specifications.md`, UC116; GBR005/007/008/010/014/146; approved explicit user–branch–department authority decision. Existing DateTime values are UTC; a changed deadline requires a reason. No undocumented planned-start field or RequiredAt equivalence is invented.

| Acceptance criterion | Implementation and executed evidence |
|---|---|
| Authenticated current receiving Department Head, permission, Pending Acceptance | DecisionService validates principal/session, DEPT_MANAGER + IR_PROCESS and exact mapping in serializable transaction. C90/C91 and C93 trial4 verify absent/revoked mapping, role/grant/branch denial, request/mapping lock coordination, wrong session, wrong lifecycle. |
| Request details and age | DecisionPanel displays subject, description, submitted UTC, age and current deadline from authorized DTO. C92 and C93 canonical browser scenarios exercise it. |
| Accept, optionally reasoned deadline adjustment | C93 trial4 and trial5 real browser submits reason and adjusted UTC deadline through authenticated HTTPS/CSRF/ETag; SQL verifies ACCEPTED event, actor and response-time audit. Missing reason on deadline adjustment is rejected in trial4. |
| Decision/response time/audit atomic | Trial4 verifies persisted event/audit/outbox and synthetic outbox fault rollback, including unchanged row version. Concurrent Accept/Reject yields one commit and one412. |
| Notify requester after commit | Trial5 uses the real hosted worker enabled at one-second polling: Accept and Reject notifications become DELIVERED without a direct delivery call. A subsequent scan preserves one notification per recipient/event. Trial4 verifies current IR_VIEW denial, retry idempotency and revoked-access source denial. |
| Offer handler assignment | Canonical AssignmentPanel appears after Accept. Trial4/trial5 select an eligible handler, POST assignment, persist ACTIVE assignment and IN_PROGRESS, reload committed state. Assignment API independently validates scope/session/current mapping and row version; missing CSRF400, missing version428, stale retry412. |
| Reject requires reason, persists Rejected, notifies, MSG19 | Both browser trials exercise client empty-reason feedback and successful reasoned rejection; SQL/worker evidence verifies REJECTED and exactly one delivered notice. |
| Security and rollback | Current authority is rechecked before commit; shared read does not authorize processing; no hard-coded role IDs. Trial4 includes inactive/wrong-department handler, revoked branch access/mapping, duplicate/stale assignment, concurrent replacement and rollback preserving previous assignment. |

Direct evidence: [trial4](checkpoint93-trial4-runtime.json), [hosted-worker trial5](checkpoint93-trial5-runtime.json), [23 backend tests](checkpoint93-backend-tests.txt), [13 frontend tests](checkpoint93-frontend-tests-final.txt). Both runtime receipts success=true, cleanup=true. C92 evidence remains historical execution of unchanged decision behavior, not a newly rerun broad suite.

Deployment limitations: notification worker defaults off outside explicitly configured environments; schema scripts allow only the verified disposable identity. Physical notification/outbox retention and JOB06 archival remain separate pending work; this acceptance does not authorize deployment or indefinite archival of personal data.
