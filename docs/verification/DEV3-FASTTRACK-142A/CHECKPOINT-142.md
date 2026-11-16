# FASTTRACK-142A checkpoint142 — RPT-02 outcome and criterion summaries

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=2635fb48982963d07cf2bbdb9dfe65f67c11199d
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Implemented inspection_outcomes grouped by stored status/result and defects_by_criterion grouped by exact checklist item, severity and stored defect status. Counts use SQL LongCount over the same authorized/date/inspector/official-cleaner inspection population as detail rows. No joins to reworks multiply counts. Null outcomes and severity remain explicit buckets; unfinished inspections are not treated as failures. The normal section renderer and frozen CSV/XLSX export include these summaries under the existing row limit, SERIALIZABLE snapshot and authorization/audit.

Requirements basis: SRS V.3.5 RPT-02 includes pass/fail and defects by criterion. First-inspection identity is backed by FE05_INITIAL_INSPECTION_ASSIGNMENT_V1 receipts and reinspection has its own bindings; the implementation does not use MIN(inspection_id), arbitrary oldest timestamps or rework row counts to invent cycle pass-rate/round semantics. These remain explicit report limitations. No schema or generated EF entity change.

Evidence:
- Backend final build PASS, zero warnings/errors. Harness final build PASS with three pre-existing credential-fixture EF1002 warnings. Initial harness compile used the wrong checklist item property; corrected to generated ItemId.
- Explicit report-name-filtered regression: 39 PASS, 1 SKIP; solution test exits successfully but does not discover the separate test project.
- Final guarded SQL probe: success=true, 65 checks, rollback=true. New checks verify one failed-inspection aggregate despite related rework, null-result bucket before conclusion, exact criterion count with unspecified severity preserved, and no summary leakage after housekeeper exclusion.
- Three preceding SQL attempts failed on fixture lifecycle guards: defect after conclusion, defect without failed criterion, then result edit outside INSPECTING. All rolled back. Final fixture initializes FAIL while INSPECTING, then creates its defect before conclusion. Original fixture source, SQL constraints and triggers are unchanged.
- Real Edge -> trusted HTTPS -> disposable SQL: success=true. Browser verifies the inspection_outcomes section contains FAIL/count=1, renders its heading, exports its columns in CSV and downloads XLSX; existing actor-filter/rework/audit/revocation checks remain green. Criterion summary has SQL evidence, not a separate browser defect fixture acceptance claim.
- Owned clone and backup removed, confirmed by cleanup receipt. Generic runtime scope text comes from the containing authority harness; reportActorsCheck/reportActorsBrowser entries identify actual report evidence. No shared/production SQL mutation.

Changed files: ReportSnapshotQueries.cs, SprintReportProbe.cs, SprintReportActorsScenario.cs, sprintReportsBrowser.mjs, receipt/matrix and SQL/runtime/cleanup evidence. No frontend product edit; real browser uses the existing production bundle.

New whole UCs COMPLETE_VERIFIED: 0; total remains 24/144. UC135 remains PARTIAL for authoritative cycle/round aggregation, other RPT indicators and PDF. UC122 reroute remains pending its existing business decision. Next: complete authoritative cycle-summary provenance; finish remaining approved report indicators/export acceptance; resume a pending FE13 lifecycle when its contract arrives. Sprint goal remains active.
