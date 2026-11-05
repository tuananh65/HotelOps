# Task62 — UC011 Access Review / UC141 acceptance

30/10/2026. **UC011: COMPLETE_VERIFIED; UC141: COMPLETE_VERIFIED trong V1 dưới đây.**
Production HTTP, real SQL và relevant regression đã chạy. ASSUMED_V1 là quyết định được Task62
cho phép khi requirements im lặng, không trình bày như SRS đã được nhóm ký duyệt.

## Sources and selected rules

- [UC011 / UC141](../../../docs/requirements/srs_sections/02_use_case_specifications.md) §1.11/1.12:
  roles, branches, department, effective permissions theo feature và role cấp quyền; filter
  feature/permission, empty match; UC141 step6 drilldown roles/effective permissions.
- [Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md): Reception/DH revisions còn pending.
- [Catalog](../../database/release/03_HotelOps_RBAC_Catalog_Seed.sql) dòng39/41/105/107/188:
  USER_VIEW, ROLE_MANAGE (quản lý vai trò và phân quyền); SYS_ADMIN hiện có cả hai ALL,
  DEPT_MANAGER chỉ USER_VIEW BRANCH. Không sửa grants/catalog hay hard-code role IDs.
- [PermissionService](../../../src/HotelOps.Api/Security/Authorization/PermissionService.cs),
  [data source](../../../src/HotelOps.Api/Security/Authorization/PermissionDataSource.cs),
  [DbContext](../../../src/HotelOps.Api/Data/HotelOpsDbContext.cs) và entities đã kiểm tra:
  User/UserRole/RolePermission/Permission/UserBranchAccess/Branch/Department.
  Schema chỉ có một primary DepartmentId, không bịa multi-department membership.

| Rule | Implemented V1 |
|---|---|
| ASSUMED_V1 authorization | Cần **USER_VIEW và ROLE_MANAGE**, giao scope hiệu lực của hai quyền. USER_VIEW riêng không cho xem access topology; không role-name bypass |
| ASSUMED_V1 scope | ALL unbound → global; ALL bound/BRANCH bound → branch khi assignment và actual branch membership hợp lệ. Branch reviewer cần target có current SQL membership chung; primary branch không cấp quyền |
| ASSUMED_V1 clipping | Branch report chỉ giữ assignment/grant bound đúng branch, membership trong scope; bỏ global assignment và branch khác. Redact primaryBranch/department ngoài scope. reviewerScope.isGlobal=false chỉ một báo cáo giới hạn, không khẳng định có dữ liệu nào bị ẩn |
| OWN/self | Không có canonical ROLE_MANAGE OWN hoặc verified ownership resolver cho access-review target. OWN riêng không cấp quyền review, self không exemption. Self read được khi đủ hai quyền/scope; không write/escalation |
| Effective capability | Shared eligibility: account ACTIVE/nondeleted, assignment/role active, assigned<=now, expiry>now, bound branch active/current membership. Unknown scopes không grant. ALL unbound GLOBAL; ALL bound BRANCH; OWN giữ requiresResourceOwnership=true |
| Assigned/history | AssignedRoles và ConfiguredGrants giữ inactive/expired/future/history trong phạm vi được phép; tách khỏi EffectivePermissions. Inactive target vẫn xem được cấu hình nhưng không có effective permissions |
| Capability limit | RBAC snapshot tại evaluatedAtUtc, không phải CanExecute. Mọi operation vẫn cần valid unrestricted session, resource context/ownership, workflow/SoD/time rules. Không đọc/tiết lộ sessions hoặc temporary-credential state của target |
| ASSUMED_V1 filters | feature=Permission.ModuleCode, permissionCode=stored code; trim, blank ignored, max raw60/100 theo schema, SQL collation. No match trả empty permissions/configuredGrants, giữ account/roles header |
| Consistency | Shared Users universe → Session → reviewer grants → target projections; SERIALIZABLE. Recheck session và scope phải bao phủ toàn bộ initial scope trước commit; scope shrink/expiry không công bố report đã materialize |
| Audit | Theo read policy hiện có: không new read-audit framework/SaveChanges. Business user/role/credential/membership/audit không đổi. Middleware vẫn touch SQL session activity/expiry; GET không tạo session |

