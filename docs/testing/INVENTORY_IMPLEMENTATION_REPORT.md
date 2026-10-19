# Báo cáo triển khai DEV2 Inventory — 19/10/2026

## A. Preflight

- Nhánh: `feature/BE2-inventory`.
- HEAD đầu và cuối: `a5507c4`.
- Working tree ban đầu sạch; không có thay đổi nguồn của người khác cần xử lý.
- SQL xác nhận trực tiếp: `@@SERVERNAME = KIENPC`, `DB_NAME() = HotelOpsDB`.
- Đã đọc yêu cầu được duyệt, quy tắc repository, tài liệu database/SRS và code DEV2 liên quan.
- Không stage, commit, push, đổi nhánh, pull, merge, rebase hoặc reset.
- Backup `COPY_ONLY, CHECKSUM` thành công; `RESTORE VERIFYONLY ... WITH CHECKSUM` xác nhận hợp lệ. Backup được lưu ngoài Git trong thư mục tạm cục bộ có quyền phù hợp.

## B. Schema delta và Database First

Patch **đã áp dụng** lên database được xác minh:
`database/patches/20261019_inventory_contract.sql`.
Kiểm tra cú pháp `PARSEONLY` và chạy lại patch đều thành công.

| Bảng mới | PK/FK | Index hỗ trợ ngoài PK |
|---|---|---|
| `stock_transfer_receipts` | PK identity `transfer_receipt_id`; FK document và user nhận | `(transfer_document_id, transfer_receipt_id)`, `received_by` |
| `stock_transfer_receipt_lines` | PK identity; FK batch và transfer line | `transfer_receipt_id`, `transfer_line_id` INCLUDE ba quantity |
| `stock_receipt_line_details` | PK/FK `stock_document_line_id`, quan hệ một-một | Không cần index bổ sung |
| `stock_return_line_details` | PK/FK return line; FK original issue line | `original_issue_line_id` |

Tổng cộng: bốn bảng, bốn PK, bảy FK, năm index hỗ trợ. Index transfer line không unique, cho phép nhiều đợt nhận.

Thêm `row_version rowversion NOT NULL` cho `inventory_items`, `units_of_measure`, `uom_conversions`, `warehouses`, `stock_thresholds`, `stock_documents`, `stocktakes`.

Scaffold đầy đủ bằng EF CLI `10.0.8` vào `src/HotelOps.Api/obj/InventoryScaffold`, dùng provider SQL Server, namespace entity `HotelOps.Api.Entities`, namespace context `HotelOps.Api.Data`, context `HotelOpsDbContext`, `--no-onconfiguring --no-build`. Giữ naming mặc định PascalCase/Fluent API; không dùng database names/data annotations. Connection lấy qua tên cấu hình `ConnectionStrings:HotelOpsDb`; môi trường thực thi chưa có User Secrets nên dùng biến môi trường tạm của tiến trình. Không ghi connection string vào nguồn hoặc cấu hình chia sẻ.

Đồng bộ chọn lọc theo scaffold: bốn entity mới, bảy property rowversion, navigation trực tiếp trong `StockDocument`, `StockDocumentLine`, `User`, cùng DbSets/mapping tương ứng. Các rowversion có `IsRowVersion`, `IsConcurrencyToken` và value generation `OnAddOrUpdate`.

Suite đối chiếu từng cột mới với SQL catalog: type, length/precision/scale, nullability, identity, PK, FK, index key/uniqueness và navigation; đồng thời kiểm tra SQL timestamp và concurrency metadata cho cả bảy root. Kết quả Passed.

Full scaffold phát hiện drift có sẵn ngoài Inventory: suffix tên PK/UQ, filtered index/navigation của cleaning/internal/operational assignments và user branch access, cùng khác biệt ở `RolePermission`/`Room`. Giữ nguyên các mapping này. Drift không được đồng bộ vào tác vụ Inventory. Encoding của các file hiện có được giữ nguyên. Scaffold tạm đã được xóa sau đối chiếu. **Không tạo EF migration.**

Quy trình tái lập: `docs/database/DATABASE_FIRST_REGENERATION.md`.

## C. API đã triển khai

Tất cả đường dẫn dưới đây có tiền tố **`/api/inventory`**.

