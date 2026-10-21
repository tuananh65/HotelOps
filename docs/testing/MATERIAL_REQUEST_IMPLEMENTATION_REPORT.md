# Báo cáo triển khai FE-09 Material Request

## Phạm vi và preflight

Nhánh `feature/BE2-material-request`, base `aa9d3c2`; working tree ban đầu sạch.
SQL xác minh `KIENPC / HotelOpsDB`. Đã backup COPY_ONLY/CHECKSUM ngoài Git và
RESTORE VERIFYONLY thành công trước patch. Không stage/commit/push hoặc đổi nhánh.

Contract triển khai là yêu cầu FE-09 cuối cùng của developer; các quyết định tạm
về approver, receiving location và UC-092 được áp dụng đúng phạm vi dưới đây.

## Schema và Database First

Patch `database/patches/20261020_material_request_contract.sql` dùng exact database
guard, XACT_ABORT, transaction, schema applock và required SET options cho filtered
indexes. Không hard-code developer hostname trong patch. Không rewrite legacy rows.

Delta: request rowversion, canonical status CHECK giữ legacy codes, Room/Area/Incident
FKs, at-most-one location, remainder closure fields; line IsActive và quantity CHECK
sau kiểm tra legacy; approval cycles và durable line snapshots; nullable cycle FK
và unique decision/cycle; nullable ISSUE-line FK và filtered unique linkage;
filtered unique fulfillment document; supporting lookup/timeline indexes.

EF CLI/provider 10.0.8 scaffold đầy đủ vào ignored `obj/MaterialRequestScaffold`,
namespaces `HotelOps.Api.Entities` / `HotelOps.Api.Data`, context `HotelOpsDbContext`,
default naming/Fluent API, `--no-onconfiguring`. Local named connection chưa được
cấu hình trong môi trường chạy nên dùng process environment với integrated
authentication. Không lưu connection string trong source.

Đồng bộ chọn lọc từ scaffold: material entities/mappings và navigation trực tiếp.
Giữ tên key/index cũ không liên quan. Giữ navigation Requester hiện hữu để tránh
thêm quan hệ shadow khi scaffold đổi tên do xuất hiện FK user thứ hai. Unique
document/line linkage làm navigation tương ứng thành one-to-one theo scaffold.
Không đồng bộ drift RolePermission/Room/assignments của baseline, không migration.
Runner đối chiếu SQL catalog với EF cho type/length/precision/nullability/identity,
PK/FK/navigation/index và rowversion concurrency/value generation.

## API và state machine

Base `/api/material-requests`: list/filter/page, detail, history, availability,
fulfillment list/detail, create Draft/create-and-submit, edit, submit,
request-information, approve, reject, cancel, close-remainder, immediate ISSUE.
Controllers mỏng, DTO độc lập EF; services async/CancellationToken.

New writes dùng DRAFT/PENDING_APPROVAL/NEEDS_INFORMATION/APPROVED/
PARTIALLY_ISSUED/FULLY_ISSUED/REMAINDER_CLOSED/REJECTED/CANCELLED.
Legacy statuses/fulfillments đọc raw, mutation fail-closed khi không có workflow
history đủ an toàn. Không biến RETURNED thành stock return.

Pending edit supersede cycle và tạo revision mới; Needs Information edit không
resubmit; Approved zero-issued edit tạo reapproval. Historical line snapshots giữ
nguyên khi current content thay đổi. Removed lines được retire, không tái dùng
historical line number hoặc LineId cho item/UOM khác. No-op không audit/version update.

Approval single-level; mỗi cycle tối đa một decision. ApprovedQty nằm trong
[0,RequestedQty]; all-zero là reject có reason. Cancel chỉ requester trước first
ISSUE. Partial remainder closure lưu actor/time/reason, không giảm ApprovedQty.

## Inventory atomic integration

Một source warehouse/request, active/same-branch khi sử dụng. Immediate ISSUE duy
nhất cho new fulfillment, không PREPARING/receive API. FE-09 sở hữu transaction và
cùng scoped DbContext với `IInventoryIssuePosting`; Inventory SaveChanges không
commit. Header/lines được lưu để có FulfillmentId, primitive trả document/line IDs,
FE-09 lưu mapping và cập nhật cumulative quantities/status/audit rồi commit chung.

Không ghi stock_movements từ FE-09, không public unrestricted ISSUE endpoint.
Approval theo request UOM; Inventory dùng factor hiện hành tại ISSUE, giữ snapshot
trong stock lines. Fulfillment dùng đúng request ItemId/UomId. Duplicate ItemId
được phép; primitive gộp base demand. Available là ledger stock, không reservation.
RETURN dùng Inventory lineage, không giảm FulfilledQty hoặc mở lại approved demand.
New fulfillment history được kiểm tra correlation/totals trước lần ISSUE tiếp theo.

