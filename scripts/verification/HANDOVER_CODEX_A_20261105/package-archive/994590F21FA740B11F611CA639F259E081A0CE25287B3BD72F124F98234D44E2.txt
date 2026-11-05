# Codex B — UC013 read-only handoff

Snapshot chỉ dành cho review. **UC013 được chủ dự án giao độc quyền Codex B; Codex A không implementation UC013, không chuyển uncommitted source hoặc áp dụng patch vào worktree B.**

## Kết luận quan trọng về điểm

**BLOCKED: không thể xác nhận “UC013 chưa được cộng điểm”.** Ledger Task80 hiện ghi UC-013 `COMPLETE_VERIFIED_V1`, weight **2**, credited **2**. Task75 reconciled ledger và Task86 progress corroborate baseline. Task64 §12 của delivery plan ghi rõ gain +2; số 0 ở bảng kế hoạch UC013 là estimated hours remaining, không phải credited points. Export giữ nguyên ledger, không trừ/cộng điểm hoặc thay classification. Tổng **26/75, 60/256 =23.4375%**. Công việc B hiện là recovery/reverification; không tính như một UC mới trừ khi có reconciliation được chủ dự án duyệt.

`DEV1_LEDGER_EXPORT.json` và CSV xuất đủ 75 UC, title/status/weight/credited/evidenceSummary/receiptReferences. Receipt paths lấy từ section được row ledger trỏ tới; có thể là batch/regression, không mặc định là full acceptance riêng của mọi UC trong section. Row không có exact receipt path được đánh `PENDING_EVIDENCE`, không tự tạo receipt. JSON giữ nguyên mọi status COMPLETE_VERIFIED và COMPLETE_VERIFIED_V1 theo nguồn. Ledger status là historical accepted progress, không chứng nhận current runtime tồn tại.

Historical Task64 receipt tìm được trực tiếp từ immutable snapshot cf8fb020362f8f64192e7f2f14a10547cebe5b91: `scripts/verification/HotelStructure/task64-accepted-final2.local.json`; 159 structure-read assertions, 11 recorded waits. Đây là receipt lịch sử, không chạy lại trong task này; exact blob/path nằm trong `UC013_HISTORICAL_RECEIPT_SUMMARY.json`. Không dùng assertions làm điểm mới.

## Quyền thực tế và semantics

**APPROVED (recorded Task64 V1 decision):** `ROOM_VIEW` độc lập cho basic physical Branch/Floor/Area/Room. `MASTER_DATA_MANAGE` độc lập cho management read gồm departments và Floor/Area descriptions. Không yêu cầu cả hai; không có permission riêng UC013, BRANCH_VIEW hoặc STRUCTURE_VIEW được tìm thấy trong catalog. Task64 report tại commit trên và current delivery plan §12 ghi developer approval ASSUMED_V1; bản tin approval gốc không nằm riêng trong snapshot. Các tài liệu HotelStructureRead còn ghi “pending mapping” là proposal cũ, không thay thế decision Task64.

| Actor/catalog role | Actual catalog grant | Hành vi V1 có điều kiện |
|---|---|---|
| Administrator / SYS_ADMIN | MASTER_DATA_MANAGE:ALL; không ROOM_VIEW | Management read qua effective ALL path hợp lệ; không bypass theo tên admin |
| Operations Manager / OPERATIONS_MANAGER | ROOM_VIEW:BRANCH; không MASTER_DATA_MANAGE | Basic physical read trong assigned active branch; không departments/descriptions nếu không có một management grant riêng hợp lệ |

Catalog và line numbers trong `UC013_PERMISSION_EVIDENCE.json`; numeric permission_id là DB-specific, không hardcode. **PENDING_EVIDENCE:** live SQL grants/effective permissions của từng user chưa query, không được nói mọi SYS_ADMIN/OM đều đang được phép. Chỉ SQL-backed evaluation của current authenticated user mới quyết định quyền request.