| Nhóm | Method và đường dẫn |
|---|---|
| Item | `GET /items`, `GET /items/{id}`, `POST /items`, `PUT /items/{id}` |
| Category | `GET /item-categories` |
| UOM | `GET /units`, `GET /units/{id}`, `POST /units`, `PUT /units/{id}` |
| Conversion | `GET /items/{id}/conversions`, `GET /items/{id}/conversions/{conversionId}`, `POST /items/{id}/conversions`, `PUT /items/{id}/conversions/{conversionId}` |
| Warehouse | `GET /warehouses`, `GET /warehouses/{id}`, `POST /warehouses`, `PUT /warehouses/{id}` |
| Threshold | `GET /warehouses/{warehouseId}/thresholds`, `PUT /warehouses/{warehouseId}/items/{itemId}/threshold` |
| Stock | `GET /stock`, `GET /low-stock`, `GET /warehouses/{warehouseId}/items/{itemId}/stock` |
| Movement/ledger | `GET /movements`, `GET /warehouses/{warehouseId}/items/{itemId}/ledger?from=...Z&to=...Z` |
| Document | `GET /documents`, `GET /documents/{id}`, `GET /documents/{id}/history`, `POST /documents/{id}/cancel` |
| Receipt | `POST /receipts`, `PUT /receipts/{id}`, `POST /receipts/{id}/post` |
| Transfer | `POST /transfers`, `PUT /transfers/{id}`, `POST /transfers/{id}/dispatch`, `POST /transfers/{id}/receipts` |
| Return | `POST /returns`, `PUT /returns/{id}`, `POST /returns/{id}/post` |
| Adjustment | `POST /adjustments` |
| Reversal | `POST /documents/{id}/reverse` |
| Stocktake read/create | `GET /stocktakes`, `GET /stocktakes/{id}`, `POST /stocktakes` |
| Stocktake actions | `PUT /stocktakes/{id}/counts`, `POST /stocktakes/{id}/submit`, `POST /stocktakes/{id}/reconcile`, `POST /stocktakes/{id}/approve-adjustment`, `POST /stocktakes/{id}/return`, `POST /stocktakes/{id}/cancel` |

Deactivation qua `PUT` với `IsActive=false`, giữ lịch sử. Threshold chỉ ghi minimum; giữ nguyên maximum/reorder legacy. DTO trả rowversion Base64; edit/action trên root hiện có yêu cầu ExpectedRowVersion. Các list chính có Page/PageSize và thứ tự ổn định. Không trả EF entity qua API.

HTTP: 201 khi tạo resource phù hợp; 200 khi update/action thành công; 400 ProblemDetails cho validation, state và stale rowversion; 404 cho root không tồn tại. Lỗi SQL/lock bất thường không được đổi thành “thiếu tồn” hoặc quantity zero. Duplicate master key được nhận diện có giới hạn theo table/index đã biết.

## D. State machines

- Receipt/Return: `DRAFT -> POSTED`, `DRAFT -> CANCELLED`; chỉ sửa Draft.
- Transfer: `DRAFT -> IN_TRANSIT -> PARTIALLY_RECEIVED -> RECEIVED`; có thể nhận đủ ngay từ IN_TRANSIT; `DRAFT -> CANCELLED`.
- Adjustment/Reversal: tạo trực tiếp `POSTED`. Original reversal hợp lệ: `POSTED -> REVERSED`.
- Issue: primitive nội bộ tạo `POSTED`, tham gia transaction của FE-09.
- Stocktake: tạo `IN_PROGRESS`; `IN_PROGRESS -> COUNTED/CANCELLED`; `COUNTED -> PENDING_ADJUSTMENT/RECONCILED`; `PENDING_ADJUSTMENT -> RECONCILED/COUNTED`. Counts có thể sửa khi IN_PROGRESS hoặc COUNTED, phục vụ recount sau return.
- Legacy trả raw status; không tự đổi status hay dựng lại ledger. Transfer legacy POSTED thiếu receiving persistence trả quantity detail không biết dưới dạng null. Legacy warehouse department null vẫn đọc được. Warehouse deactivation chặn document/stocktake chưa xác nhận được trạng thái kết thúc.

## E. Stock semantics

`OnHand = SUM(stock_movements.signed_base_qty)` trên toàn bộ ledger; `Available = OnHand`. Không có balance cache/reservation. Cutoff dùng `OccurredAt`, với opening trước From, period bao gồm hai đầu và closing bằng opening cộng period. Original REVERSED và offset REVERSAL đều hiện trong ledger.

