# Task45 - security review and verification rerun

2026-10-28 Asia/Saigon, DEV1. Baseline dc81915917142de6b640221a1f428aba7e9331fc,
feature/BE1-database-integration, upstream 0/0, index initially empty. Three tracked changes
and all new Task44 files predated Task45. Task45 changes only release/review documentation;
production C# is unchanged from Task44. Source conclusions and exact 18-file commit allowlist
are in RELEASE_SUMMARY.md. No separate human security audit or formal SRS/PO approval is claimed.

## Required gates rerun

| Scope | Result | Fresh evidence / method |
|---|---|---|
| UC010 actual HTTPS/SQL/race | PASS 226 | task45-https-sql.local.json; actual API, real SQL-backed login/session, five positive envelopes and fail-closed negatives |
| LastAdmin SQL foundation | PASS 26 | task45-lastadmin-foundation.local.json; fixture SQL mutations, not HTTP Revoke/Deactivate |
| LastAdmin HTTP integration | PASS 108 | task45-lastadmin-http.local.json; --reset-http actual HTTPS/SQL existing writers |
| UC004/Profile/List/Detail/Auth/Startup Schema Gate | PASS 546 | task45-uc004-profile-startup.local.json, startupGateVerified=true; includes patched/unpatched/wrong-type/inaccessible startup and least-privilege metadata checks |
| UC005 | PASS 165 | task45-uc005.local.json; reset/reauthentication and real SQL authority races |
| UC006 | PASS 169 | task45-uc006.local.json; actual SQL/HTTP Activate |
| UC007 | PASS 183 | task45-uc007.local.json; actual SQL/HTTP Add Branch |
| Authorization Foundation | PASS 14/43/61/49/21 | CurrentUserAccessor, PermissionService, Authorization, ResourceAuthorization, AuthorizationHardening; dotnet run --project <suite> --no-restore; doubles/synthetic loopback, not SQL credential certification |
| Fresh/upgrade schema verification | PASS 26 | existing Verify-Schema.ps1; ../UserProfileUpdate/schema-70bc846deac144578cab3d4649e4408f.local.json |
| Actual SQL schema/catalog inspection | PASS, 12 roles | task45-schema-catalog.local.json; SELECT-only identity-guarded inspector |
| Solution build | PASS, 0 warnings/errors | dotnet build HotelOps.slnx |
| Solution framework test command | Exit 0; no framework test cases discovered | dotnet test HotelOps.slnx; executable console harnesses above ran separately |

UC010 rerun proves real Assign/Assign and both Assign/Add orders; four controlled writer types
(actor assignment, actor permission grant, candidate Role and candidate RolePermission) in both
orders, retained SQL SPID/LCK_M blocking, no resurrection, stale412, audit rollback, timeout503,
cancellation removing the SQL waiter, deliberate inverse-order SQL deadlock rollback and fresh
retry. Expiry during audit wait returns403; isolated last-durable-admin expiry returns409 with
whole rollback. Canonical roles/grants/permissions/Branch/Department are equal before/after.
These controlled fixture writers do not open Role Revoke or catalog-management APIs.

Fresh isolated UC010 trial: HotelOps_Canonical_20261028_075103_9e1ee2e8_Fresh; only after that
suite completed, UC007 then UC006 ran sequentially there. LastAdmin foundation then HTTP then
UC005 ran sequentially in HotelOps_Canonical_20261028_075110_1efcd85c_Fresh. Empty-patched
UC004 trial: HotelOps_Canonical_20261028_075300_45abcdef_Fresh; separate unpatched negative:
HotelOps_Canonical_20261028_075301_45fedcba_Fresh. All identity-guarded canonical Task17 trials.
Existing setup/rowversion scripts were used unchanged; no HotelOpsDB access.

First schema setup attempt inside sandbox failed at Windows SSPI authentication before database
creation. The authorized rerun outside that sandbox passed. No SQL/HTTP assertion failed in the
completed Task45 runs. Some harness builds emit NU1900 (NuGet vulnerability metadata unreachable);
this is disclosed and dependency vulnerability scanning remains NOT VERIFIED. Solution build
has zero warnings. Git LF/CRLF notices are conversion notices, not whitespace errors.

