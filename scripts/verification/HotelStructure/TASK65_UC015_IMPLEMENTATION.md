# Task65 — UC015 Update Floor/Area

30/10/2026, Asia/Saigon. **COMPLETE_VERIFIED_V1** cho cả Floor và Area: production HTTPS GET/PUT,
SQL-backed authorization, conditional updates, immutable identity, atomic audit và actual SQL fault/
concurrency acceptance PASS. Không thay đổi production schema hoặc generated entities.

## A. Requirements và V1 contract

Đã đối chiếu [UC015 §2.4](../../../docs/requirements/srs_sections/02_use_case_specifications.md),
[GBR001/002/003/005/007/008/034](../../../docs/requirements/srs_sections/05_requirement_appendix.md),
[Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md), draft Permission Matrix,
[current catalog](../../database/release/03_HotelOps_RBAC_Catalog_Seed.sql),
[Task63](TASK63_UC014_IMPLEMENTATION.md), [Task64](TASK64_UC013_IMPLEMENTATION.md), mappings/entities.
SRS yêu cầu Active target, edit details/version, code/name/parent/description +reason, branch-unique
code, cùng branch/không cycle, stale-write rejection và update/audit atomic; Room/Asset/history giữ
nguyên tham chiếu tới ID. Schema Floor/Area **không có rowversion**. Current permission là
MASTER_DATA_MANAGE; không suy quyền từ tên Administrator hoặc thay catalog.

| Topic | Implemented contract |
|---|---|
| Authorization | Server CurrentUserAccessor + protected ticket + persisted SQL session, initial/final validation. Existing MASTER_DATA_MANAGE global hoặc branch. Bound ALL/BRANCH không vượt assignment/membership; OWN unsupported fail closed. ROOM_VIEW không cấp quyền GET edit/PUT |
| Floor editable | Code, name, description; reason bắt buộc. FloorNumber giữ nguyên vì UC015 không liệt kê nó là editable; input floorNumber/parent/branchId/id/isActive bị400. ID và branch bất biến |
| Area editable | Code/name/description và nullable FloorId, được UC015/GBR034 cho phép. Không đổi AreaId/BranchId/IsActive hoặc rewrite liên kết nghiệp vụ |
| Parent | New Floor null hoặc tồn tại/Active/cùng branch. Area chỉ trỏ Floor, Floor không có parent: không có Area→Area hoặc Floor→Area chain để tạo cycle. Legacy Area có parent khác branch không được đọc/sửa qua endpoint;404, không lộ foreign ID |
| Related Rooms | Nếu new FloorId nonnull, mọi Room đang trỏ Area phải cùng branch và cùng FloorId. Nếu không:409 PARENT_CHANGE_CONFLICT, kể cả Room inactive để giữ history. Cho phép Area thành branch-level/null: Room vẫn giữ required FloorId thực tế. Empty Area có thể đổi sang Floor khác hợp lệ; không cascade Room/Asset/assignment |
| Active | Branch và target phải Active để PUT. Global manager có thể GET chi tiết target inactive trong branch Active, nhưng PUT409. Scoped inactive branch bị evaluator deny; global inactive branch409. Không activate/deactivate trong UC015 |
| Normalization | Reuse Task63 semantics: trim+uppercase code,1–30 ASCII letters/digits/_/-, ký tự đầu chữ/số; case-insensitive trimmed duplicate lookup trên cả Floor/Area namespace trong branch, loại chính target; inactive code vẫn giữ. Name trim Unicode Floor100/Area150, required/no control chars; không ép unique name |
| ASSUMED_V1 text | Description trim, blank→null, max4000, cho newline/tab như Task63. Reason trim, required1–1000, no control chars; chỉ lưu audit, không trả trong response |
| PUT semantics | ASSUMED_V1 full replacement của editable fields; omitted description→null, omitted Area.floorId→null. Code/name/reason required. Unknown fields reject;32KiB body limit +existing CSRF. Không dùng PATCH hoặc âm thầm bỏ qua unsupported fields |
| No-op | ASSUMED_V1 accepted PUT kể cả không đổi business fields vẫn ghi audit với changedFields=[], trả ETag mới. Mỗi successful SAVE tiêu thụ tag cũ |
| Errors | Missing If-Match428; weak/wildcard/list/lowercase/wrong-width tag400; stale412. Duplicate409; parent invalid400/conflict409; inactive409; authorized missing/foreign target404; scope403; session401 theo pipeline. No-store; dedicated DTO, generic provider-error responses |

