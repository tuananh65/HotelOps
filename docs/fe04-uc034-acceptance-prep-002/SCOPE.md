# FE04-UC034-ACCEPTANCE-PREP-002 scope
Owner: DEV1 FE04. Authorization: Project Leader request in chat, 2026-11-11.
Branch/worktree unchanged: feature/dev1-fe04-uc034-full-command.
Starting HEAD: db12691295ffe0382b005c686ac7478326b0c0d6.
R05 PR base: feature/dev1-fe04-original-cleaner-binding at 95f7422e284bc81bcfcf9f460dace52022de5cc7.
Remote heads verified with git ls-remote. R05 is an ancestor; initial delta is exactly task 001's five files.

Allowlist before editing:
- tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceHarness.cs
- tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceCallbacks.cs
- tests/HotelOps.Dev1.RegressionTests/CleaningCompletionAcceptanceHarnessTests.cs
- docs/fe04-uc034-acceptance-prep-002/SCOPE.md
- docs/fe04-uc034-acceptance-prep-002/HANDOFF.md
- docs/fe04-uc034-acceptance-prep-002/RESULT.md
- docs/fe04-uc034-acceptance-prep-002/source-hashes.json

No app source, schema, gates, FE05/Notification infrastructure or shared auth edits.
Prepare compiled FE04 service/SQL harness and case-specific callbacks; do not run it against SQL.
No automatic SQL entry point. Owner composition must supply external authoritative lease validation;
the metadata envelope never grants a lease. No fixture creation until that validation and existing
runtime/schema gates pass. No SQL/HTTPS, merge, deploy or runtime activation in PREP-002.