No production source, schema, RBAC Catalog, entity/mapping or DEV2 behavior changed in Task45.
No Revoke/Deactivate/regrant/combination endpoint. Existing external-writer/expiry/load/deployment
limits remain; implementation approval does not replace PO/SRS approval. Runtime JSON receipts,
WAV, binaries, certificates and unrelated artifacts are excluded from the controlled commit.
The staged file/secret/whitespace gates and post-commit checks follow RELEASE_SUMMARY.md; the
commit hash and final index/tree status are reported after executing them.

The Task44 evidence below is historical and remains preserved; it is not summed with Task45.

---
# Task44 approved v1 - implementation and final verification

2026-10-28 Asia/Saigon. DEV1 / FE-01. Baseline HEAD dc81915917142de6b640221a1f428aba7e9331fc,
feature/BE1-database-integration, upstream origin/feature/BE1-database-integration, 0/0 at preflight.
Tracked tree/index initially clean; earlier untracked Task44 review/probe/receipts and all other
artifacts/worktrees preserved. P1-P12 now explicitly approved for technical Task44 implementation.
No replacement of formal SRS/PO approval is claimed. No Commit/Push/Merge or production deployment.

## Final executed gates

| Scope | Result | Method / new local-only receipt |
|---|---|---|
| Complete approved UC010 v1 actual HTTPS/SQL suite | PASS 226 assertions | UserRoleAssignment.Verification / AssignmentHttpSuite; task44-approved-http-complete.local.json |
| LastAdmin independent SQL foundation | PASS 26 assertions | LastAdminProtection default; task44-approved-lastadmin-foundation.local.json |
| LastAdmin actual HTTP/SQL existing writer regression | PASS 108 assertions | LastAdminProtection --reset-http; task44-approved-lastadmin-http.local.json |
| UC004 / Profile / Auth / List / Detail / Startup Schema Gate | PASS 546 assertions | UserAccountCreation.Http.Verification; task44-approved-uc004-profile-startup.local.json |
| UC005 reset / reauthentication / authority races | PASS 165 assertions | AdminReauthentication.Verification; task44-approved-uc005.local.json |
| UC006 Activate | PASS 169 assertions | UserAccountActivation.Verification; task44-approved-uc006.local.json |
| UC007 Add Branch | PASS 183 assertions | UserBranchAssignment.Verification; task44-approved-uc007.local.json |
| Authorization Foundation | PASS | CurrentUserAccessor 14, PermissionService 43, Authorization 61, ResourceAuthorization 49, AuthorizationHardening 21; dotnet run --project <suite> --no-restore, isolated doubles/loopback, not live SQL certification |
| Existing fresh/upgrade schema patch verification | PASS 26 assertions | Existing Verify-Schema.ps1; ../UserProfileUpdate/schema-9bf5002d9b294bce8eb3e5128e88dc35.local.json |
| Solution build | PASS, 0 warnings/errors | dotnet build HotelOps.slnx after final C# changes |
| Solution test command | Exit 0, no framework test cases discovered | dotnet test HotelOps.slnx; actual executable harnesses ran separately |
| Source/whitespace/secret review | PASS | Working/new-file whitespace and scoped source diff review; no staging, no credential-bearing runtime artifact is a source deliverable |

Some console harness builds emit NU1900 because NuGet vulnerability metadata is inaccessible;
compilation passes, warnings are not suppressed, dependency vulnerability scanning is NOT VERIFIED.
Git may report LF -> CRLF conversion notices; they are distinct from whitespace errors.

Earlier incremental approved-source runs passed 205, 217 and 223 assertions, each before further
cases or the final LastAdmin status translation. They are preserved, not added to the final
226 count. Final complete suite runs the final production source, including safety409 mapping.
Other existing APIs were unchanged by that final role-local mapping; their regressions above
remain applicable. No failed SQL/HTTP implementation assertion was promoted to a PASS receipt.
An initial envelope-generation helper stopped on an ambiguous documentation row before writing
its file; generation was restricted to the exact five approved appendix rows, and all five
actual API positive cases and drift negatives passed. No catalog or approved policy was changed.

## What the 226 actual HTTPS/SQL assertions prove

