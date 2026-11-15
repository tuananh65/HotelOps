# FASTTRACK-142A checkpoint89: UC123 scoped business filters

CURRENT_HEAD=commit containing this receipt; starting local/remote10c6bb0740d94905009a97bdee1498d92f4b0258.
NEW_COMPLETE_VERIFIED=[]; TOTAL_COMPLETE_VERIFIED=19/144; PARTIAL=125/144.
WORKFLOWS_COMPLETED=[UC123 status/type/priority/receiving-department filter selection and clear, authorized metadata discovery]
UI_API_SQL_E2E_VERIFIED=[related requester/handler, DH branch history, status/type/current receiving department, clear filters, revoked-role denial]
SHARED_DB_MUTATION=NO

GET /api/internal-requests/options derives metadata from the same authorized request query as history, inside the existing serializable session/scope revalidation boundary. It does not expose global type/department catalogs. Canonical history adds status, type, priority and receiving-department selects plus clear filters. Missing/failed options deny the combined page; stale data is removed during reload. No schema or permission changes in product code.

Direct evidence: [trial4](checkpoint89-trial4-runtime.json) PASS, cleanup=true, trusted HTTPS and real browser; other-branch type/status excluded from options and filtering by a hidden type returns no rows. Department change is reflected by the filter. Reused report regression also passes. Trials1-3 failed on the status selector's accessible name and cleaned up; [trial3](checkpoint89-trial3-runtime.json) retains that failure. Explicit aria-label fixed it; a focused browser regression now exercises the selector. This was a UI integration defect, not an authorization relaxation.

REGRESSION_STATUS=backend/ModelProbe builds PASS with zero warnings/errors; required solution test exits0 with no discovered tests; actual focused backend9 PASS. Client contract4 PASS and UI3 PASS (including session loss/denied refresh). Production TypeScript/Vite build PASS with unchanged use-client/chunk warnings. No broad redundant suite rerun; previous broad backend evidence remains checkpoint87. Priority rendering/query binding uses the same control as tested type/department; this fixture has no non-null priority and does not separately establish a priority-specific end-to-end result.

EXACT_REMAINING_GAPS=[UC123 sent/received direction, authorized linked-work visibility and atomic withdrawal with receiving-DH notification; FE13 requires trusted department authority for UC115/116/118, initial PENDING_ACCEPTANCE/ACCEPTED physical storage contract and request-type material classification for UC114, percentage/result/version storage for UC117]
NEW_BLOCKERS=[]; existing authority decision requested asynchronously: explicit user/branch/department mapping versus approved DEPT_MANAGER branch grant covering every active department. Neither inferred nor enabled while pending. Actual disposable inspection confirms internal_requests has no progress/rowversion columns; existing status CHECK only permits DRAFT/SUBMITTED/IN_PROGRESS/COMPLETED/CLOSED/REJECTED/CANCELLED. No guessed alias added.

NEXT_BATCH=[P0 near-complete UC001/002/003 credential/session lifecycle: inspect exact acceptance, fix any real gaps and verify public browser/HTTPS/SQL outcomes; return to FE13 mutations when authority/storage contracts suffice]
SPRINT_TIME_REMAINING=approximately23h58m at2026-11-15T17:52Z; deadline2026-11-16T17:50Z unchanged.

Zero full UC closures here reflects implemented SRS read-filter functionality, not a claim that read fragments complete UC123. Stop expanding this filter surface; use the leader's near-complete workflow priority while FE13 mutation contracts remain unresolved.
