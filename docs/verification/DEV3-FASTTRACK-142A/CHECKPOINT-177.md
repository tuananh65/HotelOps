# C177 — Approved first-inspection readiness contract

Baseline `4e9c7775b7e0d03e55e2741c141e4b99906f6c34`. Applied Project Leader decision: keep Pending Cleaning precedence for never-passed rooms. Task Pending Inspection and effective room readiness are distinct. No new COMPLETE_VERIFIED; source 29/144, pending QA 2, historical verified 27/144.

Completion/blocker receipt adds optional effectiveReadiness, computed by existing EffectiveRoomReadiness inside the caller transaction. Historical lifecycle readiness and immutable older receipts remain compatible. New receipt validation rejects READY and inconsistent effective-block summaries. UI reports actual room readiness separately; missing/UNKNOWN evidence asks for an authorized board refresh instead of claiming room usability. No generated entity/schema change and no database mutation.

Validation: dotnet build HotelOps.slnx PASS (0 warnings/errors); focused backend 74 PASS, 1 environment-dependent SQL SKIP; frontend typecheck/build PASS, 14 client tests PASS. Solution test command exits 0 but does not run the separate regression project. Existing Vite warnings: React Router use-client directives ignored and bundle above 500 kB. SQL/HTTPS/browser completion remains unverified; C176 timeout is unresolved and was not relabeled PASS.

Changes: CleaningLifecycleDtos.cs, CleaningClosureBoundary.cs, EffectiveRoomReadiness.cs, cleaningExecution.ts, CleaningExecutionPanel.tsx, two backend test files, client tests, approved change notes, handoff/receipt. No cancellation implementation claimed this batch; incoming business decision took priority.

Next highest impact: cancellation outbox delivery and authorized source resolution, then guarded capability integration. Existing OWN read requires ACTIVE assignment whereas cancellation ends it; preserve that guard and seek explicit historical receipt-read contract if required. Complete-to-Inspection authenticated acceptance and actual capture/delivery remain runtime gaps. Broad tests stay with Human QA.
