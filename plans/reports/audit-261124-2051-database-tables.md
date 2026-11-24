# Khảo sát bảng database HotelOps đang chạy

## Kết luận

Database local Production `HotelOpsCompactDB` có **49 bảng nghiệp vụ**, **46 bảng có dữ liệu**, **3 bảng rỗng**. Có thêm **6 view**; view không tính vào số bảng. Cả 49 bảng đều được EF mapping và có tham chiếu trong source Compact đang được host sử dụng. Chưa phát hiện bảng có thể DROP ngay mà vẫn giữ nguyên hệ thống.

Có thể nghiên cứu giảm về **48 bảng**, hoặc **47 bảng** nếu xử lý thêm phần chính sách tồn kho. Đây là phương án thiết kế có điều kiện, chưa phải migration đã được chứng minh an toàn. Không nên lấy số lượng bảng làm mục tiêu chính: lượng dữ liệu hiện nhỏ và việc gộp có thể làm quyền, lịch sử, truy vết vật tư và chống xử lý trùng phức tạp hơn.

## Phạm vi và cách kiểm tra

- Xác nhận runtime Production đang nghe port5769, PID30152; frontend5080 dùng API này. Metadata launcher chỉ tới `HotelOpsCompactDB`.
- Entry point `src/HotelOps.Api/Program.cs` gọi CompactHost; host đăng ký `HotelOpsCompactDbContext`. Không đếm nhầm entity/database của Project1 hoặc database TEST/Develop.
- Đọc `sys.tables`, `sys.columns`, `sys.foreign_keys`, `sys.sql_expression_dependencies`, view/trigger và chạy COUNT_BIG cho từng bảng. Chỉ lấy cấu trúc và số lượng, không trích xuất nội dung cá nhân.
- Đối chiếu toàn bộ DbSet/ToTable trong các partial context với các tệp source Compact ngoài Data/Entities. Tham chiếu source chứng minh bảng còn liên quan tới implementation, **không chứng minh mọi nhánh chức năng đã được người dùng chạy trong Production**. Không đo tần suất truy vấn hay latency.
- Có **205 FK**, **97 CHECK**, **38 trigger**. Không có FK bị disable hoặc untrusted theo metadata thu được.
- Tổng allocation của bảng và index khoảng **13,23 MiB**. Đây không phải kích thước file MDF/LDF hay số liệu benchmark hiệu năng.
- Chỉ khảo sát; không sửa schema/dữ liệu, không cần backup cho các truy vấn đọc. Không build/test ứng dụng vì không sửa source ứng dụng.

## Ba bảng rỗng không đồng nghĩa với bảng thừa

| Bảng | Vai trò còn tồn tại | Đánh giá |
|---|---|---|
| `work_checklist_items` | Lưu bản chụp checklist theo việc; kiểm tra các mục bắt buộc trước hoàn thành; `inspection_findings` có FK tới kết quả này | Không xóa riêng lẻ. Quy trình kiểm tra giấy hiện ít dùng bảng này, nhưng API checklist vẫn tồn tại |
| `attachments` | EvidenceService upload/download/invalidate; WorkService đọc tệp theo công việc; liên kết cả chứng từ/tài sản | Bỏ phần minh chứng ở drawer vệ sinh chưa phải bỏ chức năng tệp toàn hệ thống |
| `request_workflow_lines` | RequestWorkflowService tạo dòng khi yêu cầu bổ sung tiêu hao hoặc bổ sung kho; lưu lượng yêu cầu, lượng duyệt và liên kết dòng chứng từ | Phải giữ cho các workflow đã duyệt; 0 dòng ở Production chỉ phản ánh dữ liệu hiện tại |

Ví dụ source: `WorkService.cs` đọc/tạo/kiểm tra WorkChecklistItems; `EvidenceService.cs` quản lý Attachments; `RequestWorkflowService.Actions.cs` tạo RequestWorkflowLines. Chúng đều là đường nghiệp vụ còn có code xử lý, không phải chỉ có entity bỏ quên.

