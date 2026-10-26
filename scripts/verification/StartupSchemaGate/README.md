# Task 28 — Startup schema compatibility gate

Ngày 2026-10-27, branch feature/BE1-database-integration, baseline 20c1063. Task 27 demonstrated that CanConnect health returned 200 on an unpatched database while EF User queries required a missing column. This task adds a read-only prerequisite before HTTP listener startup.

Application changes: one new Data/StartupSchemaValidator.cs and minimal registration/invocation in Program.cs. No controller, middleware, authentication, authorization, DEV2 registration or SQL release script changes.

- [Required SQL contract](SCHEMA_CONTRACT.md)
- [Implementation and exact shared change](IMPLEMENTATION.md)
- [Executed verification](VERIFICATION_RESULTS.md)
- [Deployment and safety limits](DEPLOYMENT_NOTES.md)

Build StartupSchemaGate.Verification.csproj, run the existing UserProfileUpdate/Verify-Schema.ps1 on the reviewed local instance to create new fresh/upgrade trials, then create an additional unpatched trial with UserDirectory/Prepare-Database.ps1. The fresh patched trial must have empty personnel. Never reuse/reset an old fixture database.

```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/StartupSchemaGate/StartupSchemaGate.Verification.csproj
dotnet scripts/verification/StartupSchemaGate/bin/Debug/net10.0/StartupSchemaGate.Verification.dll <new-patched-empty-trial> <new-local-json> <new-unpatched-trial>
```

Harness derives from Task 27 but replaces unsafe-startup observations with required rejection assertions. It deliberately creates a binary(8) column on the verified negative trial after checking the missing-column case, and retains that malformed fixture. Only fixture sessions/test-role grants and temporary test principals are cleaned up. All pre-existing files/artifacts/worktrees are preserved except the authorized Program.cs addition.

Do not stage *.local.json, generated bin/obj, certificates, WAV, credentials or local receipts. No staging/commit/push/merge is part of this task.