New writes dùng base UOM factor 1 hoặc active item-specific direct conversion về base. Không dùng global conversion hay chain. Draft/post revalidate catalog/UOM; posted snapshot giữ nguyên. Base UOM khóa sau movement đầu tiên. Kết quả base quantity phải biểu diễn chính xác decimal(18,3), không làm tròn âm thầm. Duplicate ItemId được phép; demand/effect gộp theo warehouse/item trước kiểm tra tồn.

Receipt chỉ cộng accepted usable. Return lưu original issue line và damaged riêng; cap cumulative usable + damaged theo lượng issue, có quy đổi base. Transfer dispatch chỉ trừ nguồn; từng receiving batch chỉ cộng usable vào đích. In-transit bằng dispatched trừ received + damaged + missing đã reconcile. Toàn bộ loss vẫn có persistence.

Adjustment âm -15 lưu ActualQty/BaseQty = 15, factor 1, movement = -15. Reversal tạo exact offset theo warehouse/item, giữ nguyên movement gốc. Chỉ receipt/return/adjustment được reverse; chặn transfer, reversal và issue.

Stocktake snapshot gồm item có balance khác zero hoặc có threshold. Null count khác zero. Delta adjustment bằng counted-at-cutoff trừ system-at-cutoff; cộng delta vào tồn hiện tại, giữ movement sau cutoff. Người approve khác người count/submit. Mỗi stocktake chỉ có một adjustment link.

Numbering riêng theo prefix/ngày UTC, tối thiểu ba chữ số; suffix lớn hơn 999 hợp lệ, malformed legacy suffix bị bỏ qua. Allocator dùng transaction-owned exclusive applock, maximum suffix và unique constraint hiện có; không dùng sequence của module khác.

## F. Transaction và audit

Mọi business write dùng explicit transaction. Header/lines/detail/movements/status/audit của một action cùng commit hoặc rollback. Các timestamp của cùng action dùng một UTC timestamp. No-op không tạo UpdatedAt/audit giả.

Audit dùng `CREATE`, `UPDATE`, `CANCEL`, không thêm action label vào CHECK. Business event nằm trong JSON; EntityType dạng tên bảng chữ thường, ID sau generation, actor, branch, old/new values và actual ChangedFields. Stocktake có snapshot dòng trong JSON; cancellation không khai dòng thay đổi giả.

Cross-branch transfer tạo hai audit row cho mỗi action: SOURCE và DESTINATION, cùng entity/action/timestamp. Không dùng audit row làm primary persistence của quantity/lineage.

`IInventoryIssuePosting` dùng cùng scoped DbContext và transaction do caller mở. Nhận fulfillment/request-line correlation, kiểm tra item/UOM/tồn, tạo issue/movement/audit và trả mapping line ID. Không tự commit. Caller FE-09 chịu trách nhiệm authorization, fulfillment totals/linkage, commit hoặc rollback toàn workflow.

## G. Concurrency — SQL thật

Protocol: Catalog, owner document/stocktake/fulfillment, warehouse theo numeric ID, stock pair theo numeric warehouse/item, cuối cùng number gate. Root được query bằng parameterized UPDLOCK/HOLDLOCK khi cần. App lock có transaction ownership và bảo vệ cả pair chưa có movement.

| Race/setup | Kết quả request | Invariant đã kiểm tra sau race, trước cleanup |
|---|---|---|
| Tồn 10, hai adjustment -8 | 201 + 400 | OnHand = 2 |
| Hai post cùng receipt/token | 200 + 400 | POSTED, đúng một movement +1 |
| Edit quantity 2 vs post quantity 1 | 200 + 400 | DRAFT với 0 movement hoặc POSTED với 1 movement |
| Cancel vs post | 200 + 400 | Một terminal path; CANCELLED/0 movement hoặc POSTED/1 movement |
| Hai transfer ngược chiều, mỗi chiều 1 | 200 + 200 | Mỗi document IN_TRANSIT, một movement -1; không lock inversion/deadlock |
| Hai dispatch cùng transfer | 200 + 400 | Một source movement -1 |
| Hai receive cùng token, quantity 1 | 200 + 400 | Một batch; source -1 và destination +1, tổng hai movement/net 0 |
| Issue 3, hai return mỗi phiếu 2 | 200 + 400 | Cumulative physical accepted = 2, không vượt 3 |
| Hai reversal cùng adjustment +1 | 200 + 400 | Một reversal document; original + offset có tổng signed quantity 0 |
| Hai create stocktake cùng warehouse | 201 + 400 | Một canonical open stocktake, kiểm tra SingleAsync |
| Stocktake variance -1 vs ordinary +2 | 200 + 201 | NewCurrent = CurrentBefore + 1; link một adjustment |

