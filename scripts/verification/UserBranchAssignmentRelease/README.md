# Task 39 - UC-007 Add Branch Membership release

Release scope: Task 38's approved Add-only `POST /api/users/{userId}/branches` workflow, its reproducible SQL/HTTPS/race harness and documentation. The user explicitly authorized one controlled commit with message `feat(users): add secure branch membership assignment` after required gates pass. No Push, Merge, PR, reset or rebase is authorized.

Preflight branch: `feature/BE1-database-integration`; HEAD and upstream both `854bf057548f1d50e2da7a184f3046532939ff9c`. Index started empty. The only tracked source difference was two scoped DI registrations in Program.cs; the remaining Task 38 deliverables were untracked. Existing unrelated untracked artifacts and all four worktrees must remain untouched.

See `FILE_MANIFEST.md` for the exact 21-file allowlist, `SECURITY_REVIEW.md` for the policy/security conclusions, `VERIFICATION_RESULTS.md` for this release run and `RELEASE_LIMITATIONS.md` for deployment boundaries. Task 38's documents preserve their original verification history; this folder records Task 39's rerun.

Reproduction uses the existing guarded disposable trial setup and console projects. Run `UserProfileUpdate/Verify-Schema.ps1` for new patched fresh/upgrade trials and `UserDirectory/Prepare-Database.ps1` for a separate unpatched startup-negative trial. Never use HotelOpsDB. Build the solution and relevant harnesses, then run:

```powershell
dotnet scripts/verification/UserBranchAssignment/bin/Debug/net10.0/UserBranchAssignment.Verification.dll <patched-trial> <new-receipt>
dotnet scripts/verification/AdminReauthentication/bin/Debug/net10.0/AdminReauthentication.Verification.dll <patched-trial> <new-receipt>
dotnet scripts/verification/UserAccountActivation/bin/Debug/net10.0/UserAccountActivation.Verification.dll <patched-trial> <new-receipt>
dotnet scripts/verification/UserAccountCreation/Http/bin/Debug/net10.0/UserAccountCreation.Http.Verification.dll <empty-patched-trial> <new-receipt> <separate-unpatched-trial>
```

Use sequential workflows on the same trial and separate empty/negative databases where required. Run CurrentUserAccessor, PermissionService, Authorization, ResourceAuthorization and AuthorizationHardening verification projects independently. Receipts and database fixtures stay local; they are not commit contents. Commit authorization does not authorize production migrations or deployment.
