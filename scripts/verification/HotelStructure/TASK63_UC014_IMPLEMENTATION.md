# Task63 — UC014 Create Floor/Area

30/10/2026, Asia/Saigon. **UC014 COMPLETE_VERIFIED_V1**: cả Floor và Area có production HTTP,
actual SQL/HTTPS acceptance và relevant regression. Các lựa chọn ASSUMED_V1 được phân biệt với SRS.

## A. Requirements

[UC014 §2.3](../../../docs/requirements/srs_sections/02_use_case_specifications.md) yêu cầu chọn branch,
Floor/Area, code/name/description và optional parent floor; tạo Active và audit cùng transaction.
[Business Rules](../../../docs/requirements/srs_sections/05_requirement_appendix.md):
GBR001 session valid; GBR002 permission/scope; GBR003 trim/code case-insensitive; GBR004 referenced
parents tồn tại, trong scope và Active; GBR007 atomicity; GBR008 actor/time/entity/action/before/after;
GBR034 branch-unique code, optional same-branch Floor parent và không tạo cycle.

[Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md) và Permission Matrix đã đọc. Không
suy diễn quyền Reception/DH/OM. [Catalog hiện có](../../database/release/03_HotelOps_RBAC_Catalog_Seed.sql)
có MASTER_DATA_MANAGE, SYS_ADMIN được ALL; không thêm grant, role-name bypass hay hard-code IDs.
[Entities](../../../src/HotelOps.Api/Entities/Floor.cs)/[Area](../../../src/HotelOps.Api/Entities/Area.cs)
và [DbContext](../../../src/HotelOps.Api/Data/HotelOpsDbContext.cs) được giữ nguyên.

## B. Business rules V1

| Rule | Implementation |
|---|---|
| Authorization | CurrentUserAccessor + protected ticket + SQL session revalidation. Existing MASTER_DATA_MANAGE global hoặc branch evaluation; bound ALL không vượt branch. USER_VIEW/OWNER/restricted/inactive/future/expired grants không cấp quyền tạo |
| Parent | Branch tồn tại/Active; Area.floorId null hoặc trỏ Floor tồn tại/Active/cùng branch. Floor không có parent field; Area chỉ có thể trỏ Floor, không Area → không có cycle theo schema hiện tại. Không tạo generic floor_area table hoặc parent mới |
| Code | Trim + uppercase invariant, case-insensitive SQL duplicate lookup. Existing inactive code vẫn được giữ. **ASSUMED_V1**: code1–30 ASCII chữ/số/underscore/hyphen, ký tự đầu chữ/số; shared Floor/Area namespace trong branch là cách đọc thận trọng GBR034. DB vẫn có unique index riêng cho từng bảng; application branch lock bảo vệ cross-type namespace |
| Names | Trim, bắt buộc, Unicode; Floor max100, Area max150 theo schema; cấm control chars. **Không tự thêm unique-name rule**: cùng tên/khác code được phép và đã test |
| Other fields | ASSUMED_V1 description trim/blank→null, max4000 UTF-16 units (technical bound trên nvarchar(max)), cho phép newline/tab. Optional floorNumber dùng Int32 theo schema, kể cả basement âm; không bịa range/status. isActive luôn server=true |
| Strict input | Dedicated DTOs, unknown fields rejected, không nhận actor/branch authority/isActive từ body. Body limit32KiB, CSRF theo middleware hiện có. Không cần If-Match cho CREATE; không thêm rowversion/schema |
| Transaction | Owned SERIALIZABLE transaction, clean scoped SQL context. Retained actor User → session/grants → Branch UPDLOCK/HOLDLOCK → optional Floor HOLDLOCK → authoritative duplicate checks → insert entity → insert audit → recheck session/permission → commit |
| Contention | Branch update lock serialize cả Floor/Area creators; existing unique indexes là backstop. Parent locks giữ tới commit/rollback, không đọc EF tracked state cũ. Final session/grant expiry sau wait rollback cả entity/audit |
| Audit | Existing AuditLog, CREATE/floors-or-areas/ID/branch/server actor/UTC; old_values={"entity":null}, new_values là DTO public, changed_fields JSON array. Không có credentials/tokens. Không new audit framework |
| Failures | Audit error rollback; cancellation propagates and rollback. SQL1222/-2/1205 → Unavailable/HTTP503, **không automatic replay**. Explicit new request/context retry; nếu lần trước đã commit, duplicate409 ngăn entity/audit thứ hai |

Writer inventory trong production source chỉ có CreateFloorAsync/CreateAreaAsync mới tạo Floor/Area.
Future update/import/job producers phải phối hợp cùng User/branch/parent order; task này không chứng
nhận các writer chưa triển khai hoặc external SQL clients. Không thay đổi UC006 release gates.