- ALL + assignment BranchId null có thể global; ALL bound branch chỉ hợp lệ trên đúng branch với active assignment/access, không thành global. EvaluateBranchAsync vẫn deny inactive target, kể cả ALL. **Ngoại lệ contract Task64 đã ghi rõ:** unbound global MASTER_DATA_MANAGE qua EvaluateGlobalAsync được đọc archived/inactive Branch; không gọi đây là Branch-policy bypass. Scoped/basic không được đọc inactive Branch. Khôi phục phải giữ đúng sự phân biệt này.
- BRANCH cần branch-bound assignment đúng target, active role/account, assignment dates hợp lệ và nonrevoked/current branch access. Không ghép grant role này với membership của role khác.
- OWN cần verified resource ownership qua resolver; không cấp branch tree chỉ vì membership/department/manager. UC013 không có OWN resolver binding được duyệt; fail closed. Unknown/DEPARTMENT scope không grant.
- PermissionService kiểm account/deleted, không employment. UserEligibilityLock và service boundary phải kiểm employment ACTIVE riêng. CurrentUserAccessor chỉ đọc request identity, không tự refresh SQL session. Session/account/permission phải recheck dưới transaction và trước trả dữ liệu, kể cả management-to-basic downgrade sau khi materialize management data.
- Không dùng authentication-only, role-name bypass, RoomTypeDirectory permission, query branchId hay DTO actor làm authority. Giữ branch SQL predicate và no-store; không chạy count/node lookup ngoài scope trước authorization.

**PENDING_EVIDENCE / approval nếu muốn mở rộng:** full draft cho OM xem departments hoặc scoped/basic inactive Branch vượt V1 cần quyết định cụ thể; task giao UC013 không tự cấp quyền/grant mới. Global management archived read đã thuộc Task64 V1. Inactive child trong active branch khác inactive Branch.

## Source/helper có thể dùng khi đã hiện diện trong branch của B

`REUSABLE_SOURCE_INVENTORY.json` có exact file/class/method declaration line, SHA256 và git status cho từng path. Một số signature nhiều dòng: field signatureLine là dòng khai báo bắt đầu, đọc tiếp tại source path. Không export implementation source sang B.

| Current helper | Method/lines | Cách dùng / giới hạn |
|---|---|---|
| DTOs/HotelStructure/BranchStructureDtos.cs | BranchStructureQuery.Validate 14; CollectionsFor 30; DTO declarations 34–40 | Node kind + typed IDs, branch không nodeId, child combinations, max100/default20 và overflow validation; DTO-only, không endpoint/service |
| Security/Authorization/PermissionService.cs | EvaluateGlobalAsync 9; EvaluateBranchAsync 12; EvaluateResourceAsync 16; EvaluateAsync 21 | Existing SQL-backed scope semantics; dùng interface đã có, không sửa evaluator/resolver cho UC013 |
| Security/Authorization/PermissionDataSource.cs | EfPermissionDataSource.IsActiveUserAsync 21; IsActiveBranchAsync 25; ReadPathsAsync 28 | Giữ từng grant gắn đúng assignment; không full session/employment validator |
| Security/Authorization/CurrentUserAccessor.cs | TryGetCurrentUser 8 | Server-side identity only, không client actor hoặc tự dựng session |
| Services/Users/UserEligibilityLock.cs | LockAsync 12; RequireEligible 31; RequireEligibleAsync 38 | Caller-owned transaction; sorted/distinct users, UPDLOCK/HOLDLOCK, fresh eligibility. Không tự commit |
| Services/Authentication/SqlSessionService.cs | ValidateSessionAsync 42; CheckAsync 48; RunAsync 104 | Read-only Validate path tham gia compatible caller tx; không gọi Touch để thay authentication check |
| Services/HotelStructure/RoomTypeDirectoryService.cs | private ReadAsync 106, local Gate 144 | Pattern initial/final session+permission, clean context, User-first/branch lock; private, feature-bound, không callable reusable general API. RoomTypes applock/schema gate không tự copy vào UC013 |
| Services/HotelStructure/TurnoverHistoryBoundary.cs | private ReadAsync 20, Gate 58 | Pattern only, CLEANING_VIEW và RoomTypes lock dành UC038; không cấp authority UC013 |
| Services/HotelStructure/TurnoverHistorySchema.cs | internal RoomsAsync 10 | Current legacy/V1 RoomType scope compatibility query; phụ thuộc RoomTypeDirectorySchema.ReadAsync. Không dùng thay authorization hoặc tự thay schema |
| Services/HotelStructure/BranchAdministrationService.cs | ReadAsync 53; Execute 111; Gate 139 | UC012 global MASTER_DATA_MANAGE + rowversion dependency; không phải UC013 branch read cho OM, không dùng làm drop-in |
| Services/HotelStructure/HotelStructureCreationService.cs | CreateFloorAsync 23; CreateAreaAsync 31; transaction 52 | Writer compatibility/reference only; UC013 không gọi writes |