SQL lock wait với CancellationToken bị hủy được xác nhận. SqlClient cancellation được chuẩn hóa thành OperationCanceledException khi token thực sự canceled; lỗi lock/infrastructure khác vẫn là lỗi thực.

## H. Fault injection/rollback

Interceptor chỉ nằm trong test project, không có switch/hook hoạt động trong production.

| Điểm lỗi | Bằng chứng rollback |
|---|---|
| Sau SQL insert header | Không còn document hoặc stock effect của action lỗi |
| Sau SQL insert lines | Số header/line/movement không tăng |
| Sau movement SQL | Tồn giữ nguyên, không có movement một phần |
| Sau/đang audit persistence | Không có audit hoặc business rows commit một phần |
| Sau receiving batch SQL | Không có batch/header/detail orphan; receipt action không để stock effect |
| Sau stocktake adjustment document | Không có adjustment còn lại; stocktake không giữ adjustment link |
| Trước transaction commit | Toàn bộ footprint header/line/movement/audit/batch giữ nguyên |

Receipt post lỗi cũng giữ DRAFT. Caller-owned issue rollback không commit document, đồng thời chứng minh primitive không tự commit.

## I. Kết quả validation

Lần chạy cuối: fixture prefix `INVTEST_870b8d2bcc`, **246 HTTP requests, 510 assertions, Passed**.

| HTTP | Số lượng |
|---|---:|
| 200 | 126 |
| 201 | 57 |
| 400 mong đợi | 62 |
| 404 mong đợi | 1 |

- SDK: `10.0.402`.
- `dotnet restore HotelOps.slnx`: hoàn tất, còn NU1900.
- `dotnet build HotelOps.slnx`: Passed, 0 compile errors; cảnh báo NuGet được ghi bên dưới.
- `dotnet test HotelOps.slnx`: lệnh hoàn tất; solution hiện không có test project/case để VSTest thực thi. **Test result tại lệnh này: Not Run**.
- Executable integration suite: **Passed**, chạy riêng bằng `dotnet run --project tests/HotelOps.Inventory.IntegrationTests`; đây là suite tự động có assertion và exit code, không phải EF InMemory.
- Regression: build và GET API Assets, Incidents, Maintenance Works đều Passed; code nghiệp vụ các module này không thay đổi. Không tuyên bố đã chạy lại toàn bộ workflow ghi của các module cũ.
- Metadata SQL/EF, state/precision/UOM validation, stock arithmetic, history, cross-branch audit, race và rollback: Passed theo assertions trong suite.
- Fixtures được cleanup theo exact ID do test tạo, kể cả audit. SQL sau kiểm thử xác nhận không còn item/warehouse/unit/document/stocktake/audit payload mang prefix INVTEST. Không giữ business fixture. Không reseed identity hoặc sửa dữ liệu mẫu.

Hướng dẫn chạy: `tests/HotelOps.Inventory.IntegrationTests/README.md`.

## J. Git diff audit và danh sách file

`git diff --check` sạch. Diff tracked: **11 files, 185 insertions, 0 deletions**. Có **22 file mới chưa stage**, gồm báo cáo này. Không có file bị xóa. Đã review cả file mới, không chỉ tracked diff.

File hiện có được sửa:

1. `src/HotelOps.Api/Data/HotelOpsDbContext.cs`
2. `src/HotelOps.Api/Entities/InventoryItem.cs`
3. `src/HotelOps.Api/Entities/StockDocument.cs`
4. `src/HotelOps.Api/Entities/StockDocumentLine.cs`
5. `src/HotelOps.Api/Entities/StockThreshold.cs`
6. `src/HotelOps.Api/Entities/Stocktake.cs`
7. `src/HotelOps.Api/Entities/UnitsOfMeasure.cs`
8. `src/HotelOps.Api/Entities/UomConversion.cs`
9. `src/HotelOps.Api/Entities/User.cs`
10. `src/HotelOps.Api/Entities/Warehouse.cs`
11. `src/HotelOps.Api/Program.cs` — chỉ thêm Inventory DI/import.

