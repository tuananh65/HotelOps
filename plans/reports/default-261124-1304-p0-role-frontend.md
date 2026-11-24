# P0 role frontend handoff

## Implemented

- `frontend/src/compact/app.tsx`: removed active DEPT_MANAGER, MAINTENANCE_LEAD and MAINTENANCE_STAFF workspace branches. Maintenance Supervisor gets request/work/history/assets/calendar routes; no technician assignment or PM workspace. OM now has scheduling, combined personnel/operational work and combined housekeeping/maintenance tabs. All entries still check actual grants. Existing admin/owner shell, alerts, mobile shell and role-specific dashboards retained.
- `operations-workspace.tsx`: new permission-filtered OM tabs backed by existing pages; no placeholder supply/request routes.
- `work.tsx`: OM incidents and ordinary internal requests are read-only even if stale grants are present; no create, mutation, handover, progress or evidence upload. Maintenance Supervisor has no technician assignment/review/handover actions. Supports initial status filter for work/history. Room issue links use the room workflow.
- `account-management.tsx`: role assignment dropdown contains only active target roles; SYS_ADMIN remains excluded under existing policy. Historical role display is unchanged.
- `insights.tsx`: retired Head-only dashboard presentation; OM metrics remain before attendance.
- `maintenance-overview.tsx`: receipt-oriented wording, no technician assignment/PM links for the new role.

## Integration required by parent

1. Work detail currently does not expose these capabilities. UI deliberately fails closed until backend adds `capabilities.canSelfVerifyOperational` and `capabilities.canClaimMaintenance`. The first permits OM self-verification only for OPERATIONAL; the second exposes accept for an unassigned maintenance request only when server grants claim capability. Actual mutations must enforce equivalent rules.
2. `#requests` still uses existing INTERNAL_REQUEST Work page. Parent owns replacing this with the unified request workspace and wiring special request types. Supply/config/allocation routes are intentionally not added until parent supply pages exist.
3. HK personal history uses existing Work historyMine filtering, not branch-wide housekeeping-history endpoint (that endpoint currently requires BRANCH CLEANING_VIEW). Backend must ensure historical completed assignments remain visible under OWN permission; current generic List has an earlier OWN filter that can hide completed housekeeping assignments.
4. Parent owns room-issues.tsx/remediation. Existing text and DEPT_MANAGER UI check there still need replacement with new HK Supervisor routing. No competing edits made to that file.
5. API-backed browser tests and full role authorization verification remain parent integration work. No production changes, database edits, backend changes or deployment by this worker.

## Validation

- Frontend TypeScript typecheck passed.
- All six edited/new TSX modules were transpiled with TypeScript and their emitted JavaScript passed `node --check`.
- Frontend build running at handoff preparation; final result will be reported separately.

No user decision requested by this module. Backend capability contracts above require integration, not broader permission grants.