## B. Concurrency alternative — no schema change

GBR005 yêu cầu so sánh loaded revision, nhưng schema không cung cấp token. Theo quyền chọn least
invasive alternative trong Task65, **ASSUMED_V1 strong ETag** là quoted uppercase SHA-256 (64hex)
của fixed UC015-V1 contract +toàn bộ scalar state Floor/Area trong DTO +latest committed AuditId
cho exact entityType/entityId. Full state bao gồm ID/branch/code/name/description/active/parent/
FloorNumber; không dùng timestamp, checksum yếu hoặc mã version do client chọn.

Existing [DB rules](../../../docs/database/DATABASE_RULES.md) mô tả audit_logs append-style; schema
có identity AuditId và index(entityType,entityId,OccurredAt). Source inventory chỉ tìm thấy UC014
create và UC015 update ghi Floor/Area fields; không có production audit deletion writer. Every UC015
save atomically appends audit, nên A→B→A hoặc no-op không tái sử dụng ETag cũ. Hash state còn bắt
được legacy/raw SQL thay đổi representation hiện tại. Đây **không phải SQL rowversion column**.

Guarantee áp dụng cho hiện tại và các producer tuân thủ append-only audit +canonical lock protocol.
Không chứng nhận arbitrary privileged SQL A→B→A không audit, audit deletion/rewriting hoặc future
import/job bỏ qua protocol. Audit retention/migration phải bảo toàn revision anchor hoặc thiết kế
rollout token mới, không purge/reset nó âm thầm. Đây là maintenance constraint của alternative,
không tạo runtime/release gate UC006 hoặc giả định có production rowversion.

Owned SERIALIZABLE transaction; retained **actor User → SQL Session/grants → Branch UPDLOCK/HOLDLOCK
→ requested parent Floor HOLDLOCK → target Floor/Area UPDLOCK/HOLDLOCK → authoritative ETag/
duplicates/related-Room checks → UPDATE → audit INSERT → final session/permission → COMMIT**.
Branch lock cùng UC014 serialize cả code namespace/create/update. Không late target/manager User
lookup, không stale EF tracked state, không EF entity exposure. GET edit cũng giữ retained branch/
target locks để trả details và token từ một consistent read; không business write/audit cho GET.

Wait thắng trước PUT được reread dưới khóa: external current-state change làm tag stale; inactive
parent bị reject; revocation thắng User lock bị deny. Time-based expiry sau UPDATE rollback cả row
và audit. SQL1222/-2/1205 →503, không tự replay mutation; client retry bằng request/context mới và
ETag phù hợp. Nếu request trước đã commit, tag cũ412, không audit/update lần hai. Commit/unknown
fault không trả success; existing generic middleware không lộ SQL exception.

## C. HTTP usage

```http
GET /api/branches/{branchId}/floors/{floorId}
GET /api/branches/{branchId}/areas/{areaId}
PUT /api/branches/{branchId}/floors/{floorId}
If-Match: "<64 uppercase hex from GET>"

{"code":"F01","name":"Tầng 1","description":"Khu phòng khách","reason":"Cập nhật tên tầng"}
```

```http
PUT /api/branches/{branchId}/areas/{areaId}
If-Match: "<current tag>"

{"code":"A01","name":"Khu A","floorId":123,"description":null,"reason":"Điều chỉnh khu vực"}
```

Successful GET/PUT200 returns `{kind,id,branchId,code,name,description,floorId,floorNumber,isActive}`,
ETag header và Cache-Control:no-store. Floor.floorId/Area.floorNumber null theo đúng schema. Không
trả audit revision/reason/personnel/operational state. PUT response tag dùng cho lần save tiếp theo;
reload GET khi412. ID123 chỉ minh họa, không hard-code trong source. Existing real cookie/session/CSRF
required cho unsafe request. GET edit là management path riêng; UC013 ROOM_VIEW basic policy giữ nguyên.

## D. Source changes

