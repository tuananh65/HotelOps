# Windows host - frozen UC139 evidence and publication handoff

HOST_COMMIT_READY=NO_UNTIL_HOST_STAGE_RECHECK. EOF repair is complete in the working tree; index still contains pre-repair evidence. Current tree456PASS (409backend/DI +47host browser); historical688 is previous-tree-only. Host receipt and415canonical frontend blobs verified. No test rerun, source edit or acceptance command is needed. Evidence214paths; proposed PR238paths (24original +214evidence).

Run this block in host Git Bash in one session. It stops on any mismatch/error. Review07 and compare the manifest SHA256 printed below with the final Codex handoff before continuing. The manifest excludes its own hash; do not insert a self-hash into evidence. No fetch/merge/abort/reset/rebase/amend/squash/force operation is part of this continuation.

```bash
set -euo pipefail
trap 'echo "STOP: command failed; preserve state and report exact output." >&2' ERR
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001
FEATURE_BRANCH='feature/dev2-dev3-master001-responsibility-readers'
FEATURE_PARENT='2140522457e89b974cf2d65c64515ec15d733fa9'
INTEGRATION_PARENT='70e1d4ee2c4b63b14543f90c1e315bec39585d80'
TESTED_TREE='b3d8dfe61a528c68002ce6d725d47840604d2e27'
EVIDENCE_ROOT='docs/integration-notes/PR25-FINAL-COORD-001'
EVIDENCE_LIST="$EVIDENCE_ROOT/receipts/final_evidence_paths.txt"
FEATURE_REF="refs/heads/$FEATURE_BRANCH"
INTEGRATION_REF='refs/heads/integration/fullstack-base'
verify_evidence() {
  powershell.exe -NoProfile -Command "& ([scriptblock]::Create((Get-Content -Raw -LiteralPath 'docs/integration-notes/PR25-FINAL-COORD-001/receipts/Verify-FinalEvidence.ps1')))"
}
verify_remote_parents() {
  local refs feature integration
  refs=$(git ls-remote origin "$FEATURE_REF" "$INTEGRATION_REF")
  printf '%s\n' "$refs"
  feature=$(awk -v ref="$FEATURE_REF" '$2 == ref {print $1}' <<< "$refs")
  integration=$(awk -v ref="$INTEGRATION_REF" '$2 == ref {print $1}' <<< "$refs")
  test "$feature" = "$FEATURE_PARENT"
  test "$integration" = "$INTEGRATION_PARENT"
}

# Prepared source identity independently of already-staged evidence.
test "$(git branch --show-current)" = "$FEATURE_BRANCH"
test "$(git rev-parse HEAD)" = "$FEATURE_PARENT"
test "$(git rev-parse MERGE_HEAD)" = "$INTEGRATION_PARENT"
git diff --cached --quiet "$TESTED_TREE" -- . ":(exclude)$EVIDENCE_ROOT"
test -z "$(git ls-files -u)"
git diff --quiet -- . ":(exclude)$EVIDENCE_ROOT"
mapfile -t ORIGINAL_PATHS < <(git diff --cached --name-only "$INTEGRATION_PARENT" -- . ":(exclude)$EVIDENCE_ROOT")
test "${#ORIGINAL_PATHS[@]}" -eq 24
git diff --cached --quiet "$FEATURE_PARENT" -- "${ORIGINAL_PATHS[@]}"
test "$(git config --get core.autocrlf)" = true
test "$(wc -l < "$EVIDENCE_LIST" | tr -d '[:space:]')" -eq 214
sha256sum "$EVIDENCE_ROOT/05_EXACT_DIFF_AND_HASHES.json"
# Paste NEW_MANIFEST_SHA256 from the final repair handoff; no embedded self-hash.
read -r -p 'Expected final manifest SHA256: ' EXPECTED_MANIFEST_SHA256
test "${#EXPECTED_MANIFEST_SHA256}" -eq 64
test "$(sha256sum "$EVIDENCE_ROOT/05_EXACT_DIFF_AND_HASHES.json" | awk '{print $1}')" = "$EXPECTED_MANIFEST_SHA256"
MANIFEST_BLOB=$(git hash-object -- "$EVIDENCE_ROOT/05_EXACT_DIFF_AND_HASHES.json")

# Exact evidence staging, with no other files admitted.
git --literal-pathspecs add --pathspec-from-file="$EVIDENCE_LIST"
diff -u "$EVIDENCE_LIST" <(git diff --cached --name-only "$INTEGRATION_PARENT" -- "$EVIDENCE_ROOT" | LC_ALL=C sort)
test "$(git diff --cached --name-only "$INTEGRATION_PARENT" | wc -l | tr -d '[:space:]')" -eq 238
test "$(git rev-parse ":$EVIDENCE_ROOT/05_EXACT_DIFF_AND_HASHES.json")" = "$MANIFEST_BLOB"
git diff --cached --quiet "$TESTED_TREE" -- . ":(exclude)$EVIDENCE_ROOT"
git diff --cached --check -- src tests scripts frontend
verify_evidence
# Only this successful post-restage check establishes staged readiness.
test -z "$(git ls-files --others --exclude-standard)"
git diff --quiet
PREPARED_COMMIT_TREE=$(git write-tree)

# Immediately before commit: both remote pins must still match.
verify_remote_parents
git commit -m "merge(dev2): reconcile PR25 with latest fullstack integration"
read -r MERGE_SHA PARENT1 PARENT2 EXTRA < <(git rev-list --parents -n 1 HEAD)
test "$PARENT1" = "$FEATURE_PARENT"
test "$PARENT2" = "$INTEGRATION_PARENT"
test -z "${EXTRA:-}"
test "$(git rev-parse 'HEAD^{tree}')" = "$PREPARED_COMMIT_TREE"
git diff --quiet "$TESTED_TREE" HEAD -- . ":(exclude)$EVIDENCE_ROOT"
diff -u "$EVIDENCE_LIST" <(git diff --name-only "$INTEGRATION_PARENT" HEAD -- "$EVIDENCE_ROOT" | LC_ALL=C sort)
test -z "$(git status --porcelain)"
git log -1 --format=fuller
git rev-list --parents -n 1 HEAD

# Immediately before normal push: stop on feature OR integration drift.
verify_remote_parents
git merge-base --is-ancestor "$FEATURE_PARENT" HEAD
git push origin "$FEATURE_BRANCH"
REMOTE_AFTER=$(git ls-remote origin "$FEATURE_REF" "$INTEGRATION_REF")
printf '%s\n' "$REMOTE_AFTER"
test "$(awk -v ref="$FEATURE_REF" '$2 == ref {print $1}' <<< "$REMOTE_AFTER")" = "$MERGE_SHA"
test "$(awk -v ref="$INTEGRATION_REF" '$2 == ref {print $1}' <<< "$REMOTE_AFTER")" = "$INTEGRATION_PARENT"
test "$(git rev-parse HEAD)" = "$MERGE_SHA"
test -z "$(git status --porcelain)"
printf 'PUBLISHED_MERGE_SHA=%s\n' "$MERGE_SHA"
```