- Production child API assembly/DI runs on pinned ephemeral loopback HTTPS, real login, SQL
  sessions, cookies and CSRF. No synthetic actor replaces authentication or service authorization.
- All five approved role/Department envelopes produce201, exact response DTO, new committed
  ETag and one branch-bound UserRole with server actor/time, IsActive=true and ExpiresAt=null.
  CREATE audit matches the committed row and contains no password/hash/session material.
  Credentials/status/PrimaryBranch/Department and all pre-existing target sessions are preserved;
  the original target cookie remains valid. A custom-named actor with both ALL grants also works.
- SYS_ADMIN/OWNER/OM/DH/Reception/inactive/custom/unknown/lowercase candidate codes denied403;
  USER_MANAGE-only, ROLE_MANAGE-only, bound ALL, BRANCH, OWN, DEPARTMENT actors denied403.
  Self and restricted session forbidden; anonymous401; existing CSRF400; missing tag428;
  malformed strong tag400; strict duplicate/unknown/type/null/fractional/overposted JSON400.
- Secondary Branch, missing/revoked/future membership, Department mismatch/inactive, inactive
  Branch and target state fail closed. Inactive candidate Role and added administrative grant
  fail403. Missing/deleted target behavior retains authorized404 convention.
- Every existing UserRole path is409: exact duplicate, inactive, expired, future, unbound and OM.
  A distinct second role is also409. Old-token retry after success is412. No regrant or existing
  role update is performed, and denied requests preserve version/roles/credentials/sessions/audit.
- Concurrent actual Assign/Assign yields one201/one412. Deterministic Assign-first and Add-first
  HTTP schedules pause the real audit INSERT, observe the second request waiting on the first
  SQL SPID, then produce one201/one412 through the same aggregate User.RowVersion.
- Four controlled writer types are tested in BOTH orders: actor UserRole deactivation,
  actor ROLE_MANAGE grant scope revocation, candidate Role protected-flag drift, and candidate
  RolePermission scope mutation. Writer-first changes committed before acquisition cause403
  with unchanged target. Assign-first writers demonstrably block on retained locks until201
  commits; next fresh-target request sees changed SQL and returns403. These are fixture SQL
  writers, not production grant/revoke/catalog APIs. All canonical fault changes are restored.
- Authority expiry while the real audit is paused causes final-gate403 and whole rollback.
  On a separately isolated trial, snapshot proves the expiring actor is the sole durable admin;
  when that path expires during the wait, preservation returns409 and rolls back role/token/audit.
  This is mutation-time refusal, not a guarantee against future time-driven eligibility loss.
- Actual SQL audit CHECK failure returns sanitized500/no-store with complete rollback. DMV
  observes global lock blocking; timeout503 leaves target unchanged. Client cancellation reaches
  the blocked SQL guard, waiter disappears while owner holds its lock, and target is unchanged.
  Deliberate inverse-order Role writer creates a real SQL deadlock; victim500 rolls back and
  a fresh retry succeeds. No automatic retry or inverse-order application writer is installed.
- Information-level API logs checked against generated passwords, hashes, cookies, CSRF and PFX
  secret. Canonical Role/RolePermission/Permission and Branch/Department before/after equality
  is asserted after fixture cleanup. No lasting catalog/master changes are left by the suite.

Independent foundation simulated status/role removals are SQL fixture tests, not Deactivate /
Role Revoke HTTP tests. The new endpoint performs a first-role INSERT only. General SoD, global
role delegation, regrant or reducing workflows are not certified by these results.

## Trial identities and reproducibility

Fresh disposable setup uses the existing canonical setup and guarded User rowversion patch.
HotelOps_Canonical_20261028_071628_e39c044a_Fresh: initial Assign suite then sequential LastAdmin/
UC005/006/007 regressions. HotelOps_Canonical_20261028_071624_767e2578_Fresh: empty-patched UC004/
Profile/Startup, then later Assign suites (no simultaneous writer suites on one trial).
HotelOps_Canonical_20261028_072000_44abcdef_Fresh: separate unpatched negative startup trial.
HotelOps_Canonical_20261028_074000_44fedcba_Fresh: new isolated final226 suite, canonical setup
plus the existing guarded rowversion-forward script, no earlier UserRole assignments.
Trial names use the existing UTC setup convention; report date uses Asia/Saigon.