| File | Change |
|---|---|
| [UpdateStructureDtos.cs](../../../src/HotelOps.Api/DTOs/HotelStructure/UpdateStructureDtos.cs) | New strict request DTOs và edit response |
| [HotelStructureUpdateService.cs](../../../src/HotelOps.Api/Services/HotelStructure/HotelStructureUpdateService.cs) | New GET/update workflow, version/audit/validation/SQL safety |
| [HotelStructureUpdateController.cs](../../../src/HotelOps.Api/Controllers/HotelStructureUpdateController.cs) | New HTTPS GET/PUT routes, If-Match/HTTP errors |
| [API Program.cs](../../../src/HotelOps.Api/Program.cs) | One scoped DI registration; earlier Task62–64 registrations retained |
| [Harness Program.cs](Program.cs) | Optional --task65, runs existing --task64 regressions before new cases; legacy modes preserved |
| [Task65StructureCases.cs](Task65StructureCases.cs), [Task65StructureConcurrency.cs](Task65StructureConcurrency.cs) | New real HTTPS and actual SQL service acceptance |
| [Delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md) +this report | Verified classification/progress/evidence |

Không sửa entities/DbContext, SQL/schema/catalog, DEV2, UC006 hoặc previous receipts. No shared
architectural refactor, new test project/framework, Deactivate endpoint, SA57 activation or Git mutation.

