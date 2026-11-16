# FASTTRACK-142A checkpoint140 — actor-filtered inspection report E2E

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=07473f4da6bc3732d87bab428c8447016d4988a2
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Closed the C139 runtime evidence gap with a new ReportActors scenario on a verified disposable database clone. The canonical frontend production bundle calls the real HTTPS API and SQL; there are no route mocks or TLS/CSRF bypasses. No product source changed in C140.

Seven named reportActorsCheck entries PASS: combined official cleaner/inspector inclusion, exclusion of inspector/assigner as cleaner, intersection with mismatching inspector, historical attribution after cleaner deactivation, separate persisted CSV/XLSX export audits, cached export denied after Operations Manager role revocation despite DEPT_MANAGER, and public 503 HOUSEKEEPER_HISTORY_INCOMPLETE when the selected population contains an inspection without attribution.

Real Edge browser PASS: selects the two actor filters, verifies confirmed filter metadata and one inspection, downloads CSV/XLSX, checks CSV snapshot/filter metadata and XLSX container signature, clears preview on filter edit, observes zero rows for the non-cleaner, and logs out through CSRF-protected endpoint. The server verifies two persisted export audits for that browser snapshot. XLSX desktop rendering is not covered.

First attempt failed at the negative fixture: the database correctly rejected an inspection without its own cleaning cycle. The fixture now creates a second room, task and cycle without inventing cleaner attribution. No trigger/FK was bypassed. First and retry clones/backups were both dropped. The retry runtime receipt is success=true; cleanup=false delegates to the separate whole-clone cleanup receipt, whose status is DROPPED_AND_BACKUP_REMOVED. The inherited runtime scope label describes the surrounding department-authority harness; named reportActorsCheck/reportActorsBrowser entries provide the specific public report evidence. This does not certify any FE13 mutation.

Validation: backend build PASS0 warnings/errors; ModelProbe build PASS with three existing SprintCredentialScenario EF1002 warnings. Solution test exits successfully without discovering the separate regression project. Explicit FullyQualifiedName~Report regression: 39 PASS, 1 SKIP (the filter also matches a skipped cleaning Report lifecycle test). Browser script syntax and git diff checks PASS. No frontend product edit; real browser used the C139 production build. Earlier broad release results are historical, not rerun claims.

Changed files: SprintReportActorsScenario.cs (new), SprintDepartmentAuthorityScenario.cs dispatch, SprintReportsBrowser.cs actor fixture arguments, sprintReportsBrowser.mjs actor scenario, Invoke-Dev3LinkedTaskCreationTrial.ps1 guarded clone mode, receipt/matrix and evidence.

Whole UCs newly COMPLETE_VERIFIED: 0; total remains 24/144. UC135 actor-filter preview/export subflow now has direct E2E evidence; complete RPT indicators and PDF remain. UC122 priority/deadline/At Risk subflows remain verified; reroute lifecycle awaits the existing pending decision. Next highest-impact implementation: complete remaining approved report indicators or another independently specified lifecycle, preserving report actor/scope restrictions and immutable attribution. Sprint goal stays active.
