# C175 — Durable cleaning closure integration

Baseline `51eb09d957a6a2c28c6f3a927c1914b8797996ab`. Newly completed UCs: **0**. Source coverage 29/144; pending QA 2; historical COMPLETE_VERIFIED 27/144.

Implemented FE04_CLOSURE_V1: existing reservation/publication/delivery-intent storage and immutable cleaner attribution. Source trigger binds closure publication to task revision, assignment, progress, closed interval and audit. Closure service/metadata recognize verified execution foundation; publisher requires explicit Development opt-in and exact schema capability. Staging validates receipt, fingerprint and recipient shape. Existing authorization, CSRF, concurrency and transaction controls remain; no generated entity edits.

Recovery clone only: identity/backup guarded rollback/apply/replay verified, 89 to 93 tables. Manifest SHA256 `2CF11CF1A599F1887679D8F116C7FF5BDC6BB31905BAE591BE4B05F770B05ECB`. No HotelOpsDB/production writes or config activation. Cleaner capture EXECUTE grant remains a prerequisite for a restricted application identity; no grants fabricated.

Validation: dotnet build HotelOps.slnx PASS, 0 warnings/errors. Initial sandbox access to user NuGet.Config failed; authorized rerun passed. Solution test command exits 0 without executing the separate regression project. Explicit regression filter: 84 PASS, 5 environment-dependent SKIP. Recovery SQL fixture: 1 PASS, including staging, rejection 51915 for missing source evidence and rollback. Injected admission authority and synthetic source/audit do not certify real HTTP authorization, cleaner capture, recipient eligibility or delivery. Frontend unchanged; C173 typecheck/build is historical evidence, not rerun.

Changed files: closure SQL/verify and two manifests; installer/README; CleaningClosureSchema; CleaningClosureBoundary; CleaningLifecycleService; ProvisionalCleaningClosure; FreshStartAdmissionSqlTests; Git attributes; handoff/receipt.

Remaining: authenticated Resume to Complete to Inspection, actual immutable cleaner capture and post-commit delivery; cancellation; incident/material source adapters. Full workflow/browser/concurrency acceptance deferred to Human QA. No complete business workflow newly verified. Next batch continues these lifecycle gaps.