Actual line index trong JSON là authoritative nếu bảng tóm tắt khác. Entities Branch/Floor/Area/Room/Department và DbContext mappings có hashes cùng inventory: Area.FloorId nullable, Room.FloorId required, Room.AreaId nullable; không invent unified floor_area table hoặc floor-parent-floor. Giữ same-branch predicates và reject corrupt/foreign parent links; DTO không expose EF entities.

**BLOCKED current runtime reuse:** BranchStructureService.cs và BranchStructureController.cs không có trong current DEV1 main; Program không đăng ký BranchStructureService. DTO còn. Historical service/controller tại immutable commit có exact Git blobs/method lines trong `HISTORICAL_UC013_IDENTITIES.json`; đây không phải code đã restore và không phải permission áp patch vào B. Historical service line54 còn gọi RoomTypeSchema.RoomsAsync, nhưng RoomTypeSchema class không có trong current main. Current TurnoverHistorySchema.RoomsAsync là dependency candidate để review, không tự động thay thế/restore. Codex B tự review branch của mình; Codex A không copy source/diff.

## Ownership và shared contracts

**APPROVED:** B độc quyền UC013 theo current user instruction. A chỉ COORD-FINAL-01; các UC liên quan handoff A là UC012/029/037/041/044/045/046/111/138/139 ở mức contract/dependency, không nhận implementation UC013. UC038/UC142 là recovery đã có receipt; không mở batch mới. Không coi danh sách UC dependencies là một allocation implementation mới.

DEV2 module ownership từ AGENTS: FE06/07/08/09/10/14/15. `UC_OWNERSHIP.json` xuất exact IDs/titles/features từ SRS; UC063/064 shared phải phối hợp. Current confirmed handoff gồm Notification V1, UC062 Maintenance/PM participant, Branch-active guards cho Incident/Maintenance/Inventory/MR/Replenishment. **PENDING_EVIDENCE:** không có live task-allocation roster của DEV2 để chứng nhận mọi UC thuộc module đang triển khai; module ownership không phải current task claim. Không có overlap UC013 implementation với A/DEV2 theo assignment đã xác nhận.

Shared baseline: **COORD-FINAL-01 / parallel revision _02**, không có unified semantic version release được xác minh. Exact ZIP/manifest hash và source SHA được ghi trong EVIDENCE_MANIFEST.json. Task64 ASSUMED_V1 là UC013 read contract; A1/B1.1 Workday approved/applied, B1.2 pending; Notification A01 ACCEPTED user-confirmed, A02 DEV2-reported structural, B01 architecture DESIGN ONLY, B01 executable/B02/C01 REVISE; Room exact shared diffs NOT APPROVED; global total lock order NOT SOURCE-VERIFIED.

| Thay đổi pending | Ảnh hưởng UC013 |
|---|---|
| BranchStructureService DI / restore vào main | BLOCKED áp dụng nếu chưa có exact diff + review conflicts + approval riêng; không kế thừa DI approval Task84/85/86 |
| Branch deactivation writer protocol (UC012) | Giữ SQL authority/retained read locks và archived global-management contract Task64; không bật Deactivate, không thay BranchAdministrationService để phục vụ UC013 |
| Branch rowversion Task86 trial | Không phải prerequisite tự động cho basic EF tree; không dùng private UC012 raw row_version query làm generic read; SQL rollout ngoài approval hiện tại |
| Room 3-dimension lifecycle/mapping migration | Chỉ đọc actual physical identity; không invent status/readiness/block/cycle hoặc thay EF mappings |
| Notification C01 / DbContext / Program shared diffs | Không implementation, giữ local registrations. Không mặc định mọi read cần notification/audit publication; UC013 no business mutation |
| OM departments/scoped inactive Branch/full SRS mở rộng | PENDING_EVIDENCE/approval specific nếu khác Task64 V1; global management archived read đã nằm trong V1, không tạo permission/grant mới |
| Source B vào main | Exact diff + base HEAD/hashes → conflict review → approval riêng → verification. Không auto sync hoặc checkout |

Build/test/HTTPS/SQL: **NOT_RUN**. Không kết nối DB. Đây là snapshot source/contract/ledger, không nghiệm thu UC013 runtime. Chỉ tạo artifact mới; HEAD/index/local patches và các worktrees được kiểm pre/post. Metadata B thay đổi đồng thời, nếu có, được báo riêng; không chứng nhận source B.

Current main branch: feature/BE1-database-integration; HEAD: 4a187320643d0c82f232c6b9dd5a667b8401c280.

