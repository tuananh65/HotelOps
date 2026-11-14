# Current staging status — updated 2026-11-14

**Authoritative user scope:** DEV3 code and DEV3-related documentation only. The provided DEV2 handoff was contextual input; DEV2's 131-path PR #120 is already merged into the shared target and is **not** part of this PR. The previously created DEV2-only handoff synopsis has been removed from this DEV3 staging branch.

- Collection branch: `DEV/pushcodetocombine3dev`; target: `integration/dev1-dev2-dev3-unblock-20261114`.
- **Already staged application code:** exact 19 FE01/FE02 source/test files from PR #117, staged in `c35cab26dfd0aff4f024ab29623c800af8156a1a`. File-by-file remote tree blob SHA check matched PR117 **19/19**.
- **Still not staged:** the main historical DEV3 work from diverged `integration/fullstack-base`, and four local-only patch commits `1930ff5177779e995826fc2e0ad527a45b313d34`, `f7c061745c9279beb0cc94ee91d1e0514306175f`, `680ede49b227ab2d676bbe83f75c5c4c8a6b58ba`, `67b41c7dec08541043311c6aaad5e965399fb9eb`, plus any approved UC004 unmerged source on DEV3's Windows worktrees.
- **No full integration build or merged-source focused regression has been run on this collection branch yet.** Historical focused tests apply only to their exact source commits. Never report the collection as source-complete or merge-ready before local imports, conflict resolution and test evidence.
- Existing PR #121 is Draft and must remain unmerged until the complete DEV3 payload is present and validated.

> ## CURRENT STAGING STATUS — 2026-11-14
>
> - Shared target HEAD `939017f7ea791a666c1ce95fd4afbc73df061af1` already includes **DEV2 PR #120 merged** (131 changed paths).
> - The branch now contains **the exact 19-path FE01/FE02 application/test composition from PR #117**: staged via Git object SHA-preserving copy at commit `c35cab26dfd0aff4f024ab29623c800af8156a1a`. All 19 source blobs match PR #117 HEAD `ad63426bf9913843309bc0c5ceea5eae3957d8a8`; all 19 paths matched the shared target's develop baseline before import. PR #121 is Draft, **not merged**.
> - The four UC010/006/016 local-only commits below remain NOT ON GITHUB and NOT INCLUDED.
> - The full `integration/fullstack-base` source has **not** been merged. Based on exact common ancestor `6aee2a0e692a7814f41c81224709cddfbe1e2ec4`, historical shared target vs fullstack-base have **36 paths independently changed to different blob SHAs**. A mechanical Git merge may raise further path-level conflicts due to the 19 new FE01/FE02 paths and renames. Resolve in a local integration worktree with full builds/tests; never auto-pick entire Auth/DEV1/DEV2 files.
> - No runtime, SQL/HTTPS, route activation, merge to shared target, or completed UC acceptance is claimed.
>
# DEV3 source consolidation → shared DEV1/DEV2/DEV3 integration (2026-11-14)

## Purpose and target

Repository: `tuananh65/HotelOps`.
Collection branch: `DEV/pushcodetocombine3dev`.
Target: `integration/dev1-dev2-dev3-unblock-20261114`.
Target seed HEAD: `939017f7ea791a666c1ce95fd4afbc73df061af1`.
Other source history: `integration/fullstack-base` at `b85743af975b42ea34aae6fa5ef4a4eb9945f22c`.
At source check, the two histories were **diverged**: fullstack-base 150 commits ahead of shared target, shared target 48 commits ahead of fullstack-base. This is not a fast-forward and must not be force-pushed. Full compare API file listing is truncated at 300 files.

**This document is an inventory/receipt only. The branch's presence or this document does NOT mean the listed local-only code has been pushed, that source is accepted, or that the shared integration merge has happened.**

## DEV3 owned scope and historical ledger

DEV3 owns FE01 (12 UCs), FE02 (16 UCs), FE12 (3 UCs): 31 UCs total. Historical coordination ledger: 18/31 COMPLETE_VERIFIED (58.1%, **not independently reaudit-verified**), 13 still incomplete. This figure is not the unrelated project-wide percentage (51.8% reported by the user).

- FE01 6/12 historical completed: UC001, UC002, UC003, UC005, UC011, UC141. Incomplete: UC004, UC006, UC007, UC008, UC009, UC010.
- FE02 12/16 historical completed: UC013, UC014, UC015, UC017, UC018, UC019, UC020, UC021, UC023, UC024, UC025, UC142. Incomplete: UC012, UC016, UC022, UC026.
- FE12 0/3 historical completed: UC138, UC139, UC140 remain incomplete.

## Confirmed accessible GitHub work

- Integration `integration/fullstack-base` HEAD `b85743af975b42ea34aae6fa5ef4a4eb9945f22c`, which contains previously merged DEV3 and dependency contributions; DO NOT blindly replay already-merged changes.
- PR #25 (DEV2 shared writer dependencies), #47 (DEV3 UC006/UC010 transactional readers), #61 (UC007 restricted department change), #63 (UC138 shift preview/save candidate), #66 (UC139 scheduled workday), #68 (UC009 initial grants), #102 (UC009 browser uncertainty), #48 (UC022 room candidate), #50 (UC026 department candidate) are **merged into historical integration/fullstack-base**, not evidence of runtime acceptance.
- PR #51 UC012 Branch RowVersion cutover remains **open/merge hold**, HEAD `be6c7278e1f5749211e97b55c8c21c0727b31fba`. Paired frontend/backend changes need accepted SQL schema/index and version semantics.
- PR #117 FE01/FE02 read-only shared composition is **Draft / unmerged**, HEAD `ad63426bf9913843309bc0c5ceea5eae3957d8a8`; 19 paths (8 FE01, 7 FE02, 4 shared). No route mount or writer activation. This commit can be retrieved from GitHub and integrated into the staging branch with exact path/hash verification.
- UC004 immutable archive source admission `SOURCE_ACCEPTED_READ_ONLY`; PR90→92→93→94 remain unmerged, local source composition may not be on GitHub. Source-merge/runtime admission separately withheld. Archive SHA256 `82642330a5cb164a2d88e4d96b12318ba55682ca5997f5d2e0fad4fdd2e22fd1`; source inventory 20/20, 12/12 blobs matched by ENV. The archive and Windows-only worktrees are not transferred by this GitHub manifest.