## Các phương án giảm bảng

| Phương án | Số bảng giảm | Đánh giá |
|---|---:|---|
| Gộp `supply_stock_documents` vào `inventory_documents` | 1 | Ứng viên rõ nhất nếu bắt buộc giảm số bảng; mức rủi ro trung bình |
| Hợp nhất chính sách tồn tối thiểu giữa `inventory_policies` và `supply_configurations` | Có thể 1 | Cần thiết kế lại; rủi ro trung bình–cao, không gộp máy móc |
| Gộp `request_workflows` vào `work_items` | 1 | Làm được về kỹ thuật, nhưng không khuyến nghị ở giai đoạn này |
| Ngừng toàn bộ checklist điện tử và bỏ nhóm 3 bảng checklist | Tối đa 3 | Chỉ khi có quyết định bỏ nghiệp vụ và giữ được lịch sử; chưa đủ căn cứ thực hiện |
| Gộp các bảng event/audit thành một bảng chung | Nhiều | Không khuyến nghị: đổi ràng buộc tham chiếu, chống ghi đè và idempotency |

### 1. Supply stock documents: 49 → 48 có điều kiện

`supply_stock_documents` hiện có 1 dòng, 6 cột: document_id, allocation_id, warehouse_id, purpose, operation_key, request_work_id. DocumentId là khóa tương ứng chứng từ kho; đây là metadata mở rộng cho cấp/hoàn vật tư theo ca.

Có thể đưa các trường chuyên biệt vào `inventory_documents`, dùng CHECK/FK/index có điều kiện để chỉ áp dụng cho chứng từ vật tư ca. Phải giữ purpose, allocation, request và khóa chống cấp trùng. Cần sửa StaffSupplyService, các kiểm tra chứng từ, mapping và trigger `TR_supply_stock_provenance`; kiểm thử cấp đầu ca, cấp thêm, trả cuối ca, retry và đối soát.

Lợi ích là giảm một bảng và bớt phép nối. Đổi lại bảng chứng từ chung có thêm cột nullable và quy tắc riêng. Giữ bảng mở rộng hiện tại vẫn là thiết kế hợp lệ; không có bằng chứng cho thấy việc gộp sẽ cải thiện hiệu năng đáng kể.

### 2. Chính sách tồn kho: có trùng dữ liệu nhưng không trùng hoàn toàn

- `inventory_policies`: 27 dòng; 24 dòng có cấu hình supply hiện hành tương ứng, 3 dòng không có cặp tương ứng theo branch/warehouse/item.
- 6 dòng có TargetQty khác MinimumQty; bảng còn giữ LowStockEpisode và AlertedAt. Không thể chỉ chuyển mỗi MinimumQty rồi bỏ bảng.
- `supply_configurations` có 30 dòng và quản lý phiên bản định mức. Ca đang mở giữ phiên bản đã chụp; không được đổi lịch sử cấu hình để cập nhật trạng thái cảnh báo.
- SupplyConfigurationService hiện đồng bộ MinimumStock sang MinimumQty; LowStockService lại đọc inventory_policies. Đây là nơi nên ưu tiên làm rõ nguồn dữ liệu chính, dù có thể chưa cần giảm số bảng.

Muốn giảm thêm một bảng phải thống nhất nơi lưu minimum/target cho **mọi vật phẩm kho**, xử lý 3 policy chưa có cấu hình tương ứng, bảo toàn cấu hình lịch sử và quyết định chỗ lưu vòng đời cảnh báo. Nếu phải tạo bảng runtime thay thế thì không còn lợi ích giảm số bảng. Vì vậy **47 bảng là phương án nghiên cứu, không phải cam kết**.

### 3. Request workflows và work items

