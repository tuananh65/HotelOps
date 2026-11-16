# C181 ? Cancel refreshes monitor detail

Baseline 5816169b06c5d3d30bcac3a297b0d4ebfbc67cf0. New completed UCs: 0; source covered 29/144, pending QA 2, historical COMPLETE_VERIFIED 27. UC037 stays partial; no new full workflow acceptance.

After a validated cancellation receipt, the parent detail clears its old snapshot, resets timeline to page one and reads current detail/audit through the authorized monitor API. It retains a success notice and hides the completed cancellation form. Refresh failure remains retryable without submitting another cancellation. Pending mutation continuations cannot update or refresh after the keyed task component unmounts. Existing route and authority boundaries already remount on identity/task changes; no new route permission or redundant wrapper was added. Uncertain mutation retries retain the same payload/idempotency key.

Changed: frontend/src/screens/housekeeping/CleaningCancellationPanel.tsx, CleaningMonitorDetail.tsx and frontend/tests/cleaning-cancellation.spec.ts, plus this receipt/handoff. Browser fixtures now provide the real route's synthetic auth session, isolate actual /api/ requests and return CANCELLED on the post-command read. No API call reaches SQL.

Validation: frontend typecheck/build PASS; cancellation/monitor transport 12 PASS; focused Playwright browser 3 PASS, runner exit 0. Earlier fixture run failed because the broad **/api/** glob also blocked Vite /src/api modules; corrected to URL pathname prefix /api/. Sandbox runs completed cases but hung during Vite teardown and were interrupted; final elevated run completed in 3.5 seconds. First stale fixture lacked session mock; fixed. Warnings: React Router use-client ignored, Vite bundle over 500 kB, NO_COLOR/FORCE_COLOR warning. Backend unchanged, no backend rerun; C180 build/tests remain historical. No database writes, migration application or live HTTPS/SQL acceptance.

Remaining: guarded C179/C180 migration and full Cancel/Resume/Complete/inspection runtime acceptance; latest receipt-read authority and transaction rollback/concurrency checks require Human QA. Sprint remains active, no COMPLETE_VERIFIED promotion.
