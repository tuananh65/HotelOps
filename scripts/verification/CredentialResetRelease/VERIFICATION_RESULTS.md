# Task 34 verification results

Date: 2026-10-27 (Asia/Saigon). Results rerun for this review after the security fix.

| Check | Result |
|---|---|
| Reauthentication + Reset SQL/HTTPS harness | PASS 165/165 |
| Six commit-boundary revoke races | PASS 18 assertions included above; actual blocking SPID/wait observed |
| Reset transaction/scope primitive | PASS 45/45 |
| Initial/legacy/commit-boundary credential | PASS 26/26 |
| UC-004 + Directory/Detail/Profile/PATCH + Authentication/Startup | PASS 546/546 |
| CurrentUserAccessor/PermissionService/Authorization/ResourceAuthorization/Hardening | PASS 14+43+61+49+21 =188/188 |
| Fresh/upgrade/replay/rollback schema dependency | PASS 26/26 |
| Solution build after security fix | PASS 0 warnings / 0 errors |
| New reauth/reset harness build | PASS 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; solution has no framework test discovery; console suites run separately |
| First exported index snapshot build | PASS, 0 errors; NU1900 metadata warnings only |
| Second exported index snapshot build | PASS, 0 errors; NU1900 metadata warnings only |
| git diff --check / staged check | PASS |
| Three-trial SELECT-only cleanup audit | PASS: zero live sessions, temporary roles/principals, fault constraints/triggers |
| Actual wire response-loss/provider commit acknowledgement loss | NOT VERIFIED |

165 combines service/backend-clock, real HTTPS API and SQL cases; not165 independent HTTP reset scenarios. Proof expiry uses controlled backend time with actual SQL/workflow and final-gate rollback; the child API uses normal real time. No production time override/debug route exists.

## Trial targets / local evidence

- Fresh HTTP: HotelOps_Canonical_20261027_113901_63679d64_Fresh.
- Patched technical upgrade: HotelOps_Canonical_20261027_113909_8a7d42ee_Fresh.
- Negative startup: HotelOps_Canonical_20261027_121000_34abcdef_Fresh.

Local outputs under ../AdminReauthentication/: task34-reauth-reset.local.json, task34-reset-primitive.local.json, task34-credential-legacy.local.json, task34-create-read-startup.local.json.
Schema receipt: ../UserProfileUpdate/schema-4dc957637d0d470191a11842a221c935.local.json.
Receipts contain labels/counts/target metadata only and remain excluded.

Six race fixtures alter only newly owned trial roles/assignments/grants/memberships/sessions/users. DMV observations prove actual SQL lock waits, not a scheduling delay. Owned role/fault fixtures are removed and sessions revoked; databases/users retained. HotelOpsDB is never targeted.

## Warnings / limits

Existing UC-004 harness builds and isolated snapshot restore retain NU1900 due unavailable NuGet vulnerability metadata; an approved outside-sandbox restore of snapshot 1 also retained it. Auditing remains enabled. Main solution and new/reset harnesses have no C# warnings/errors. Git stage also emits LF-to-CRLF working-copy conversion warnings under existing configuration; whitespace checks pass. No failed runtime assertion has occurred in the Task 34 completed suites.

Task 33 historical reports retain their execution results; their former in-flight grant limitation is superseded by this reviewed fix, not retroactively claimed tested then. Production deployment/clock/TLS/multi-instance and actual wire delivery remain outside certification.

Final commit hashes/file lists and Git status are checked and reported after each commit; no self-referential hash is embedded in its own commit.

Both snapshots were exported from the index into unique ignored obj/CredentialResetRelease directories and built there. Snapshot 2's verification document was then updated with the observed result; the staged application source blob IDs remain unchanged. Generated snapshots/staging patch are retained and excluded.
