# Task81 — DEV1 → DEV2: UC138 notification/replay contract request

**REVIEW REQUEST ONLY. Not an approved interface or implemented integration.**
Local handoff; no message sent externally. Existing Task79 draft checkpoint is unchanged:
branch `feature/DEV2-coordination-contracts`, HEAD4a187320643d0c82f232c6b9dd5a667b8401c280;
draft `scripts/verification/Coordination/NOTIFICATION_DELIVERY_RETRY_CONTRACT_DRAFT.md`,
blob dc5ceb10121f752351e56e4b7150277c96271e1c. User confirmed DRAFT_READY_FOR_JOINT_REVIEW;
INotificationPublisher/outbox/replay adapter/worker not implemented. Draft contents unavailable
locally; no claim they were read, approved or implemented. No further snapshot search needed.

## Required shared API facts, before DEV1 writes a production adapter

Provide actual namespace/signatures, source commit and DI lifetime for publisher and durable
operation replay. Required capabilities (not invented INotificationPublisher signatures):

- Enqueue durable event/recipient intent using **caller's same scoped HotelOpsDbContext and
  SQL transaction**; no inner commit, detached context, network send or ambient transaction.
- Stable operation lookup/reservation/completion with request fingerprint and original business
  result identity, or explicitly identify another approved owner. Deduplicating delivery does
  not make business assignment mutation replay-safe. No operation store is invented in DEV1.
- Event and inbox identity, recipient eligibility/failure policy, retry/backoff/poison recovery,
  retention and restart behavior; approve exact event registry/type mappings.

## UC138 event facts to review

Logical event purposes: assignment created, changed/reassigned, cancelled; and a template edit
if the approved propagation policy changes existing assignments. Names are proposals only,
not new seeded NotificationType values. Exact serialized schema/version needs joint approval.

| Field | Authoritative source / restriction |
|---|---|
| operationId / eventId / eventKind / schemaVersion | Stable server-understood operation key and publisher identity; same logical event on retry |
| actorUserId / branchId / departmentId | Actor exclusively ICurrentUserAccessor + fresh SQL session; branch/department from scoped authoritative records |
| shiftTemplateId / shiftId / assignmentId / revision | Committed materialized source IDs and actual SQL rowversion; not client claims |
| affectedEmployeeIds | Derive from before/after persisted assignments, deduplicate. Cancel targets original employee; reassignment old/new recipient semantics PENDING_DECISION, not role broadcast |
| old/new scheduled UTC windows / assignment state / reason | Approved transition facts; history preserved, no salary/attendance calculation |
| occurredAtUtc / action path | Server time and permission-protected source path; no unauthenticated resource link |

No password, cookie, session credential or raw storage key in payload. Department Head is an
actor scope issue, **not** the UC138 recipient: notifications go to affected employees. Do not
reuse UC139's unresolved branch-DH recipient lookup for this different use case.

## Atomicity, locking and replay

Proposed order: discover affected users provisionally -> sorted actor/employee User locks ->
re-read permission/session/eligibility -> source/template/assignment and overlap range locks
in deterministic order -> durable replay ownership -> writes/audit/intents -> one outer commit.
Actual publisher/replay lock order must be jointly reconciled; if replay must precede source
locks, amend the explicit protocol before integration. Do not guess a lock-resource name.
If affected lock set changes during discovery, rollback/retry; never extend A1's fixed lease
out of order. Cancel/Edit races with UC139 Start must serialize on the same affected User locks.

Business mutation, original-result replay record, audit and durable intents commit together.
Any mandatory failure rolls everything back. Same key/same request returns the original result;
same key/different normalized payload conflicts. Retry after assignment changed/cancelled must
not create a second assignment or send another event for the same revision. Concurrency,
retention and payload-conflict behavior need approved SQL-backed evidence.

## Delivery is a separate acceptance boundary

Enqueue/outbox insert alone is **not** notification delivery. Required evidence:

1. Recipient can authenticate and read the durable inbox item with source authorization.
2. Transient dispatcher failure/restart retries without loss or duplicate readable messages.
3. Same event re-published/replayed is idempotent; distinct revisions remain distinct events.
4. Inactive/revoked/cross-branch recipient policy is explicit; terminal failure observable,
   not silently called delivered. Business outcome on delayed delivery needs approved semantics.
5. Business/audit/intent failure or outer rollback leaves no publishable intent/inbox message.
6. SQL writer/dispatcher lock ordering is compatible; no hidden commit or out-of-transaction send.

## Ownership and resumption

DEV1: UC138 request validation, own module writers/readers, SQL session/PermissionService scope,
employee/branch/department references, overlap/concurrency, source audit, event facts and adapter.
DEV2: publisher/outbox/replay implementation, delivery/retry/worker/inbox and observability.
Shared: schema/versioned contract, DI integration and consistent SQL lock protocol require
specific review; no DEV2 source changes by this task.

Supply joint-approved contract + implementation commit + disposable SQL/HTTPS acceptance
evidence. Then DEV1 finalizes only the bounded adapter/DI diff for approval. Until then C1-C
is BLOCKED, public UC138 writers remain NO_GO, UC138 NOT_IMPLEMENTED, UC139 BLOCKED_EXTERNAL.
