# Task 30 — UC-004 Create Employee Account

Branch feature/BE1-database-integration; baseline dda26e4648027a004bca1a2865e0302f0b063122. No commit/push/merge. Existing untracked artifacts and Task 29 reports are preserved.

Implemented POST /api/users with one initial role, one active Branch and one active Department belonging to it. HR/Admin supplies employeeCode; no generated identity code or new schema. The exact six role/department pairs and both required unbound ALL grants were explicitly approved by the user during Task 30. This is not an inferred authorization decision from the historical draft matrix.

The account, membership, initial role, creation audit and temporary credential/audit are written in one transaction. Plaintext handoff is success-only, after commit, expires after 24 hours and cannot be replayed. No reset/status/assignment-update/UC-011 endpoint is added.

- [API contract](API_CONTRACT.md)
- [Authorization and approved role policy](AUTHORIZATION.md)
- [Transaction participant design](TRANSACTION_DESIGN.md)
- [Executed verification](VERIFICATION_RESULTS.md)
- [Remaining rollout limits](OUTSTANDING_ISSUES.md)

## Reproduce safely

Build solution and the two verification projects. UserProfileUpdate/Verify-Schema.ps1 creates new guarded fresh/upgrade patched trials. The participant harness can use the upgrade trial; it generates unique non-sensitive fixture identifiers. The HTTP harness requires an unused empty patched personnel trial plus a separate unused unpatched trial from UserDirectory/Prepare-Database.ps1. Do not reuse/reset old fixture databases.

```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/UserAccountCreation/UserAccountCreation.Verification.csproj
dotnet build scripts/verification/UserAccountCreation/Http/UserAccountCreation.Http.Verification.csproj
dotnet scripts/verification/UserAccountCreation/bin/Debug/net10.0/UserAccountCreation.Verification.dll <owned-patched-trial> <new-local-json>
dotnet scripts/verification/UserAccountCreation/Http/bin/Debug/net10.0/UserAccountCreation.Http.Verification.dll <new-empty-patched-trial> <new-local-json> <new-unpatched-trial>
```

HTTP harness incorporates existing List/Detail/Profile/Startup checks, then tests UC-004. Temporary permission-path roles and fault constraints exist only in owned trials and are removed in finally. Owned sessions are revoked; fixtures retained. Evidence *.local.json, bin/obj and temporary certificates must remain outside commits.
