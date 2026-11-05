# Publication scope / selective index plan

Authorization: explicit final source-preservation request dated05/11/2026. Root current branch only; no branch switch, merge, reset, clean, stash or force-push. Parent HEAD4a187320643d0c82f232c6b9dd5a667b8401c280. Target ref handoff/codex-a-20261105 did not exist at remote preflight. Existing integration/dev1-fe-handoff atcf8fb020362f8f64192e7f2f14a10547cebe5b91 is preserved without overwriting.

Selective staging uses PRESERVATION_MANIFEST.json and new handover directory only. Initial root index was empty. Every production source change is Codex A current scope or previously approved shared integration; this commit preserves it, does not grant new runtime/schema approval. Historical snapshot reference excerpts of DEV2 are marked references, not authored changes.

60 text files contain private local SQL host names. Their original bytes stay local. Sanitized copies are archived with provenance, and the same sanitized content is staged at original relative paths using Git blob/index operations so historical projects retain their source filenames on GitHub. The index representation differs from local originals deliberately. Source/receipt hashes in historical reports describe original capture; use publishedSha256 for exported bytes. They are not new SQL test receipts. These originals will remain as local modified/untracked paths after commit as applicable; do not discard them.

Build/source behavior in src/ is not altered by redaction (all sanitized paths are verification/evidence). SQL test scripts require deliberate local configuration and new disposable-trial approval; placeholder host does not authorize execution. No secret/private connection-string bytes are staged.

Five ZIPs are not committed as opaque duplicate packages: all222 text entries are preserved in package-archive with ZIP_INVENTORY.json. All originals stay local. A42-byte historical PDF fixture is preserved in HISTORICAL_PDF_FIXTURE.json as base64 with hash. No database dumps, private certificates, credentials or binaries/build output are staged.

Root scratch inventory3422 files:2561 Codex A/reference candidates preserved as476 unique text blobs with exact original hashes/Git blob IDs;861 DEV2 copy candidates inventory-only, no authorship claim. These are archival/unapproved WIP, not runtime. Review originalPath/knownGitLocation before reusing. Other worktree snapshots are not copied/committed by A.

Current validation: build PASS (2 NU1900);91 xUnit cases PASS; production fallback93 PASS; solution test exit0/no cases. Historical verification remains historical. Source preservation does not complete UC012/037/138/139 or pending shared contracts.
