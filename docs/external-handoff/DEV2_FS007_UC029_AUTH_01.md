# DEV2 → DEV1 | FS-007 Notification V1 Approval Response & UC029 Implementation Authorization

**Project:** HotelOps  
**From:** DEV2 – Notification / Shared Integration  
**To:** DEV1 – Room Operations  
**Date:** 06/11/2026  
**Target branch:** `feature/dev1-room-ops`  
**DEV1 baseline:** `a5b3f66e9732ff00035f0f5fc43971322e0122e6`  
**Reference:** `docs/fs007/DEV2_NOTIFICATION_APPROVAL_REQUEST.md`

**Decision:** FULL IMPLEMENTATION AUTHORIZATION FOR UC029 — CONTRACT ACCEPTANCE REMAINS EVIDENCE-GATED.

---

## 1. Overall Decision

DEV2 acknowledges DEV1's FS-007 completion status and the current blocking dependency for UC029 – Assign Cleaning Task.

DEV2 authorizes DEV1 to proceed with full-stack development and mandatory Notification V1 integration for UC029 without requesting permission at each coding checkpoint.

This authorization includes:

- Backend implementation and modification.
- Frontend implementation and integration.
- Feature-specific Notification adapter.
- Cleaning Assignment event/payload integration.
- Recipient resolution and source-read authorization adapter.
- Idempotency and replay integration.
- Transaction coordination.
- Required unit and integration tests.
- Development-branch implementation of proposed B01/B02/C01 changes.
- Commit, push and Pull Request creation.

DEV1 may modify shared source code where necessary to prepare an integrable implementation, provided that exact diffs and compatibility evidence are supplied before shared changes are merged.

**Authorization does not imply that unverified infrastructure, database migrations, runtime behavior or shared contract changes are already approved.**

Mandatory Notification remains a hard acceptance requirement for UC029.

## 2. Approval Status Matrix

| Item | Status | Decision |
|---|---|---|
| A01 Publisher interface | APPROVED | Previously accepted under COORD-FINAL-01; implementation source still required |
| A02 Publisher implementation | BLOCKED | Exact executable implementation and commit not verified |
| B01 Notification architecture | APPROVED | Three-table architecture and caller-owned transaction principles accepted |
| B01 Executable DDL | REVISE | Actual schema, constraints, migration and runtime evidence required |
| B02 EF Core mapping | REVISE | SQL/EF parity and additive mapping evidence required |
| C01 Commit guard | REVISE | Physical transaction, persisted guard and bypass coverage required |
| C01 DI integration | REVISE | Scoped lifetime, registration and compatibility verification required |
| UC029 logical event | REVISE | Proposed event must be reconciled with actual catalog |
| Immutable payload | APPROVED IN PRINCIPLE | Required facts agreed; exact executable schema pending |
| Recipient resolution | APPROVED IN PRINCIPLE | Target persisted assignment assignee only |
| Source-read authorization | REVISE | Real adapter and denied/revoked tests required |
| Transaction boundary | APPROVED IN PRINCIPLE | Mandatory staging inside caller-owned physical transaction |
| Idempotency/replay | REVISE | Semantic requirements agreed; implementation proof pending |
| Delivery/retry behavior | APPROVED IN PRINCIPLE | Post-commit retry without rolling back assignment |
| Full UC029 implementation | AUTHORIZED | DEV1 may code, test, commit, push and create PR |
| Shared infrastructure merge | BLOCKED | Requires joint review and acceptance evidence |
| Production DDL/deployment | BLOCKED | Requires separate owner approval |

All approvals above are limited to Cleaning Assignment integration. They do not expand to unrelated Notification producers or other HotelOps modules.

---

## 3. A01 — Publisher Interface and A02 Implementation

**Status: A01 APPROVED / A02 BLOCKED**

The existing COORD-FINAL-01 decision remains authoritative for the A01 interface.

DEV1 must reuse the accepted publisher interface and must not create a competing publisher contract merely because its implementation is absent from the currently inspected baseline.

