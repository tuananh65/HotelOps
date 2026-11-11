# Host Git Bash â€” one normal merge commit and publication

HOST_COMMIT_READY=YES. Source tree85bd4cfa8816736231449abeb985a5b573fb79ab is unchanged; backend/DI593 plus host frontend95 =688PASS,0finalFAIL/0SKIP. No source corrections. All historical failures are retained.

Cháº¡y trÃªn Windows host trong Ä‘Ãºng worktree. Dá»«ng ngay khi báº¥t ká»³ assertion/lá»‡nh nÃ o lá»—i; khÃ´ng force, amend, rebase, squash hoáº·c tá»± merge PR. KhÃ´ng cáº§n cháº¡y láº¡i tests Ä‘Ã£ Passed náº¿u source khÃ´ng Ä‘á»•i.

## 1. Verify prepared merge and evidence

```bash
set -euo pipefail
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001
FEATURE_BRANCH='feature/dev2-dev3-master001-responsibility-readers'
FEATURE_PARENT='2140522457e89b974cf2d65c64515ec15d733fa9'
INTEGRATION_PARENT='00bdc481cb224d7117a79c95669abc7016f51ba4'
TESTED_TREE='85bd4cfa8816736231449abeb985a5b573fb79ab'
EVIDENCE_ROOT='docs/integration-notes/PR25-FINAL-COORD-001'
EVIDENCE_LIST="$EVIDENCE_ROOT/receipts/final_evidence_paths.txt"
EXPECTED_EVIDENCE=168
EXPECTED_PR=192

test "$(git rev-parse --show-toplevel)" = 'C:/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001'
test "$(git branch --show-current)" = "$FEATURE_BRANCH"
test "$(git rev-parse HEAD)" = "$FEATURE_PARENT"
test "$(git rev-parse MERGE_HEAD)" = "$INTEGRATION_PARENT"
test -z "$(git ls-files -u)"
test "$(git write-tree)" = "$TESTED_TREE"
git diff --quiet
mapfile -t ORIGINAL_PATHS < <(git diff --cached --name-only "$INTEGRATION_PARENT")
test "${#ORIGINAL_PATHS[@]}" -eq 24
git diff --cached --quiet "$FEATURE_PARENT" -- "${ORIGINAL_PATHS[@]}"
test "$(wc -l < "$EVIDENCE_LIST" | tr -d '[:space:]')" -eq "$EXPECTED_EVIDENCE"
powershell.exe -NoProfile -Command "& ([scriptblock]::Create((Get-Content -Raw -LiteralPath 'docs/integration-notes/PR25-FINAL-COORD-001/receipts/Verify-FinalEvidence.ps1')))"
sha256sum "$EVIDENCE_ROOT/05_EXACT_DIFF_AND_HASHES.json"
```

Äá»‘i chiáº¿u manifest SHA256 vá»›i giÃ¡ trá»‹ final response; khÃ´ng nhÃºng hash Ä‘Ã³ vÃ o chÃ­nh cÃ¡c artifacts Ä‘Ã£ hash. Helper kiá»ƒm hash tá»«ng evidence, source blobs, final688 counts, exact evidence list vÃ  merge parents. KhÃ´ng stage bin/obj/node_modules/scratch.

## 2. Explicitly stage evidence and verify all source blobs

```bash
git --literal-pathspecs add --pathspec-from-file="$EVIDENCE_LIST"
git diff --cached --check
diff -u "$EVIDENCE_LIST" <(git diff --cached --name-only "$INTEGRATION_PARENT" -- "$EVIDENCE_ROOT" | LC_ALL=C sort)
test "$(git diff --cached --name-only "$INTEGRATION_PARENT" | wc -l | tr -d '[:space:]')" -eq "$EXPECTED_PR"
git diff --cached --quiet "$TESTED_TREE" -- . ":(exclude)$EVIDENCE_ROOT"
powershell.exe -NoProfile -Command "& ([scriptblock]::Create((Get-Content -Raw -LiteralPath 'docs/integration-notes/PR25-FINAL-COORD-001/receipts/Verify-FinalEvidence.ps1')))"
git status --short
```

