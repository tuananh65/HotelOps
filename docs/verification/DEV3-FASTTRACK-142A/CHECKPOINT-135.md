# FASTTRACK-142A checkpoint135 — UC122 At Risk and recipient authority

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=7506270e735f3d64df9b702b431b1f8f8864966c
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Implemented the At Risk list filter in the API and canonical history UI. Its SQL predicate uses official CreatedAt and current DueAt, includes 80% through exactly 100%, excludes overdue/terminal/invalid windows, and preserves datetime2 precision without arithmetic overflow. The predicate intersects the authorized branch/direction query before pagination and total count. It does not grant access. UI At Risk and Overdue selections are mutually exclusive.

Direct verification:

- checkpoint135-risk-filter-sql.json: success, 53 checks, rollback=true. Includes 79/80/100/overdue boundaries, invalid windows, exact 100ns arithmetic crossing a microsecond boundary, full datetime2 range and intersection excluding foreign requests. This is query-component evidence, not standalone authorization acceptance.
- checkpoint135-risk-filter-ui-runtime.json: success, 12 escalation UI checks. HTTPS At Risk + Sent intersects branch scope; real browser priority/deadline escalation, MSG08/history, hosted worker, explicit acknowledgment and authorized source remain verified. CSRF, ETag, stale replay, cross-branch and revoked-role failures remain enforced.
- checkpoint135-escalation-recipients-runtime.json: success, 30 existing escalation checks plus 13 distinct-recipient checks. Manager-only delivery requires current explicit department mapping, IR_VIEW and IR_PROCESS; foreign manager excluded. Handler-only delivery/source requires current assignment. Department changes and ended assignments revoke source access while preserving delivered history. Fixture responsibility changes test authorization, not implementation of UC122 rerouting.
- Both paired cleanup receipts confirm disposable clones and backups removed. Query fixture rolled back. No shared/production database mutation.
- Current backend build: PASS, zero warnings/errors. dotnet test HotelOps.slnx exits successfully without test discovery; explicit regression project: 760 PASS, 13 environment-gated SKIP.
- Current complete history browser file: 33 PASS, zero failures. TypeScript PASS. Production build passed during C135 before runtime trial; existing Router use-client and bundle-size warnings remain.
- The earlier full frontend run completed with 3442 PASS, 1 FAIL, 1 SKIP. Failure was escalation select accessible-name lookup in a bundle compiled before the C134 fix. Current 33-case browser rerun includes that case and passes. Do not describe the full suite as green or as a final-revision release run.
- An initial C135 probe build failed because the query helper was inaccessible. An accidental old-binary probe run returned 42 checks; that result is not new-change evidence. Corrected the helper to expose only Apply over an authorized query, rebuilt, and obtained the retained 53-check receipt.

Product files: HistoryPage.tsx, InternalRequestHistoryController.cs, InternalRequestReadService.cs, new InternalRequestAtRiskQuery.cs. Validation files: internal-request-history-browser.test.mjs, SprintInternalRequestHistoryProbe.cs, SprintInternalRequestEscalationScenario.cs, SprintInternalRequestEscalationUiScenario.cs, new SprintEscalationRecipientScenario.cs.

New whole UCs completed: 0; matrix remains 24/144 COMPLETE_VERIFIED. UC122 remains PARTIAL. Priority/deadline intervention through acknowledgment is demonstrated; reroute/handler lifecycle and pending-handover treatment still need the requested business decision. No inferred lifecycle changes were implemented.

Next highest-impact work: finish UC122 reroute/handler intervention once its lifecycle contract is resolved; close UC120 mandatory-material completion and UC121 closure authority acceptance when their pending contracts arrive; continue independent FE11/FE13 acceptance rather than block the sprint. Preserve current security and database boundaries.