However, DEV2 cannot claim executable implementation acceptance without identifying the actual source.

### Required DEV2 implementation handoff

The integration package must identify:

- Exact source branch and commit SHA.
- Interface source path and implementation path.
- Blob hash and SHA256 where available.
- Namespace and method signatures.
- Constructor dependencies and DI lifetime.
- Reserve operation method.
- Resolve/replay method.
- Mandatory publication staging method.
- Durable operation completion method.
- Result and exception contracts.
- Physical transaction participation behavior.

The exact API signatures must come from the accepted implementation. They must not be invented by DEV1.

### Acceptance criteria

- A01 matches the accepted shared interface.
- A02 implementation compiles and resolves through DI.
- DEV1 can call publisher methods from the existing scoped DbContext/transaction.
- Mandatory staging is durable before commit.
- No second publication implementation or independent transaction is created.

**Decision:** DEV1 may prepare its adapter now, but actual A01 invocation must be wired against verified interface source.

---

## 4. B01 — Notification Schema

**Status: APPROVED ARCHITECTURE / REVISE EXECUTABLE DDL**

The accepted Notification V1 architecture includes:

1. `notification_operation_reservations`
2. `notification_publications`
3. `notification_delivery_intents`

Only `notification_delivery_intents` acts as the retry queue.

No fourth queue, reservation purge or V1 historical backfill is authorized.

The retained SQL trial reported zero of these three tables. Therefore, no prior physical-schema acceptance may be assumed.

### Required schema guarantees

**Operation reservations:**
- Stable actor + operation GUID uniqueness.
- Versioned canonical request fingerprint.
- Durable completed authorized result.
- Concurrency-safe replay resolution.
**Publications:**
- Immutable publication identity.
- Source event identity and version.
- Unique source-transition publication semantics.
- Transactional relationship with the business operation.

**Delivery intents:**
- Unique publication + recipient + channel + source-transition identity.
- Delivery status, claim/version and attempt tracking.
- Retry scheduling and failure evidence.
- Durable linkage to the correct inbox recipient.

### Database integrity

The schema must reject:

- Duplicate delivery intents.
- Forged DELIVERED states without valid inbox linkage.
- Cross-recipient or cross-channel linkage.
- Invalid nullable pre-delivery references.
- References to nonexistent or non-unique SQL keys.

Any composite FK must reference an actual unique key existing in the target schema.

### Acceptance criteria

- Forward, verify and recovery SQL scripts.
- Idempotent installation.
- Partial-version refusal.
- Actual SQL metadata compatibility report.
- Real SQL constraint tests.
- Rollback and concurrent insertion tests.
- Explicit owner approval before shared database writes.

**Decision:** DEV1 may prepare and test proposed migrations in an isolated environment. No shared or production DDL execution is authorized by this response.

---

## 5. B02 — EF Core Mapping

**Status: REVISE**

DEV1 is authorized to prepare the necessary application-side integration and proposed additive EF mapping.

### Acceptance criteria

- Exact SQL-to-EF table/column parity.
- Correct primary, unique and foreign key mapping.
- Correct nullability and value generation.
- Correct insert and concurrency behavior.
- No unnecessary modifications to generated entities.
- No forced navigation or principal-key changes to NotificationRecipient.
- Exact file/hunk/hash inventory.
- Shared mapping compatibility reviewed by both DEV1 and DEV2.

The existing SQL Server Database First approach must be preserved.

**Decision:** Implementation permitted on a feature branch. Shared mapping acceptance requires evidence.

---

## 6. C01 — Commit Guard and DI

**Status: REVISE**

The mandatory Notification guard must protect the authorized UC029 business producer, not every database write globally.

### Guard requirements

Before the outer transaction commits, the implementation must prove that:

- The authoritative Cleaning Assignment mutation exists.
- The expected assignment identity and source transition match.
- The operation reservation is durably completed.
- The mandatory publication is staged.
- The required delivery intent is persisted.
- All required records belong to the correct physical transaction.

