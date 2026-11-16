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

The request history now refreshes after confirmed handover Submit/Acknowledge/Reject,
linking a task/cleaning task/incident, or creating a linked task. Refresh invalidates
the old timeline and unmounts its completion panel immediately, then reloads current
authority, responsibility, linked work and completion eligibility. A confirmation is
retained only for the same request/session after authorized history reload. Existing
task assignment navigation, CSRF, ETag and independent target authorization remain.

Frontend `npm.cmd run build` (TypeScript + Vite): PASS. Vite still warns about ignored
React Router `use client` directives and a chunk above 500 kB. No test suites, browser,
API or SQL business checks run for this refresh change, as requested. No backend or
database changes. This integration repair does not promote UC120 to complete.
