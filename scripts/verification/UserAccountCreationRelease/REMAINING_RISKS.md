# Remaining risks and recovery work

- Actual HTTP connection loss after commit and provider commit acknowledgement loss are NOT VERIFIED. The deterministic after-SQL-commit exception confirms service behavior, not wire delivery.
- After successful/uncertain commit without credential delivery, do not repeat identity under new values or expect credential replay. Confirm account existence through authorized read operations. Exact retry returns conflict; original password is not recoverable.
- Recovery requires a separate approved credential-reset workflow: authorize actor and target, explicitly rotate credential atomically with audit/session revocation and secure one-time handoff. That workflow, endpoint, operational approval and tests are separate work; this task adds none. A handoff failure can leave an ACTIVE account whose initial secret is unavailable until recovery or expiry.
- Pending SRS permission matrix and Reception/Department Head room behavior remain unresolved outside the recorded Task 30 six-role account-admission approval.
- Existing rowversion startup dependency must be deployed through its separately approved rollout. This task only executes guarded trial SQL.
- In-flight authorization revocation is not globally serialized. Master-row locking may reduce creation throughput.
- NU1900 leaves package vulnerability metadata unavailable. Keep auditing enabled and retry in a working NuGet environment.
- Production least-privilege SQL, TLS/DataProtection, APM/body logging, old-binary coexistence and full DEV2 workflow certification remain outside this release review.
- Managed secret strings cannot guarantee memory zeroization. Never enable credential request/response body logging.
- Local fixtures, runtime receipts and pre-existing untracked artifacts are intentionally retained outside the commit. No database or worktree is dropped/reset.
