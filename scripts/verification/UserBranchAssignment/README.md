# Task 38 — UC-007 Add Branch Membership

Implements only a new, nonprimary `STANDARD` Branch membership under the developer's explicit Task 38 approval. No Department assignment/replacement, primary Branch change, regrant, transfer, removal or Deactivate workflow is exposed.

Production files:

- `src/HotelOps.Api/Controllers/UserBranchAssignmentController.cs`
- `src/HotelOps.Api/DTOs/Users/AddUserBranchRequest.cs`
- `src/HotelOps.Api/Services/Users/UserBranchAssignmentAccess.cs`
- `src/HotelOps.Api/Services/Users/UserBranchAssignmentService.cs`
- Two scoped service registrations in `src/HotelOps.Api/Program.cs`.

Generated entities, mappings, RBAC Catalog and DEV2 source are unchanged. The existing SQL `users.row_version` patch remains a prerequisite; this task introduces no schema migration.

The verification project contains schema/foundation probes, workflow SQL tests, SQL lock-wait races and a real HTTPS child API with SQL-backed login, cookies and CSRF. Runtime receipts contain assertion labels only and remain local artifacts. Binaries, receipts and generated temporary certificates are not source deliverables.

Reproducible harness source: `Program.cs`, `WorkflowSqlSuite.cs`, `AssignmentRaceSuite.cs`, `BranchHttpSuite.cs` and `UserBranchAssignment.Verification.csproj`. Documentation: this README, `API_CONTRACT.md`, `AUTHORIZATION.md`, `TRANSACTION_AND_CONCURRENCY.md`, `VERIFICATION_RESULTS.md` and `OUTSTANDING_ISSUES.md`.

From the repository root:

```powershell
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx
dotnet build scripts/verification/UserBranchAssignment/UserBranchAssignment.Verification.csproj
dotnet scripts/verification/UserBranchAssignment/bin/Debug/net10.0/UserBranchAssignment.Verification.dll <owned-patched-trial-name> <new-local-receipt-path>
```

The harness refuses an existing receipt or a database outside the canonical trial naming convention; it verifies the SQL server, database name and `HotelOps.UserDirectory.Trial=Task17` marker before writes. Use the existing `UserProfileUpdate/Verify-Schema.ps1` trial setup, never HotelOpsDB. The harness targets the same developer-local SQL instance as the existing verification tools. It retains fixture users/memberships while cleaning its own temporary grants, sessions, fault constraint and certificate directory.

Read the other documents here for the exact contract, locking boundary and outstanding workflow dependencies.