Lock order: Catalog → request gate/row → FUL number gate → primitive fulfillment
gate → sorted warehouse/stock gates → ISS number gate. Number MR/FUL theo UTC,
D3 tối thiểu, >999, bỏ qua malformed suffix; unique constraint là hàng rào cuối.
ExpectedRowVersion Base64 chống stale intent, không thay transaction locks.
Failure rollback và clear tracked state; infrastructure/cancellation không giả
thành validation. Validation/state/stale trả400 ProblemDetails; missing root404.

Audit dùng action CHECK hiện hữu, business event trong JSON, actual changed fields,
actor/branch/UTC và cycle/line/document correlation. ISSUE có Inventory audit,
fulfillment audit và request audit trong cùng transaction.

## Ranh giới nghiệp vụ

ActorUserId không phải authenticated identity. Active predicate: chưa deleted,
account/employment ACTIVE; branch relationship là primary branch hoặc non-revoked
UserBranchAccess. Requester/assigned approver ID consistency và issuer khác approver
là intrinsic checks, không chứng minh Department Head/Storekeeper authorization.
Auth/RBAC/current-user/permission/read-scope enforcement chờ DEV1; không thêm auth.

UC-092 PARTIAL: chỉ Incident typed FK, same branch và OPEN/IN_PROGRESS/RESOLVED khi
tạo/thay link. Không mutate Incident, không chặn ISSUE vì incident đóng sau linking.
Attribution immutable sau first ISSUE. Cleaning/Maintenance occurrence/Operational
Task links deferred; không thay occurrence bằng corrective MaintenanceWork.

Receiving department dùng department_id. Nullable Room/Area typed FK, tối đa một,
active/same branch nếu chọn. V1 cho null cả submit; applicability enforcement deferred.
Không same-department giả định, free text hoặc auto-derived future work location.
NeededAt null Draft, required Submit, UTC; không ép future/created/submitted bound.

Open approved shortage observable qua OpenFulfillableQty; không FE-10 writes,
notification, consumption ledger, auto-resume Maintenance, frontend, procurement,
reservation, ISSUE reversal hoặc legacy repair.

## Kiểm thử

Runner tái lập: `tests/HotelOps.MaterialRequests.IntegrationTests`; hướng dẫn cùng
thư mục. HTTP tests, SQL lock-manager barriers, separate connections và fault
injection thực sự chạy trên local target, fixtures cleanup trong finally.

Acceptance FE-09 cuối: **179 HTTP requests, 832 assertions, 11 overlapping SQL races,
10 rollback/fault cases: PASS**. Ngoài counter rollback có MR-number rollback
injection và cancellation khi đang chờ Catalog lock; đều PASS. Đã xác minh 78
columns trong bảy bảng FE-09, PK/FK/target columns/navigation, supporting indexes,
trusted/enabled CHECKs và rowversion metadata. Fixture cleanup hoàn tất.

Các race: edit/submit, submit/cancel, approve/reject, approve/cancel, fulfill/cancel,
hai partial cùng remaining, issue/close remainder, MR numbering, FUL numbering,
hai request tranh stock, FE-09 ISSUE/Inventory writer. SQL lock-manager barrier
xác nhận hai connection đều WAIT trước khi giải phóng blocker.

Fault injection: sau header, fulfillment lines, ISSUE/movements, Inventory audit,
FE-09 audit; trước primitive, sau primitive, trước commit, trong FE-09 audit save,
cancellation sau line save. DbContext mới xác minh không rò header/line/movement/
status/quantity/audit hoặc stock effect. Tracked state đã bỏ sau rollback.

`dotnet build HotelOps.slnx`: PASS, 0 errors; NU1900 feed warning còn tồn tại.
`dotnet test HotelOps.slnx --no-restore`: exit 0; solution hiện không chứa test
project được discovery bởi test runner. Bằng chứng test nghiệp vụ nằm ở hai
console integration runners thực thi riêng, không suy ra từ lệnh dotnet test.
Inventory regression đã chạy: 246 HTTP requests, 510 assertions, success=true,
fixture cleanup committed. Bao gồm receipt/return/transfer/stocktake/adjustment/
reversal/ISSUE primitive, concurrency/rollback và GET smoke regression của Incident,
Maintenance, Asset; không tuyên bố đã test lại toàn bộ write workflow ba module này.

Trong self-fix đã sửa SQL batch compilation/SET options cho filtered indexes và
navigation Requester trùng gây shadow FK. Các lỗi này không còn ở final build/
acceptance. Scaffold tạm đã xóa; encoding/BOM hiện hữu được giữ; không có migration,
backup, dump, secret hoặc unrelated EF drift trong diff. Không stage/commit.

Known dependency warnings: NU1900 khi vulnerability feed không truy cập được;
Inventory runner ghi NU1902/NU1903 cho transitive System.Security.Cryptography.Pkcs
9.0.4. Không đổi dependencies ngoài scope. Loopback test host có thể báo không tìm
thấy HTTPS redirect port; không thay production HTTPS configuration.
