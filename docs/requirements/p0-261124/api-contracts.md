# P0 — API contracts theo source

Prefix `/api/v2`. Các controller có `[Authorize]`; bảng dưới mô tả contract source hiện tại, **runtime NOT_TESTED** tại mốc tài liệu. DTO là request/response riêng, không phải EF entity công khai. Xem `Compact/Controllers`, `Compact/Contracts`, `Compact/Services` trong `src/HotelOps.Api`.

## Quy tắc chung

- `operationKey` là GUID của một ý định mutation. Retry giữ nguyên key và payload; ý định mới dùng key mới. Nơi có payload hash, tái sử dụng key khác payload bị từ chối.
- `rowVersion` là chuỗi base64 từ hồ sơ vừa đọc. Work, room, document và request coordination có version riêng; không hoán đổi chúng. Retry hợp lệ phải được xét trước stale-version tại các endpoint có receipt idempotent.
- UI chỉ báo thành công sau response thành công, giữ dialog/lỗi khi thất bại. HTTP 403/404 được dùng để chứng minh từ chối truy cập; 409 chỉ chứng minh conflict nghiệp vụ, không đủ chứng minh RBAC.
- Không gửi token, password/hash hay connection string vào báo cáo. Scope/handler/capabilities được server tính lại khi mutation; client không tự quyết định quyền.
- Số lượng dùng decimal, đơn vị catalog/snapshot; không âm hoặc 0 ở dòng đề nghị/cấp thực tế. Phạm vi và precision cụ thể được service kiểm tra.

## Vật tư cá nhân và cấu hình

Controller: `SuppliesController.cs`; DTO: `SupplyContracts.cs`.

| Method/route | Input | Response/trách nhiệm |
|---|---|---|
| GET `/supplies/configuration` | `branchId` | Danh sách configuration hiện hành trong scope |
| PUT `/supplies/configuration` | `SaveSupplyConfigurationRequest` | Revision cấu hình, rowVersion mới; OM + ROOM_SUPPLY_CONFIG |
| GET `/supplies/allocations` | `branchId`, `userId?` | Allocation và balances; HK OWN, vai trò giám sát theo grant |
| GET `/supplies/allocations/{id}/history` | Allocation ID | Ledger được phép đọc, không endpoint ghi tay |
| GET `/supplies/documents/{id}` | Document ID | Chứng từ supply liên quan theo quyền |

`SaveSupplyConfigurationRequest`: `branchId`, `itemId?`, `assetId?`, `supplyGroup`, `warehouseId?`, `unitId?`, `initialQuantity`, `minimumStock`, `perGuestQuantity`, `isActive`, `rowVersion?`. Service kiểm tra đối tượng theo nhóm A/B/C, unit và đúng một kho hoạt động; sửa tạo revision thay vì sửa snapshot ca.

Configuration response có ID, item/asset code/name, nhóm, warehouse/unit, định mức, active, revision, updatedBy/At, rowVersion. Allocation response có allocation/branch/user/assignment, status, shortageReason, created/closedAt, version và items. Balance response phân biệt initialTarget, initialIssued, additionalIssued, reserved, used, returned, remaining, available. Ledger có kind, quantity, work/reservation/document-line liên quan, actor và thời gian.

Không có API độc lập cho allocation/consume/return: các writer nội bộ chạy từ start/end ca, phân công/hoàn tất việc và yêu cầu refill trong transaction nghiệp vụ.

## Workday và cleaning

| Method/route | Contract P0 |
|---|---|
| GET `/workday?branchId=&date=` | Lịch, ca hiện tại; `canEnd`/`endBlockedReason` là quyết định backend |
| POST `/workday/assignments/{id}/{command}` | `start`/`end` qua `ActionRequest`; version, key, kiểm tra chủ ca/khung giờ; cấp/trả supply cùng transaction |
| GET `/workday/assignments/{id}/history` | Lịch sử ca được phép đọc |
| GET `/workday/personal-summary?branchId=` | Tổng ca/giờ thực tế tuần/tháng |
| POST `/work/{id}/actions/{command}` | Hành động work hiện có; CLEANING assignment gọi reserve, complete gọi consume, cancel gọi release |
| POST `/work/{id}/inspection-result` | Kết quả checklist giấy theo `PaperInspectionRequest`, không cấu hình checklist điện tử |
| GET `/work/housekeeping-history` | `branchId`, `date?`, `page`, `search?`; lịch sử theo ngày địa phương |

