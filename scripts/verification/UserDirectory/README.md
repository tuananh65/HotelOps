# UC-141 User Directory — read-only list slice

Task 17 implements `GET /api/users`. Scope follows Task 16's FIRST_IMPLEMENTATION_PLAN: a caller must have an effective **global USER_VIEW:ALL grant with no branch-bound assignment**. BRANCH/OWN and branch-bound ALL fail closed. No user administration, detail/effective-permissions editor, credential management, schema or catalog change is included.

See [API contract](API_CONTRACT.md), [verification results](VERIFICATION_RESULTS.md), and [remaining limits](OUTSTANDING_ISSUES.md).

## Files and architecture

- `src/HotelOps.Api/Controllers/UsersController.cs`: GET route, existing global policy and HTTP validation response.
- `DTOs/Users/UserDirectoryContracts.cs`: query validation and response allowlist.
- `Services/Users/IUserDirectoryService.cs`, `UserDirectoryService.cs`, `UserDirectoryQueries.cs`: asynchronous, no-tracking database queries and DTO projection.
- The only existing source file changed is `Program.cs`, with one approved scoped DI registration. Existing registrations/auth configuration/middleware order remain unchanged.
- The harness starts the actual built API assembly; it does not replace authentication or UserDirectoryService with a test endpoint. PermissionService scope doubles supplement the real SQL tests without changing catalog grants.

## Reproduce safely

Use Windows Development, .NET 10, and access to the reviewed SQL instance. Setup runs with the available developer principal; least-privilege runtime access is not certified. Never use HotelOpsDB or a database containing real users. The setup/harness require a new isolated target plus a Task17 extended-property receipt; the harness also refuses existing personnel/sessions. It creates no SQL login or grants and does not disable existing guards.

```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/UserDirectory/UserDirectory.Verification.csproj
$directoryTrial = 'HotelOps_Canonical_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N').Substring(0,8) + '_Fresh'
& ./scripts/verification/UserDirectory/Prepare-Database.ps1 -Database $directoryTrial
dotnet scripts/verification/UserDirectory/bin/Debug/net10.0/UserDirectory.Verification.dll $directoryTrial ('scripts/verification/UserDirectory/' + $directoryTrial + '-results.json')
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj --no-restore
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj --no-restore
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj --no-restore
dotnet run --project scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj --no-restore
git diff --check
```

Do not add `--live` to the existing regressions: their live target guards are separate and are preserved. These console verification projects are not discovered by `dotnet test HotelOps.slnx`.

The new setup derives database-targeted copies of the original release scripts in memory, retaining the guards. It creates 82-table combined schema/reference seed in a new database, then marks it Task17. It refuses overwrite and never modifies Task15 scripts/evidence. A setup failure can leave a partial independent database; retain it and use a new name rather than reset it.

Fixture passwords are generated at runtime in memory; API cookies and CSRF tokens stay in memory. The TLS certificate is unique and temporary; its password travels only in the child environment and its PFX is deleted after the child API stops. Only that child's process is stopped. Fixture accounts and audit rows are retained for investigation; all fixture sessions are revoked at the end. A rerun requires a new database and evidence filename. No automatic database deletion or main-database connection is included.
