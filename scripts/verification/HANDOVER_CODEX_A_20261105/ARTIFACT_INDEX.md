# Artifact index

- README.md: overview, architecture, auth/schema/lifecycle/workday/notification contracts, Task80–87 history, approvals and next actions.
- FULL_UC_LEDGER.md/.csv/.json:144 SRS UC rows with new owner; UC_LEDGER.csv/.json:75 historically credited Codex A ledger rows.
- SOURCE_OWNERSHIP.csv: current source classification (DEV1/DEV3/DEV2/SHARED), with SRS/contract basis.
- WORKTREES.md, WORKTREE_INVENTORY.json, OTHER_WORKTREE_POSTFLIGHT.json:5 worktrees and read-only preservation checks.
- GIT_AUDIT.json, UNPUSHED_ANALYSIS.json, remote-heads-before.txt: branch/upstream/ref provenance; baseline commits already published elsewhere.
- PRESERVATION_MANIFEST.json, STAGED_FILES.json, SELECTED_PATHS.txt: selective publication plan, original filesystem hashes and Git index blobs. STAGED_FILES excludes its own self hash and later publication receipt by design.
- PHYSICAL_SOURCE_ARTIFACT_INVENTORY.json, SCRATCH_SOURCE_INVENTORY.json, ZIP_INVENTORY.json: source/artifact audit including ignored scratch snapshots.
- scratch-archive/:476 unique archival text sources/WIP, not production-ready; original source paths and Git blob IDs in inventory.
- package-archive/:expanded ZIP text evidence with original ZIP/entry identities; sanitized/:local-host-redacted reference copies.
- LOCAL_ORIGINALS_NOT_PUBLISHED.csv:60 original private-host-bearing text files and6 binary packages/fixture retained locally; sanitized source/text or expanded/encoded equivalent published.
- framework-results/:current91-case TRX/summary/log. Computer metadata redacted, raw/export hashes in SECURITY_REVIEW_RESOLUTION.json.
- build.log, solution-test.log, production-fallback.log, current-validation.json:current handover validations;93 production fallback assertions.
- HANDOVER_VALIDATION.json, SECURITY_REVIEW_FINAL.json, SECURITY_REVIEW_RESOLUTION.json:scope/known warnings and secret-scan results.
- staged-diff-check.log, source-diff-check.log:known historical whitespace failures, not hidden.
- PUBLICATION_PLAN.md, COMMIT_MESSAGE.txt:authorized commit scope and explanation.
- Audit-Handover.ps1, Prepare-Preservation.ps1, Preserve-Scratch.ps1, Preserve-Packages.ps1, Stage-ReviewedSnapshot.ps1:auditable local preparation scripts; not database deployment tools. Audit/preparation outputs are capture-specific; do not rerun into this frozen handover folder.
- PUBLICATION_RECEIPT.json:added after initial push, identifies exact source commit and remote verification.

Historical Task75–87 source/evidence remain in their original verification directories; private instance literals are sanitized in the exported Git snapshot. No fresh SQL acceptance is claimed. No build output, dumps or private certificates are packaged.
