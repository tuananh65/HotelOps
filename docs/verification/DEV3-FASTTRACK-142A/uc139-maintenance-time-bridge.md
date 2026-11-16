# UC054/055/058/139 Maintenance interval bridge increment

Source increment only. Newly completed UCs: 0. UC139 remains PARTIAL_IMPLEMENTATION.

## Implemented

The existing corrective Maintenance command transaction now takes actor/owner time locks before Incident/Maintenance locks, and rechecks discovered ownership after the wait. Start and Resume persist the source transition and open a work interval in that same transaction; failures roll back the source transition. Hold, Submit and cancellation of running work close its exact current interval before changing the source state. Cancellation of a non-running work refuses an orphan open interval. Server SQL time is used, with Start/Submit timestamps aligned to the opened/closed interval boundary. Resume preserves historical ActualStartAt.

The shared time participant recognizes MAINTENANCE keyed by maintenance_works.maintenance_work_id. It independently checks current assignee, IN_PROGRESS work/Incident, branch/location coherence and absence of preventive occurrences. It retains the existing current Working workday, single open interval, eligibility, ETag, audit and caller transaction checks. Existing work_intervals.work_item_kind is varchar(24), with no kind CHECK in the verified disposable schema; no schema/entity change or data migration was needed. No historical interval is inferred from maintenance_work_logs.spent_minutes.

Missing or inconsistent legacy intervals fail closed. Work-time errors are translated into sanitized existing authorization-boundary statuses (403/409/412/503) after transaction disposal. Existing RequireEntry, request/principal binding, before-persistence and final authorization remain mandatory. No production mutation contract or permission grant was added. The Workday read decoder recognizes MAINTENANCE so an existing interval can be displayed, while End/Pause All remains blocked for that source until its authorized adapter is implemented.

## Direct evidence, 2026-11-16

- Backend build PASS, zero warnings/errors.
- Frontend TypeScript/Vite PASS; existing React Router directive and large-bundle warnings remain.
- Existing MaintenanceReadAccess regression tests: 5 PASS; these prove read-policy regression only, not interval writes.
- Scheduled Workday transport regression: 78 PASS. Own Workday read transport: 45 PASS. These are mocked transport checks, not SQL workflow acceptance.
- dotnet test HotelOps.slnx --no-restore: exit 0 with no discovered tests, not counted as coverage.
- Read-only SQL identity/schema verification on the same approved disposable recovery database: PASS. No SQL mutation or schema installation in this increment.
- Actual Maintenance service -> SQL lifecycle/rollback and browser -> HTTPS -> SQL: NOT_RUN.

## Required next work

Maintenance mutations are still absent from the compiled production Dev2ActionContracts registry. Do not use a demo policy or client ActorUserId to open them. Production source authorization, caller ETag/idempotency and lifecycle/UI wiring must be completed before production admission.

Return currently restores IN_PROGRESS under the reviewer's action without opening an executor interval. It must gain an explicit executor re-execution boundary; no effort may be attributed to a technician merely because a reviewer returned work. Preventive occurrence work remains separate. UC139 still needs the Maintenance END_OF_SHIFT adapter and receipt, reconciliation of legacy running work, and runtime acceptance. The existing End guard prevents claiming the day ended while Maintenance remains running.
