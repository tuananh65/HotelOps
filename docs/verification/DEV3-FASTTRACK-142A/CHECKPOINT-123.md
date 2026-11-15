# FASTTRACK-142A checkpoint123 — RPT07 overdue snapshot indicator

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=5eda232c4eff9f6151c515a6e50576fd360bb253
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144 (historical aggregate unchanged)
SHARED_DB_MUTATION=NO

RPT07 now includes overdue for each scoped Internal Request row, evaluated at the report's frozen snapshot time using GBR156: a non-null deadline strictly before snapshot time and status other than CLOSED/REJECTED/CANCELLED. The report no longer calls this established contract unavailable. The existing authorization, period/source scope, snapshot ownership, CSV/XLSX rendering, CSRF and export-audit admission remain unchanged. This adds a row indicator, not all RPT07 aggregates or full UC135 completion.

Direct evidence: checkpoint123-reports-sql.json contains45 successful SQL/CSV component checks with rollback=true. Added coverage includes no deadline, before/equal/after snapshot, all three terminal states, frozen overdue after source status/deadline changes, and the overdue CSV column. Existing branch/inconsistent-department exclusions, empty authorized scope and row-limit checks also execute. Exact disposable server/database/GUID/run and empty fixture checks precede synthetic mutation; all data remain in the rolled-back transaction. No shared DB mutation. This probe is explicitly not a new HTTPS/browser authorization/export-audit acceptance run.

Backend build PASS0 warnings/errors. Focused report regression39 PASS,1 SQL/HTTPS-gated test SKIP. Before this report edit, release regression was750 PASS/13 SKIP at base HEAD; it does not substitute for post-edit full-suite evidence. ModelProbe retains3 existing EF1002 warnings.

Frontend unchanged. Broad frontend regression started at base HEAD remains live in exec session41837 with output artifacts/checkpoint123-frontend-tests.log; no final PASS claimed, no restart. Continue polling that handle until terminal before recording release acceptance. No source files were modified by that test run at delivery review.

UC135 remains PARTIAL: response/current-cycle completion/reopened indicators, other RPT gaps, specialized filters and PDF still require work. UC122 at-risk time origin remains pending; existing overdue semantics were already explicit and needed no invented origin. Next: finish release regression, repair evidenced failures, then continue approved business workflow gaps.

Files: ReportSnapshotQueries.cs, SprintReportProbe.cs, UC_STATUS_MATRIX.md, this receipt and SQL evidence.
