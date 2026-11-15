# FASTTRACK-142A checkpoint87: UC123 authorized history API

NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.

Implemented the approved IR_VIEW read contract through GET /api/internal-requests and GET /api/internal-requests/{id}/timeline. Requester and unambiguous current handler see related records with a current eligible grant. Operations Manager, Department Head and Administrator require an eligible grant on the same role assignment to see branch records. Explicit global ALL on Administrator is honored; an Administrator role alone grants nothing. Branch manager reads require a branch assignment, not a global role shortcut. Invalid/expired/revoked grants, inactive branches and lost current-handler relationships deny access. Multiple active assignments fail closed for handler-based access.

Queries filter authorization before counts/pagination and return DTOs. List and timeline run in a serializable transaction with the existing user eligibility lock, fresh session checks and final scope revalidation. Receiving-department changes do not infer authority from User.DepartmentId or Department.ManagerUserId. Read scope is deliberately independent from UC115/116 mutation authority; no processing endpoint was added.

InternalRequestViewIsolated.sql validates exact server/database/GUID/run ownership and existing permission metadata, then seeds IR_VIEW idempotently using the existing IR_CREATE module/resource identity. It refuses conflicting entries and creates no default role grants. No generated entity, schema, CHECK constraint or shared configuration changes.

Direct verification:

- Backend and ModelProbe builds PASS, zero warnings/errors. Nine new authorization tests PASS, including SQL translation of the actual scope predicate. Full regression670 PASS/12 SKIP. Solution test exits0; regression project is executed separately.
- Auth state/router/shared-host regression224 PASS, zero failures/skips. No frontend source changed in checkpoint87; checkpoint86 production build and report tests remain the relevant frontend validation.
- [HTTPS/SQL trial](checkpoint87-trial1-runtime.json) verifies seed twice, missing permission403, requester/current handler exact visibility, wrong branch403, unrelated timeline404, receiving-department change, ended handler404, scoped DH/Operations Manager/Administrator, revoked assignment403, removed grant403, invalid pagination400 and unchanged business status. The same run repeats report browser/export regression.
- All synthetic request/assignment/type/user/branch/department and temporary catalog/grant data cleaned up; trusted HTTPS host stopped. Shared/production DB untouched. Existing catalog rows are not overwritten.

UC123 remains PARTIAL: canonical history UI, direction filter, authorized linked work and withdrawal acceptance are not yet implemented. The runtime scenario tests read authorization with synthetic records, not a completed create-to-close FE13 lifecycle. UC115/116 retain their blocker: trusted current receiving-department authority under GBR-146 is still required. Other FE13 lifecycle/status and material classification contracts remain separate.

Next highest impact: connect canonical UC123 list/timeline UI with session invalidation and browser-to-SQL tests; then finish the independently specified FE13 lifecycle transitions once their existing status and ownership contracts are verified. JOB06 remains archive-design-only and must not purge referenced audit rows.
