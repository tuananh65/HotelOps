# UC092 cleaning-task links: source slice

Status: PARTIAL_IMPLEMENTATION / NOT_TESTED. No new completed UC.

Implemented:
- Material detail lists existing cleaning links and authorized open candidates, paginated. Reuses the operational-link UI with an explicit target kind and canonical cleaning execution navigation.
- GET/POST /api/material-requests/{id}/linked-cleaning-tasks require HTTPS, current cookie session, independent MR_VIEW and CLEANING_VIEW. Mutations additionally require current requester and existing MR_CREATE eligibility. Unsafe requests remain subject to global CSRF middleware.
- Reuses the existing cleaning visibility predicate, including active room/branch and OWN current active assignment. No role IDs or new grants.
- Candidates use cleaning_tasks.workflow_status, not generated legacy Status. Target must remain open and in the same branch through final revalidation.
- Serializable transaction protects request rowversion, additive link and audit. Duplicate links return 409, stale versions 412, absent capability 503; failure rolls back. No issued quantity, stock document, lifecycle state, or previous link is changed.
- Prepared MaterialCleaningLinksRecoveryV1.sql: typed request/cleaning/audit FKs, composite PK, unique audit, reverse index, exact disposable identity/backup/restore guards and default rollback. No generated EF entity changes. Script NOT executed; no database mutation.

Direct validation:
- dotnet build HotelOps.slnx --no-restore: PASS, 0 errors / 0 warnings.
- npm.cmd run typecheck: PASS.
- npm.cmd run build: PASS; existing React Router use-client and >500kB chunk warnings remain.
- dotnet test HotelOps.slnx --no-restore: exit 0 with no test output; no executed test case evidence. Automated acceptance NOT_RUN.
- API / SQL / browser: NOT_RUN. Missing installed capability intentionally blocks these endpoints.

Remaining UC092 source dependency:
- Maintenance occurrence is not interchangeable with an arbitrary MaintenanceWork. PreventiveMaintenanceRun has a nullable MaintenanceWorkId; the existing production Dev2ActionContracts registers no maintenance read contract, and Fe12MaintenanceDependencyAdapter explicitly lacks an accepted cycle source. Implement the actual maintenance occurrence workflow/read authority before exposing that target; do not borrow demo authority or guess lifecycle from PlannedAt/Result.
- New audit references must participate in the approved archive-before-purge design. Never cascade-delete receipts or disable FKs to satisfy retention.

Human QA after guarded installation:
- Own requester + valid MR_CREATE/MR_VIEW and independently visible same-branch open cleaning task succeeds, with one persisted link/audit and advanced material rowversion.
- Other requester, foreign branch, revoked grant/assignment, Cancelled/Rejected material, closed cleaning task, malformed/stale version and duplicate link fail without partial persistence.
- Concurrent task closure, permission change and duplicate link requests remain atomic; verify rollback and audit consistency.
- Verify UI selection, current-session CSRF, pagination, cleaning navigation and independent operational/cleaning panels.
