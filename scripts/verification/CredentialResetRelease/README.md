# Task 34 - Controlled release review for credential reset

Date: 2026-10-27 (Asia/Saigon). DEV1 / UC-005.
Branch: feature/BE1-database-integration. Pre-review HEAD: 92f14634b92d8fda6e6b044b92bf318e76ee3168.
The index was empty; tracked edits plus feature files from Task 32/33 were uncommitted. Existing unrelated artifacts/worktrees are retained.

The developer authorized this review, minimal Task 32/33 security fixes and controlled commits. No push, merge, PR, reset/rebase, production migration or HotelOpsDB change.

## Resulting behavior

Reauthentication verifies the current password for the exact active SQL-backed session and grants a server-only, bounded <=5 minute process-local proof. Reset requires the approved ALL/BRANCH authority, no self/OWN, active target, offline identity attestation and rowversion. Rotation, token, revoke-all and success audit commit atomically before one-time credential delivery.

Task 34 closes an authorization read/commit race: relevant assignment/role/grant/permission/membership/branch rows and key ranges are held until commit. Ordinary SQL revokers must serialize with reset. Six real SQL blocking races verify this, including session/credential protections that already existed.

## Commit packaging

Prefer the requested two logical commits:
1. feat(auth): add secure session-bound reauthentication
2. feat(users): add authorized credential reset workflow

The first contains three reauthentication application files, only its two Program.cs registrations, and the proof contract. It has no dependency on uncommitted reset classes. The second adds reset primitive/orchestration/controllers, remaining DI, complete reproducible harnesses, Task 32/33 docs and this release dossier. The combined harness depends on both features and therefore belongs in commit 2.

Verified commit 1: 34ec7440f8c500f14186851291a5f1045a04bd15 (5 exact allowlisted paths; independent exported index build PASS).

Exact per-commit paths are in FILE_MANIFEST.md. Export the index into a NEW ignored obj/ directory and build there to prove the first snapshot, without switching branches or including untracked source. Build the final index similarly before the second commit. Commit IDs and exact post-commit file validation are reported in the final Git report outside the self-referential commit.

## Reproduction

Existing tracked setup/scripts are dependencies, not new staging candidates. Follow ../AdminReauthentication/README.md with NEW owned patched fresh/upgrade trials and a separate unpatched negative startup trial. Run solution build/test, the reauth/reset harness (now 165 checks), Task 32 primitive/legacy harnesses, inherited UC-004/read/profile/startup HTTP suite and authorization/hardening suites. No suite may target HotelOpsDB.

Runtime receipts, generated snapshots, WAV, backups, binaries, secrets and unrelated Task 20A-31 analysis remain excluded. A commit is packaging only, not approval to deploy or run production SQL.