Pure PermissionGrantRules được tách nguyên nghĩa từ evaluator và dùng chung với report; không giả
danh target qua CurrentUserAccessor. Query bulk: account, assignments, grants, memberships (4 query,
thêm1 department-scope check cho scoped report), không N+1; auth/session đọc riêng trong cùng tx.

## HTTP contract

- **ASSUMED_V1 route** `GET /api/users/{userId}/effective-access?feature=ADMIN&permissionCode=USER_VIEW`:
  dedicated UC011, hỗ trợ global và proven branch review.
- `GET /api/users/{userId}?includeAccess=true`: UC141 step6 dùng **cùng DTO/projection**;
  giữ Global USER_VIEW của controller và thêm ROLE_MANAGE. `GET /api/users` và minimal detail mặc
  định giữ nguyên contract/filter/sort/page20/max100/visibility. Scoped directory không được mở rộng.
- Response UserAccessReviewDto: account, evaluatedAtUtc, reviewerScope, assignedRoles,
  branchMemberships, configuredGrants, effectivePermissions (code/name/feature/sources).
  Source gồm assignmentId/roleCode/scope/branchId/requiresResourceOwnership. SQL timestamps UTC `Z`.
  Không EF entity, credential/hash, temporary credential/expiry, session/token hoặc auth internals.
- 200/no-store; 400 invalid route/model/query; 401 invalid/expired/revoked session;
  403 restricted/insufficient authority/out-of-scope target. Global missing/deleted target404;
  branch missing/out-of-scope cùng403 tránh existence oracle. Existing session-store503;
  query/provider fault generic500, không diagnostics hoặc partial report. Không authorization retry.

## Changed files

- [Controller](../../../src/HotelOps.Api/Controllers/UserAccessReviewController.cs),
  [UsersController](../../../src/HotelOps.Api/Controllers/UsersController.cs),
  [DTOs](../../../src/HotelOps.Api/DTOs/Users/UserAccessReviewDto.cs),
  [service](../../../src/HotelOps.Api/Services/Users/UserAccessReviewService.cs): production feature.
- [Metadata](../../../src/HotelOps.Api/Controllers/Filters/UserAccessReviewAttribute.cs),
  [filter](../../../src/HotelOps.Api/Controllers/Filters/UserDirectoryReadFilter.cs),
  [guard](../../../src/HotelOps.Api/Services/Users/UserDirectoryReadGuard.cs): reuse retained SQL boundary,
  private server scope capability, original-scope final recheck; legacy Global USER_VIEW unchanged.
- [Grant rules](../../../src/HotelOps.Api/Security/Authorization/PermissionGrantRules.cs) and existing
  PermissionService: shared exact eligibility; [Program](../../../src/HotelOps.Api/Program.cs): one DI registration.
- [SQL/HTTPS cases](Task62AcceptanceCases.cs), [harness](../UserDirectory/Program.cs),
  [project](../UserDirectory/UserDirectory.Verification.csproj): optional --task62, legacy modes retained.
- This report, [plan](../DEV1_21_DAY_DELIVERY_PLAN.md), [API contract](../UserDirectory/API_CONTRACT.md),
  [limits](../UserDirectory/OUTSTANDING_ISSUES.md): current contract/progress; historical receipts preserved.

## Executed evidence

All SQL used new canonical disposable trials with exact server/database/Task17 receipt guards;
existing approved rowversion-forward.sql applied unchanged. Startup schema compatibility ran before
Task62 acceptance and in the actual application startup. SQL needed sandbox escalation for integrated
security; no main database connection. HTTPS uses actual root Program.cs/DI/middleware, ephemeral
pinned loopback TLS, real login/cookies/SQL sessions, no replacement host or authorization hook.

| Suite / receipt | Result | Coverage |
|---|---|---|
| [Final UC011 + UC141](task62-accepted-final.local.json) | **293 PASS /16 DMV waits** (8 Task62+8 Task61) | Global/branch/USER_VIEW-only denial, scope clipping/redaction, self, roles/provenance, conditional OWN, empty/inactive/future/expired grants, account INACTIVE, target/reviewer membership revocation, filters/validation/injection, UTC, UC141 drilldown, unchanged default list/detail; real SQL races/cancel/timeout |
| [Initial accepted run](task62-accepted.local.json) | 287 PASS /16 waits | Before final UTC serialization/additional negative cases; retained |
| [Detail regression](task62-detail.local.json) | **234 PASS** | Existing production HTTPS/SQL list/detail/session/authorization |
| [Profile regression](task62-profile.local.json) | **329 PASS** | Existing production HTTPS/SQL profile/list/detail/ETag/scope/session |
| PermissionService / Authorization / ResourceAuthorization / AuthorizationHardening | **43/61/49/21 =174 PASS** | Foundation core/HTTP with documented doubles; no --live main-database mode; separate from actual SQL acceptance |
| Solution + targeted harness builds | **PASS**, 0 errors | NU1900 vulnerability-feed warnings retained; no C# warnings/audit suppression |
| dotnet test HotelOps.slnx | Exit0, **no discovered tests** | Actual tests are the explicitly executed console harnesses, not discovered unit tests |

