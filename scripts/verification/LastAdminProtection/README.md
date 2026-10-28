# Last-admin protection - Tasks 41-43

GlobalAdminSerialization enforces the approved mutation-time durable administrator invariant
inside caller-owned SQL Server transactions. Existing integrated writers: HTTP admin reset,
raw conditional reset, legacy Apply AdminReset, Create User and Profile update. No Deactivate
or Role Revoke endpoint is introduced. No production schema/catalog/DEV2 change.

- EFFECTIVE_ADMIN_DEFINITION.md: effective grants, supported regular credentials, SQL identifier
  ambiguity, finite lockout and explicit future-expiry limitation.
- MUTATION_PATH_MATRIX.md: current reachability and coverage classification for every writer.
- LOCK_PROTOCOL.md: current global-first SQL lock/transaction ownership contract.
- INTEGRATION_VERIFICATION.md: Task 43 evidence followed by historical Task 42 results.
- REMAINING_UNPROTECTED_PATHS.md and INTEGRATION_BLOCKERS.md: external writers, future expiry,
  trusted raw-caller authorization, load and deactivation dependencies.
- INTEGRATION_DECISION.md / SQL_VERIFICATION_RESULTS.md: historical Task 42 / Task 41 records.
- RELEASE_SUMMARY.md / FILE_MANIFEST.txt: controlled release scope and exact source allowlist.

Reproduction from repository root:

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx
dotnet build scripts/verification/LastAdminProtection/LastAdminProtection.Verification.csproj
dotnet scripts/verification/LastAdminProtection/bin/Debug/net10.0/LastAdminProtection.Verification.dll <guarded-patched-trial> <NEW-local-receipt>
dotnet scripts/verification/LastAdminProtection/bin/Debug/net10.0/LastAdminProtection.Verification.dll <guarded-patched-trial> <NEW-local-receipt> --reset-http
```

Default mode is the independent 26-assertion SQL foundation. --reset-http runs actual HTTPS
API plus SQL writer/race tests; --writers-http focuses the new writer cases; --reset-create-order
runs the controlled legacy lock-order probe. These modes never certify Deactivate/Role Revoke.
Console harnesses must execute separately: dotnet test solution does not discover their cases.

Use the existing guarded canonical trial setup/rowversion patch. Harness verifies exact local
server, canonical database name, Task17 marker, compatible schema and no pre-existing UserRole
assignments before fixture writes. Never target HotelOpsDB. Tests preserve trials/owned users/
audits while cleaning only their own temporary roles, grants, sessions, fault constraint and
TLS/process resources. Credentials are generated in memory; receipts contain assertion labels
only. Runtime receipts, preflight hashes, WAV, bin/obj and certificates are excluded from commit.
