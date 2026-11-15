# FASTTRACK-142A checkpoint37 — FE05 committed authority and GBR046

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=4d35394f90486838279cd03411190c5f949d455f
NEWLY_COMPLETED_UCS=0
UC_COMPLETE_VERIFIED=2 (UC044 and UC110, prior checkpoint evidence)
UC045_UC046_UC047=PARTIAL

## Implemented

Request Rework now issues the existing FE05_COMMITTED_V1 authority contract in the same SERIALIZABLE transaction as rework and selected defects. The registered owner-executed issuer writes immutable proof, completed operation receipt and CREATE audit. Audit retains assignee, deadline, reason and selected defects. The backend computes source/evidence digests from actual persisted source and evidence bytes; HTTP clients cannot supply proofs. Missing/untrusted issuer fails closed before creation. No synthetic authority fallback, generated EF changes or relaxed executor checks.

The disposable-only additive installer verifies exact instance/database/GUID/run and caller transaction, refuses existing objects, and registers the actual source commit plus SQL module hash. Existing rework/readiness CHECK constraints remain unchanged. The protected proof/issuer tables deny direct application DML. Installation is a verified trial, not deployment: cleanup removes all trial objects, so this runtime prerequisite must be provisioned under an approved deployment before Request Rework can run outside the trial.

Applied the user's new GBR046 decision: Request Rework and rework-options require active account/employment, valid HK_STAFF, current branch access and matching active HK department, without an active PLANNED shift. Existing EligibleHousekeeper retains the shift predicate for its other callers, including execution/workday guards. Current Failed inspection/cycle, no open rework, failed defect subset, future UTC due time, CSRF, strong ETag and initial/final authorization remain.

## Direct execution evidence

- checkpoint37-build-final.txt: `dotnet build HotelOps.slnx --no-restore` PASS, zero warnings/errors. ModelProbe also built with zero warnings/errors.
- checkpoint37-regression-final.txt: 507 PASS, 11 SKIP; skipped runtime suites are not counted as passed. checkpoint37-authority-tests.txt: 42 PASS, a subset of existing regression coverage.
- checkpoint37-solution-test.txt: solution test command succeeded but the solution contains no test project; not test coverage.
- checkpoint37-inspection-client.txt: 24 PASS. Frontend files were not changed; npm ci/typecheck/production build/auth-host results are historical checkpoint36 evidence, not re-executed results here.
- checkpoint37-rework-browser.json: first authority-enabled browser -> trusted HTTPS -> disposable SQL trial PASS for normal and blocked rooms, with an active shift fixture.
- checkpoint37-authority-browser-retry.json: approved no-shift fixture PASS in both room variants, including restricted SQL reader, tampered-source/disabled-issuer/unauthorized denial and negative eligibility checks.
- checkpoint37-authority-final-retry.json: final source/probe PASS in both variants. The probe additionally tests a role assigned to a different synthetic branch, non-HK department, revoked branch access, inactive account/role, and idempotent issuer replay with exactly one audit and operation receipt. Creation is verified through actual UI/HTTPS; private authority-reader and eligibility checks are real SQL component verification, not HTTP executor acceptance.
- checkpoint37-authority-browser.json and checkpoint37-authority-final.json retain FAILED attempts at login. API output identified SQL command timeouts in permission lookup and session/audit persistence (30-second timeout). No authentication bypass, TLS bypass or timeout increase was used. Subsequent unchanged-code retries passed; intermittent environment latency remains a limitation.
- checkpoint37-cleanup.txt: independent exact GUID match=1; users/branches/reworks/defect links/audits/notifications/recipients/operations all zero; proof/registry/procedure/role/reader user absent; authority marker zero. Parent fixture rehearsed cleanup with rollback before committed setup, and eligibility/reader negative mutations rolled back. Owned HTTPS host stopped.

## Business completion and remaining blockers

Verified sequence: inspect Failed -> canonical Request Rework options -> select official original cleaner without a shift -> Pending Acceptance -> persisted defects/audit/committed authority -> duplicate rejection. Effective block remains BLOCKED/MSG17. Existing Reject fanout/revocation/retry checks also ran successfully.

This is not the complete rework lifecycle. Assignee notification remains NOT_INTEGRATED; no claim of MSG09 delivery. Source-opening authorization, full creation rollback/concurrency acceptance, canonical executor composition and reinspection remain open. Runtime executor still has the provisional database/config gate, completion notification gate, and existing schema CHECK incompatibilities for later lifecycle states. These protections were not removed. The current integrated-security host is privileged; least-privilege proof consumption was verified using a disposable SQL user without login, not by weakening the reader's direct-DML-denial check.

Runtime EF mapping and unordered Take warnings remain. No shared HotelOpsDB mutations, production seed/deployment, dependency downgrade or security relaxation.

## Next highest-impact coding

1. Integrate durable post-commit assignee delivery with current source-read authorization, deduplication and retry; retain honest NOT_INTEGRATED until proven.
2. Compose canonical Accept/Start/Complete using the committed authority contract, narrow reviewed disposable schema changes and unchanged shift/workday guards.
3. Complete reinspection assignment/conclusion and verify full Reject -> Rework -> Reinspect with effective blocks and failure rollback before marking UCs COMPLETE_VERIFIED.

## Changed paths and delivery

Application: HousekeepingWorkflowController.cs, HousekeepingAssignmentsService.cs, InspectionReworkOptionsService.cs, new ReworkAuthorityIssuer.cs.

Verification: SprintApprovalBrowserProbe.cs, SprintReworkFixture.cs, new SprintReworkAuthority.sql/SprintReworkAuthorityFixture.cs/SprintReworkAuthorityProbe.cs; approved decisions, UC matrix and checkpoint37 artifacts.

Logical source commits: 5a6a9ec (transactional issuer), a6cae59 (GBR046), 221d3e9 (scope/replay verification and approved contract). Remote fetched before delivery: local ahead3/behind0 at that point. Final evidence commit and non-force push identity are reported in the delivery receipt.
