# UC120 material completion decision

2026-11-16: DEV3 approved both FULLY_ISSUED and REMAINDER_CLOSED as valid
terminal material outcomes. Other states remain blockers for mandatory links.

The existing Complete read/command now evaluates material evidence in the caller's
Serializable transaction. The evidence reader verifies same-branch request,
department and warehouse; latest independent approved cycle and exact line snapshot;
fulfillment-to-posted-ISSUE correlation and cumulative quantities. Full issue requires
all approved quantities fulfilled and matching completion time. Remainder closure
requires issued quantity plus open remainder, assigned approver, reason, timestamp
and matching existing business audit. Missing schema/history fails closed.

The existing fulfillment reconciliation validator is reused without changing its
behavior. No new permissions, mutation endpoints, schema or database writes were added.
IR_VIEW, mapped IR_PROCESS, CSRF/ETag, closure/audit/outbox transaction stay intact.
Material identifiers/details are not disclosed by the completion prerequisite.

Build: `dotnet build HotelOps.slnx --no-restore` PASS, zero warnings/errors.
Tests, HTTP and business SQL acceptance: NOT RUN per user instruction.
Read-only recovery metadata inspection found the legacy material schema lacks the
new approval/remainder capability; this database cannot verify these paths yet.
No shared database mutation. Whole UC120 remains PARTIAL: independently authorized
linked-material display, legacy reconciliation and Human QA remain outstanding.
