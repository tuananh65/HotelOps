# FASTTRACK-142A checkpoint132 - UC122 intervention transaction

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=9401551d78dc1de942cfda69994bf44945baa573
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Implemented the UC122 priority/deadline intervention service. It validates a current unrestricted session, Operations Manager branch authority with IR_OVERDUE_HANDLE and independent IR_VIEW, approved timing eligibility using SQL UTC, ETag, a changed intervention, active PRIORITY reference and GBR-012 reason. Serializable user/request/range locks plus the existing unique Open index preserve concurrency. Deadline/priority, Open escalation, event, audit-linked action receipt and deduplicated recipient intent commit atomically. Requester, current handler and explicitly mapped Department Heads are selected under current read/processing access. Creation time and authoritative lifecycle are preserved.

The additive identity-guarded script creates only escalation action receipts and recipient outbox, reusing existing escalation storage. No generated EF entities, existing status constraints or shared schema changed. Script runs twice in the disposable clone to verify idempotency. No CASCADE DELETE or disabled FK/trigger. The test-created fault trigger is removed after its rollback test. Audit-linked receipts remain subject to the approved archive-before-purge design; no retention policy is invented here.

Direct evidence:

- Backend solution build PASS, zero warnings/errors. Solution test exits zero without test discovery; explicit regression project 760 PASS, 13 environment-gated SKIP. ModelProbe builds with 3 existing credential-fixture EF1002 warnings. An initial compile error involving await/ReadOnlySpan was corrected before trials.
- checkpoint132-escalation-retry-runtime.json success=true, 22 escalation checks: DH with permission denied; revoked OM role; stale version; terminal statuses; under-80%; invalid reason/deadline/priority/no-op; injected outbox failure rolls back event/audit/version/escalation; concurrent requests yield one success and one 412; exactly one Open; audit provenance; requester/handler/manager deduplication; duplicate Open denied; inactive priority denied; positive overdue priority intervention preserves deadline/lifecycle.
- checkpoint132-escalation-retry-cleanup.json confirms clone and backup removed. The initial trial passed its 20 intervention assertions but failed parent logout verification because its separate synthetic service session remained. Fixture cleanup now removes that exact credential before parent logout; initial failure and cleanup receipts are preserved. No shared/production mutation.
- This is direct service-to-SQL evidence, not public Escalate HTTPS/browser acceptance. No controller/DI/UI exposure was added: acknowledgment delivery/source authorization must be connected first. Reroute/handler interventions also remain.
- Full frontend retry session81908 TERMINAL FAIL: 3439 PASS, 1 FAIL, 1 SKIP. Failure: revoke-user-role-browser late mutation fixture attempted to resolve a DELETE callback before DELETE dispatched. Added an explicit dispatch wait before session invalidation; all 11 tests in that file PASS. No frontend security/product change. New release run session37649 is live, log artifacts/checkpoint132-frontend-release.log; no full-release PASS claimed.

Product: src/HotelOps.Api/Services/InternalRequests/InternalRequestEscalationService.cs.
Validation/schema: InternalRequestEscalationIsolated.sql, SprintInternalRequestEscalationScenario.cs, SprintDepartmentAuthorityScenario.cs, Invoke-Dev3LinkedTaskCreationTrial.ps1; frontend revoke-user-role-browser.test.mjs fixture.

No whole UC completed. Next highest-impact implementation: UC122 acknowledgment delivery with current recipient/source authorization, then the public HTTPS/UI adapter and full end-to-end acceptance; complete reroute/handler intervention paths without bypassing their lifecycle guards.
