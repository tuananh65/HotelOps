# Task61 — UC141 HTTPS & SQL acceptance

30/10/2026, DEV1; branch `feature/BE1-database-integration`, HEAD
`4a187320643d0c82f232c6b9dd5a667b8401c280`. **Approved list/minimal-detail slice PASS;
full UC141 PARTIAL → PARTIAL, gain0; verified DEV1 vẫn 12/256 =4.69%.**

## A–D. Gap analysis và approved contract

| Nhóm | Finding / kết quả |
|---|---|
| A — Có và đã verify trước task | GET /api/users list/search/filter/sort/page, Global USER_VIEW, DTO allowlist; GET /api/users/{id} minimal detail. Task17/22 evidence được giữ; Task61 chạy lại production HTTPS/SQL |
| B — Thiếu executable evidence / reliability | Chưa chứng minh retained session/authority qua SQL wait, count/page consistency, in-flight cancellation và final time-based revocation. Baseline mới tái lập revoker không bị chặn trong khi request còn chờ query; đã sửa read boundary và verify bên dưới |
| C — Missing production functionality | UC141 normal step6 yêu cầu detail có roles và effective permissions (UC005/011); minimal detail DTO hiện không có hai phần này. List role assignments không phải target effective-access report |
| D — External approval | UC011 permission code/scope/self-review/target-inactive/response visibility chưa duyệt. Scoped user-directory visibility BRANCH/OWN cũng chưa duyệt. Không mở access report hoặc scoped list bằng giả định |

Nguồn: [UC141 §1.12](../../../docs/requirements/srs_sections/02_use_case_specifications.md),
[User List screen / related UCs](../../../docs/requirements/srs_sections/03_functional_requirements.md),
[GBR001/002/015 + SYS-PAR11](../../../docs/requirements/srs_sections/05_requirement_appendix.md),
[Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md),
[Task23 authorization decision](../EffectiveAccessReview/AUTHORIZATION_DECISION.md),
[approved list contract](../UserDirectory/API_CONTRACT.md),
[minimal detail scope](../UserAccountDetail/README.md),
[Task59 plan](../DEV1_21_DAY_DELIVERY_PLAN.md). SRS/matrix và DB design là draft;
không lấy actor role name thay cho approved permission mapping. Đối chiếu current DbContext,
User/UserSession/UserRole/UserBranchAccess/Department/Branch mappings, không migration.

Contract giữ nguyên: Global unbound USER_VIEW:ALL cho list/detail; BRANCH/OWN/branch-bound ALL
deny. Branch filter chỉ là User.PrimaryBranchId, không phải membership/authority. Department
filter phải cùng selected branch; active/time-valid roles, include inactive nondeleted targets,
exclude deleted; FullName→UserId order; default20/max100; approved fields/errors/no-store.
Không thêm response fields, sort expression, permission hoặc claim-based bypass.

## Source change và actual defect

- [UsersController](../../../src/HotelOps.Api/Controllers/UsersController.cs): thêm controller-local
  TypeFilter; giữ routes, existing Global policy, DTOs/service/query primitives.
- [UserDirectoryReadGuard](../../../src/HotelOps.Api/Services/Users/UserDirectoryReadGuard.cs):
  clean dedicated context, owns SERIALIZABLE read transaction; **shared Users-universe lock
  first → Session → grant/feature projections**. TableS bao trùm cả target Users mà list có thể
  đọc, tránh lấy thêm target User locks sau feature locks. Không nâng shared read sang User
  UPDLOCK; reuses SqlSessionPolicy cho đầy đủ hash/ticket/session/revocation/time checks và
  existing PermissionService Global USER_VIEW. Recheck cả hai trước publish, rollback/dispose
  khi fault/cancel; không SaveChanges/session touch/audit hoặc hidden retry.
- [UserDirectoryReadFilter](../../../src/HotelOps.Api/Controllers/Filters/UserDirectoryReadFilter.cs):
  HTTP orchestration trên existing scoped dependencies, không sửa Program/DI/middleware.
  Final rejection thay **ActionExecutedContext.Result** trước serialization, không chỉ đổi
  executing context rồi vô tình trả dữ liệu đã materialize. Lỗi dùng existing problem shape,
  401 session invalid /403 permission denied /503 permission data unavailable; no-store.
