# UC100 creation notification and shortage selection

2026-11-16. TASK_ID=DEV3-FASTTRACK-142A. No new COMPLETE_VERIFIED UC.

Implemented UC100 normal-flow step 7: a committed replenishment header with matching CREATE audit is the durable notification source. The existing opt-in worker scans retained events, delivers one in-app receipt per eligible Operations Manager, and retries without duplicate delivery. Current active role, user, branch access and independent REPLENISHMENT_VIEW are checked. Linked material demand additionally requires current MR_VIEW/provenance, consistent with the replenishment detail endpoint. Notification opening checks the current session and repeats source/recipient authorization. No grants, schema, generated entities or shared database were changed. Worker enablement remains unchanged.

Shortage responses now include item and base-unit names from existing entities. The Create screen retains those labels for selected quantities and links to the existing replenishment when coverage blocks a duplicate. Opening that link still uses the authorized detail endpoint. Names are presentation fields, excluded by the existing command payload allowlist.

Direct validation:

- Backend solution build: PASS, zero warnings/errors.
- Frontend production build (including TypeScript compilation): PASS. Existing React Router `use client` and bundle-size warnings remain (945.24 kB JS).
- Existing frontend dev2-api tests: 7 PASS, including CSRF/current identity and independent concurrency versions. Node module.register deprecation warning remains.
- `dotnet test HotelOps.slnx --no-restore`: exit 0, no tests executed by this solution; not a test PASS.
- Focused disposable SQL/HTTPS scenario: PASS in `recovery-replenishment-created-notice-fixed-20261116.json`. Exact server/database/GUID guard precedes all synthetic writes. Checks include shortage labels, Create, duplicate denial, scoped detail/list, notification delivery, sequential retry deduplication, HTTPS source 200, revoked manager source 404, Close, audit, role denial, CSRF, session revocation and cleanup.
- Initial scenario failed because the test used RecipientId instead of the endpoint's NotificationId. The failed artifact is retained as `recovery-replenishment-created-notice-20261116.json`; only the test URL was corrected. Both attempts report cleanup=true.

Only the existing disposable recovery database was mutated, using isolated synthetic identities/branch/grants and cleanup. Delivery service was invoked directly by the probe; the background scheduler was disabled. The temporary HTTPS host was stopped after validation. Probe compilation retains three pre-existing EF1002 warnings in SprintCredentialScenario (not executed). Runtime retains existing RoomTypeBranchV1 index, unordered limiting and multiple-Include warnings.

Remaining acceptance: browser flow, material-demand source including distinct request/base units, concurrent delivery and rollback fault injection, and receipt Post/link lifecycle. UC100 and UC101 remain PARTIAL_IMPLEMENTATION. This receipt supersedes earlier NOT_RUN claims only for the specifically executed paths above. No production enablement/deployment or automatic grant assignment.

Changed source: ReplenishmentContracts.cs; ReplenishmentService.Queries.cs; ReplenishmentCreateFields.tsx; ReplenishmentCreatedNotification.cs; MaterialApprovalNotificationWorker.cs; NotificationCenterService.cs. Verification: RecoveryReplenishmentScenario.cs, RecoveryReplenishmentNotice.cs, the two evidence JSON files, UC_STATUS_MATRIX.md and this receipt.