## DEV3 local-only verified source — MUST import from developer's machine

The following four full SHAs were **not accessible as commits on GitHub** when checked on 2026-11-14. They need local Git object/ref/worktree access, then scoped cherry-picks or verified file-content import. Never claim included until verified on the staging branch.

| UC | Local commit | Changed scope and receipt |
|---|---|---|
| UC010 Assign recovery | `1930ff5177779e995826fc2e0ad527a45b313d34` | 2 paths: `frontend/src/users/useAssignUserRole.ts`, `frontend/tests/assign-user-role-router-integration.test.mjs`; 556/556 reused evidence |
| UC010 Revoke effective-grant impact | `f7c061745c9279beb0cc94ee91d1e0514306175f` | 5 paths: `UserRoleRevocationService.cs`, `RevokeRoleGrantImpact.cs`, `RevokeRoleResponsibilities.cs`, verification `Program.cs`, `GrantImpactChecks.cs`; 129/129 no-SQL PASS; backend build PASS |
| UC006 Deactivate no-replay after 503/remount | `680ede49b227ab2d676bbe83f75c5c4c8a6b58ba` | `frontend/src/users/UserDeactivationCandidate.tsx` + `frontend/tests/user-deactivation-candidate-browser.test.mjs`; 64/64 API/browser + 97/97 Auth PASS |
| UC016 Floor/Area Deactivate no-replay after 503/remount | `67b41c7dec08541043311c6aaad5e965399fb9eb` | `frontend/src/structure-deactivation/StructureDeactivationPanel.tsx` + `frontend/tests/structure-deactivation-browser.test.mjs`; 91/91 focused PASS |

These are source increments, **not** whole-UC acceptance; page-lifetime reservation is not durable server-side idempotency.

## Priority remaining blockers (source and acceptance)

- UC004: ENV-001 isolated disposable DB/HTTPS trust/lease, schema and principal matrix, persistent read-back; source promotion not yet approved.
- UC006: DEV1/DEV2 current responsibility writers and global/User-feature lock protocol, SQL/HTTPS race/rollback acceptance.
- UC007: role-bearing branch/department transfer and policy/contracts.
- UC008: Update role stale-version contract.
- UC009: source initial grants integrated; broader update/replacement and runtime gates still open.
- UC010: DEV1 FE03/04/05/11/13 responsibility owners; DEV2 reviewer/triage/fulfillment/warehouse/custody writer coverage; runtime acceptance.
- UC012: PR51 schema RowVersion/unique index/paired ETag cutover and trial.
- UC016: current Operational Task/child location/reference writer coordination and SQL race acceptance.
- UC022: room lifecycle canonical status engine and Cleaning/Inspection/Incident/Asset/RoomBlock, history/audit contracts.
- UC026: Department deactivation Warehouse/Asset/Task/Internal Request live dependencies.
- UC138: FE14 atomic notification source producer, full Save/Assign and remaining operations, SQL acceptance.
- UC139: unscheduled start notifications, Pause All writer contract, scheduled SQL acceptance.
- UC140: accepted shift handover schema, immutable snapshot, acknowledge/reject, notifications.

## Safe consolidation procedure — work on local developer machine

1. `git worktree list --porcelain` across DEV3 checkouts; verify four local SHAs are reachable in local object stores and working trees are clean. Preserve all unrelated dirty/untracked files without reset/clean.
2. Fetch shared target and fullstack-base. Work on this staging branch (created from the shared target), and merge `origin/integration/fullstack-base` with **no force**, resolving conflicts against actual DEV1/DEV2 changes. Check commit ancestry and changed paths first; prefer shared target for unrelated DEV1/DEV2 ownership, and preserve DEV3 approved source.
3. Import exact PR117 composition with source/hash verification, avoiding duplicate or stale overwrites.
4. Cherry-pick the four local-only commits by SHA when reachable; when not reachable across clones, use a local Git fetch/ref or verified patch transfer without losing parent identity. Resolve overlaps in dependency-safe order: shared integration → FE01/FE02 composition → UC010 Assign/Revoke → UC006 → UC016.
5. Check whether UC004 other not-yet-published source exists locally; stage it **only with explicit source admission/scope review**, never treat an immutable archive or unapproved runtime candidate as safe to activate.
6. Verify diff vs shared base, complete staged manifest, API/backend build, frontend typecheck/build and relevant focused regression suites. Keep all `[NonController]`, immutable release gates, route defaults and no live SQL/HTTPS constraints.
7. Push only the staging branch without `--force` or `--force-with-lease`; use a Draft PR targeting the shared integration branch. Update this document with exact imported commit IDs, conflict resolutions, source/hash manifest and test receipt.
8. Do not merge the Draft PR while source/import/test completeness is unknown. Do not claim production/runtime acceptance from Git publication.

## Operational ownership

After a fully tested consolidation, a single DEV3 operator can use the shared integration checkout for **local development, build and offline tests** independently. Git write access is determined by GitHub repository permissions/branch rules; Git aggregation does not itself grant SQL provisioning, trusted credentials, runtime activation, bypass of security guards, or production acceptance.
