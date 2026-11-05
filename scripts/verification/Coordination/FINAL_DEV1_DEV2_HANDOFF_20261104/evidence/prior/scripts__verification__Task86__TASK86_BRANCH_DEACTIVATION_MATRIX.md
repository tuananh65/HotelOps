# Task86 — GBR-033 và concurrency handoff

**DEACTIVATION_CONCURRENCY_BLOCKED. Không có Active→Inactive writer trong Task86.** Empty dependency snapshot không cho phép deactivate. Known blockers trả 409; không có known blockers vẫn 503. Snapshot `CanDeactivate=false`, `ScopeResolutionComplete=false`, `HasUnresolvedStates=true` cố định tới khi joint contract được xác minh.

## Dependency facts thực sự truy vấn

| Nhóm | Nguồn/branch mapping | Classify hiện tại | Giới hạn |
|---|---|---|---|
| Active rooms | rooms.BranchId + IsActive | Active count | Khác branch không được cộng |
| Warehouse stock | stock_movements → Warehouse.BranchId, SUM(SignedBaseQty) theo WarehouseId+ItemId, count distinct warehouse có net≠0 | Known blocker, kể cả balance âm hoặc warehouse inactive | Theo InventoryService.Balance; không cộng net các item khác nhau; chưa live SQL verified |
| Operational tasks | operational_tasks.BranchId | TODO/IN_PROGRESS/BLOCKED open; COMPLETED/CANCELLED closed | Unknown unresolved |
| Cleaning tasks | cleaning_tasks.Room.BranchId | PENDING/IN_PROGRESS open; COMPLETED/CANCELLED closed | Chỉ nguồn persisted |
| Cleaning reworks | cleaning_reworks.CleaningTask.Room.BranchId | Như cleaning tasks | Không suy diễn từ latest assignment |
| Inspection tasks | room_inspections.Room.BranchId, kiểm CleaningTask.Room nhất quán | PENDING/IN_PROGRESS open; COMPLETED/CANCELLED closed | Mismatch sang branch khác thành inspection_scope_mismatch/UNRESOLVED |
| Maintenance works | Incident.BranchId hoặc Department.BranchId khi các link nhất quán | PENDING/IN_PROGRESS/ON_HOLD open; CLOSED/CANCELLED closed; COMPLETED unresolved | Link null/orphan và PM run/schedule cần scope contract; conflict thành maintenance_scope_mismatch/UNRESOLVED |
| Incidents | incidents.BranchId | OPEN/IN_PROGRESS open; CLOSED/CANCELLED closed; RESOLVED unresolved | Không coi RESOLVED là closed khi chưa joint review |
| Internal requests | internal_requests.BranchId | SUBMITTED/IN_PROGRESS open; CLOSED/REJECTED/CANCELLED closed; DRAFT/COMPLETED unresolved | Không tự loại draft/completed khỏi GBR-033 |
| Material requests | material_requests.BranchId | SUBMITTED/PENDING_APPROVAL/NEEDS_INFORMATION/APPROVED/PARTIALLY_ISSUED open; CANCELLED/REJECTED closed | FULLY_ISSUED/REMAINDER_CLOSED và các legacy states chưa binding GBR-033 → unresolved |
| Replenishments | material_replenishments.Warehouse.BranchId | REQUESTED/PARTIALLY_RECEIVED open; CLOSED closed | FULLY_RECEIVED unresolved |

Status vocabulary đối chiếu schema `scripts/database/release/01_HotelOps_Full_Schema.sql` (cleaning:3347/3351, internal:3379, maintenance:3387, operational:3427, inspections:3439) và effective DEV2 patches `database/patches/20261020_material_request_contract.sql`, `20261021_replenishment_contract.sql`. Schema cho phép một trạng thái không đồng nghĩa chủ dự án đã duyệt trạng thái đó là terminal theo GBR-033. Không có blocker count tuyệt đối cho PM chưa resolve; snapshot báo scope incomplete toàn cục, không báo zero đủ điều kiện.

## Actual writer evidence và race

| Writer | Source evidence | Kết luận |
|---|---|---|
| DEV1 Floor/Area | HotelStructureCreationService.cs:52 transaction Serializable; :63 Branch UPDLOCK/HOLDLOCK; :65 IsActive | Có retained active-branch guard cho riêng writer này; không chứng minh các writers khác |
| DEV2 Incident | IncidentService.cs:228 ValidateCreateAsync gọi trước BeginTransaction :232; :319–322 chỉ Any BranchId tồn tại; SaveChanges :293/:312, Commit :315 | **Counterexample xác định**: sau DEV1 deactivate commit, branch vẫn tồn tại nên writer hiện tại có thể tạo incident vào branch inactive |
| DEV2 Warehouse/stock | InventoryService.Masters.cs:64 SaveWarehouse; InventoryService.cs:92 Begin transaction; InventoryAccess.cs | Cần proof active-branch guard cuối cùng dưới retained lock cho create/transfer/posting; read-scope IsActive không đủ |
| DEV2 Material request | MaterialRequestService.Commands.cs:22/:90/:158 transactions | Cần kiểm final active branch trong mọi create/submit/issue/reopen path; không tự sửa |
| DEV2 Replenishment | ReplenishmentService.cs + Inventory receipt posting participant | Cần cùng transaction, warehouse authoritative branch + active branch retained guard |
| DEV2 Maintenance/PM | MaintenanceWorkService.Create.cs/Commands.cs/Workflow.cs; PreventiveMaintenanceSchedule/Run entity | Cần source branch identity từ Incident/Department/Schedule.Room/Area/Asset, xử lý link conflict/orphan, và retained branch guard |
| DEV1 Room/Task/Cleaning/Inspection/Internal Request | Current writer availability không đồng đều giữa checkout và snapshot | Phải kiểm kê tất cả writer/reopen/reassign/transfer trước khi bật deactivation; không lấy snapshot/historical report làm proof runtime |

Serializable dependency scans có thể giữ key-range locks tới commit nhưng chỉ ngăn insert trong khoảng scan/commit. Writer không kiểm active sau khi chờ vẫn có thể ghi ngay sau deactivate commit. Vì vậy một sequential negative test không đủ và không bật deactivation để chạy “success” giả.

## Exact dependency request, không cấp quyền sửa writer

1. DEV2 xác nhận tất cả writer entrypoints vào Branch/Warehouse/Incident/Request/Maintenance và nguồn BranchId authoritative; chốt terminal-state mapping GBR-033, PM scope và orphan/conflict behavior.
2. Joint review một protocol retained active-branch guard trong caller transaction, nhất quán User/session → Branch → source/dependent rows; đánh giá lock order thực tế của Inventory/RoomTypes/PM trước khi duyệt. Đây mới là đề xuất, **chưa là approved protocol**.
3. Từng DEV2 writer phải reject inactive branch cả khi branch chuyển inactive trong lúc chờ; transfers/multiple branches cần thứ tự ổn định đã review. Không bù bằng việc hủy records đang mở.
4. Sau code+approval: chạy deterministic races theo cả hai thứ tự (writer thắng → deactivate thấy blocker; deactivate thắng → writer từ chối), actual SQL locks, rollback+audit, không coi deadlock/retry là concurrency PASS.
5. DEV1 khi đó mới triển khai Active→Inactive conditional SQL update, final dependency recheck và audit cùng transaction. Cần trial execution scope riêng nếu vượt trial approval đã cấp.

Toàn bộ step trên chưa thực hiện; không có thay đổi DEV2, shared lock helper, schema trigger bảo vệ branch hoặc deactivation gates.
