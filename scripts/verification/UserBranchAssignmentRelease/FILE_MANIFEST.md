# Exact commit allowlist

Only these 21 paths may be staged. No directory-wide add is permitted.

## Production source (5)

- `src/HotelOps.Api/Program.cs`
- `src/HotelOps.Api/Controllers/UserBranchAssignmentController.cs`
- `src/HotelOps.Api/DTOs/Users/AddUserBranchRequest.cs`
- `src/HotelOps.Api/Services/Users/UserBranchAssignmentAccess.cs`
- `src/HotelOps.Api/Services/Users/UserBranchAssignmentService.cs`

Program.cs contains only the two Add Membership service registrations. Controllers handle HTTP; the service owns business validation, locks, transaction and audit. Generated entities, mappings and RBAC Catalog are excluded.

## Reproducible Task 38 verification (5)

- `scripts/verification/UserBranchAssignment/UserBranchAssignment.Verification.csproj`
- `scripts/verification/UserBranchAssignment/Program.cs`
- `scripts/verification/UserBranchAssignment/WorkflowSqlSuite.cs`
- `scripts/verification/UserBranchAssignment/AssignmentRaceSuite.cs`
- `scripts/verification/UserBranchAssignment/BranchHttpSuite.cs`

## Task 38 documentation (6)

- `scripts/verification/UserBranchAssignment/README.md`
- `scripts/verification/UserBranchAssignment/API_CONTRACT.md`
- `scripts/verification/UserBranchAssignment/AUTHORIZATION.md`
- `scripts/verification/UserBranchAssignment/TRANSACTION_AND_CONCURRENCY.md`
- `scripts/verification/UserBranchAssignment/VERIFICATION_RESULTS.md`
- `scripts/verification/UserBranchAssignment/OUTSTANDING_ISSUES.md`

## Task 39 release documentation (5)

- `scripts/verification/UserBranchAssignmentRelease/README.md`
- `scripts/verification/UserBranchAssignmentRelease/FILE_MANIFEST.md`
- `scripts/verification/UserBranchAssignmentRelease/SECURITY_REVIEW.md`
- `scripts/verification/UserBranchAssignmentRelease/VERIFICATION_RESULTS.md`
- `scripts/verification/UserBranchAssignmentRelease/RELEASE_LIMITATIONS.md`

## Excluded and preserved

All `*.local.json` receipts, runtime evidence, WAV, backups, binaries, bin/obj files, local credentials, secrets and unrelated analysis remain outside the index. Existing Task 20A-37 documentation/artifacts are not newly staged. No DEV2 business source, RBAC Catalog, SQL schema script or generated EF mapping/entity is included. Test certificates/passwords are generated at runtime, not literal credentials in source.
