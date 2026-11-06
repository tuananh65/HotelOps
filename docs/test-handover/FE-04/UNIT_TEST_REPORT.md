# Unit test report

Implementation commit SHA: ad30eb0ea671518ff8469d06ab2e64362eddc4f7. The TRX was executed on the identical runtime/test working tree immediately before this logical commit; source changes after this commit are harness/docs only. Baseline: 11a2277f077d7310ec29593993afdade11cce03e.

Framework: xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, net10.0.

Commands executed:

    dotnet build HotelOps.slnx
    dotnet test HotelOps.slnx
    dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --no-restore --logger 'trx;LogFileName=fs010.trx' --results-directory docs/test-handover/FE-04/TEST_EVIDENCE

Backend build PASS, 0 warnings/0 errors. Solution contains API only, so solution test does not execute this xUnit project. Explicit test project: 127 PASS / 0 FAIL / 0 SKIP. Actual machine log/results: TEST_EVIDENCE/fs010.trx (per-case method/input/time/outcome); workbook validation: TEST_EVIDENCE/WORKBOOK_VALIDATION.json. Existing NU1900 vulnerability metadata warning remains; no clean vulnerability scan claimed.

Workbook maps 67 executed theory/fact cases to 31 distinct source methods across CleaningCreationTests, CleaningLifecycleContractTests, NotificationCandidateTests, CandidateCommitGuardTests, AuthorizationTests (shared auth), CleaningMonitorRouteTests, BoundaryTests (FE04 Monitor/History only). SourceTestFile/TestMethod are validated against source and all actual outcomes come from TRX. Remaining tests concern other existing backend boundaries; not falsely mapped to UC028/029.

Coverage: NOT_MEASURED. No line/branch coverage percentage generated. No unit proof of actual SQL transactions or HTTP authorization is inferred: SQL commit-fault tests are COMPONENT and actual browser/API suites have separate receipts under docs/fs010. Untested unit branches include real network acknowledgement loss, durable-recovery DB outage, arbitrary external provider writes and shared publisher compatibility. Some validation tests intentionally supply null SQL collaborators to prove early rejection; they do not test successful persistence.

Developer unit PASS is not TESTER_ACCEPTED. All 38 Tester rows remain NOT_RUN.
