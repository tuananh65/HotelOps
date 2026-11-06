# UC coverage matrix

| UC | Acceptance criterion / actual scope | Unit source / cases | API / UI evidence | Tester cases / gap |
| --- | --- | --- | --- | --- |
| UC028 | create validation/security, one open task, atomic audit/cycle/readiness, preview/navigation | CleaningCreationTests; shared AuthorizationTests (TRX rows in workbook) | fs010-live create/concurrency/validation/blocked cases; cleaning-create Node/interception | FT-FE04-001?012; inactive fixture case NOT_RUN; VERIFIED_LOCAL only |
| UC029 | authoritative assignment, durable replay, mandatory notification, commit/recovery/source security | CleaningLifecycleContractTests, CleaningCreationTests fingerprint/default-off, NotificationCandidateTests, CandidateCommitGuardTests, shared AuthorizationTests | fs010-live assignment/replay/concurrency/security/delivery; candidate-commit-proof and candidate-physical-proof COMPONENT; default-off and restart separate | FT-FE04-013?038; shared integration FT037 blocked; PROVISIONAL_NOT_ACCEPTED |
| UC030 | Reassign | NOT_COVERED | no new endpoint; supersession fixture is not reassign acceptance | NOT_COVERED |
| UC031 | Start and frozen item snapshot | NOT_COVERED | no new Start endpoint acceptance; seeded IN_PROGRESS is not Start evidence | NOT_COVERED |
| UC032 | Existing progress, ownership/ETag; 100 remains IN_PROGRESS | no new dedicated xUnit mapping claimed | cleaning-progress Node/interception; fs010-live final PUT regression | FT038 regression; no additional UC credit |
| UC033 | Blocker | NOT_COVERED | none claimed | NOT_COVERED |
| UC034 | Complete | NOT_COVERED | none claimed | NOT_COVERED |
| UC035 | Receive Rework | NOT_COVERED | none claimed | NOT_COVERED |
| UC036 | Complete Rework | NOT_COVERED | none claimed | NOT_COVERED |
| UC037 | Monitor route/filter/pagination/access | CleaningMonitorRouteTests; BoundaryTests Monitor/Timeline | cleaning-monitor Node/interception; live Create refresh/Monitor | FT001/036/038 regression; full acceptance not claimed |
| UC038 | Turnover history boundary | BoundaryTests History methods only | inherited inactive controller; LIVE NOT_COVERED | manual full acceptance NOT_COVERED |

Unit workbook includes only executed source-backed cases mapped to FE04 or explicitly labeled shared authorization. It excludes unrelated room/branch validation tests from 127 total. Parameter cases and distinct methods counted separately. No line/branch coverage measured. No fabricated PASS or module percentage.
