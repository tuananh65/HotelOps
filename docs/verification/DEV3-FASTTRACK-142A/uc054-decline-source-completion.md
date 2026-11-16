# UC054 — Accept/Start and Decline source completion

Status: IMPLEMENTED_PENDING_QA. Newly source-complete UC: UC054. NEW_COMPLETE_VERIFIED=0.

Scope: SRS 6.6 / GBR-012, 072, 136–138 and Project Leader's approved Decline decision. UC053 assignment/reassignment remains a separate, gated dependency; the complete Triage → Assign → Decline → Reassign chain is **not** runtime-verified or claimed complete here.

## Implemented source

- Existing authorized maintenance detail shows Incident evidence and SLA deadlines with independent Incident read authority. Pending workflow now requires matching Triage event/audit/current SLA and an eligible current assignee, never physical status alone. Incident detail distinguishes proven Pending Triage, Pending Assignment and Assigned; inconsistent history is UNVERIFIED.
- Accept/Start retains current executor/session/branch/grants, native If-Match, Working workday and single interval guards. It atomically records start/acceptance time, Incident/work transition, work interval and audit. Prior execution/log/review evidence now also prevents a false initial assignment projection.
- Dedicated `POST /api/maintenance-works/{id}/decline` requires HTTPS, global CSRF, current unrestricted SQL session and independently scoped MAINTENANCE_EXECUTE/MAINTENANCE_VIEW. Actor is taken from the session, not request data. Native ETag, matching Triage proof, current assignment and absence of execution are checked under User → Incident → Work locks in a Serializable transaction.
- Decline requires a trimmed 10–2000 character reason, clears only the assignee, preserves physical PENDING/OPEN, SLA and blocks, and saves audit, immutable receipt and durable notification intent together. Receipt contains actor/previous assignee, incident/work, consumed assignment version, server timestamp, reason and audit FK. Unique work/version prevents duplicate events. Failure rolls back all writes; no automatic retry. Response contains acknowledgement only, not continued work access for the former assignee.
- Receipt history is returned only through current scoped work detail. Dedicated confirmation UI sends CSRF + If-Match, rejects stale response, never retries ambiguous writes, and is hidden from read-only workspace and unauthorized/non-assignee users.
- Existing notification worker now discovers committed Decline intents, revalidates active Maintenance Lead + MAINTENANCE_VIEW/branch/source scope, deduplicates event/recipient under a transaction lock and retries within configured retention. Source opening rechecks current session/scope and derives the maintenance route; no stored URL authority or INCIDENT_VIEW expansion. Worker enablement is unchanged.
- `MaintenanceDecline.sql` is additive, versioned and identity guarded. It changes no generated entities, physical status CHECKs, grants or role IDs. Receipt UPDATE/DELETE are rejected. Its audit FK is a new JOB06 archive dependency: do not purge referenced audit until a compatible reviewed archive/finite-retention policy exists.

## Direct validation in this run

- Backend build: PASS, 0 warnings / 0 errors.
- Solution test entry point exits successfully but discovers no test project; not counted as a test pass.
- Focused regression project: 25 PASS (Triage, assignment evidence, Incident read authorization, Decline reason/payload). These are not SQL authorization/concurrency tests.
- Browser with mocked HTTP: 4 PASS (2 Triage and 2 Decline). Confirms explicit confirmation, CSRF/version, no client actor claim for Decline, stale rejection and single submit. Not HTTPS → API → SQL E2E.
- Frontend typecheck/production build: PASS; existing React Router use-client and bundle-size warnings remain.
- Initial sandbox SQL connection failed with Windows credential/TLS provider errors. Authorized host-context retry with encryption enabled verified `meita\MSSQLSERVER01`, disposable `HotelOps_DEV3_142A_Recovery_2b221f40`, GUID `5270D07C-7C27-477B-A60E-86485ADD7FAD`.
- Decline installer ApplyChanges=0 preview: PASS. SLA/Triage/Decline installers each executed twice inside one outer transaction: PASS; outer rollback and comparison of original object/column/capability identities: PASS (`INSTALL_TWICE_AND_ROLLBACK_CLEANUP_PASS`). No persistent schema changes, seeds, grants or business-data mutations. No shared/production access.

## Human QA / activation dependencies

Install reviewed capabilities on the approved target and supply approved SLA targets; no durations are invented. Exercise actual authorized Start/Decline, stale/replayed requests, concurrent Start/Decline/Reassign, revoked role/grant, rollback fault, unchanged SLA/blocks, immutable history, Lead delivery/retry/source opening and old-assignee denial. UC053 must provide authorized assignment/reassignment; its current public mutation gate is not opened by this change. UC052 cancellation alternative remains incomplete. Full business runtime acceptance is NOT_RUN.

Changed source groups: Controllers/MaintenanceDeclineController; DTOs/Incidents and Maintenance; Services/Incidents (Triage/projection), Maintenance (proof/access/Decline), Dev2Authorization (Triage/read contracts), Notifications (Decline source/delivery/worker/opening); Program registrations; frontend Triage/Decline/detail/host; guarded SQL installers; focused tests; this receipt and UC matrix. Existing uncommitted Triage work was preserved and completed as the upstream evidence writer.
