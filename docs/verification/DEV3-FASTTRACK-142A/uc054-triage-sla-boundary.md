# UC054 Triage/SLA boundary — 2026-11-16

UC054 remains PARTIAL_IMPLEMENTATION. New source-complete UCs: 0; new COMPLETE_VERIFIED UCs: 0. This increment implements prerequisites, not the approved Decline workflow.

## Approved decision and actual gap

The Project Leader approved physical maintenance PENDING / Incident OPEN with assignee NULL after Decline, an immutable receipt, atomic audit/notification intent and subsequent UC053 reassignment. ASSIGNED/PENDING_ASSIGNMENT must be supported by valid Triage evidence. Missing evidence cannot be converted into a workflow state by inspecting physical status/assignee alone.

Actual IncidentService.TriageIncidentAsync currently emits TRIAGED plus priority/severity audit only; it does not establish the approved response/resolution SLA deadlines. Its production admission is still separate from this change. A legacy TRIAGED event alone is therefore insufficient to prove the new approved boundary. No existing records are backfilled or labelled Pending Triage from absent history.

## Implemented

- IncidentSla.sql prepares two approved nullable datetime2 columns on incidents, a trusted discovery-time CHECK and V1 marker. Exact disposable server/database/nonzero recovery GUID, migration application lock and ApplyChanges=0 rollback are required. It supplies no target duration, default, data backfill, role/grant or public mutation. Generated EF entities are unchanged.
- IncidentSlaRead reads recorded values only. Missing storage, absent deadlines and inconsistent dates remain distinguishable unavailable results; timestamps are returned as UTC. Maintenance planned start/end never supply SLA values.
- Incident detail exposes SLA under existing Incident admission. Maintenance detail independently checks current Incident visibility and revalidates the SLA result before disclosure; maintenance access does not imply Incident access. UI displays response/resolution separately from plan times and explains unavailable values.
- MaintenanceAssignmentProof correlates the latest TRIAGED event with an exact actor/time/branch Incident audit containing current priority and both recorded SLA deadlines. Missing, stale, malformed or ambiguous evidence fails closed. Pending work also requires no actual start/resolution or historical work interval. An eligible current executor yields ASSIGNED; no assignee with valid proof yields PENDING_ASSIGNMENT; insufficient evidence yields UNVERIFIED.
- Start now rechecks that proof under existing source locks/native ETag before opening an interval. CanStart no longer admits legacy PENDING rows solely from assignee/status. This deliberately makes legacy rows with insufficient Triage/SLA proof unavailable for Start until the upstream workflow is reconciled.

## Verification

- Backend solution build PASS, zero warnings/errors. Solution-level test command exits zero without discovery.
- 28 focused backend tests PASS: deadline normalization, malformed/legacy/mismatched triage evidence, and existing Incident/Maintenance access boundaries.
- Frontend TypeScript/Vite build PASS with existing React Router use-client and bundle-size warnings.
- Four mocked browser submission/evidence/SLA checks PASS. Initial command used the wrong test working directory; the first actual browser run exposed a fixture JSX brace typo. Corrected before the successful run. No production validation was weakened.
- SQL installer and EF query execution NOT_RUN in this increment. Prior turn's disposable pre-login timeouts are historical evidence only. No database mutation, persistent installation, live HTTPS trial or production deployment performed.

## Remaining work, without new business approval

Implement the approved Triage SLA writer using approved configured targets (no invented durations), preserving audit/deadline history and explicit change reasons. Then implement the approved Decline receipt/transaction, independent authorized outcome after assignment removal, durable Lead delivery/source opening, and UC053 reassignment with current eligibility/native version. Add the dedicated UI command and focused rollback/idempotency coverage. Do not promote UC054 merely because these prerequisite changes build.
