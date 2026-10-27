# Task37 verification — 2026-10-27

All following runtime checks were rerun during Task37, not inferred from prior reports. Main C# source from Task35–36 is unchanged by review. Final staged/file checks are completed before the controlled commit; its hash/file list are verified and reported afterward without amend.

| Executed gate | Result |
|---|---|
| dotnet build HotelOps.slnx | PASS,0 warnings/0 errors |
| dotnet test HotelOps.slnx | Exit0; no framework test project in solution; console harnesses run explicitly |
| Activate actual HTTPS/SQL +7 observed SQL lock races | PASS169; ../UserAccountActivation/task37-activation.local.json |
| Task35 parser/transition/session policy | PASS26, executed before SQL primitive suite |
| Task35 activation SQL primitive/rollback/pre-postcommit | PASS27; task37-primitive.local.json |
| UC005 Reauthentication + Reset real HTTPS/SQL | PASS165; task37-reauth-reset.local.json |
| Reset primitive/scope | PASS45; task37-reset-primitive.local.json |
| UC004 credential participant/legacy/commit boundary | PASS26; task37-credential-legacy.local.json |
| UC004 Create + List/Detail/Profile/PATCH + Startup HTTP/SQL | PASS546; task37-create-read-startup.local.json |
| CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / Hardening | PASS14/43/61/49/21 =188 |
| Fresh/upgrade schema compatibility | PASS26; ../UserProfileUpdate/schema-168ad210e1d54747a7052713b92ea88b.local.json |
| Read-only FK/CHECK inventory | PASS collection71 user FK /45 CHECK definitions, not a certified open-work predicate |
| git diff --check | PASS before staging |
| Exact32-file index, cached whitespace and worktree/index blob identity | PASS |
| Index known-secret/literal-credential/forbidden-artifact scan + manual review | PASS, no candidate secrets or forbidden files found |
| Exported index solution + both new verification project builds | PASS, each0 warnings/0 errors |

Receipts named task37-* are local JSON under UserAccountActivation and are excluded from the allowlist. Tests verify the actual cookie/SQL session/CSRF path, strict reason-only input,428/412/409, authorized ALL/BRANCH and negative paths, target soft-delete, SQL audit rollback, real concurrent activation, preservation of credentials/lock/role/membership/operational assignment/sessions, status/deactivate route absence and in-memory secret-leak checks. Real SQL races observe blocked writer SPIDs after the final gate, then deny next request after revocation. No failed gate has been relabeled PASS.

## Owned trial targets

- HotelOps_Canonical_20261027_161959_74a7715d_Fresh — fresh patched empty personnel trial, then UC004/read/PATCH HTTP fixtures.
- HotelOps_Canonical_20261027_162006_5ec6f9d6_Fresh — patched technical trial for Activate/UC005/credential suites and metadata reads.
- HotelOps_Canonical_20261027_162500_37abcdef_Fresh — new separate negative Startup trial, intentionally malformed by inherited schema checks.

Guarded existing setup/schema scripts create disposable databases only; actual names/server/Task17 marker/schema are checked before harness writes. No HotelOpsDB command, new schema migration, production execution, RBAC source or DEV2 business change. Fixtures/databases remain; only owned temporary sessions/roles/faults/certificates are cleaned. Existing artifacts/worktrees are preserved.

Post-suite SELECT-only checks on all three trials:0 live sessions,0 T35/T36 temporary roles,0 T33/T36 fault CHECKs,62 permissions and12 roles. No cleanup failure or catalog count drift observed. Metadata inventory remains71 user FK/45 CHECK definitions; it does not certify open-work semantics.

## Packaging checks

The exact32-file manifest is the only staging allowlist. Runtime JSON/WAV/backups/secrets/binaries/bin/obj/other analysis and generated entities/mappings/DEV2 sources are excluded. Index is checked for exact path equality, cached whitespace, source/worktree blob identity and known secret patterns plus manual review. A unique ignored index export verifies the commit snapshot builds independently of excluded untracked artifacts; results are recorded after the actual build. No directory-wide add, reset, amend or branch/worktree change.

Actual export: obj/AccountActivationRelease/task37-3479d4a28f9a/, not a Git worktree. Solution and UserAccountActivation/UserAccountStatus projects build successfully there with0 warnings/errors. Feature/test source blobs remain identical to the index after the final documentation-only update. Original worktree harness builds retain their reported NU1900 warnings. The generated manifest's extra final blank line was caught and removed before staging; all32 files then passed whitespace checks. Git's LF-to-CRLF notices are line-ending notices, not suppressed build warnings.

181 unrelated pre-existing untracked artifact files were fingerprinted for preservation; no artifact is staged. Hooks inspection found no active hook scripts. The final cached checks and staged/worktree identity are rerun immediately before commit. Task35 historical records and Task36 results are retained unchanged; this Task37 report is the current release gate record.

Some harness builds emit NU1900 because NuGet vulnerability metadata is unavailable; retained, not suppressed. Main solution build is clean. Real network response-loss and deactivation invariants are NOT VERIFIED and are not activation release gates under the approved scope. No production deployment or DEV2 approval is implied by commit.
