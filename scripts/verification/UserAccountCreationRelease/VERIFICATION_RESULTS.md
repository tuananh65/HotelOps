# Task 31 executed results

Date: 2026-10-27 (Asia/Saigon). Results below were rerun for Task 31; Task 30 reports are not substituted.

| Validation | Result |
|---|---|
| dotnet build HotelOps.slnx (initial and final after C# change) | PASS, 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; no framework test projects, console suites run separately |
| UC-004 participant/legacy harness before extension | PASS 22/22 |
| Expanded participant/legacy/post-commit harness | PASS 26/26 |
| UC-004 combined actual HTTP/SQL suite | PASS 546/546 |
| PermissionService console | PASS 43/43, no --live |
| CurrentUserAccessor console | PASS 14/14 |
| Authorization console | PASS 61/61, no --live |
| ResourceAuthorization console | PASS 49/49, no --live |
| Fresh/upgrade/replay/rollback schema dependency | PASS 26/26 |
| git diff --check | PASS |
| Post-run read-only cleanup on all three trials | PASS: zero live sessions, test roles, metadata principals, fault CHECK constraints and fault triggers |
| Actual transport lost-response / commit acknowledgement loss | NOT VERIFIED |

546 is the combined count, including inherited directory/detail/profile/startup/helper assertions, not 546 creation-only cases. Authentication regression covers real credential login, SQL session expiry/revocation, cookie and CSRF, new temporary-credential login and first-login restriction in this suite. This is not certification of every authentication workflow.

## Owned trial targets

- Empty patched HTTP trial: HotelOps_Canonical_20261027_103537_5c4b26d7_Fresh.
- Patched upgrade/participant trial: HotelOps_Canonical_20261027_103544_1c466ffe_Fresh.
- Negative startup trial: HotelOps_Canonical_20261027_104000_31abcdef_Fresh (later deliberately malformed binary(8) row_version fixture retained).

Only these owned trial targets receive fixtures/schema test changes. Setup connects to master to create new databases; neither setup nor harness targets HotelOpsDB. No original SQL source edits or migrations.

## Coverage and fault injection

HTTP cases verify both ALL grants, six initial role mappings, overposting/type/length validation, inactive/mismatched masters, correct status/Location/no-store/response allowlists, two audits, hashing, branch scope, duplicate field identification, exact retry 409 and concurrent duplicate creation. Trial CHECK constraints force account-audit and credential-update failures; both yield generic 500 without handoff and roll back all workflow rows.

List/Detail/Profile/PATCH regression includes projections, input validation, auth/scope, soft deletion, pagination, If-Match 428/412, duplicate email 409, no-op, concurrent updates and audit rollback. Startup tests cover missing/wrong schema, inaccessible database, sanitized failures, restricted metadata, object-SELECT-only principal and cancellation. Whole-process negative timings were 22008ms, 21934ms and 12523ms; not a SQL duration or production SLA.

Four new participant assertions inject IOException from EF TransactionCommittedAsync after the actual SQL commit. They verify no service credential result, complete committed rows, fresh-context duplicate retry, unchanged credential hash/expiry and unchanged audit/account count. Identity/permission doubles isolate this commit test; actual auth is independently tested over HTTP. No claim of network failure reproduction is made.

Direct sys.indexes/sys.index_columns metadata read on the HTTP trial confirms unique keys for email, employee_code, username and user_id. Fixture records and all trial databases remain retained; cleanup verification is read-only.

## Warnings and initial failures

- First sandbox SQL setup failed SSPI authentication before database creation. Retried outside sandbox with approval; trial setup and suites then passed.
- Both UC-004 harness builds retain NU1900: NuGet vulnerability metadata unavailable. An approved outside-sandbox restore also retained NU1900. Audit was not disabled; no C# warnings/errors.
- Selective staging initially hit sandbox index.lock permission denial, then succeeded with approval. Git emitted LF-to-CRLF working-copy conversion warnings under the existing configuration; whitespace validation remains a separate required gate.
- One inspection command used unsupported PowerShell brace expansion and was rerun with explicit paths; no file changes. An optional historical manifest path did not exist; the new exact manifest was prepared independently.
- No failed assertions occurred in the completed Task 31 suites.

Runtime JSON receipts remain local/untracked and are excluded from staging. New artifacts: task31-participant.local.json, task31-http.local.json, task31-commit-boundary.local.json and schema-d880de9e77d74e62b4dd10af4c5475fb.local.json. No passwords, hashes, cookies or CSRF values are recorded here.

## Release gate

Implementation, review and executed regression checks pass with the explicit unverified transport boundary and environmental warning above. Staged whitespace and exact-file validation must pass before the single authorized commit. Post-commit hash/file list and Git status are reported by the final response; no push/merge.