Giữ API phân ca batch, cảnh báo vượt 8h/ngày hoặc 40h/tuần theo contract đang có. Không thêm quyền nhận việc sau giờ chỉ vì ca HK còn WORKING. Handover/đổi người phải đi qua reservation guard; generic typed request mutation phải bị chặn bằng `USE_REQUEST_WORKFLOW` khi thuộc luồng chuyên biệt.

## Workspace yêu cầu

Controller `RequestWorkflowsController.cs`; DTO `RequestWorkflowContracts.cs`.

| Method/route | Input/ý nghĩa |
|---|---|
| GET `/requests` | `branchId`, `queue=mine|action|progress|completed|rejected|all`, `page=1`, `pageSize=30`, `search?` |
| GET `/requests/types` | `branchId`; loại tạo trực tiếp được phép, không tự bịa loại trên client |
| GET `/requests/{id}` | Request detail theo Work ID |
| POST `/requests/refills` | `allocationId`, `items[{itemId,quantity}]`, `reason`, `operationKey` |
| POST `/requests/replenishments` | `branchId`, `warehouseId`, `items`, `reason`, `operationKey`, `priorityId?` |
| POST `/requests/{id}/actions/{action}` | `rowVersion`, `operationKey`, `reason?`, `approvedItems?`; claim/release/approve/reject/issue/cancel theo capability |
| POST `/requests/{id}/receive` | `rowVersion`, `operationKey`, actual `items`, `supplierName?`, `note?` |

Summary: workId/number/type/title, branch/room/creator/departments, status, queueRole, handlerUserId, priority, created/updatedAt, correlationId, rowVersion, `structured`. Detail thêm description, allocation/warehouse/document/parent, items, history, allowed actions. Item line có requested/approved quantity, stock/minimum snapshot và received/current stock. History ghi trước/sau status/queue/handler, actor/role/time, reason, document, correlation, previous version.

`structured:false` là projection legacy: mở detail domain gốc; không suy ra timeline đầy đủ từ một status. Các request room có coordination metadata dùng claim/release tại requests nhưng hành động chuyên môn tại room API. Trạng thái/queue không có endpoint sửa tự do.

Phê duyệt không được thêm mặt hàng, bỏ một phần tập dòng, tăng vượt đề nghị hoặc dùng số lượng không dương. Refill issue một lần đủ lượng đã duyệt; replenishment receive hỗ trợ thực nhận từng phần. OM ordinary request readonly; exception chỉ replenishment approval đúng queue.

## Báo cáo phòng

Controller `OperationsControllers.cs` (Work); DTO `RoomIssueRequests.cs`.

| Method/route | Input/ý nghĩa |
|---|---|
| GET `/work/room-issue-types` | `branchId`; loại được cấu hình và quyền đọc phù hợp |
| PUT `/work/room-issue-types` | `branchId` query; kind/name/isActive/rowVersion/description; cấu hình OM |
| GET `/work/room-issue-objects` | `branchId`, `roomId`; đồ đã gán, phân nhóm phù hợp |
| GET `/work/room-issues` | `branchId`; visibility theo role/ownership |
| POST `/work/room-issues` | `RoomIssueCreateRequest` |
| GET `/work/room-issues/{id}` | Work/detail/history/capability theo phạm vi |
| POST `/work/room-issues/{id}/{command}` | `RoomIssueActionRequest` + domain stage/handler checks |

Create: `branchId`, `roomId`, `typeId`, `assetId?`, `itemId?`, `roomRowVersion`, `operationKey`, `note?` (≤2.000), `quantity=1`, `impact=READINESS|SAFETY`, `cleaningWorkId?`. HK/Reception dùng **IR_CREATE**, không generic INCIDENT_CREATE. Server cố định phòng, tạo title và routing. HK phải liên hệ công việc đúng phòng/chu kỳ; Reception chỉ báo hỏng. Đối tượng phải thuộc phòng và nhóm hợp lệ.

Action: `rowVersion`, `operationKey`, `note?`, `documentId?`, `roomRowVersion?`, `itemId?`, `unitId?`, `quantity?`, `warehouseId?`; DTO còn các field tương thích cũ không phải quyền tự chọn handler/bộ phận. Commands hiện có: `approve`, `reject`, `create-repair`, `start-repair`, `request-material`, `report-remedy`, `inspection-pass`, `inspection-fail`. `send-repair`/`request-inspection` giữ đường tương thích hồ sơ cũ, vẫn kiểm queue và role.

Reject/fail yêu cầu lý do; báo remedy cần đúng chứng từ vật tư đã giao hoặc repair con hoàn tất. Inspection dùng version room cùng version work khi điều kiện yêu cầu. Phòng OCCUPIED/RESERVED không bị sửa occupancy từ các thao tác này.