The guard must not trust in-memory counters or flags as proof of database persistence.

### Required coverage

- Synchronous and asynchronous SaveChanges.
- DbTransaction commit paths.
- Raw ExecuteSql/ExecuteUpdate/ADO.NET paths.
- Unsupported bypass rejection where complete interception is impossible.
- Rollback and savepoint paths.
- Cancellation and exception paths.
- Ambiguous commit and durable resolution.
- Wrong transaction or DbContext participation.

A SaveChanges interceptor alone is not sufficient to establish full transaction-commit protection.

### DI requirements

- Same scoped DbContext and transaction participation.
- No competing DbContext registration.
- No circular dependency.
- No replacement of authentication/session services.
- Exact Program.cs registration diff.
- Dispatcher ownership stays with DEV2 shared infrastructure.

**Decision:** DEV1 may implement and test the integration, but shared guard activation remains subject to joint review.

---

## 7. UC029 Event Catalog and Immutable Payload

**Status: REVISE — PROPOSED CONTRACT ACCEPTED FOR IMPLEMENTATION**

Proposed logical event:

`CLEANING_TASK_ASSIGNED`

Schema version: V1 candidate.

The event must be reconciled against the actual approved notification type catalog before database seeding.

### Required immutable event facts

| Field | Requirement |
|---|---|
| OperationId | Stable GUID |
| PublicationId | Server-generated durable identity |
| ActorId | Actual authenticated actor |
| BranchId | Authoritative branch |
| TaskId | Cleaning task identity |
| TaskCycleId | Current cycle identity, where available |
| RoomId | Source room |
| AssignmentId | Persisted CleaningAssignment identity |
| AssigneeUserId | Persisted assigned employee |
| AssignedAtUtc | Server timestamp |
| SourceRevision | Authoritative assignment transition revision |
| DueAtUtc | Due time, when applicable |
| Reason | Only if normalized and approved |

No session tokens, credentials, raw headers or unrestricted employee data may be published.

### Acceptance criteria

- Exact catalog identifier and schema version.
- Canonical serialized payload.
- Source-transition uniqueness.
- Correct persisted AssignmentId and revision.
- Verified relationship with the committed business action.

---

## 8. Recipient Resolution and Source-Read Authorization

**Status: RECIPIENT PRINCIPLE APPROVED / SOURCE-READ IMPLEMENTATION REVISE**

### Recipient policy

- Recipient = AssigneeUserId of the committed CleaningAssignment.
- Channel = IN_APP, subject to existing schema compatibility.
- One assignment event must not broadcast to department staff.
- A retry must not resolve recipient through the latest assignment.
- Department.ManagerUserId must not be used as fallback.

### Authorization

The notification must not grant access to the underlying Cleaning Task.

The implementation must independently evaluate recipient identity and current source-read authorization.

The worker must not rely on the original producer HttpContext.

### Terminal conditions

- SUPERSEDED
- RECIPIENT_INACTIVE
- RECIPIENT_NOT_FOUND
- RECIPIENT_OUT_OF_SCOPE
- SOURCE_READ_DENIED
- SOURCE_NOT_FOUND

Transient authentication, database or source-service outages must remain retryable instead of being incorrectly classified as permanent denial.

### Supersession
A later reassignment transition may supersede a previous assignment event according to an explicit source-specific predicate.

Unrelated Cleaning Task progress updates must not automatically invalidate the original assignment recipient fact.

### Acceptance criteria

- Real source-read adapter.
- Explicit recipient identity resolution.
- Recipient state and scope checks.
- Denied/revoked tests.
- Reassignment supersession tests.
- No unauthorized source-data disclosure.

---

## 9. UC029 Transaction Boundary

**Status: APPROVED IN PRINCIPLE**

The existing integration point is:

`HousekeepingWorkflowController.AssignCleaning`

→ `RoomTypeCreationService.AssignCleaningAsync`

→ `RoomTypeBatchService.BatchAsync`

The existing batch service owns the Serializable transaction and associated business coordination.

