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

Supplemental audit: IGNORED_EVIDENCE_INVENTORY.json + ignored-evidence-archive1597 unique text blobs; ADDITIONAL_SCRATCH_INVENTORY.json + additional-scratch-archive26 unique schema/review script blobs; SUPPLEMENTARY_SECURITY_REVIEW.json; Preserve-IgnoredEvidence.ps1 / Preserve-AdditionalScratch.ps1. These archives are historical only and not executable deployment artifacts. Database-export/build-only metadata excluded as recorded. PUBLICATION_RECEIPT documents the verified initial source commit; the final child commit adds only evidence.

## Publication restriction — resolved by explicit local-retention decision

Status: HANDOVER_COMPLETE within the clarified scope. User explicitly chose “Giữ local, bàn giao inventory/hash” after review rejection. Source snapshot e859006fcff91b1b913b18c27844a73809a18d8a was already pushed and verified. Automatic approval review rejected the later bulk publish of1597 ignored-evidence text archives and26 additional schema/review script archives: Codex A ownership and sensitivity of the full historical set were not sufficiently established. No workaround or indirect publication of that content was attempted.

The supplementary archive contents remain LOCAL ONLY in ignored-evidence-archive/ and additional-scratch-archive/. Published supplementary inventories contain paths/hashes/classification only; these are not the contents. An explicit decision was requested; the user chose LOCAL retention and inventory/hash handover, so no further archive upload is authorized or pending. Earlier sections describing these archives describe local preservation, not GitHub availability. The476 source backup blobs, expanded ZIP text, current source/tests/Task75-87 evidence in the original e859006f snapshot remain published.

The next evidence-only commit is deliberately limited to receipt, corrected handover reports and path/hash inventories. It excludes all newly blocked archive payloads and their SQL/text contents. Historical SQL integration remains NOT RUN in this handover. Partial/blocked UC classifications and60points are unchanged.

Final user decision: “Giữ local, bàn giao inventory/hash”. The local-only archives and their supplementary preparation scripts are intentionally excluded from publication. This resolves the rejected bulk-egress step by reducing scope; it does not approve or indirectly publish the archive contents. Handover is complete when the metadata/receipt commit is pushed and final remote SHA verified.
