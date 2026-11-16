# FASTTRACK-142A checkpoint148 — closed and provisional task effort

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=eaaa4ef588b855b4b3674ac5c1d074288df1660c
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

UC132 KPI-06 now uses persisted effective TASK intervals under the existing scoped SERIALIZABLE dashboard snapshot. Closed effort hours, provisional open effort hours and open interval count are separate. UTC period clipping reuses WorkTimePeriod.Seconds and existing overlap validation, converting seconds to hours; workday duration and pauses are not counted. Snapshot time bounds open effort and future periods cannot project it forward. Task/workday/user/branch/department provenance and effective timestamp consistency are checked before totals; inconsistent or overlapping intervals return N/A with a missing-source explanation. Optional work-time tables absent or more than 50,000 candidate intervals also produce explicit unavailable metrics instead of fabricated zero. This bound is not a partial sum. No generated model/schema/security-policy changes.

Direct validation: solution build PASS zero warnings/errors; explicit backend regression 798 PASS/14 SKIP; solution test command succeeds without discovering that separate project. ModelProbe build PASS with its three existing EF1002 warnings. Frontend typecheck/production build PASS; existing use-client/large-chunk warnings remain. No dependency change or full frontend test rerun.

Trial1 PASS107 records; final trial2 PASS109, including 14 named dashboard checks and a browser result with four assertions. Final synthetic source: two current closed intervals total30 minutes, a zero-duration event contributes0, a20-minute historical interval crosses the UTC start and contributes10 minutes to each adjoining period; selected-period closed effort is40 minutes (browser0.67 hours). A separate open interval contributes provisional time and count1, never closed effort. Future-period values/count are0. Injected overlap makes effort N/A; removing only that owned negative fixture restores consistency. Existing branch-denial, unknown lifecycle, read-only behavior, linked work, deadline and handler regressions pass. These are real persisted interval read/aggregate fixtures, not a claim of new producer lifecycle verification or correction workflow acceptance.

Both identity-verified disposable clones/backups were removed. Evidence: checkpoint148-trial2-runtime.json and both cleanup receipts. Runtime cleanup=false delegates immutable source removal to whole-clone disposal. No shared/production mutation or deployment.

Changed product files: TaskEffortDashboard.cs, DashboardService.cs, DashboardPage.tsx. Verification: SprintTaskDashboardScenario.cs and sprintTaskDashboardBrowser.mjs; status matrix and receipt/evidence. Newly completed whole UCs:0; total stays24/144. UC132 remains PARTIAL: KPI-05 actual-end/processing-cycle semantics await the precise user question (ActualStartAt survives Reopen, CompletedAt records verification Accept); Workload drill-down must reconcile dashboard UTC periods with its hotel-local calendar without silently shifting boundaries. Next highest-impact independent task is that calendar-explicit drill-down, preserving Workload's own actor/permission boundary. Sprint remains active, deadline unchanged.