## C. HTTP contract

**ASSUMED_V1 routes**, reuse cookie/SQL-session/CSRF conventions:

```http
POST /api/branches/{branchId}/floors
{ "code": "F03", "name": "Tầng 3", "floorNumber": 3, "description": null }

POST /api/branches/{branchId}/areas
{ "code": "LAUNDRY", "name": "Giặt là", "floorId": 123, "description": null }
```

Floor request: code/name required; floorNumber/description optional. Area: code/name required;
floorId/description optional; omitted/null floorId tạo Area trực tiếp dưới branch.
Response201/no-store: id, branchId, code, name, floorNumber **hoặc** floorId, description, isActive.
Không expose entities/navigation; không phát Location tới GET chưa triển khai.

400 invalid payload/parent/branch/CSRF; 401 invalid/expired/revoked session; 403 thiếu effective
permission/scope hoặc restricted session; 409 STRUCTURE_CODE_EXISTS; 413 REQUEST_TOO_LARGE;
503 STRUCTURE_WRITE_UNAVAILABLE cho known contention/timeouts. Other audit/provider faults dùng
generic500 hiện có. Scoped unauthorized branch không lộ parent data. Validation details chỉ mô tả
field rules, không echo credential/provider exception. Authenticated POST luôn cần CSRF.

## D. Source changes

| File | Change |
|---|---|
| [HotelStructureCreationController](../../../src/HotelOps.Api/Controllers/HotelStructureCreationController.cs) | Two production POST routes; HTTP/session ticket/validation/status mapping; reuse existing route-local413 filter |
| [CreateStructureDtos](../../../src/HotelOps.Api/DTOs/HotelStructure/CreateStructureDtos.cs) | Strict request and response DTOs for both operations |
| [HotelStructureCreationService](../../../src/HotelOps.Api/Services/HotelStructure/HotelStructureCreationService.cs) | Business validation, actual permission/session checks, retained locks, atomic audit, failure handling |
| [Program](../../../src/HotelOps.Api/Program.cs) | One scoped service registration; existing middleware/auth infrastructure unchanged |
| [Harness](Program.cs), [SQL cases](StructureSqlAcceptance.cs), [verification project](HotelStructure.Verification.csproj) | Actual root Program.cs HTTPS and production service fault/concurrency tests; no new application architecture project |
| This report + [delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md) | Contract, evidence, progress and remaining limits |

## E–G. SQL, HTTPS and concurrency acceptance

[Final receipt](task63-accepted-final.local.json): **215 PASS, 23 DMV-observed SQL waits**.
Only new canonical disposable trial `HotelOps_Canonical_20261029_235344_7bf00ae3_Fresh`, exact
server/database/Task17 marker checked, approved guarded User rowversion prerequisite applied
unchanged; startup Compatible trước fixtures và trong production app startup. No HotelOpsDB.

| Executed path | Evidence |
|---|---|
| Production HTTPS | Actual root assembly/Program.cs DI/startup/middleware, pinned ephemeral loopback TLS, real credential login/cookie/SQL sessions/CSRF; no replacement host |
| Functional / security | Floor + parented/unparented Area201; normalization/basement/null fields; unauthorized401, forbidden403, cross-branch403, parent missing/inactive/wrong branch400; duplicate409, inactive code409, same code other branch201, same name different code201; strict payload400/body413; no secret fields |
| Concurrent duplicates | Floor/Floor, Area/Area, Floor/Area requests from different real actors: exactly one201 +one409; exactly one persisted entity. Actual SQL existing unique indexes independently reject same-type duplicate inserts |
| Parent-first | Branch/Floor disable/delete holds SQL locks; production HTTP waits then rereads committed missing/inactive state, rejects with no entity/audit. FK/branch relationships checked for every created Area |
| Create-first | API stages INSERT then waits on audit SQL; parent deactivation and actor permission/session revoker wait for retained locks. Creation linearizes first, then next request denies after committed revocation |
| Mid-operation expiry/cancel | Actual HTTPS grant expiry after entity INSERT/audit wait →403 and rollback both rows. RequestAborted after INSERT releases transaction locks **before** blocker release; neither entity nor audit remains |
| Audit rollback | Both production service operations run real business INSERT then audit command deliberately replaced by SQL THROW51063 in harness-only interceptor. Captured actual SQL error; transaction/tracker clean, zero business/audit rows. No trigger/schema/prod hook added |
| SQL1222 / command timeout-2 | Both operations wait after entity INSERT; real SET LOCK_TIMEOUT1200 or command timeout2s. Captured SQL numbers, service Unavailable, zero partial effects; new-context retry succeeds once, replay returns409/no duplicate audit |
| SQL1205 | Both operations forced victim after INSERT: test-only Audit→User inverse blocker versus production User→Audit, explicit LOW/HIGH priorities and observed waits. Actual1205, no auto replay/partial writes; fresh retry succeeds. This proves fault recovery, not global deadlock freedom |
| Stale authority | Direct production service waits on retained User, then sees actor INACTIVE/revoked grant/expired session and rejects. Real HTTPS expired session and revoked reviewer membership also reject |
| Preservation/audit | POST-only phase compares **every User column**, including last_login_at/row_version. Whole suite compares other User fields (credentials included), roles/memberships/catalog; excludes only expected real-login LastLoginAt and deliberate fault-fixture rowversion. Every successful entity has exactly one CREATE audit; actor/branch/entity/before/after/UTC verified |