RequestWorkflow dùng chung WorkId nhưng bổ sung loại yêu cầu, hàng đợi vai trò, người xử lý hiện tại, allocation/warehouse/document và correlation. Cả 4 request hiện có đều có status khác với status của WorkItem; hai status đang mô tả hai lớp trạng thái khác nhau, không phải hai bản sao có thể xóa tùy ý.

Gộp sẽ làm WorkItems vốn 37 cột rộng hơn, nhiều trường chỉ dùng cho một số loại công việc; phải giữ hai lớp trạng thái hoặc thiết kế lại state machine. Không khuyến nghị chỉ để tiết kiệm một bảng.

### 4. Checklist điện tử

Ba bảng gồm `checklist_templates` (2 dòng), `checklist_items` (6 dòng), `work_checklist_items` (0 dòng). Hai template hiện đang active, kind INSPECTION; hiện chưa có WorkItem hoặc WorkTemplate gắn ChecklistTemplateId.

Quy trình kiểm tra giấy là bằng chứng nhóm này ít được dùng trong dữ liệu hiện tại, nhưng TemplateService/API tạo và thực thi checklist vẫn tồn tại, cùng trigger và FK liên quan. Chỉ cân nhắc loại bỏ sau khi chốt không dùng checklist điện tử cho kiểm tra/bảo trì/công việc khác, bỏ endpoint/permission tương ứng một cách có chủ đích và lưu được lịch sử. **Không bỏ `inspection_findings`**: PaperInspection đang tạo finding khi kiểm tra FAIL và dùng nó để theo dõi dọn lại.

## Những nhóm nên giữ riêng

- `workday_shifts` và `workday_assignments`: mẫu ca khác với lịch phân ca/chấm công từng nhân viên.
- `inventory_documents`, `inventory_document_lines`, `stock_movements`: chứng từ, dòng hàng và phát sinh kho đã ghi sổ có vai trò khác nhau.
- `staff_supply_allocations`, `staff_supply_balances`, `supply_reservations`, `staff_supply_ledger`: phiên cấp ca, số dư, phần giữ chỗ và lịch sử biến động; cần để tránh âm vật tư/cấp trùng.
- `user_roles`, `role_permissions`, `memberships`: vai trò/quyền và membership chi nhánh/bộ phận không thay thế nhau.
- Event nghiệp vụ và `audit_logs`: event phục vụ trạng thái/retry/lịch sử chuyên biệt; audit ghi thay đổi tổng quát. Gộp cần thiết kế lại FK, uniqueness, trigger bất biến và quyền đọc.
- Bảng warehouses có 3 dòng nhưng chỉ **2 kho active**, 1 kho inactive. Kho cũ giữ lịch sử không phải một bảng thừa. Roles có 11 dòng cũng không đồng nghĩa 11 actor đang hoạt động; không xóa role lịch sử chỉ để làm đẹp số lượng.

## Đề xuất thực hiện

1. Hiện tại giữ 49 bảng; sắp xếp ERD theo nhóm nghiệp vụ để dễ kiểm soát.
2. Nếu mục tiêu là bớt đồng bộ dữ liệu, ưu tiên làm rõ nguồn minimum stock giữa policy và supply configuration.
3. Nếu bắt buộc giảm số bảng, thử phương án supply_stock_documents trên fixture để xuống 48, đo lợi ích và kiểm tra bảo toàn lịch sử trước.
4. Chưa đề xuất ép về 30–35 bảng: phần lớn mức giảm đó sẽ đổi nghiệp vụ hoặc dồn dữ liệu vào bảng rộng/JSON, không tự làm hệ thống đơn giản hơn.

Mọi migration sau này phải backup, chạy fixture, cập nhật context/API/source/trigger và đối soát trước. `SchemaGate.cs` hiện yêu cầu đúng **49 bảng, 205 FK, 97 CHECK, 38 trigger**; xóa một bảng mà không cập nhật schema gate theo phiên bản sẽ khiến API không khởi động.

## Bằng chứng và giới hạn

