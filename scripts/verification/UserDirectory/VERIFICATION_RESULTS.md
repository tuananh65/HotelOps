# UC-141 verification results — 26/10/2026

Baseline: `feature/BE1-database-integration`, HEAD `5299b5e`. Direct origin heads check found develop unchanged and all advertised BE2 heads matching locally available refs; `HEAD..origin/feature/BE2-*` checks were empty. No fetch/merge/commit/push.

## Build

- `dotnet build HotelOps.slnx`: **PASS, 0 warnings / 0 errors**.
- `dotnet build scripts/verification/UserDirectory/UserDirectory.Verification.csproj --no-restore`: **PASS, 0 warnings / 0 errors** after restore.
- Initial harness restore had NU1900 due sandbox NuGet access. Explicit network-enabled forced restore succeeded; package audit was not disabled.
- An initial navigation-name compile error was corrected to the generated `User.UserRoleUsers` navigation; generated entities were not modified.
- `dotnet test HotelOps.slnx`: exit 0, no test cases discovered (solution contains the API only). Actual verification below was run explicitly; this command is not counted as functionality coverage.

## Executed checks

| Harness | Result | Assertions | Method |
|---|---|---:|---|
| UserDirectory.Verification | PASS | 144 | Actual production API child process, HTTPS, cookie authentication, SQL-backed sessions, real EF/SQL queries; additional core scope doubles |
| PermissionService.Verification | PASS | 43 | Existing core test doubles; no --live |
| CurrentUserAccessor.Verification | PASS | 14 | Existing identity/snapshot/DI tests |
| Authorization.Verification | PASS | 61 | Existing real cookie/policy/middleware HTTP tests with synthetic SQL/session adapters; no --live |
| ResourceAuthorization.Verification | PASS | 49 | Existing Cleaning adapter/core/handler tests with synthetic data; no --live |
| Total | PASS | **311** | Assertion counts, not UC counts or 311 independent business scenarios |

Machine-readable UC141 evidence: [uc141-results.json](uc141-results.json). No passwords, hashes, cookies, session credentials or raw user response payloads are stored there.

Verified directory behavior: initially empty canonical personnel; populated list; soft-deleted exclusion; inactive account visibility/filtering; FullName/Username/Email searches with trim; literal wildcard/SQL-special input; branch/department/role/status and combined filtering; default20/max100 paging; stable FullName/UserId ordering; empty/out-of-range pages; nonduplicate multi-role users; invalid values, overflow and mismatched branch/department; recursive DTO property allowlist.

Verified query behavior: two SQL commands for ordinary count/page regardless of row/role count, database OFFSET paging, no credential/lock columns projected, no EF tracking, cancellation propagation. The service never materializes the full user table before applying query filters.

Verified actual API security: anonymous401, invalid protected cookie401, valid global USER_VIEW200, no-permission403, restricted403, OWNER does not inherit USER_VIEW, inactive role/assignment403, expired/future assignment403, branch-bound ALL403 even with a matching filter; permission revocation is reflected on the next request; revoked SQL session rejects the original protected cookie401. Pure USER_VIEW BRANCH/OWN/legacy scope paths and inactive-role-with-explicit-grant denial use the production PermissionService with doubles, because the SQL catalog intentionally remains unchanged and only has the approved USER_VIEW grants.

## Database safety

Only `HotelOps_Canonical_20261026_132341_3498fdd7_Fresh` on the reviewed SQLEXPRESS instance was provisioned/written. It has a new Task17 receipt and initially empty users/sessions. Setup refuses existing target; harness refuses existing personnel/sessions. No HotelOpsDB connection, migration, reset, schema alteration to application entities, SQL permission grant or catalog-grant change occurred.

The harness created owned fixture accounts/assignments and real login sessions/audits with runtime-only random credentials. Hash comparisons confirmed that directory requests preserve users, assignments and branch access; no sessions were created during reads. Exact roles/permissions/grants/branches/departments/reference rows were unchanged over the whole run. Session activity touches are expected existing auth behavior. Fixture sessions were revoked at exit; fixture accounts/audits and the independent database are retained, with no usable credential written into the repository.

## DI and source review

Developer explicitly approved the minimal shared DI change. Program.cs diff is exactly one insertion:

```csharp
builder.Services.AddScoped<HotelOps.Api.Services.Users.IUserDirectoryService, HotelOps.Api.Services.Users.UserDirectoryService>();
```

Real API startup and successful protected GET confirm route/controller/service resolution. No authentication/authorization registrations, middleware order, generated EF models, DEV2 controllers/services or shared connection configuration changed.

`git diff --check`, PowerShell syntax validation and a separate whitespace check for new source/docs: **PASS**. SHA256 comparison of 543 pre-existing files found exactly one changed file, the approved Program.cs registration; the other **542 files remain byte-for-byte unchanged**, including all Task15/16/untracked/WAV/src/main.cs files. There are 13 new task files; none are staged. No commit/push/merge occurred.
