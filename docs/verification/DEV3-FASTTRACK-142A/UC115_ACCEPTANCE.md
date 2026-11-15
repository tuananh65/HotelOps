# UC115 acceptance — checkpoint95

Status: COMPLETE_VERIFIED on the identity-guarded disposable SQL environment through trusted HTTPS and canonical browser UI. This closes Route/Assign/Reassign, not UC114 creation or UC117/118/120 lifecycle. The synthetic Pending Acceptance request is an explicit precondition. No shared/production mutation or deployment.

Requirement source: `docs/requirements/srs_sections/02_use_case_specifications.md`, UC115; GBR001/002/004/005/007/008/010/012/147/148; approved explicit user–branch–department authority contract.

| Acceptance criterion | Implementation and executed evidence |
|---|---|
| Current receiving Department Head and permission | Current SQL session, active account/employment, DEPT_MANAGER + IR_PROCESS branch grant and exact current mapping. C95 trial3 reruns absent/wrong department, cross-branch, revoked role/grant/mapping and concurrent authority checks. Handler with role/grant but no mapping receives403 for assignment. |
| Request, history, handlers and open requests | Canonical details/AssignmentPanel/HistoryPage. C95 trial3 renders all four events and both assignment records; handler workload moves0→1→0 for the first handler and0→1 for the replacement. |
| Pending request must be accepted first | Pending assignment returns409; UI offers UC116. New receiving manager accepts and the assignment selector becomes available. |
| Assign and reassign eligible receiving-department handler | C95 trial3 selects two eligible handlers through UI; first assignment moves Accepted→In Progress immediately in UI and SQL. Reassignment ends the old record as REASSIGNED and leaves exactly one ACTIVE record. MSG08 shown for both operations. C93 trial4 covers invalid/inactive/wrong-department handlers and revoked access. |
| Atomic assignment/event/audit and concurrency | C95 trial3 verifies four persisted events with actual actor attribution and four audits, old assignment end time and current handler. Stale public reassignment returns412. C93 trial4 supplies prior executed concurrent replacement and injected rollback proof. |
| Route Pending Acceptance with target and reason | C95 trial3 source manager routes in UI; destination manager receives the routed Pending Acceptance request. Original manager loses processing authority. C94 trial4 covers invalid target/reason/lifecycle, no CSRF/version, concurrent Route, audit/outbox fault rollback and stale version. Route after acceptance returns409 in C95. |
| Notifications after commit, deduplication and current access | C95 trial3 uses the actual hosted worker, no direct delivery calls: Route requester/new manager, Accept requester, Assign requester/first handler, Reassign requester/new handler = exactly7 delivered recipient/event receipts after another scan. Both destination manager and current handler open authorized notification sources. C94 trial4 verifies revocation denies delivery/source. |

Direct new evidence: [final trial3](checkpoint95-trial3-runtime.json), success=true and cleanup=true; [29 backend tests](checkpoint95-backend-tests.txt), [15 frontend tests](checkpoint95-frontend-tests.txt), [backend build](checkpoint95-backend-build.txt), [TypeScript/production build](checkpoint95-frontend-build.txt). [Trial2](checkpoint95-trial2-runtime.json) also passes, before explicit rendered-history assertions. C93/C94 citations are historical executed regression evidence, not rerun claims.

[Trial1](checkpoint95-trial1-runtime.json) failed during source-manager browser loading before Route; cleanup passed. Successful unchanged retry and final extended trial do not establish the cause of intermittent15-second navigation/read timeouts. No timeout or security guard was relaxed.

Deployment limitations: worker remains disabled by default; schema scripts are disposable-identity guarded. Physical outbox retention/JOB06 archive remains pending separately. This acceptance does not authorize shared deployment or indefinite personal-data retention.