File mới:

1. `database/patches/20261019_inventory_contract.sql`
2. `docs/database/DATABASE_FIRST_REGENERATION.md`
3. `docs/testing/INVENTORY_IMPLEMENTATION_REPORT.md`
4. `src/HotelOps.Api/Controllers/InventoryController.cs`
5. `src/HotelOps.Api/DTOs/Inventory/InventoryContracts.cs`
6. `src/HotelOps.Api/Entities/StockReceiptLineDetail.cs`
7. `src/HotelOps.Api/Entities/StockReturnLineDetail.cs`
8. `src/HotelOps.Api/Entities/StockTransferReceipt.cs`
9. `src/HotelOps.Api/Entities/StockTransferReceiptLine.cs`
10. `src/HotelOps.Api/Services/Inventory/IInventoryIssuePosting.cs`
11. `src/HotelOps.Api/Services/Inventory/InventoryService.cs`
12. `src/HotelOps.Api/Services/Inventory/InventoryService.Adjustments.cs`
13. `src/HotelOps.Api/Services/Inventory/InventoryService.Documents.cs`
14. `src/HotelOps.Api/Services/Inventory/InventoryService.Issues.cs`
15. `src/HotelOps.Api/Services/Inventory/InventoryService.Masters.cs`
16. `src/HotelOps.Api/Services/Inventory/InventoryService.Queries.cs`
17. `src/HotelOps.Api/Services/Inventory/InventoryService.Stocktakes.cs`
18. `tests/HotelOps.Inventory.IntegrationTests/HotelOps.Inventory.IntegrationTests.csproj`
19. `tests/HotelOps.Inventory.IntegrationTests/Program.cs`
20. `tests/HotelOps.Inventory.IntegrationTests/DeepTests.cs`
21. `tests/HotelOps.Inventory.IntegrationTests/SchemaMetadata.cs`
22. `tests/HotelOps.Inventory.IntegrationTests/README.md`

Không thay package/version, auth middleware, shared connection configuration, module Incident/Maintenance/Asset hoặc kiến trúc một API project. Không có migration, secret, connection string local, scaffold tạm, production fault hook hoặc TODO che invariant Inventory trong thay đổi. Kết quả tìm TODO chỉ gặp default status TODO có sẵn trong mapping OperationalTask không thay đổi.

## K. Ranh giới được giữ

Không triển khai public issue/FE-09 Material Request workflow, FE-10 replenishment, RBAC/authentication, notifications/jobs, frontend, procurement/supplier, export/report product, valuation/FIFO/COGS, reservation/allocation hoặc Maintenance material-consumption integration. Không reverse ISSUE. ActorUserId là input tích hợp tạm và không chứng minh caller đã authenticated; ManagerUserId không phải authorization.

## L. Cảnh báo và giới hạn thực tế

- NU1900: không lấy được NuGet vulnerability feed, vẫn xảy ra khi thử restore ngoài sandbox. Build thành công.
- Test restore/build báo NU1902/NU1903 cho dependency bắc cầu `System.Security.Cryptography.Pkcs 9.0.4`; dependency này cũng có trong assets của API baseline. Giữ package versions theo phạm vi được duyệt; cảnh báo này cần được đội xử lý trong tác vụ dependency riêng.
- HTTP harness phát cảnh báo HTTPS redirection không xác định HTTPS port từ middleware hiện có. Kiểm thử local dùng HTTP; chưa xác minh TLS deployment.
- Catalog gate hiện exclusive cho mọi Inventory write, ưu tiên tính nhất quán và serialize các writer hợp tác. Chưa benchmark throughput; writer mới phải giữ cùng lock protocol.
- Drift EF ngoài Inventory được giữ nguyên như mục B; chưa chạy lại mọi workflow ghi của module cũ.
- Suite executable cần chạy riêng; `dotnet test` của solution hiện tại không tự gọi suite này. Cần port 5188 trống và môi trường SQL đúng target. Runtime connection dùng biến môi trường tạm; User Secrets của môi trường thực thi chưa có cấu hình sẵn.
- Không còn lỗi correctness Inventory được phát hiện sau các lần diagnose/fix/retest. Các giới hạn auth và module tích hợp là phạm vi deferred đã duyệt.

## M. Kết luận

READY TO COMMIT: YES