Preflight/final HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`, branch
`feature/BE1-database-integration`, upstream `origin/feature/BE1-database-integration` unchanged.
Index và bốn worktrees giữ nguyên. Backup `obj/task65-preflight-20261030-074410`: **436/436 hashes
valid,433 protected files unchanged,3 intended edits** (API Program.cs, harness Program.cs, plan);
six remaining delivery source/docs files are new. Ignored `obj/task65-validation.local.json` records
checksums, whitespace/local links, classification counts and evidence. Snapshot preflight→validation
**16.3 elapsed minutes**, gồm test/tool waits, không phải human-hours hoặc full-backlog speed claim.

## E. Executed SQL/HTTPS acceptance

[Final receipt](task65-accepted-final2.local.json): **589 assertions PASS =215 UC015 +215 UC014 +159
UC013**. **53 DMV-confirmed SQL waits =19 UC015 +23 UC014 +11 UC013**; **14 UC015 post-UPDATE fault/
expiry schedules**. Assertions include login/setup/invariant checks, không phải589 independent UCs.
Final trial `HotelOps_Canonical_20261030_005426_4f3d446c_Fresh`, freshly initialized canonical SQL +
unchanged approved guarded rowversion prerequisite, instance/DB/Task17 marker and startup gate checked
before fixtures/regression. HotelOpsDB never connected to; no original SQL source modified.

Production root Program.cs child API thực chạy HTTPS, ephemeral pinned certificate, actual password
login, protected cookies, persisted SQL sessions, normal CSRF/middleware/DI. Không mocked API host,
permission store hoặc fake Update service. Receipt không lưu password/cookie/raw session/connection.

| Verification | Result / actual evidence |
|---|---|
| Successful Floor/Area | PUT200, normalized persisted values, strong new ETag/no-store, exact DTO; branch/global management access. FloorNumber/IDs retained, Area null-parent/reparent empty Area work |
| HTTP denial/validation | Anonymous GET/valid-CSRF PUT401; no-CSRF unsafe400; ROOM_VIEW/no grant/Owner/restricted403; scoped cross-branch403; invalid IDs/payload/unsupported ownership/status/number400; authorized missing/foreign target404; corrupt foreign-parent Area404 |
| Constraints | Duplicate same/cross Floor/Area code409; inactive target409; foreign/inactive parent400; Room-incompatible parent409; no rewrite of Room/Asset references |
| Optimistic version | Stale412; missing428; malformed/weak/wildcard/list400; actual A→B→A and no-op tag rotation; same-tag two-actor HTTPS race exactly one200/one412 |
| Namespace coordination | Different-target duplicate update race one200/one409. Actual UC014 POST versus UC015 PUT same code: one success/one409, no duplicate |
| Audit | Exactly one UPDATE audit per accepted save, SQL actor/branch/UTC/action/entity, reason, old/new state, exact changedFields; failed mutation does not retain audit/revision |
| Authoritative waiting | Real HTTPS waits target Floor lock; raw committed state change makes old tag412. Parent-deactivate-first SQL wait: newly inactive Floor rejected, Area revision unchanged |
| Revocation | Actual HTTPS retains actor User while waiting target; canonical permission revoker waits User, first request consistent, next PUT403. Expired SQL session HTTPS PUT401 |
| Cancellation | Real HTTPS RequestAborted releases locks before blocker release. Separate **after UPDATE** direct-service cancellation for both Floor/Area rolls back row/audit and clears transaction/tracker |
| Post-UPDATE faults | Both Floor/Area: actual SQL THROW51065 audit failure, LOCK_TIMEOUT1200→1222, command timeout2s→-2, actual deadlock1205, cancellation, session expiry and grant expiry. Verify UPDATE executed, clean transaction/tracker and exact previous DTO/ETag/audit count restored |
| Retry | Fresh context/request after each fault/expiry can commit exactly once; replay with old tag stale, no blind automatic replay |
| Unrelated data | Functional PUT phase full Users/roles/grants/memberships/Room/Asset snapshots unchanged after logins; session activity middleware stays enabled. No account/credential/role or business assignment mutation by service |

Direct fault tests use persisted session fixtures, protected principals, production CurrentUserAccessor,
PermissionService/SQL source, SqlSessionService and Update service. These are distinguished from actual
HTTPS cases. Audit command interceptor injects SQL THROW or a blocking RoomType read **only in harness**
after production UPDATE, without DDL/test hooks in runtime. Intentional inverse RoomType→User holder
forces a real victim1205; it is not production lock order or global/UC006 acceptance proof. Restore
and retry only affect new owned trial fixtures, never old artifacts or production data.

Current catalog has MASTER_DATA_MANAGE ALL; actual SQL scoped test uses a branch-bound ALL assignment.
BRANCH/OWN evaluator semantics are additionally covered by documented authorization foundations;
no synthetic catalog grant was inserted to label an unconfigured mapping as live SQL acceptance.

Initial run stopped at a harness assertion that looked for TagWith in ExecuteUpdate SQL when recording
whether UPDATE ran. It was corrected to observe actual UPDATE/column SQL, not to bypass rollback.
Earlier failed log/trial and successful intermediate585-assertion receipt remain; final589 receipt
adds explicit concurrent parent eligibility, changedFields and legacy-parent scope checks.

## F. Regression/build and remaining limits

- **UC014215 /23 waits** and **UC013159 /11 waits** PASS in final production HTTPS/SQL run.
- **UC023117 /11 waits** [PASS](task65-room-regression.local.json), separate disposable trial
  `HotelOps_Canonical_20261030_004942_dae2c869_Fresh`; internal query only, **PARTIAL unchanged**.
- **AuthN/AuthZ:** Permission43/Authorization61/Resource49/Hardening21 =**174 PASS**, documented
  doubles/loopback scope. No main-DB --live. UC015 receipt provides actual SQL login/session evidence.
- **Build:** solution +HotelStructure/RoomDirectory harnesses PASS. **NU1900** vulnerability-feed
  access warnings remain; no C# warnings/errors, no suppression/package change.
- **dotnet test HotelOps.slnx:** exit0, no discovered test project; do not infer unit-test PASS.
  Explicit console SQL/HTTPS suites above executed. git diff --check and new-file whitespace PASS.

No mandatory blocker remains within this V1 UC015 contract. DBA/import writers and audit-retention
changes must follow revision/lock rules; not certified by local acceptance. Production deployment,
load and final SRS/Permission Matrix ratification remain separate. UC006 release gates unchanged.

**UC015 COMPLETE_VERIFIED_V1; +2 points →23/256=8.98%,9/75 complete.**
Remaining233 verified points; UC023 PARTIAL, no denominator/scope reduction.

**Recommended next:** UC016 Deactivate Floor/Area, reusing scoped edit/ETag/branch-lock infrastructure;
first implement exact GBR035 guards for active child Areas/Rooms/Assets and open Incidents/Tasks and
coordinate actual reference writers. Không giả định mọi historical reference đều blocker hoặc an toàn.