- `artifacts/database-table-audit/database.json`: metadata, FK, cột, SQL dependency và COUNT_BIG.
- `artifacts/database-table-audit/source.json`: mapping và vị trí source tham chiếu từng bảng.
- `artifacts/database-table-audit/candidates.json`: đối chiếu policy, checklist và trạng thái request.
- Các script khảo sát ở cùng thư mục artifacts; không ghi credential vào output.
- Lần chạy helper Python bị lỗi executable không truy cập được; đã thay bằng Node và hoàn thành đối chiếu 49/49 mapping. Không ảnh hưởng truy vấn database.
- Số dòng là tại thời điểm quét, có thể thay đổi khi ứng dụng tiếp tục hoạt động. Chưa kiểm thử migration hay benchmark vì yêu cầu hiện tại là khảo sát/đánh giá.

## Danh sách đầy đủ

L?n qu?t: 2026-11-24T13:56:41.8288666Z (UTC). T?ng 6531 d?ng tr?n 49 b?ng; ??y l? t?ng d?ng k? thu?t, kh?ng ph?i s? giao d?ch nghi?p v?.

| STT | B?ng | S? d?ng | S? t?p source Compact c? tham chi?u |
|---:|---|---:|---:|
| 1 | `asset_events` | 1 | 2 |
| 2 | `assets` | 6 | 16 |
| 3 | `attachments` | 0 | 2 |
| 4 | `audit_logs` | 732 | 9 |
| 5 | `branches` | 2 | 26 |
| 6 | `checklist_items` | 6 | 3 |
| 7 | `checklist_templates` | 2 | 3 |
| 8 | `departments` | 8 | 14 |
| 9 | `document_events` | 399 | 2 |
| 10 | `inspection_findings` | 1 | 4 |
| 11 | `inventory_document_lines` | 2360 | 15 |
| 12 | `inventory_documents` | 199 | 23 |
| 13 | `inventory_items` | 15 | 17 |
| 14 | `inventory_policies` | 27 | 5 |
| 15 | `locations` | 16 | 8 |
| 16 | `memberships` | 124 | 19 |
| 17 | `notifications` | 54 | 15 |
| 18 | `permissions` | 72 | 23 |
| 19 | `reference_values` | 20 | 18 |
| 20 | `request_workflow_events` | 8 | 2 |
| 21 | `request_workflow_lines` | 0 | 3 |
| 22 | `request_workflows` | 4 | 9 |
| 23 | `role_permissions` | 155 | 12 |
| 24 | `roles` | 11 | 31 |
| 25 | `room_blocks` | 4 | 4 |
| 26 | `room_events` | 132 | 2 |
| 27 | `room_types` | 5 | 7 |
| 28 | `rooms` | 96 | 27 |
| 29 | `staff_supply_allocations` | 1 | 8 |
| 30 | `staff_supply_balances` | 8 | 8 |
| 31 | `staff_supply_ledger` | 72 | 1 |
| 32 | `stock_movements` | 1207 | 13 |
| 33 | `supply_configurations` | 30 | 5 |
| 34 | `supply_reservations` | 32 | 5 |
| 35 | `supply_stock_documents` | 1 | 2 |
| 36 | `user_roles` | 72 | 21 |
| 37 | `user_sessions` | 370 | 4 |
| 38 | `users` | 57 | 26 |
| 39 | `warehouses` | 3 | 17 |
| 40 | `work_assignments` | 8 | 22 |
| 41 | `work_checklist_items` | 0 | 2 |
| 42 | `work_events` | 53 | 17 |
| 43 | `work_intervals` | 7 | 5 |
| 44 | `work_items` | 17 | 40 |
| 45 | `work_links` | 3 | 3 |
| 46 | `work_templates` | 2 | 5 |
| 47 | `workday_assignments` | 38 | 20 |
| 48 | `workday_events` | 51 | 2 |
| 49 | `workday_shifts` | 40 | 18 |