## MR, transfer và asset

MR thông thường tiếp tục `/documents`, dòng và `/actions/{command}`. ApprovalAuthority yêu cầu STOREKEEPER + MR_APPROVE_L1 BRANCH; người tạo không tự duyệt. OM không là fallback. Room-remediation MR dùng writer nội bộ sau xác nhận giám sát, không mở tự duyệt chung.

Transfer catalog: GET `/documents/transfer-catalog?branchId={destination}`. POST `/documents` kind `TRANSFER_REQUEST`: branchId là **nguồn**, destinationBranchId là **nơi OM yêu cầu**, source/destinationWarehouseId đúng hai branch. Tạo dòng, submit, OM nguồn approve/reject; thủ kho nguồn derive `TRANSFER` và post; thủ kho đích derive `TRANSFER_RECEIPT` và post lượng thực nhận. Listing cần explicit participant branch. Approval không chuyển tồn; amount còn lại kiểm lại lúc post.

Detail transfer có source/destination branch, projected workflowStatus và capabilities, children. PREPARING/IN_TRANSIT/COMPLETED là projection, không bắt client ghi các status này vào document. Chỉ hủy trước dispatch; prepared draft không được post sau cancellation. Notify cho đúng người của hai bên, FK notification vẫn theo document branch.

Asset API hiện hữu dùng `AssetService.Act`: move, condition, handover, accept, reject, retire. Rowversion/reason/key và branch/location/custodian vẫn bắt buộc. Không nhầm `ASSET_TRANSFER` di chuyển tài sản nội bộ với stock transfer xuyên cơ sở. Kiểm thử A về asset receiver và audit vẫn cần riêng.

## Bổ sung contract sau integration 24/11/2026

- POST `/work/{id}/reconcile-supplies`: `rowVersion`, `operationKey`, `reason`. Giám sát đang trực mới được xử lý CLEANING đã bắt đầu, chưa có reservation/consumption và còn đúng chu kỳ phòng. Nếu thiếu allocation, chỉ trường hợp attendance/work/assignment thực sự trước cutover migration mới được cấp bổ sung ban đầu; lấy cấu hình hiện tại và xuất kho thật trong cùng transaction giữ chỗ. Không viết lại thời gian hay lịch sử công việc. Kiểm thử shortage rollback, replay, quyền, hoàn thành sau giờ và trả đồ thừa: runtime-legacy-supplement.json.
- Request con INTERNAL_REQUEST liên hệ cha bằng `RequestWorkflow.ParentRequestWorkId` và correlation. Không gán `WorkItem.ParentWorkId` cho kind không được constraint cho phép. Generic maintenance action của repair con bị từ chối; dùng room workflow.
- Request list/read chỉ hiển thị claim lại khi handler cũ không còn đủ điều kiện trong ca. Claim mutation kiểm tra lại trong transaction, ghi previous/new handler; actor đang trực khác không chiếm claim của người vẫn hợp lệ. runtime-handler-supplement.json.
- Asset offer/accept/reject đều kiểm tra recipient đủ quyền ASSET_VIEW trong chi nhánh và vai trò nghiệp vụ, không nhận bàn giao qua Owner/Admin read-only.
- Error middleware tìm SQL deadlock lồng trong lỗi EF và trả `409 TRANSACTION_RETRY_REQUIRED`; không tự retry giao dịch ghi không rõ idempotency.

## Lỗi và kiểm thử contract

Các mã source cần dịch sang thông báo nghiệp vụ rõ ràng gồm `SUPPLY_RESERVATIONS_OPEN`, `SHIFT_WORK_OPEN`, `ACTIVE_SHIFT_REQUIRED`, `REFILL_REQUEST_ALREADY_OPEN`, `SUPPLY_ITEM_NOT_IN_SHIFT_SNAPSHOT`, `REQUEST_ALREADY_CLAIMED`, `INDEPENDENT_APPROVER_REQUIRED`, `APPROVED_LINES_MISMATCH`, `IDEMPOTENCY_KEY_REUSED`, `USE_REQUEST_WORKFLOW`, `OM_REQUEST_READ_ONLY`, `ROOM_ISSUE_WRONG_STAGE`, `ROOM_ISSUE_OBJECT_NOT_IN_ROOM`.

Mã từ implementation có thể thay đổi trong integration; test phải đối chiếu response thực tế, không dùng chuỗi tài liệu để báo PASS. Kiểm tra route/DTO/source là IMPLEMENTED, build thành công là compile evidence; authorization, stock và concurrency cần runtime riêng.
