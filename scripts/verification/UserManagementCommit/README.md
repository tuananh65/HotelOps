# Task 29 — Review and controlled commits

Ngày 2026-10-27. Branch feature/BE1-database-integration; initial HEAD 20c1063e24cf170450d2dffaa81df6c7ef3a896f. Remote develop remains 5299b5e9e8dd917abef327ab9f9bd5494064ccf9; remote feature initially matched HEAD. Verified using ls-remote without fetch/merge.

Scope: commit the original Task 27 allowlist of 66 reviewed candidates plus 8 Task 28 paths. No new application implementation or source refactoring. Exact paths and Program.cs split are in COMMIT_MANIFEST.md.

Order: Detail, trial SQL artifacts, Startup Gate, Profile/EF plus approved planning/verification. Startup precedes the new User EF mapping. Partial entity and mapping stay atomic with profile; the combined startup/profile harness is committed with profile because it depends on those types. Historical requirement/approval reports are snapshots, not new approvals.

Task 29's five reports are local-only and deliberately excluded from staging. Runtime/schema receipts, WAV, backups, credentials, binaries, src/main.cs and all other files outside the exact allowlist remain excluded and preserved. No push, merge, PR, amend, rebase or database-main operation is authorized or performed.

Current reproducible runtime suite is StartupSchemaGate.Verification, which includes List/Detail/Profile and actual runtime scoped-grant checks. UserManagementIntegration's old-schema health-200 assertions are historical pre-fix diagnostics, not acceptance tests for the current binary. Original Task 22 read harness now also requires a patched database when used against the final binary with Task 26/28.

See SECURITY_REVIEW.md, VERIFICATION_RESULTS.md and RELEASE_RESTRICTIONS.md for limits. Commit hashes and post-commit outcomes are recorded in COMMIT_MANIFEST.md after verification.

Final outcome: COMMITTED. Four local commits, 74 unique allowlisted paths, clean tracked working tree/index and 140 retained untracked files. Final solution build PASS, 0 warnings/errors. No push/merge/PR; production rollout remains unapproved.
