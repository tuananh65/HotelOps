# Task 31 - UC-004 final review and controlled commit

Reviewed on 2026-10-27 (Asia/Saigon), DEV1 / FE-01 UC-004.
Branch: feature/BE1-database-integration.
Pre-review HEAD: dda26e4648027a004bca1a2865e0302f0b063122.
Upstream: origin/feature/BE1-database-integration, locally ahead 4 at preflight; no fetch/pull performed.

This release packages POST /api/users with atomic employee, membership, approved initial role, credential and audit creation. Existing Task 30 reports are historical; Task 31 results are in [VERIFICATION_RESULTS.md](VERIFICATION_RESULTS.md).

The only Task 31 C# edit extends the credential harness with an exception injected after actual SQL commit. Application source was reviewed without additional changes. No reset endpoint, schema/entity change or permission catalog grant is introduced.

Use [FILE_MANIFEST.md](FILE_MANIFEST.md) as the exact selective staging allowlist. No runtime receipts, credentials, WAV, backups or binaries are eligible. Existing untracked files and all four worktrees are retained.

## Reproduction

All referenced setup scripts, SQL sources and authorization projects already exist in the baseline. See ../UserAccountCreation/README.md for build and run commands. Setup is deliberately machine-specific in the existing verification harness; adapt its server guard for another workstation, never shared application configuration.

1. Run dotnet build HotelOps.slnx and dotnet test HotelOps.slnx --no-restore. The solution contains no framework test project; console harnesses must run separately.
2. Run scripts/verification/UserProfileUpdate/Verify-Schema.ps1 with the verified local SQL instance to create NEW guarded fresh/upgrade databases and execute 26 schema checks.
3. Create another NEW unpatched trial with UserDirectory/Prepare-Database.ps1. Never reuse an existing personnel database for the HTTP harness.
4. Build both UserAccountCreation verification projects. Run the participant harness on the patched upgrade trial and HTTP harness on the empty patched fresh trial plus the unpatched trial. Use new *.local.json output paths.
5. Run PermissionService, CurrentUserAccessor, Authorization and ResourceAuthorization console projects without --live.
6. Run git diff --check and inspect staged names against the allowlist.

All mutation targets must satisfy the owned canonical trial name, server and Task17 receipt guards. Never target HotelOpsDB. Preserve fixtures/databases for inspection.

Controlled commit message: feat(users): implement atomic employee account creation.
Commit ID is intentionally recorded by the final Git report outside its own commit. No push, merge, PR, amend or rebase is authorized.