SQL evidence observes waiter/owner IDs and lock kinds, not timing alone. Writer-first commits are
authoritatively reread. Read-first reviewer/session revoker waits until boundary ends; next request
denies. Grant expiry during wait discards materialized report before serialization. RequestAborted
releases blocked SQL **before** blocker release. Actual default SQL command timeout returns generic500
without access data; next fresh read succeeds. Read fingerprints preserve users (including credential
columns), roles, memberships and audit; recursive DTO allowlist excludes secrets. Catalog unchanged.
This suite does not claim SQL1222/1205 injection, full UC006 race acceptance, load capacity or deployment
under a least-privilege application principal.

Trials (UTC names, report uses Asia/Saigon): final `HotelOps_Canonical_20261029_231511_aa06de7d_Fresh`;
initial `HotelOps_Canonical_20261029_231145_e5636ef5_Fresh`;
Detail `HotelOps_Canonical_20261029_231212_6069b085_Fresh`;
Profile `HotelOps_Canonical_20261029_231215_dc84a37f_Fresh`. Retained for review, never reset/reused.

Reproduce: [Prepare-Database](../UserDirectory/Prepare-Database.ps1) with a **new** canonical trial,
then unchanged [guarded prerequisite](../UserProfileUpdate/rowversion-forward.sql) on that trial;
build solution/harness and execute:

```powershell
dotnet scripts/verification/UserDirectory/bin/Debug/net10.0/UserDirectory.Verification.dll <new-trial> <new-receipt.local.json> --task62
```

Ignored `obj/task62-*` retains prepare/build/test logs, hashes and preservation checks. Two harness
compile failures from a missing namespace import were fixed before SQL runs; logs retained.

## Preservation / classification / remaining limits

- HEAD4a187320643d0c82f232c6b9dd5a667b8401c280, branch/upstream/index/four worktrees retained.
  Recoverable copies+SHA256 for406 pre-existing dirty files: `obj/task62-preflight-20261030-060612`.
  Intended source/doc edits listed above; old sources/artifacts retained in backup. Final checks in
  `obj/task62-validation.local.json`; no Git commit/push/fetch/merge/reset/clean/rebase.
  Validation: **398/406 files byte-identical, 8 intended edits; all406 backup hashes match**;
  index/HEAD/branch/four worktrees unchanged, no missing/unexpected files. git diff --check and
  cached check PASS (LF→CRLF Git notices retained, no line-ending normalization).
  Measured preflight-to-validation elapsed **14.8 minutes**, includes tool/test waits, not human hours.
- No mandatory implementation/verification gap remains for UC011 or UC141 **under ASSUMED_V1**.
  Future SRS approval may revise policy or add scoped-directory/OWN-resource semantics; current
  unsupported review paths fail closed. No unapproved grant is introduced.
- Conservative Users table S lock can delay unrelated account/session writers, as in Task61;
  not throughput certification. Separate requests may see different snapshots. No snapshot DB setting.
- No HTTP Deactivate, SA57 activation or UC006 gate changes. No DEV2/entity/DbContext/schema/catalog
  changes. HotelOpsDB untouched. Production deployment/schema/principal acceptance remains separate.
- Verified gain **+5 points** (UC0113+UC1412): **17/256=6.64%, 6/75 COMPLETE_VERIFIED**;
  Partial4, NOT_IMPLEMENTED44, BLOCKED_EXTERNAL21. Estimated implemented equivalent27.75/256=10.84%
  is separate from verified credit. Full scope denominator unchanged; current delta in plan §10.
- Next implementation **UC014 Create Floor/Area**, after extracting actual branch/permission/
  uniqueness/audit/concurrency rules, rather than deferred UC006 workflows or another audit-only task.