The mandatory Notification hook must be called within this transaction, after persisted assignment identity, source revision and audit are known, and before the outer commit.

### Required sequence

1. Authenticate and authorize the actor.
2. Acquire required existing coordination locks.
3. Revalidate user, branch, source and assignment eligibility.
4. Resolve operation reservation/idempotency.
5. Apply the Cleaning Assignment business transition.
6. Persist assignment and audit.
7. Determine authoritative publication facts.
8. Stage mandatory publication and recipient intent.
9. Complete durable operation result.
10. Validate transaction commit guard.
11. Commit the existing physical transaction.
12. Dispatch delivery asynchronously after commit.

This is a logical sequence, not a newly approved universal SQL lock-acquisition order. The exact reservation-versus-business-lock sequence must still be jointly verified.

### Failure policy

If mandatory publication, intent staging, audit or durable completion fails before commit:

**ROLLBACK THE ENTIRE BUSINESS TRANSACTION.**

No successful assignment response may be returned.

If notification delivery fails after the business transaction commits:

**KEEP THE ASSIGNMENT COMMITTED AND RETRY DELIVERY DURABLY.**

Do not execute network delivery inside the business transaction.

---

## 10. Idempotency, Replay and Failure Behavior

**Status: REVISE — SEMANTICS ACCEPTED**

DEV1 may implement the following UC029 idempotency semantics.

### Stable request identity

Use a stable client `Idempotency-Key` GUID.

The canonical fingerprint must bind:

- Operation/action version.
- Actual session actor.
- Branch and task identity.
- Assignee identity.
- Due UTC.
- Normalized reason.
- Adoption flag = false.
- Original If-Match value.

### Replay behavior

**Same key + same canonical command, already committed:**

- Reevaluate current permissions/source-read authorization.
- Return the original durable authorized business result.
- Do not repeat the assignment transition.
- Do not create a second publication.
- Do not reject solely because the original If-Match is now stale.
**Same key + different command:**

Return HTTP 409.

**New key:**

Apply normal current state, eligibility and concurrency checks.

**Ambiguous commit:**

Resolve against durable reservation state. Never infer success from an in-memory attempt.

**Revoked actor:**

Do not expose historical source data solely because a reservation exists.

### Required tests

- Identical replay.
- Conflicting replay.
- Concurrent same-key requests.
- Post-commit retry.
- Rollback before completion.
- Ambiguous commit.
- Permission revocation.
- Missing source.
- Cancellation and restart.

---

## 11. Lock Coordination and Concurrency

**Status: BLOCKED PENDING REAL SQL ACCEPTANCE**

No new universal lock-order rule is approved.

The exact sequence involving reservations, Users, sessions, Branch, feature locks and authoritative source rows must be reconciled with existing DEV1/DEV2 coordination protocols.

### Acceptance criteria

- Document lock acquisition order.
- Verify existing UserEligibilityLock compatibility.
- Test competing writers in both orders.
- Test concurrent revoke/deactivation against assignment.
- Test stale DbContext and final authoritative revalidation.
- Test rollback restoration.
- Verify compatibility with global-admin and shared-writer coordination.
- Reject SQL Server deadlock 1205 as an acceptance PASS.

DEV1 may build the implementation, but concurrency acceptance must be demonstrated before integration merge.

---

## 12. Frontend Integration Authorization

**Status: AUTHORIZED**

DEV1 is authorized to implement the complete UC029 frontend workflow using the repository's existing shared frontend conventions.

DEV1 must inspect and reuse the actual frontend implementation rather than assume a framework or introduce a separate UI architecture.

### Required frontend behavior

- Assign Cleaning Task form.
- Eligible assignee selection.
- Existing permission/CSRF handling.
- Existing version/concurrency contract.
- Stable idempotency key for safe request retries.
- Assignment result display.
- Notification state presentation where supported.
- Refresh of assignment/task state after successful commit.
- Appropriate pending-delivery display without claiming notification was delivered.

Frontend success must reflect committed business state, not merely successful notification enqueueing.