Run README build/executable commands with a NEW receipt path. Main verifies exact SQL server /
database / canonical name / Task17 marker / startup compatibility before fixtures. The final
single-admin assertion requires an isolated trial with no unrelated eligible admin assignments.
Metadata/constraint evidence from the earlier SELECT-only probe remains unchanged and preserved.
No HotelOpsDB access. No production schema/entity/mapping/catalog/auth-policy/DEV2 change.

## Files and intentional limits

Production: new UserRoleAssignmentController.cs, AssignUserRoleRequest.cs, UserRoleAssignmentService.cs,
UserRoleDelegationPolicy.cs; Program.cs adds one scoped registration. Reuses current DbContext,
session/permission evaluator, authority-lock helper and GlobalAdminSerialization without editing
those implementations. No hashing or credential/session semantics changes.

New harness: UserRoleAssignment.Verification.csproj, Program.cs, AssignmentHttpSuite.cs. Existing
six Task44 documents updated; earlier Inspect-TrialSchema.ps1 and runtime receipts preserved.
LastAdminProtection MUTATION_PATH_MATRIX.md and REMAINING_UNPROTECTED_PATHS.md clarify the new
additive participant without changing the external/expiry boundaries or Task43 release history.
No other modules changed, no staging/commit/push/merge/worktree manipulation.

NOT RUN / NOT VERIFIED: production deployment/HotelOpsDB schema, load/least-privilege deployment,
universal external-writer deadlock freedom, infrastructure failover, real transport-loss recovery,
formal SRS/PO approval and broader UC010 (global/protected/secondary/combined roles/regrant/revoke).
Current v1 has no remaining failed technical gate. External catalog changes and scheduled expiry
retain the published LastAdmin limitations; existing session middleware may touch the actor as
before while the Assign service itself only validates sessions.

The following blocked-review report is historical, before the developer's explicit P1-P12
technical approval. Its NOT IMPLEMENTED / NOT RUN labels are superseded for the new v1 tests
above; they are not reused as evidence of the implementation.

---
# Task 44 verification - contract review only

Date: 2026-10-28 Asia/Saigon. HEAD `dc81915917142de6b640221a1f428aba7e9331fc`,
branch `feature/BE1-database-integration`; upstream `origin/feature/BE1-database-integration`,
0 ahead / 0 behind at preflight. Tracked tree and index initially clean. Existing untracked
artifacts/WAV/receipts and four worktrees were preserved. No Commit/Push/Merge.

Developer clarification: no complete UC-010 delegation approval exists; do not transfer UC-007
approval, self-approve policy or open Assign Role. Final status is **CONTRACT PENDING /
ENDPOINT BLOCKED**. No production C# source, DTO, route, DI, catalog, generated entity, mapping
or schema was changed. New executable source is a SELECT-only schema inspection script.

## Executed checks

| Scope | Result | Method / local-only evidence |
|---|---|---|
| Actual disposable SQL metadata/catalog inspection | PASS | Inspect-TrialSchema.ps1; task44-schema-final.local.json, exact server/database/Task17 identity checked |
| Existing fresh/upgrade rowversion/schema verification | PASS 26 | Existing UserProfileUpdate/Verify-Schema.ps1; ../UserProfileUpdate/schema-8b1a658c7e854b028d2c89690b41f788.local.json |
| LastAdmin independent SQL foundation | PASS 26 | Existing LastAdminProtection default executable; task44-lastadmin-foundation.local.json |
| LastAdmin actual HTTPS/SQL existing writer regression | PASS 108 | Existing LastAdminProtection --reset-http; task44-lastadmin-http.local.json |
| UC-004 / Profile / Auth / List / Detail / Startup Schema Gate | PASS 546 | Existing UserAccountCreation.Http.Verification; task44-uc004-profile-startup.local.json |
| UC-005 reset / reauthentication / authority races | PASS 165 | Existing AdminReauthentication.Verification; task44-uc005.local.json |
| UC-006 Activate | PASS 169 | Existing UserAccountActivation.Verification; task44-uc006.local.json |
| UC-007 Add Branch | PASS 183 | Existing UserBranchAssignment.Verification; task44-uc007.local.json |
| Solution build | PASS, 0 warnings/errors | dotnet build HotelOps.slnx |
| Solution test command | Exit 0; no framework test cases discovered | dotnet test HotelOps.slnx; executable harnesses above ran separately |
| Inspector syntax and preconnection guards | PASS | PowerShell parser; invalid trial name rejected without connecting; existing receipt refused before connecting and preserved |
| Whitespace and repository boundary | PASS | git diff --check plus new-file whitespace checks; no tracked/index changes, HEAD/upstream/worktrees unchanged |

