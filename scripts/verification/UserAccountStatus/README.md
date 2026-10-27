# Task 35 — UC-006 account status

Verdict: PARTIAL implementation; both HTTP status mutations remain BLOCKED. No controller, route or DI registration is added. Deactivation is refused even when calling the activation primitive directly.

Implemented independently approved components:

- Strict `UserAccountStatusChange` input/transition validation.
- `UserAccountStatusAccess`: approved USER_MANAGE ALL/BRANCH composition, no OWN/self/role-name bypass; reuses UC-005 evaluator and authority locks without changing shared infrastructure.
- `UserAccountActivationService`: SQL-session-bound, authorization-locked, rowversion-conditional INACTIVE-to-ACTIVE transaction with before/after/reason audit. Preserves credential/lock, sessions, profile, role and scope assignments.
- Console parser/session and isolated SQL activation verification; read-only dependency metadata inventory.

Read [authorization](AUTHORIZATION.md), [business dependencies](BUSINESS_RULES.md), [transaction](TRANSACTION_AND_CONCURRENCY.md), [results](VERIFICATION_RESULTS.md) and [remaining gates](OUTSTANDING_ISSUES.md). These documents do not approve deployment or schema changes.

## Reproduce

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet build scripts/verification/UserAccountStatus/UserAccountStatus.Verification.csproj
dotnet scripts/verification/UserAccountStatus/bin/Debug/net10.0/UserAccountStatus.Verification.dll
# Create NEW guarded schema trials; never target HotelOpsDB.
& scripts/verification/UserProfileUpdate/Verify-Schema.ps1 -Server '<verified-local-server>'
dotnet scripts/verification/UserAccountStatus/bin/Debug/net10.0/UserAccountStatus.Verification.dll <owned-patched-trial> <NEW-local-json>
& scripts/verification/UserAccountStatus/Inspect-Dependencies.ps1 -Database <owned-patched-trial> -OutputPath <NEW-local-json>
```

The SQL suite checks name, actual server/database, Task17 ownership marker and startup compatibility before fixture writes. Its unique fixture users remain; its sessions are revoked and its temporary trial grants/roles removed. No existing artifacts or worktrees are removed. JSON receipts, bin/obj and machine-specific runtime evidence are local artifacts, not source packaging candidates.

Source scope: three new API files (DTO, access adapter, activation service); three harness C#/project files; one inspection script; seven documents in this directory. No Program/controller/entity/DbContext/DEV2/catalog/schema edits. No stage, commit, push or merge.