Existing GET assignment-options and UC032 behavior must remain compatible.

---

## 13. Responsibilities and Delivery Ownership

### DEV1 owns

- UC029 business producer.
- Existing Cleaning Assignment transaction integration.
- Persisted assignment facts.
- Feature adapter invoking accepted A01.
- Source-read and supersession adapter in coordination with DEV3.
- Frontend UC029 integration.
- Feature-specific unit and integration tests.
- Feature branch, commits and PR preparation.

### DEV2 owns

- Existing accepted A01/A02 publisher implementation package.
- Notification operation reservations and publication store.
- Delivery intents, inbox and dispatcher/retry infrastructure.
- Shared EF/schema compatibility.
- Shared commit-guard integration.
- Notification infrastructure evidence and acceptance.
- Joint review of shared source changes.

DEV1 may propose and implement patches for DEV2-owned shared infrastructure on an isolated development branch, but ownership and final shared merge approval do not transfer automatically.

---

## 14. Implementation and Git Permissions

**Status: FULL IMPLEMENTATION AUTHORIZATION**

DEV1 may immediately:

- Modify UC029 feature-local source.
- Prepare required shared integration changes.
- Add tests and migrations in a development branch.
- Run isolated development tests.
- Commit and push to its authorized branch.
- Create Pull Requests.
- Submit evidence for joint acceptance.

DEV1 does not require DEV2 permission for each internal coding change.

However, this authorization does not grant GitHub Admin permissions, bypass branch protection, permit unauthorized shared database writes or approve production deployment.

The following still require explicit acceptance:

- Shared schema deployment.
- Shared commit-guard activation.
- Breaking shared contract changes.
- Merge of unverified shared infrastructure.
- Production rollout.

---

## 15. Required Implementation Package and Evidence

Before marking UC029 mandatory Notification integration ACCEPTED, the combined delivery must contain:

1. Exact source branch, commit SHA and changed-file inventory.
2. A01/A02 executable source references.
3. B01 forward/verify/recovery SQL.
4. B02 EF-to-SQL compatibility report.
5. C01 DI/guard diff and bypass tests.
6. Event catalog and payload contract.
7. Source-read adapter and supersession tests.
8. Idempotency and ambiguous-commit tests.
9. Real SQL race, rollback and lock-order evidence.
10. Dispatcher retry, claim, crash/restart tests.
11. Real authorized recipient inbox-read proof.
12. Denied/revoked recipient and source authorization proof.
13. Backend/frontend build and regression results.
14. PR review and shared-change approval receipts.

A missing implementation must be reported as UNKNOWN or BLOCKED, not represented as accepted.

---

## 16. Final DEV2 Decision

**UC029 DEVELOPMENT: AUTHORIZED**

DEV1 may proceed with full-stack implementation without waiting for repeated per-step approval.

**A01 INTERFACE: PREVIOUSLY ACCEPTED**

DEV1 must reuse the accepted interface once its exact implementation source is supplied.

**B01/B02/C01: IMPLEMENTATION AUTHORIZED, EXECUTABLE ACCEPTANCE PENDING**

DEV1 may prepare and test changes, but shared infrastructure deployment and merge remain gated.

**MANDATORY NOTIFICATION: NOT WAIVED**

UC029 cannot be declared fully accepted or merged as complete while mandatory Notification remains unimplemented or unverified.

**NO OUT-OF-SCOPE APPROVAL**
This response does not approve unrelated Room Operations features, Notification producers, RBAC changes, authentication changes, global infrastructure rewrites, production migrations or deployments.

**Checkpoint:** `DEV2-FS007-UC029-AUTH-01`  
**Checkpoint conclusion:** DEVELOPMENT UNBLOCKED / SHARED CONTRACT VERIFICATION PENDING / FINAL ACCEPTANCE BLOCKED.

**Next action:** DEV1 proceeds with implementation preparation while DEV2 supplies the exact accepted A01/A02 executable package and revised shared Notification infrastructure evidence.