Existing binaries were used after building the current API; no production source changed from
the committed Task43 code. Foundation's simulated status/role removal and Add Branch's role-writer
fixtures are not UC-010 HTTP behavior. Existing SQL/HTTP race evidence is not mock evidence,
but it does not validate the proposed UC-010 transaction plan or delegation contract.

Inspected actual 12 canonical roles and their grants, columns, unique indexes, FK columns,
checks and triggers on UserRole/Role/RolePermission/UserBranchAccess plus User.RowVersion.
Final receipt confirms nullable UserRole.BranchId, unfiltered tuple/membership uniqueness,
filtered primary-membership index, no UserRole->membership FK, one RolePermission scope-domain
check and no triggers on the four inspected tables. See TRANSACTION_AND_CONCURRENCY.md.
No UserRole mutation was performed by this metadata inspector; no credentials or sessions read.

## UC-010 tests NOT RUN

| Requested test category | Status / reason |
|---|---|
| Authorized Assign Role | NOT RUN - no approved positive delegation contract or endpoint |
| Unauthorized/cross-Branch/self/protected/global assignments | NOT RUN - proposed deny rules have not been implemented or approved |
| OM/SoD/duplicate/inactive/expired/revoked tuple handling | NOT RUN - schema metadata reviewed, no UC-010 business mutation exists |
| Concurrent Assign/Assign and Assign/Add Branch | NOT RUN - no Assign workflow to exercise; existing Add SQL races do not substitute |
| UC-010 authority revocation, audit rollback, token preservation | NOT RUN - no UC-010 handler; existing reset/Create/Profile/Add checks are regression only |
| UC-010 cancellation/timeout/deadlock/retry | NOT RUN - future locking plan is unimplemented |

No UC-010 end-to-end PASS is claimed. No mocked endpoint/service was created to satisfy a test count.
Authorization Foundation's existing permission evaluator was source-reviewed; standalone offline
Foundation console suites were not rerun in Task44 (NOT RUN), since no evaluator source changed.

## Inspector corrections and limits

Initial inspector failed before connecting because PowerShell adapted SqlConnectionStringBuilder
property assignment as an unsupported keyword. Corrected to canonical dictionary keywords.
First metadata receipts exposed PowerShell array wrappers/null entries; final script normalizes
arrays, and final receipt confirms 0 triggers. Earlier artifacts remain preserved; use only
task44-schema-final.local.json for the final metadata shape. No SQL constraint/catalog change
or policy relaxation was used to fix the inspector.

Regression trials: `HotelOps_Canonical_20261028_065101_12592774_Fresh` for sequential LastAdmin/
005/006/007; `HotelOps_Canonical_20261028_065050_e3ff8159_Fresh` for SELECT-only metadata and
empty-patched UC-004/Profile/Startup; `HotelOps_Canonical_20261028_065600_44abcdef_Fresh` for
unpatched startup negatives. Names follow existing UTC setup convention. HotelOpsDB was never
accessed. Existing harnesses clean only their own temporary grants/sessions/fault resources,
retaining trial databases, fixture users/audits and receipts; no pre-existing artifact is deleted.

Source deliverables: README.md, CONTRACT_AND_AUTHORIZATION.md, PRIVILEGE_ESCALATION_REVIEW.md,
TRANSACTION_AND_CONCURRENCY.md, VERIFICATION_RESULTS.md, OUTSTANDING_BLOCKERS.md and
Inspect-TrialSchema.ps1, all in this directory. JSON receipts are local artifacts. No DEV2
source modification, Catalog edit or production deployment. Proposed policy needs explicit
approval before implementation; successful regression does not supply that approval.