Sau evidence staging, full tree Ä‘á»•i lÃ  bÃ¬nh thÆ°á»ng. Lá»‡nh diff so vá»›i TESTED_TREE pháº£i xÃ¡c nháº­n toÃ n bá»™ source ngoÃ i evidence giá»¯ nguyÃªn. Proposed PR paths gá»“m24 original vÃ  exact evidence list, khÃ´ng pháº£i122 integration paths cá»™ng evidence.

## 3. Create and verify exactly one normal merge commit

```bash
git commit -m "merge(dev2): reconcile PR25 with latest fullstack integration"
read -r MERGE_SHA PARENT1 PARENT2 EXTRA < <(git rev-list --parents -n 1 HEAD)
test "$PARENT1" = "$FEATURE_PARENT"
test "$PARENT2" = "$INTEGRATION_PARENT"
test -z "${EXTRA:-}"
git log -1 --format=fuller
git status --short
test -z "$(git status --porcelain)"
```

KhÃ´ng amend Ä‘á»ƒ thÃªm merge SHA vÃ o evidence: giá»¯ placeholder trong committed reports, Ä‘iá»n exact SHA vÃ o PR body/review request khi publish. Chá»‰ dÃ¹ng follow-up normal evidence commit náº¿u tháº­t sá»± cáº§n vÃ  refresh hashes trÆ°á»›c commit Ä‘Ã³.

## 4. Check live refs and push normally

```bash
FEATURE_REF="refs/heads/$FEATURE_BRANCH"
INTEGRATION_REF='refs/heads/integration/fullstack-base'
REMOTE_REFS=$(git ls-remote origin "$FEATURE_REF" "$INTEGRATION_REF")
printf '%s\n' "$REMOTE_REFS"
REMOTE_FEATURE=$(awk -v ref="$FEATURE_REF" '$2 == ref {print $1}' <<< "$REMOTE_REFS")
REMOTE_INTEGRATION=$(awk -v ref="$INTEGRATION_REF" '$2 == ref {print $1}' <<< "$REMOTE_REFS")
test "$REMOTE_FEATURE" = "$FEATURE_PARENT"
test "$REMOTE_INTEGRATION" = "$INTEGRATION_PARENT"
git merge-base --is-ancestor "$REMOTE_FEATURE" HEAD
git push origin "$FEATURE_BRANCH"

REMOTE_AFTER=$(git ls-remote origin "$FEATURE_REF" "$INTEGRATION_REF")
printf '%s\n' "$REMOTE_AFTER"
REMOTE_FEATURE_AFTER=$(awk -v ref="$FEATURE_REF" '$2 == ref {print $1}' <<< "$REMOTE_AFTER")
REMOTE_INTEGRATION_AFTER=$(awk -v ref="$INTEGRATION_REF" '$2 == ref {print $1}' <<< "$REMOTE_AFTER")
test "$REMOTE_FEATURE_AFTER" = "$(git rev-parse HEAD)"
test "$REMOTE_INTEGRATION_AFTER" = "$INTEGRATION_PARENT"
git rev-parse HEAD
```

Náº¿u feature/integration drift, dá»«ng vÃ  bÃ¡o exact SHA; khÃ´ng tá»± reconcile/rewrite/force. Drift sau push cÅ©ng pháº£i bÃ¡o Ä‘Ãºng lÃ  Ä‘Ã£ publish nhÆ°ng cáº§n delta review, khÃ´ng tuyÃªn bá»‘ nguá»“n váº«n current.

## 5. Existing PR and targeted review

Refetch PR25 metadata, actual changed paths vÃ  mergeability báº±ng GitHub connector. DÃ¹ng09_PR25_DESCRIPTION.md, thay HOST_TO_FILL_AFTER_COMMIT báº±ng actual merge/final SHA sau local=remote. Gá»­i Ä‘Ãºng má»™t06_DEV3_FINAL_REVIEW_REQUEST.md chá»‰ sau successful publication; lÆ°u comment URL. KhÃ´ng má»Ÿ PR khÃ¡c.

DEV3_FINAL_REVIEW=PENDING_NOT_SENT hiá»‡n táº¡i. Chá»‰ sau actual APPROVED_SOURCE_ONLY má»›i xin má»™t PL merge decision. SQL19NOT_RUN, business activation BLOCKED; khÃ´ng deploy, activate Workday/Save/Notification hoáº·c merge PR25/51.
