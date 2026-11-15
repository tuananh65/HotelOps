# FASTTRACK-142A checkpoint91: FE13 decision transaction and requester delivery

TASK_ID=DEV3-FASTTRACK-142A
CURRENT_HEAD=commit containing this receipt; starting local/remote 770746b3514dcc74235da68a427e81908cfdb2a3.
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=21/144
PARTIAL=123/144
SHARED_DB_MUTATION=NO

UC116 now has a real SQL-backed Accept/Reject transaction using the approved explicit department mapping. The isolated migration extends the inspected status CHECK with SRS-defined PENDING_ACCEPTANCE and ACCEPTED, preserves all seven legacy values without converting SUBMITTED, adds a physical row_version, and creates a decision notification outbox keyed by the existing event. Generated EF entities remain unchanged. The exact original CHECK is verified before alteration; instance/database/GUID/run identity and capability checks protect the disposable trial. No shared/production changes.

The service validates the current principal and SQL session, current DEPT_MANAGER/IR_PROCESS/mapping, loaded row_version, pending state, rejection reason and reason for changed deadline. It records decision time and elapsed response time, updates request state, and writes the event, audit and requester notification intent atomically. It revalidates session and authority before commit. No guessed SUBMITTED alias, second authoritative workflow state, automatic assignment or closure.

Requester delivery discovers committed intents, checks current IR_VIEW against the actual request, serializes concurrent delivery on the outbox, and writes one notification/recipient receipt. Source opening checks event/recipient/notification provenance and current read permission again. Retention is the existing configured notification window; events outside it are not rediscovered or delivered. An opt-in InternalRequestNotifications worker is registered (Enabled defaults false); its scheduled loop has not yet been exercised. Existing notification cleanup must account for the new outbox FK before physical purge; this batch does not claim JOB06 retention completion.

The canonical notification source accepts only a positive safe-integer requestId, preserves it through login return validation, and opens that authorized timeline. Back returns to the list; denied refresh removes the timeline.

## Direct evidence

[SQL/service trial3](checkpoint91-trial3-runtime.json) PASS, cleanup=true. Calls use the real SqlSessionService and persisted synthetic session. The test-only current-principal adapter supplies fixture identity; these decision calls are **not public HTTP/browser mutations**.

- Accept and Reject persisted with new row versions, actor attribution, response-time audit and requester intent.
- Principal/session mismatch, revoked mapping, missing rejection reason, unexplained deadline change, stale version and already-decided state rejected.
- Injected outbox failure rolls back status, row_version, event, audit and intent; owned fault trigger removed.
- Two concurrent Accept/Reject calls yield exactly one success, one 412, and one decision event.
- Requester without IR_VIEW receives no notification; granting read permission permits discovery and delivery; repeat delivery is idempotent.
- Source resolves to the current request; revoked IR_VIEW denies opening the historical notification.
- Reused authority checks include missing capability, wrong/inactive department, cross-branch FK, grant/role revocation, concurrent routing/mapping waits and rollback.

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS, 0 warnings/errors; required dotnet test HotelOps.slnx exits0 without discovered tests; actual focused FE13 regression18 PASS. TypeScript/production build PASS with existing Vite use-client/chunk warnings. [Frontend tests](checkpoint91-frontend-tests-final.txt):10 PASS, including deep-link browser navigation, denial privacy and strict notification/return URLs. ModelProbe build PASS with3 pre-existing EF1002 warnings in the credential fault probe. One initial probe compile error (Span across await) was fixed before runtime; it was not a product build failure. No new frontend dependency installation or broad unrelated regression.

WORKFLOWS_COMPLETED=[UC116 service transaction and requester delivery fragments; no whole new UC]
UI_API_SQL_E2E_VERIFIED=[none newly completed; existing credential/FE05/FE11 evidence unchanged]
EXACT_REMAINING_GAPS=[UC116 public read/decision HTTP endpoints with CSRF/ETag, canonical Accept/Reject form and worker/browser acceptance; UC115 offer/assign transition and routing; UC114 pending-request creation; legacy SUBMITTED provenance review; link/progress storage and UC118/117; physical notification/outbox retention integration]
NEW_BLOCKERS=[]; pending/accepted storage and authority no longer require a guessed mapping, but the public mutation path remains closed until the rest of its acceptance is wired.
NEXT_BATCH=[mount UC116 HTTP and UI around the tested service, verify UI/HTTPS/SQL and requester notification; implement UC115 assign handoff/routing with explicit current receiving-department authority; complete UC118 linked operational work]
SPRINT_TIME_REMAINING=approximately23h33m at2026-11-15T18:17Z; deadline2026-11-16T17:50Z unchanged.

Schema verification is disposable-only, not a production migration approval. No complete-UC upgrade based on source integration or passing component tests. Sprint remains active.