The verifier checks raw SHA256, Git-clean-filter predictions and staged IDs, exact evidence inventory,415 canonical frontend inputs, unexpected staged/untracked files and source blobs. core.autocrlf=true means raw CRLF worktree SHA256 can differ from LF Git blob bytes. Full commit tree differs from tested pre-evidence tree only under the evidence directory. No attributes change or raw receipt cleanup.

Strict whitespace checks cover source and current reports/tooling. Only exact captured .txt/.tsv raw receipt paths listed in source_checks.json are excluded by the verifier's evidence diff-check; trailing whitespace/blank lines and historical failures are preserved. Do not replace this with broad directory exclusions or edit raw output to hide warnings.

If any remote pin advanced, STOP and report exact drift; no automatic reconciliation or force. Readiness is for the pinned source only. One normal merge commit, exactly two parents in the specified order. No circular final-SHA amendment: fill actual merge/final SHA in published PR metadata using09 and targeted06; drafts intentionally retain HOST_TO_FILL_AFTER_COMMIT.

After confirmed publication only: refetch existing PR25 metadata/changed paths/mergeability with GitHub connector; require exact238path set against unchanged integration. Update existing PR25 description from09, send exactly one targeted06 request. DEV3 must return APPROVED_SOURCE_ONLY or CHANGES_REQUIRED. Only PL decides final PR merge after approval. No PR25/51 merge, SQL/HTTPS acceptance, shared migrations/live mutation, Save/Assign/notification activation or deployment. PR51 remains INDEPENDENT_MERGE_HOLD.

EOF repair note: do not run Refresh-Evidence.ps1 on this frozen staged handoff. Final manifest raw hashes/predictions reflect repaired working-tree bytes; stagedGitBlob records the observed pre-repair index and may differ until host re-stages. Verify-FinalEvidence.ps1 compares actual staged IDs against gitNormalizedBlob predictions and requires the updated manifest itself staged. Non-verbatim evidence has no broad whitespace exception. Backups and all eight before/after hashes are in verification_summary.json. No full-tree equality check against pre-evidence TESTED_TREE is valid once evidence is staged; only the source-only comparison above is used. No backend/frontend rerun.
