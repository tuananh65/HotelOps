# Note 25_11 implementation

Baseline: feature/project2-role-alignment-20261123, d8d3263a. Preserve prior uncommitted cleaning assessment/checklist fixes. Source: user attachment 336a035d (full textual requirements); referenced Word/images not supplied in that folder.

## Ownership and phases
- Parent: GLOBAL-01 shared shift enforcement, NOTE01/02/04/05/06/07/10/12/15, shared navigation, integration and evidence.
- note_inventory: NOTE09/11B-E/14A,C inventory commands and UI.
- note_maintenance: NOTE08/16 templates and maintenance outcomes, WorkService family.
- note_management: NOTE03/13 personnel and asset tree,14B reusable presentation.
- Parent handles NOTE11A/F dashboard shortcuts/routes.

## Checklist
- [x] Read provided text and inspect baseline; no destructive database or Git operations.
- [x] Implement P0 shift validation and inventory/input corrections.
- [x] Implement requested workflows and role presentation.
- [x] Complete shared UI, notifications and dashboards.
- [x] Build backend/frontend; unit, isolated DB integration, browser, authorization regression.
- [x] Independent review; reconcile docs/changes/note-25-11 tracker with exact evidence.

Database changes require a verified backup first. Official DB is not a test fixture. No new push or production deployment included by default in this task. Existing local runtimes remain needed for review; update only after validation. Never claim unavailable Word images were reviewed.

## Evidence checkpoint 26 November 2026

- Source implementation covers 16 groups plus GLOBAL-01; tracker in `docs/changes/note-25-11/01_REQUIREMENT_TRACKER.md` distinguishes IMPLEMENTED from VERIFIED.
- Confirmed final: Release build0warnings/errors;.NET197pass/11skip; frontend build/typecheck;compact11pass; component browser8pass, layout5pass. Broader legacy frontend suite timed out and was interrupted; not green.
- Isolated SQL6pass (six-role/receipt+stocktake/refill/maintenancePASS+FAIL/partialtransfer). HTTP8pass before expanded personnel/workload checks; shell verification expanding with remaining_acceptance agent.
- Clone and Development migration after CHECKSUM/VERIFYONLY backup:57tables,no legacy pending refill. OfficialDB unchanged. Development updated5173/API5081, Production5080 kept old release; both healthy.
- Independent review corrections added: maintenance history ownership, ordinary incident completion, OM tasks report rejection, permission bound to active shift role.
- Final expanded acceptance:HTTP/SQL14PASS;actual shell17PASS/5NOT_RUN (no unread badge data);seven roles. All16groups and GLOBAL-01 have scoped evidence. Source builds and targeted SQL/browser regressions after review complete;four reports reconciled. Broader legacy frontend timeout remains outside the changed Compact flows and is disclosed. No user approval pending; no push/deploy performed. Journal written locally, AgentWiki publish skipped.
