# Task81 changelog — additions only

Task81 did not exist at task start. All new files and build/test outputs are inside
`scripts/verification/Task81/`. No pre-existing source, configuration, schema, ledger,
receipt, tracked/staged/unstaged/untracked file was intentionally changed or removed.

## Review deliverables

- TASK81_UC138_C1_DECISION_PACKAGE.md —7 operation contracts, schema/authorization analysis,
  pending decisions, legacy compatibility options and five gate decisions.
- TASK81_DEV2_CONTRACT_REQUEST.md — publisher/replay/event/recipient/transaction/inbox handoff;
  a local request, not a message sent or an implemented contract.
- TASK81_VALIDATION.json — final commands, exit codes, counts, timestamps, scope/limits and safety.
- TASK81_CHANGELOG.md — this additions-only change record.

## Test-only source/project additions

- SqlOverlap.csproj — links the reviewed Task80 runner read-only, with new receipt/output paths.
- SqlReadiness.csproj / SqlReadinessProbe.cs — current metadata/grants, EF gap, duplicate SQL
  index and two-session retained User-lock readiness proof; fixture rollback, no new DDL.
- FilePostflight.csproj / FilePostflight.cs — parallel file hash/postflight and set comparison
  utility, not part of production solution or runtime.

## Evidence/output additions

PREFLIGHT.json, FILE_PATHS.json, FILE_HASHES.json, CURRENT_FILE_PATHS.json,
FILE_NAME_DELTA.json, POSTFLIGHT_FILES.json, inventory-current-path.txt, per-command logs,
execution JSONs and SQL receipts. NEW_FILES.json lists **every new path**, including generated
files in artifacts/{build,overlap,readiness,math,authorization,permission,resource,current-user,
postflight,inventory-fast,names,framework}. These are isolated compiler/test outputs, not
new production architecture or deployed binaries. No existing output location was reused.

## Capture limitations / failed attempts retained honestly

Initial PowerShell inventory failed on hidden .vs .suo with Get-Item lacking Force. Two
subsequent streaming/sequential hash readers and one quadratic Compare-Object reader were
slow; only those task-owned reader processes were stopped. No user/app/build process was
terminated. Native parallel byte reads/set comparison replaced them.

Full21668-file byte inventory completed **after** isolated Task81 builds/tests; do not claim
it was saved before those executions. Initial Git identity/index/worktree checks and Task80
protected artifact hashes were checked before builds. All execution output was constrained
to the excluded new Task81 subtree. Postflight compared all21668 outside-subtree files,
including ignored historical artifacts: bytes and LastWriteTime unchanged relative to full
capture,0 read errors; missing/added outside subtree0. Root .git metadata is handled by separate
HEAD/index/branch/upstream/worktree/status checks, not included in the file hash inventory.

No test, approval or full UC completion is fabricated. Task80 historical results are not
new Task81 results. No C1 schema/mapping/API/notification implementation was applied.
