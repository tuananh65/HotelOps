# Task 29 actual verification

## Before commits

- dotnet build HotelOps.slnx: PASS, 0 warnings / 0 errors.
- dotnet test HotelOps.slnx --no-restore: exit 0; this does not discover the console verification harnesses.
- StartupSchemaGate harness build: PASS, 1 NU1900 warning. Security auditing remains enabled.
- New fresh/upgrade/rollback/replay schema trials: 26/26 PASS. Existing non-sensitive upgrade fixture preserved; stale/ABA checks pass; original SQL scripts unchanged.
- Current runtime harness on new empty patched + new negative trial: 377/377 PASS. List/Detail/Profile/PATCH, real cookie/SQL sessions, scoped runtime test grants, strict PATCH, 428/412/409, two-writer conflict, audit rollback and sensitive-field protection executed again.
- Startup patched/health contract PASS; missing/wrong-type/inaccessible process exits before HTTP responses. Negative process times: 22,060ms / 18,814ms / 15,676ms, separately from SQL timeout and prior cold-start observations. Not a production startup SLA.
- Metadata SELECT-only principal Compatible; hidden-metadata principal denied; caller cancellation and unexpected-configuration classification verified.
- Foundations 167/167 PASS: PermissionService 43, CurrentUserAccessor 14, Authorization 61, ResourceAuthorization 49. No --live main database mode.
- Read-only cleanup SQL on all three new trials: 0 unrevoked fixture sessions, 0 test roles, 0 metadata test principals. Trials/fixtures retained; no HotelOpsDB connection or write.
- 74 candidate paths inspected for excluded artifacts/secrets and source ownership. No literal secret; broad password patterns were manually resolved to generated fixture values. Only bounded Program/DbContext shared changes.

Local receipts: task29-runtime.local.json in this directory and a new schema-*.local.json in UserProfileUpdate. Both excluded from commits. Existing receipts are preserved.

## Commit validation method

Each group is selective-staged from COMMIT_MANIFEST.md; cached file names must exactly match the group. Cached whitespace and secret/artifact path patterns are checked. The exact index tree is exported to a new ignored obj/task29-reviewed-* snapshot and its solution is built before commit. This proves the committed source builds without unstaged profile/gate files masking missing dependencies. Snapshots are retained, not worktrees or branches.

Post-commit hashes, exact file lists and final verification are appended to COMMIT_MANIFEST.md. Task 29 reports remain local and untracked intentionally.

## Completed commit checks

All four index snapshot solution builds PASS, each with 0 warnings/errors; tree IDs match the actual committed trees. Exact file counts 11/4/7/53 and union 74/74 match the manifest, with no outside file. Each staged diff passed git diff --cached --check. After every commit, SHA-256 comparisons confirm all 638 pre-existing files unchanged; no file was deleted.

Final tracked working tree/index clean; 140 excluded/local untracked files retained. Runtime receipts and the five Task 29 reports are not committed. Remote refs rechecked unchanged; feature ahead 4, no push/merge/PR. All trial cleanup checks passed. The only remaining warning is NU1900 on the verification project, not on the validated solution/index snapshots.

External AI-Loop worktree advanced independently to ba50d1b; it was not modified by this task. Its separate log could not be inspected under sandbox ownership checks, and no Git trust configuration was changed.

Final post-commit dotnet build HotelOps.slnx PASS, 0 warnings/errors (54.86s). git diff --check and git diff --cached --check PASS; no tracked/index differences. Final verdict: COMMITTED, with the release restrictions still in force.
