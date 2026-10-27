> Historical Task 32 snapshot. Task 33 now implements trusted reauthentication and the gated reset API; see [current integration](../AdminReauthentication/RESET_INTEGRATION.md) and [executed results](../AdminReauthentication/VERIFICATION_RESULTS.md). Original Task 32 findings below are retained.

# Task 32 executed verification

Date: 2026-10-27 (Asia/Saigon). Implementation is partial; no reset endpoint is exposed.

| Check | Result |
|---|---|
| dotnet build HotelOps.slnx after C# changes | PASS, 0 warnings / 0 errors |
| Reset verification project build | PASS, 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; solution contains no framework tests, console harnesses run separately |
| Conditional reset transaction/scope harness | PASS 45/45 |
| Existing initial/legacy/commit-boundary credential harness | PASS 26/26 |
| UC-004 + List/Detail/Profile/PATCH + Authentication/Startup actual HTTP/SQL suite | PASS 546/546 |
| CurrentUserAccessor | PASS 14/14 |
| PermissionService | PASS 43/43 |
| Authorization | PASS 61/61 |
| ResourceAuthorization | PASS 49/49 |
| AuthorizationHardening | PASS 21/21 |
| Fresh/upgrade/replay/rollback schema dependency | PASS 26/26 |
| git diff --check (final) | PASS |
| Read-only cleanup verification, all three trials | PASS: zero unrevoked sessions, temporary roles, metadata principals, fault CHECK constraints or triggers |
| Actual HTTP reset success/unauthorized/crossbranch/inactive/428/412/no-store | NOT RUN - no reset endpoint, missing trusted reauthentication |
| Real actor/session-bound reauthentication within five minutes | BLOCKED - primitive absent |
| Actual HTTP lost response / provider commit acknowledgement loss | NOT VERIFIED |

546 is a mixed combined suite, not 546 reset tests. Authentication over HTTP covers existing login, SQL sessions, CSRF and temporary first-login restrictions; it does not certify a new reauthentication feature.

## Technical reset cases

Success commits a verified new hash/24h expiry/new rowversion, revokes both seeded sessions and makes the old protected reference fail real SQL validation. Existing profile/status/role/membership snapshot is unchanged. One audit contains actual actor, procedure code, server receipt time and revoked count, with no secret/hash values. One-time take rejects a second read.

Rejects self/inactive/deleted/missing targets, stale retry, malformed version/procedure, dirty context and caller transaction. Faults after credential UPDATE, actual revocation, audit INSERT (before and after SQL), precommit and cancelled request roll back all effects.

Two independent connections/admin IDs with the same token yield one committed reset and one stale result. This suite does not measure lock-wait DMV events or certify every possible scheduler ordering. A fresh-token second rotation invalidates the first password. Post-commit injected exception returns no result and old-token retry preserves the committed state without replay.

Scope helper tests use actual trial SQL membership and grant-path doubles: unbound ALL, BRANCH shared membership, crossbranch denial, PrimaryBranchId alone insufficient, OWN/self denial, branchbound ALL denial, role/time/assignment eligibility, target revocation and restricted session. These are not HTTP reset authorization tests or real reauthentication tests.

## Isolated databases and evidence

- Patched empty HTTP trial: HotelOps_Canonical_20261027_110017_2e3d6dd1_Fresh.
- Patched upgrade/technical trial: HotelOps_Canonical_20261027_110022_fdd9c192_Fresh.
- Negative startup trial: HotelOps_Canonical_20261027_111500_32abcdef_Fresh (later retains deliberately wrong binary(8) token fixture).

Only these newly owned guarded trial databases receive writes. Setup uses master for database creation, never HotelOpsDB. Catalog seeds/schema sources are unchanged.

Local receipts: reset-transaction.local.json, legacy-regression.local.json, create-read-startup-regression.local.json; schema receipt ../UserProfileUpdate/schema-2ece4d590a7f4d399f1bcfc207a3674b.local.json. They contain labels/counts/target metadata, not secrets. Fixtures and all previous artifacts remain retained.

## Intermediate failures / warnings

- First reset run stopped because the harness invoked SqlSessionService.ValidateSessionAsync without its required caller transaction. Harness fixed; no production workaround.
- Second reset run stopped because the audit-after-SQL fault interceptor threw with an open DataReader, preventing clean transaction disposal. Harness now disposes its intercepted reader before throwing. Complete final suite rerun passes; earlier stopped runs are not counted PASS.
- Existing UC-004 harness builds retain NU1900 from unavailable NuGet vulnerability metadata. This environmental warning was already reproduced outside sandbox in prior review; auditing remains enabled.
- Solution and new reset harness final builds have zero warnings/errors.
- An inspection used unsupported PowerShell glob paths and was rerun with explicit paths; optional guessed source paths did not exist. No files were deleted or changed by these inspection failures.

No claim of completed UC-005 HTTP implementation or production readiness. See OUTSTANDING_ISSUES.md.

## Final workspace review

HEAD and staging remain unchanged. Only the existing TemporaryCredentialService.cs is modified; two application files, two harness files, six requested documents and four local JSON receipts are added. All 149 pre-existing untracked status entries remain present. No paths or worktrees were removed.

An independent HEAD change was observed in HotelOps-AI-Loop: 54c8649 to e0f73b0 during this task. This agent performed no Git mutations in that worktree and did not revert its concurrent change. The HotelOps feature branch and the other two worktree HEADs are unchanged.
