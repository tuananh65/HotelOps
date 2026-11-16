# UC144 scoped detail source

Status PARTIAL_IMPLEMENTATION, NOT_TESTED at business acceptance level. No new UC completed.

- A004A-036 GET maintenance detail now has a source-owned read contract. Branch is derived from SQL incident or PM schedule target, never client actor/department assumptions. Existing MaintenanceReadAccess excludes ambiguous sources and cross-branch locations. Staff remains assigned-only. Current user/session, role/grant, ownership and branch are checked again before disclosure in the same serializable read transaction.
- Detail projects DTO fields/logs from the scoped query. It does not invoke the legacy unscoped usage query. Stock/material references are suppressed and MaterialUsageAvailable=false explicitly distinguishes unavailable data from zero usage; separate MR/inventory authorization remains unfinished.
- MaintenanceReadView replaces raw data rendering for list/detail: source, status, planned times, detail link gated by available action, instructions, resolution and work history. Planned dates are labeled as planned, never substituted for SLA deadlines. UI adds no mutation authority.
- No entities, schema, database, grants or deployed runtime changed.

Validation: dotnet build HotelOps.slnx --no-restore PASS (0 warnings/errors); npm.cmd run build (tsc -b + vite) PASS. Existing React Router directive and >500kB bundle warnings remain. Focused MaintenanceReadAccessTests: 4 PASS, 0 failed/skipped. These verify source authority predicates/admission, not SQL translation, projection or browser acceptance. An initial file-generation command had a JavaScript syntax error; it made no edits and was corrected before final build. No runtime PASS claimed.

Remaining UC144/UC092 dependencies: canonical deadline/overdue with due-range filtering/sort; target location/asset display; independent usage access; actual occurrence lifecycle/checklist/work intervals. Human QA must verify list to detail for Lead/Staff, reassignment, revoked grants, foreign branch, mixed source lineage and session expiry; material fields must remain unavailable without independent authority.
