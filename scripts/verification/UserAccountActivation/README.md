# Task 36 — safe account activation

Implemented `PATCH /api/users/{userId}/activate` using the Task35 activation transaction and approved UC-006 authority. The dedicated route accepts reason only and cannot select INACTIVE. No `/status` or `/deactivate` mutation route is added. Deactivation remains BLOCKED pending cross-module contracts and last-admin verification.

Task35's historical HTTP-blocked notes are superseded **only for activation** by this directory. No permission catalog, generated entity, DbContext, credential algorithm, session middleware or DEV2 source is changed.

- [API contract](API_CONTRACT.md)
- [Authorization and actual SQL races](AUTHORIZATION_AND_RACE.md)
- [Executed verification](VERIFICATION_RESULTS.md)
- [DEV2 deactivation handoff](DEV2_DEACTIVATION_HANDOFF.md)

## Source and reproducibility

New API files: `Controllers/UserAccountActivationController.cs` (route and route-local safe exception responses) and `DTOs/Users/UserAccountActivationRequest.cs` (strict input and response DTO). Program adds only two scoped registrations for Task35 `UserAccountStatusAccess` and `UserAccountActivationService`. Task35 service summary and two console result messages are updated to avoid obsolete HTTP-blocked output; transaction behavior is reused unchanged. Task35 DTO/access/service files remain required dependencies even though they were untracked at preflight.

New verification source: UserAccountActivation.Verification.csproj, Program.cs, ActivationRaceSuite.cs and ActivationHttpSuite.cs. Five Markdown documents here are part of the reproducible feature record. JSON receipts and bin/obj are local artifacts, not commit candidates. No staging/commit/push/merge is performed.

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx --no-restore
dotnet build scripts/verification/UserAccountActivation/UserAccountActivation.Verification.csproj
dotnet build scripts/verification/UserAccountStatus/UserAccountStatus.Verification.csproj
# Creates NEW guarded fresh/upgrade trials; never target HotelOpsDB.
& scripts/verification/UserProfileUpdate/Verify-Schema.ps1 -Server '<verified-local-server>'
dotnet scripts/verification/UserAccountActivation/bin/Debug/net10.0/UserAccountActivation.Verification.dll <owned-patched-trial> <NEW-local-json>
dotnet scripts/verification/UserAccountStatus/bin/Debug/net10.0/UserAccountStatus.Verification.dll <owned-patched-trial> <NEW-local-json>
```

Harnesses require a guarded canonical trial name, verify actual server/DB/Task17 ownership and startup schema before fixture writes. HTTP uses a real child API, ephemeral loopback HTTPS certificate, real credential login, cookies, CSRF and SQL sessions. SQL fixtures have unique prefixes; sessions and temporary trial roles/grants are cleaned in finally; users/work/history and trial databases are retained. The harness removes only its own generated temporary certificate/child process. Existing files/artifacts/worktrees are preserved.

Existing UserAccountCreation/AdminReauthentication READMEs describe regression commands. The UC004 HTTP suite requires its own unused empty patched trial plus a separate unpatched negative trial; do not reuse/reset old fixtures. Backend requires the already approved users.row_version schema patch; this feature introduces no migration and grants no production deployment approval.