- Reuse [UserDirectory harness](../UserDirectory/Program.cs) với optional `--task61` và compile
  link tới [additional cases](Task61AcceptanceCases.cs); chế độ2 args cũ vẫn giữ. Không tạo host
  hoặc test framework mới. API contract/persistence-limit docs cập nhật đúng boundary mới.

Baseline logs được giữ: `obj/task61-baseline.log` là lỗi harness mapping DMV smallint→Int32
(đã CONVERT int, không phải lỗi app); `obj/task61-baseline-probe.log` tái lập
`no retained SQL wait for session-revoker-waits-for-inflight-read`: SQL revoker hoàn tất trong
khi production HTTPS read chờ count. Không suy rằng baseline đã kiểm tra response200 sau
revocation; đó là failure của retained-authority acceptance. Lượt accepted ban đầu PASS184;
review lock order sau đó chuyển sang shared Users-first namespace trước Session/features.
Các final receipts dưới đây là kết quả cho **final source**, trên trials mới; không ghi đè
receipts/logs/database các lượt trước.

## HTTPS / SQL executed evidence

| Suite / final receipt | Result | Phương thức |
|---|---:|---|
| [UC141 final](task61-accepted-final.local.json) | **184 checks PASS**, **8 DMV waits** | Actual built API child process, original Program.cs/StartupSchemaValidator/DI, loopback HTTPS certificate pinning, real credential login/cookie/SQL sessions, production routes/services/EF queries |
| [User Detail regression](task61-detail-final.local.json) | **234 PASS** | Existing harness, actual production HTTPS/SQL plus direct projection checks |
| [Profile regression](task61-profile-final.local.json) | **329 PASS** | Existing profile/list/detail suite, actual production HTTPS/SQL + fault/ETag/scope regressions |
| AuthN/AuthZ foundations | **167 PASS** | Rebuilt PermissionService43, CurrentUserAccessor14, Authorization61, ResourceAuthorization49; no `--live`. These use core/synthetic adapters, không gọi là SQL/UC141 HTTP evidence |

Assertion counts có overlap giữa suites; không gọi tổng là các independent business scenarios.

| Required case | Actual acceptance |
|---|---|
| Authenticated ALL / missing auth / forbidden | 200 for actual unbound global admin grant; anonymous/invalid cookie401; denied/restricted/inactive/future/expired assignments403. Auth comes from real startup, not manually selected test principal at HTTP |
| BRANCH / OWN / branch isolation | Actual canonical DEPT_MANAGER USER_VIEW:BRANCH returns403 even matching branch and client actor selector; branch-bound ALL denied. OWN không được contract cho phép; denied core paths, không fake SQL catalog/HTTP OWN grant. Global ALL may see both branches; explicit branch filter narrows only, never grants access |
| Empty / filters / sorting / pagination | Empty canonical result + unmatched search; primary branch/department/role/status/keyword, combined filters, FullName/UserId tie-break, duplicates across roles deduped, SQL OFFSET, default20/max100/beyond-last; 400 invalid bounds/IDs/status/search/branch-dept mismatch |
| Sensitive fields / persistence | Recursive approved DTO allowlist; business SELECT excludes password/session/security graph. Business users/roles/memberships and canonical catalog fingerprints unchanged during read phase; HTTP middleware may touch session activity as before, no new session from directory GET |
| Expired/revoked sessions / actor grants | Real protected cookies denied401 after SQL expiry/revoke; assignment revocation403. No totalCount/provider information in error responses |
| In-flight session/permission (4 waits) | Hold target role projection with fixture SQL; real HTTPS page waits; User-first revoker also waits behind retained Users namespace. Release blocker → authorized read completes → revocation commits → next real HTTP request401/403 |
| Writer-first (1 wait) | Competing target User change blocks shared Users boundary; real HTTP waits then reads committed authoritative target data |
| Request cancellation (1 wait) | Cancel actual HTTPS caller during SQL User wait; RequestAborted removes blocked SQL request before blocker release, no fabricated service-only cancellation |
| Final assignment expiry (1 wait) | Grant expires by wall clock while page waits; action query resumes, final gate replaces data with403 before serialization |
| Count/page concurrent INSERT (1 wait) | Supplementary actual production guard/query SQL test, pause before page after count; INSERT waits on read boundary; result retains26/26 matching users, no phantom split; writer then commits. Không gọi service probe là HTTP evidence |
| Query failure rollback | Fault actual page command; read boundary disposes transaction, no tracked changes or partial returned directory. Read-only rollback, không bịa audit-write rollback yêu cầu cho GET |

