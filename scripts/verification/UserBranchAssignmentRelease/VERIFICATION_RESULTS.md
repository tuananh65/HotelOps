# Task 39 release verification - 2026-10-28 Asia/Saigon

All mandatory executable verification suites completed successfully on the reviewed Task 38 source. No production C# fix was needed during this review.

| Gate | Result | Local evidence (excluded from commit) |
|---|---|---|
| Task 38 schema/workflow/SQL-race/real HTTPS | PASS - 183 assertions | `../UserBranchAssignment/task39-http-sql-race.local.json` |
| UC-004 + User List/Detail/Profile/PATCH + Startup Schema Gate | PASS - 546 assertions | `../UserBranchAssignment/task39-create-read-startup.local.json` |
| UC-005 Reauthentication/Reset + SQL revocation races | PASS - 165 assertions | `../UserBranchAssignment/task39-reauth-reset.local.json` |
| UC-006 Activation + SQL races + blocked Deactivate routes | PASS - 169 assertions | `../UserBranchAssignment/task39-activation.local.json` |
| CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / Hardening | PASS - 14 / 43 / 61 / 49 / 21 checks | Console executable projects |
| Fresh/upgrade rowversion tests | PASS - 26 checks | `../UserProfileUpdate/schema-487e59834bde4d87b986b466d16d024e.local.json` |
| `dotnet build HotelOps.slnx` | PASS - 0 warnings, 0 errors | Console build |
| Task 38 harness build | PASS - 0 warnings, 0 errors | Console build |
| `dotnet test HotelOps.slnx` | Exit 0 - no framework test cases run | Verification is supplied by the separately executed harnesses above |

Task 38 rerun includes ALL/BRANCH success; OWN/self/source-only/destination-only/branch-bound ALL denial; strict JSON and If-Match; inactive/deleted targets and invalid/inactive Branch; duplicate/revoked membership; latent/dormant destination roles and OPERATIONS_MANAGER safety; one-success/one-stale concurrent Add; audit rollback and postcommit retry; unchanged roles/credentials/status/primary/Department/sessions; secret-free tested HTTP/audit/log values.

Real SQL DMV observations confirm blocking relationships for actor assignment/role/grant/membership/session revocation and target role insert/revoke. Writer-first role insertion/source membership revocation are also tested. Controlled backend clock advancement during real SQL verifies final-gate source expiry cannot use the newly inserted membership to bootstrap authority. Authorization Foundation doubles are identified in their own harness output; they do not replace these real SQL/HTTPS cases.

## Database targets

- Add Membership technical trial: `HotelOps_Canonical_20261027_165037_5c4e2c2b_Fresh`.
- New patched empty fresh trial for UC-004/read/startup: `HotelOps_Canonical_20261027_172804_dd4727ae_Fresh`.
- New patched upgrade trial for UC-005/006: `HotelOps_Canonical_20261027_172810_c7e722a1_Fresh`.
- New unpatched startup-negative trial: `HotelOps_Canonical_20261027_172900_39abcdef_Fresh`.

Names use UTC setup timestamps; this report uses the user's Asia/Saigon date. Guarded setup and harnesses verify trial identity before writes. The negative schema trial intentionally remains malformed. No HotelOpsDB mutation or production migration was performed.

SELECT-only postverification on the Add trial returned roles=12, permissions=62, temporary Task 38 roles=0, unrevoked Task 38 sessions=0, Task 38 UserRole fixture rows=0 and remaining Task 38 audit fault constraints=0. Fixture users/memberships and all receipts are retained.

## Controlled packaging gates

Commit is permitted only after the index matches all 21 paths in FILE_MANIFEST.md, staged and new-file whitespace checks pass, staged content is reviewed for credential material, and forbidden local/runtime extensions are absent. Staged review must preserve the Program.cs change as exactly two DI registrations. Postcommit verification compares the committed path list against the same allowlist and checks index/tracked status and the unchanged upstream/worktrees. Runtime receipts are never the commit evidence payload.

Real network response loss remains **NOT VERIFIED**. Injected postcommit failures prove SQL outcome/stale retry only. No deployment, recovery workflow, future role-writer contract or dependency vulnerability scan is approved by these results.
