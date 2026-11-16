# FASTTRACK-142A checkpoint146 — official creation boundary in deadline writers

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=138847bc403cdb264698b19bea3c3452780b8d5d
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

Applied the approved UC122 CreatedAt-to-DueAt contract to the two related deadline writers that lacked it: UC116 Accept and UC117 progress. A changed deadline must be strictly later than the persisted official CreatedAt. Existing actor/department authority, session, reason, ETag, transaction and audit guards remain. Valid already overdue deadlines remain allowed; neither UpdatedAt nor submission resets the origin. No schema/generated entity or permission changes. UC122 escalation already enforced this boundary.

Direct verification: required `dotnet build HotelOps.slnx` PASS with zero warnings/errors. ModelProbe build PASS with three existing EF1002 warnings in SprintCredentialScenario. `dotnet test HotelOps.slnx` exits successfully but does not discover the separate regression project; explicit regression project run after the final change: 798 PASS, 14 SKIP, zero failures. First sandbox build attempts could not read local NuGet.Config; authorized elevated builds succeeded. Direct PowerShell script invocation was blocked by local execution policy before running; the reviewed runner was executed with process-scoped execution policy, without changing machine policy or application security.

Final trial2: 94 checks PASS, including 14 new deadline assertions through authenticated HTTPS into the verified disposable SQL clone. Before/equal CreatedAt returns HTTP400 without changing progress, lifecycle, row version, events or audit. Valid overdue and extended deadlines persist with old/new deadline and reason in audit, preserving CreatedAt; stale progress version returns HTTP412. Pending acceptance rejects invalid deadlines atomically, then Accept succeeds with a valid overdue deadline and audit. Existing browser manager deadline update, linked task creation and handler security regression also pass. New negative boundary cases use HTTPS, not browser form interaction. Trial1 passed the progress cases before adding UC116 coverage. Both clones and backups were removed by the existing guarded cleanup.

Evidence: checkpoint146-trial2-runtime.json, checkpoint146-trial2-cleanup.json, checkpoint146-trial1-cleanup.json. Runtime cleanup=false delegates immutable-fixture removal to whole-clone disposal; separate cleanup receipts prove disposal. Runtime outer scope text is inherited; named nested checks identify the executed public HTTPS/browser coverage. No shared/production data touched. Frontend source, dependencies, typecheck and production build were not changed/rerun in this batch; browser used the existing built frontend.

Changed product files: InternalRequestDecisionService.cs, InternalRequestProgressService.cs. Verification: SprintInternalRequestDeadlineScenario.cs, SprintLinkedTaskCreationScenario.cs; receipt/evidence and UC matrix.

New whole UCs completed: 0. UC116 retains its prior COMPLETE_VERIFIED acceptance with strengthened deadline regression. UC117 remains PARTIAL for material/legacy linked work; UC122 remains PARTIAL for reroute/current-handler/pending-handover semantics awaiting the existing decision. No runtime/security gate was relaxed to claim completion. Next highest-impact batch: assess and finish outstanding UC102/105 lifecycle acceptance while FE13 material/reroute decisions remain pending; preserve UC006/010 release gates; complete the approved FE13 interventions when their outstanding contracts arrive. Sprint remains active; deadline unchanged.