Final trials (fixtures retained, no reset/deletion):

- UC141: `HotelOps_Canonical_20261029_224226_baf36329_Fresh`.
- Detail: `HotelOps_Canonical_20261029_224230_4cf65bb0_Fresh`.
- Profile: `HotelOps_Canonical_20261029_224233_326886bc_Fresh`.

Database names use UTC generation; report date dùng Asia/Saigon30/10. Setup unchanged
UserDirectory/Prepare-Database.ps1 + approved guarded UserProfileUpdate/rowversion-forward.sql;
exact canonical name/instance/Task17 marker checked, startup Compatible. Không truy cập
HotelOpsDB, không thêm SQL login/grant/catalog/schema. Password/cookies/CSRF in memory;
no secrets in receipts. Harness revokes only owned fixture sessions, stops only spawned API,
cleans its exact temporary TLS PFX; retains databases, source and artifacts.

Reproduce on a **new** guarded canonical personnel-empty trial with approved rowversion
prerequisite; NEW receipt path (existing files refused):

```powershell
dotnet build HotelOps.slnx
dotnet build scripts/verification/UserDirectory/UserDirectory.Verification.csproj
dotnet scripts/verification/UserDirectory/bin/Debug/net10.0/UserDirectory.Verification.dll <fresh-trial> <new-receipt.local.json> --task61
```

No unbounded timeouts: overall harness8min; HTTP20s; command provider default; SQL wait
observation6.4s, barrier15s, cancellation release observation4.8s. Retained Users shared table
lock is conservative: it can delay User writers and cookie validation while the directory read
runs; no load/least-privilege production certification. Separate requests/pages are not one frozen
snapshot. No global writer/UC006 release gate is closed by these bounded schedules.

## Build / preservation / classification

- Solution + relevant harness builds **PASS**, 0 errors/no C# warnings. NU1900 warnings remain
  because api.nuget.org vulnerability feed unavailable; package audit was not disabled.
- `dotnet test HotelOps.slnx` exit0; no discovered test project in solution. Actual testing is
  the explicit suites above, not a unit-test success claim.
- Git diff/whitespace + protected-source/index/worktree checks: `obj/task61-validation.local.json`.
  Preflight backups391 files at `obj/task61-preflight-20261030-052305`; Task51–60 source/artifacts
  retained, with only the requested Task59 delivery-plan update among protected dirty files.
- Measured elapsed from preflight to report snapshot: **26.4 minutes**.
  Wall-clock includes tool/test waits, không quy thành billable hours hoặc whole21-day effort.
- **UC141 final PARTIAL**: approved list/minimal-detail reliability acceptance PASS; full step6
  roles/effective permissions chưa triển khai do UC011 contract còn pending. Không đóng full UC
  bằng cách đổi định nghĩa list slice. **Weighted gain0; 12/256=4.69%; estimated implementation
  equivalent giữ24.25/256=9.47%.** UC023 Task60 vẫn chưa COMPLETE_VERIFIED.

**Next UC: UC011 Review User Access Permissions**, dependency trực tiếp để đóng UC141.
Cần quyết định permission hiện có (USER_VIEW/ROLE_MANAGE/combination), Global-only hay scoped
visibility, self-review và field whitelist/inactive-target semantics; approval minimal detail cũ
không bao gồm access topology. Sau quyết định, triển khai target access read model + approved
detail roles/drilldown và actual HTTPS/SQL acceptance; không mở thêm internal-only workflow khác.