Direct SQL cases use actual PermissionService/EfPermissionDataSource/SqlSessionService/
CurrentUserAccessor and persisted fixture sessions. This supplements actual HTTPS tests, not a claim
that direct service calls traversed the HTTP pipeline. Locks/error numbers are captured in the JSON
receipt; no fabricated SQL exception or timing-only serialization assertion. Session activity touches
and login audits remain enabled. Trial fixture state/logs retained; no reset/reuse.

Earlier attempts/logs retained in ignored obj: record-property validation metadata was corrected
to constructor-parameter metadata; scalar-null audit JSON violated canonical ISJSON CHECK and
was corrected without schema changes (SQL confirmed failed Floor rollback). Two harness assertions
were corrected: public PASSWORD_CHANGE_REQUIRED is not a secret; real login legitimately updates
LastLoginAt. Final proof adds POST-only full User snapshot rather than hiding those mutations.

## H. Regression / build

| Verification | Result |
|---|---|
| [Existing Room Directory regression](task63-room-regression.local.json) | **117 PASS /11 SQL waits**, new separate trial `HotelOps_Canonical_20261029_234517_996366f5_Fresh`; relevant Floor/Area reads/integrity; UC023 still PARTIAL |
| PermissionService / Authorization / ResourceAuthorization / AuthorizationHardening | **43/61/49/21 =174 PASS**, documented core/HTTP doubles; no --live main-DB mode. New feature HTTPS uses real SQL separately |
| dotnet build HotelOps.slnx; targeted harness builds | **PASS**, 0 errors/no C# warnings; NU1900 vulnerability-feed warnings retained, audit not disabled |
| dotnet test HotelOps.slnx | Exit0, **no discovered tests**; explicit console harness execution supplies actual coverage |
| git diff --check / cached check / new-file whitespace | PASS; preservation/checksum evidence in ignored obj/task63-validation.local.json |

No unrelated full regression. Reproduce with a new canonical trial via existing
[Prepare-Database](../UserDirectory/Prepare-Database.ps1) + unchanged
[guarded prerequisite](../UserProfileUpdate/rowversion-forward.sql), build solution/harness, then:

```powershell
dotnet scripts/verification/HotelStructure/bin/Debug/net10.0/HotelStructure.Verification.dll <new-trial> <new-receipt.local.json>
```

## I–K. Delivery / progress / next task

- **UC014 NOT_IMPLEMENTED → COMPLETE_VERIFIED_V1**, both mandatory operations delivered. No
  mandatory UC014 implementation/acceptance blocker remains under the explicitly documented V1 rules.
- **+2 verified points →19/256=7.42%, 7/75 completed UCs**. No UC023 gain; it remains PARTIAL.
  Current plan §11 reconciles its historical blocked row with Task60/Task63's partial classification.
- Future Floor/Area updates, deactivation, full Branch Structure selectors/tree and deployment under
  a least-privilege SQL principal/load certification remain separate. Branch serialization is
  conservative, suited to infrequent structure creation; no throughput or external-writer certification.
- HEAD4a187320643d0c82f232c6b9dd5a667b8401c280, feature branch/index/four worktrees preserved.
  Recoverable preflight copies/SHA256 for419 pre-existing dirty files in
  obj/task63-preflight-20261030-063427. Only Program.cs and delivery plan intentionally edit protected
  files. No DEV2/entity/DbContext/schema/catalog/UC006 gate changes, no Git mutations or HotelOpsDB access.
  Final preservation: **417/419 byte-identical, 2 intended edits, all419 backup hashes valid**;
  HEAD/branch/upstream/index/four worktrees unchanged, no missing/unexpected protected files.
  Measured preflight-to-validation elapsed **26.4 minutes**, including tool/test waits, not human-hours.
- **Recommended next: UC013 View Branch Structure**, full branch/floor/area/room/department tree
  with server-side scope and SQL/HTTPS verification, to supply the selectors for UC014. Do not call
  a few lists full UC013. UC015 separately needs a verified concurrency/version contract against
  the actual Floor/Area schema; do not invent a production rowversion migration.
