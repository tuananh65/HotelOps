# Task 35 verification — 2026-10-27

Baseline HEAD `1a969e3731d12d430a250e219caa1e1fbfeb52b9`, branch feature/BE1-database-integration. Baseline index and tracked worktree clean; upstream at same HEAD. Existing local artifacts/worktrees preserved. No commit/stage/push/merge.

| Check actually executed | Result |
|---|---|
| Solution build | PASS, 0 errors / 0 warnings |
| dotnet test HotelOps.slnx --no-restore | Exit0; solution has no framework test project; console harnesses run separately |
| UC-006 parser/transition/session policy | PASS 26 assertions |
| New SQL activation/helper harness | PASS 27 assertions, activation-final.local.json |
| Metadata-only dependency inventory | PASS collection: 71 FK / 45 CHECK definitions; NOT proof of open-work safety |
| Reauthentication + Reset real HTTPS/SQL regression | PASS 165 assertions, reauth-reset.local.json |
| Reset transaction/legacy scope regression | PASS 45 assertions, reset-primitive.local.json |
| Credential participant/legacy/commit-boundary | PASS 26 assertions, credential-legacy.local.json |
| UC004 + List/Detail/Profile/PATCH + Startup schema HTTP/SQL | PASS 546 assertions, create-read-startup.local.json |
| CurrentUserAccessor / PermissionService / Authorization / ResourceAuthorization / Hardening | PASS 14 / 43 / 61 / 49 / 21 =188 assertions |
| Fresh/upgrade schema trials | PASS26; existing guarded patch scripts, not a new schema change |
| git diff --check and independent no-index whitespace checks | PASS, including all14 new source/harness/documentation files |

New SQL checks include actual server/DB guard, compatible startup, ALL activation, actual rowversion change, preservation of credential/locked_until/profile/sessions/grants/memberships, narrow actor/target audit, stale retry/no-op, self rejection, hard-blocked deactivation, unauthorized actor, mismatched/revoked SQL session, missing/deleted target, inactive actor, revoked assignment, audit/precommit rollback, postcommit exception/stale retry, simultaneous admins one success/one stale, real BRANCH/shared membership, cross-branch denial and OWN denial. Password hashes/sessions used by tests remain in memory/owned SQL, never in receipts.

Trials created/used only for this run:

- HotelOps_Canonical_20261027_153435_1def3339_Fresh: empty patched personnel trial for UC004/read/PATCH HTTP suite.
- HotelOps_Canonical_20261027_153441_72eb1c7f_Fresh: patched technical trial, schema upgrade fixture plus unique test fixtures.
- HotelOps_Canonical_20261027_154000_35abcdef_Fresh: separate negative startup trial (intentionally malformed by inherited harness).

Schema receipt: ../UserProfileUpdate/schema-e5d50c6ef9f749de8838d0ab82262c3b.local.json. Other JSON in this directory is local runtime evidence, excluded from source packaging. Trials/fixtures retained; harness-owned sessions revoked and temporary grant roles cleaned in finally. No HotelOpsDB command was issued.

Final SELECT-only cleanup verification on the technical trial: zero live T35 sessions, zero temporary T35 roles and zero T35 role assignments. Fixture users/memberships/history remain in the disposable database. Final HEAD/upstream unchanged; index and tracked diff empty; all three other worktree HEADs unchanged.

Initial new harness restore/build emitted NU1900 because NuGet vulnerability metadata could not be fetched. Build succeeds; this warning is retained and not suppressed. Main solution build has zero warnings. Earlier activation-sql.local.json has23 checks; activation-final.local.json has27 after four additional negative cases; the latter is current.

NOT VERIFIED / NOT RUN: UC-006 HTTP428/412/CSRF/error mappings, activate-specific final-commit authority revocation race, deactivate/open-work/last-admin races, UC-006 deactivate session-revocation/rollback, actual network response loss. No PASS inferred from existing UC005 tests. Both HTTP mutations remain BLOCKED.
