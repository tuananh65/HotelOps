# Unit test report ? FS011

Runtime source checkpoint: 98e5fcf159113a692ac329a1fed956ec791c73b2. The135-case TRX was executed on identical runtime/test source immediately before logical commit (final EOF whitespace only). Complete reproducible harness checkpoint is CODE_COMMIT.txt.

Framework xUnit2.9.3 / Microsoft.NET.Test.Sdk17.14.1 / net10.0.

    dotnet build HotelOps.slnx
    dotnet test HotelOps.slnx
    dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --no-restore --logger 'trx;LogFileName=fs011.trx' --results-directory docs/fs011/evidence

Backend build PASS0warnings/0errors. Solution contains API only; explicit xUnit project executes135 PASS /0 FAIL /0 SKIP. NU1900 package vulnerability metadata warning retained. First new test compilation used wrong InterceptionResult<T> property, fixed before executed run; do not report that attempt as test PASS. Initial133 PASS followed by135 PASS after adding Production flag cases.

75 actual parameter cases /34 methods mapped, including6 command guard cases and2 new flag cases. Baseline mapping audit verifies all67 original FS010 cases /31 methods still exist AND occur in latest TRX-backed selection with same input; no methods invented. Source files: AuthorizationTests, BoundaryTests (Monitor/History only), CleaningCreationTests, CleaningLifecycleContractTests, CleaningMonitorRouteTests, NotificationCandidateTests, CandidateCommitGuardTests, CandidateCommandGuardTests. Exact source/method/input/command/actual result in workbook.

Evidence TEST_EVIDENCE/fs011.trx (actual VSTest log/results), BASELINE_MAPPING_AUDIT.json and WORKBOOK_VALIDATION.json. No line/branch coverage measured; no percentage fabricated. DB/commit proofs are COMPONENT_REAL_SQL, not unit/HTTP authorization. Untested branches include actual network partition, durable recovery DB outage, arbitrary dynamic procedures and global external writer protection. Manual headed launcher is not a Tester acceptance run.
