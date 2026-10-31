# Task 02D — Phương án cuối cùng xử lý dữ liệu legacy để trình duyệt

Ngày: 21/10/2026. Branch: `feature/BE1-authorization`.

**Trạng thái: hoàn tất phân tích; chưa phê duyệt/thực thi phương án xử lý dữ liệu. Department Retirement vẫn BLOCKED.**

## Phạm vi và bằng chứng

- Server đã SELECT xác minh: `DESKTOP-JI8U50T\SQLEXPRESS`.
- Trial: `HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282`.
- Nguồn: `HotelOpsDB`, chỉ đọc.
- Task này không chạy DML, DDL, Migration, Backup/Restore, reset fixture hay transaction thử ghi. Chỉ tạo tài liệu này.
- Đã đọc AGENTS.md; Release README và scripts 01–04; báo cáo/scripts Task02, 02B, 02C; entities và DbContext liên quan; UC-006/026/075/083/084/093/097/098/123/138/139; các business rules và bảng chuyển trạng thái; FE-01 SessionRevocationService, SqlSessionPolicy, AuthenticationAudit.
- Đã SELECT lại dòng cha/con, quantities, FK và liên kết đa hình. Không lưu credentials, session hashes, toàn bộ bản ghi users hoặc thông tin đăng nhập.
- Catalog trial vẫn 12 roles / 10 active / 62 permissions / 140 grants. Các thay đổi Task02C được xác nhận: warehouses 8/9, request types 4/5/9, work categories 2/10/11 Inactive; type 6 còn Active.
- Kiểm tra fingerprint cuối Task02D: cả 72 bảng nguồn và 72 bảng trial khớp số dòng và SHA-256 với baseline sau Task02C. Đây là kiểm chứng dữ liệu; không phải kiểm thử API hay chứng nhận workflow đã triển khai.

### Nguồn tham chiếu dùng trong báo cáo

| Mã | Nguồn trong repository |
| --- | --- |
| S1 | `docs/requirements/HotelOps_SRS_DRAFT.md`: UC-006 khoảng dòng 2192–2277, UC-026 khoảng 3920–3991, UC-075 khoảng 8354–8432 |
| S2 | Cùng SRS: UC-083/084 khoảng 9035/9123; UC-093 khoảng 9889–9980; UC-097/098 khoảng 10235/10316 |
| S3 | Cùng SRS: UC-138/139 khoảng 11650–11829; UC-123 khoảng 12687; GBR-085/091/095–108/113/116/143/154 và bảng state transition cuối tài liệu |
| S4 | `src/HotelOps.Api/Entities/` và `Data/HotelOpsDbContext.cs`: stock/material/request/shift/user/department/audit entities, FK và constraints thực tế từ SELECT |
| S5 | `Services/Authentication/SessionRevocationService.cs`, `SqlSessionPolicy.cs`, `AuthenticationAudit.cs`; FE-01 yêu cầu transaction và user-before-session locking |
| S6 | `scripts/database/release/README.md`, scripts 01–04; `Task02B/department-cleanup.sql` với guard 52005; bằng chứng Task02C |

SRS là bản lịch sử. Những mô hình đã chốt trong yêu cầu hiện tại ưu tiên hơn SRS cũ; không tự coi tên trạng thái SRS là giá trị database tương đương. Các business rules được dùng khi không có yêu cầu mới phủ định chúng. Không chỉnh Permission Matrix.

## A. Root Cause Analysis

### A1. WH-FB: không phải lỗi FK; có ledger và workflow chưa kết thúc

Kho 4 WH-FB, Branch 1, Department FB còn Active. Tồn được tính từ movement đã có:

| Item | Receipt POSTED | Issue POSTED | Tồn ròng |
| --- | ---: | ---: | ---: |
| 14, FB-WATER | +480 | −96 | 384 |
| 15, FB-SODA | +240 | −48 | 192 |

Receipt document 3 và issue document 8 là POSTED. Issue 8 liên kết material request **3**, không phải request 8. Hai stocktakes 3/7 là APPROVED/COMPLETED, các dòng đã đếm có variance 0. Thresholds 13/14 còn bật cảnh báo.

Material requests 8/9 thuộc Department HK (ID 1), requester 6, source warehouse 4; approved quantities 48/24; fulfilled_qty đều 0. Fulfillments 8/9 PREPARING, chưa có stock_document_id, fulfilled_at hoặc received_at; fulfillment lines 9/10 có issued_qty=0, received_qty NULL.

**Phân loại:** seed gắn kho cũ vào nhu cầu của bộ phận còn hợp lệ; phụ thuộc tồn kho/chuỗi chứng từ có thật trong DB; thiếu implementation workflow kho trong backend hiện tại; thiếu quyết định nơi nhận tồn. Không có bằng chứng số tồn tương ứng hàng vật lý ngoài đời.

- Theo UC-093/GBR-113, requester có thể hủy approved material request khi chưa phát sinh xuất. Requests 8/9 thỏa điều kiện quantities đã kiểm tra tại thời điểm SELECT; phải kiểm tra lại dưới lock khi thực thi.
- CHECK fulfillment cho phép CANCELLED nhưng không định nghĩa transaction hủy PREPARING: actor, điều kiện quantity/reservation, note/audit và cập nhật request. **Cần chốt phần này**, không dùng CHECK làm workflow.
- Hoàn thành fulfillment chỉ hợp lệ khi thực sự có nghiệp vụ cấp/nhận tương ứng trong môi trường cần giữ lịch sử: chứng từ, movement, quantities và actor/SoD đầy đủ. Không đặt RECEIVED/COMPLETED để làm sạch.
- UC-083/084 và GBR-103/104 mô tả transfer: cần kho nhận hợp lệ được chọn, nguồn đủ tồn, phát/nhận được xác nhận, quantities in-transit/received/loss truy vết và audit. Không dùng số dư để tự suy ra đã chuyển hàng.
- Adjustment phải có cơ sở kiểm kê/sửa sai theo GBR-107; reversal có điều kiện GBR-108. Không điều chỉnh tồn về 0, reverse receipt có issue phụ thuộc, hoặc ghi nhận mất hàng không có căn cứ.
- Ở trial thuần synthetic có thể tạo test scenario transfer được ghi nhãn fixture nếu được duyệt riêng; điều đó là thử nghiệp vụ, không phải chứng minh chuyển hàng vật lý đã xảy ra. Không có phê duyệt đó trong Task02D.

Kho hợp lệ đang Active tại Branch 1 có WH-MAIN (1/WH), WH-HK (2/HK), WH-MT (3/MT). Đây **chỉ là danh sách ứng viên**, chưa chọn nơi nhận và chưa chứng nhận chúng phù hợp cho items 14/15. WH-DISP (10) không có department_id, cần rà soát riêng nếu muốn dùng; không mặc định chọn nó.

### A2. Request 13: khoảng trống yêu cầu và implementation, được phơi bày bởi seed

Internal request 13 DRAFT, type FB_ORDER 6, requester 23, receiving department FB. Có duy nhất CREATED event 13; không có assignment hoặc feedback trực tiếp.

UC-123/GBR-154 chỉ mô tả PENDING_ACCEPTANCE→CANCELLED. Schema có DRAFT/SUBMITTED/IN_PROGRESS/COMPLETED/CLOSED/REJECTED/CANCELLED, không có PENDING_ACCEPTANCE hoặc ACCEPTED như một số đoạn SRS. Backend chưa có InternalRequest service/controller triển khai state machine này.

**Kết luận:** không phải chỉ thiếu một câu UPDATE. Thiếu ánh xạ trạng thái và quy tắc hủy draft đã duyệt; implementation cũng chưa có. DRAFT→CANCELLED chưa được chứng minh là thao tác hợp lệ. Không đẩy DRAFT qua SUBMITTED chỉ để đến trạng thái được phép hủy.

Nếu giữ lịch sử theo A, cần quyết định mới về quyền hủy draft, actor, điều kiện chưa giao/nhận, reason, updated_at, event và audit; chỉ dùng cột hiện có, không invent cancelled_at. Nếu chọn B, có thể đề nghị archive rồi loại/rebuild **fixture 13 và CREATED event của nó** theo manifest đã duyệt, thay vì mô tả rằng request đã được xử lý nghiệp vụ. Không xóa hàng loạt event/request.

### A3. Shifts 4/5: seed thời gian dang dở; không có bằng chứng kết thúc

- Shift 4 LAU IN_PROGRESS, ngày 16/10/2026, lịch 06:00–14:00. Assignments 7/8 cho users 11/4 CONFIRMED; attendance 7/8 PRESENT, check-in 05:55/06:00, không có check-out.
- Shift 5 LAU PLANNED cùng ngày, lịch 14:00–22:00; không có assignments/attendance.
- UC-139/GBR-143 mô tả Workday WORKING→ENDED khi không còn open work interval. Schema hiện dùng work_shifts/shift_assignments/shift_attendance, không có entity Workday tương ứng hoặc shift handover table; không có service xác nhận ánh xạ này. Cột shift status không có CHECK cũng không tạo quyền dùng một giá trị mới.
- UC-138 cho phép hủy **future assignment**; không chứng minh cách hủy một WorkShift quá khứ hoặc một ca đã check-in. Không tự gán scheduled_end_at hay thời điểm chạy script làm actual_check_out_at.

User 4 hiện DH Branch 1, department_id NULL, còn quản lý legacy LAU qua manager_user_id. DH là vai trò cấp branch, không bắt buộc thuộc một department. Vì vậy, khác biệt này là dấu hiệu fixture/line-management cũ; **chưa phải bằng chứng vi phạm branch authorization**. Giữ User 4 và DH assignment, không chuyển user về LAU hoặc deactivate họ.

### A4. Users: tách thiếu quy tắc thật khỏi trì hoãn bảo thủ ở Task02C

Users 11/17/18/20 vẫn ACTIVE; user 11 còn assignment vào role STAFF Inactive; DH assignments của 17/18/20 đã Inactive. Branch access STANDARD/MANAGER của họ chưa revoked, không tự tạo effective permission nếu user/role không hợp lệ.

User 11 có phụ thuộc hoạt động cụ thể là shift 4. Với 17/18/20, SELECT FK ở Task02C không tìm thấy công việc được giao trực tiếp chưa hoàn thành; họ còn manager_user_id của các department 5/6/10. Warehouse 8 do user 20 quản lý đã Inactive.

**Điều chỉnh đánh giá trước:** manager_user_id là FK, không phải constraint yêu cầu manager luôn Active. UC-006 không định nghĩa thêm điều kiện cấm deactivate chỉ vì FK này tồn tại. Không cần một business rule mới để cho phép giữ FK lịch sử trên department đã Inactive. Có thể xử lý account retirement và department retirement nguyên tử; nếu chưa thể retire department, cần quyết định giữ hay bỏ liên kết quản lý hiện hành, không tự reassign.

Đường Active→Inactive đã có UC-006; điều kiện cần là không bỏ dở assigned work, không vô hiệu hóa last administrator/own acting administrator, giữ lịch sử, audit và revoke sessions. Không cần reset user/password. Khi thi hành, khóa user trước session; account-status update, revoke tất cả sessions có revoked_at NULL và audit nằm trong cùng transaction. User 11 có 1 session chưa revoked; 17/18/20 không có session tại lần SELECT. Không coi chưa idle-expired là chứng minh session hợp lệ đầy đủ; phải kiểm tra lại khi chạy.

## B. Resolution Matrix trình duyệt

“Duyệt batch” là phê duyệt thực thi kế hoạch sau này; Task02D không cấp lệnh ghi. “Quy tắc mới” là phần chưa thể suy ra từ nguồn hiện có.

| Đối tượng / ID | Hiện tại | Đích đề xuất | Thao tác, căn cứ và dữ liệu liên quan | Duyệt cần có |
| --- | --- | --- | --- | --- |
| Warehouse 4 WH-FB | Active; 384/192 tồn | Inactive, zero balance, không workflow mở | A: transfer được xác nhận theo UC-083/084 sau xử lý nhu cầu mở; B: rebuild component synthetic đã duyệt, không fake transfer. Liên quan ledger, docs, requests, fulfillment, stocktake, threshold | Chọn A/B, chứng cứ vật lý hoặc xác nhận synthetic; kho nhận chỉ khi A |
| Material request 8 | APPROVED; 48 item 14; fulfilled 0 | CANCELLED nếu nhu cầu hủy; hoặc fixture hợp lệ nếu nhu cầu giữ | UC-093/GBR-113: requester 6, reason/audit, kiểm tra chưa issued; preserve approval 9 và line 9 nếu theo A | Quyết định giữ nhu cầu hay hủy; duyệt batch |
| Material request 9 | APPROVED; 24 item 15; fulfilled 0 | Tương tự request 8 | Preserve approval 10, line 10; không dùng quyền Storekeeper để giả làm requester | Như trên |
| Fulfillment 8 | PREPARING, issued 0, không stock doc | CANCELLED theo quy tắc được chốt hoặc rebuild synthetic | Line 9; chốt hủy preparation/request cùng transaction, không post hoặc nhận giả | Quy tắc hủy PREPARING + audit/actor hoặc manifest B |
| Fulfillment 9 | PREPARING, issued 0, không stock doc | Tương tự fulfillment 8 | Line 10; không thay đổi completed fulfillment 3 để hủy 8/9 | Như trên |
| Docs 3/8, movements 3/4/10/11 | POSTED receipt/issue | A: giữ nguyên; B: chỉ rebuild nếu xác nhận synthetic từng khóa | Doc 8 gắn request 3/fulfillment 3. Không xóa ledger đơn lẻ, không zero số dư trực tiếp | Nếu B, phê duyệt loại/rebuild posted-fixture component đầy đủ |
| Stocktakes 3/7; thresholds 13/14 | APPROVED/COMPLETED; alert bật | A: giữ kiểm kê lịch sử, tắt alert khi kho retire; B: duyệt fixture closure | Stocktake lines 4/5/9; không tạo adjustment cho variance 0 | Duyệt closure/tắt alert; không cần invent giao dịch |
| Warehouses 8/9 | Inactive sau 02C | Giữ Inactive | Không reset lại hoặc đưa chúng thành kho nhận ngầm | Không thêm ngoài duyệt kế hoạch |
| Internal request 13 | DRAFT; event 13 CREATED | A: trạng thái kết thúc sau rule mới; B: archive/rebuild fixture | UC-123 hiện chưa hỗ trợ draft; giữ requester 23 và không giả status chain | Duyệt rule draft-cancel hoặc archive/rebuild theo manifest |
| Shift 4 | IN_PROGRESS; 2 check-ins thiếu check-out | A: kết thúc đúng evidence; B: archive/rebuild fixture sai | Assignments 7/8, attendance 7/8; không dùng scheduled time làm actual | Chứng cứ thời gian + workday mapping hoặc duyệt B |
| Shift 5 | PLANNED quá khứ, không assignment | A: hủy sau rule mới; B: archive/rebuild fixture | UC-138 không mô tả trường hợp này; không invent status | Duyệt shift-cancel hoặc B |
| User 11 | ACTIVE LAU; STAFF Inactive; ca 4 mở | Account INACTIVE, credential giữ nguyên | Sau xử lý ca: revoke toàn bộ sessions, audit; giữ user và FK lịch sử; đánh dấu assignment STAFF inactive/revoke branch access nếu batch được duyệt | Duyệt batch account/access, phụ thuộc shift phải hết |
| User 17 | ACTIVE SEC B1; DH assignment inactive | Account INACTIVE | UC-006; giữ references report/request 5 CLOSED, department 5 manager link làm lịch sử sau retirement | Duyệt batch; không cần rule status mới |
| User 18 | ACTIVE FB; DH assignment inactive | Account INACTIVE | UC-006; manager link department 6 xử lý cùng retirement; không tự chuyển FO/WH | Duyệt batch |
| User 20 | ACTIVE SEC B2; DH assignment inactive | Account INACTIVE | UC-006; kho 8 đã inactive; manager link department 10 giữ lịch sử sau retirement | Duyệt batch |
| User 4 | Active DH B1, department NULL | Giữ user, credential và DH assignment | Chỉ resolve erroneous shift fixture/link lịch sử; không deactivate DH | Không thay phân công đã chốt |
| Users 1/5/19 | OM B1 / Storekeeper B1 / DH B2 | Giữ nguyên | Không đưa vào reset account hay assignment | Protected |
| Request types 4/5/9 | Inactive | Giữ Inactive | Tham chiếu lịch sử không chuyển department | Giữ kết quả 02C |
| Request type 6 FB_ORDER | Active, request 13 mở | Inactive | Sau xử lý request 13; giữ type ID/code làm lịch sử | Duyệt batch |
| Work categories 2/10/11 | Inactive | Giữ Inactive | Task 14 operational COMPLETED giữ nguyên | Giữ kết quả 02C |
| Departments 3/5/6/10 | Active LAU/SEC/FB/SEC | Inactive | Sau zero blockers: update đúng bốn flags; history/manager FKs giữ nguyên nếu batch chốt như vậy; audit | Duyệt kế hoạch cuối, guard phải đạt |

### Phạm vi bảng nếu xem xét B — không phải danh sách được phép xóa

**Bảo vệ mặc định, không reset:** schema/constraints/indexes; roles/permissions/role_permissions; branches; users (đặc biệt password hash, temporary credential flags, lockout); user_roles/user_branch_access ngoài changeset retirement được duyệt; user_sessions ngoài revocation đúng policy; audit_logs cũ; toàn bộ dữ liệu không thuộc closure; items/UoM/conversions/master data chưa được duyệt thay. Không truncate/reseed identity.

**Có thể đề nghị tái tạo theo từng khóa và closure được duyệt:**

1. Request 13 component: internal_requests 13 và internal_request_events 13. Assignment/feedback trực tiếp hiện 0. Kiểm tra lại mọi attachment/notification/source link đúng **type + ID**, không xóa theo số 13 đơn lẻ.
2. Shift component: work_shifts 4/5; shift_assignments 7/8; shift_attendance 7/8. Giữ users 4/11 và shift_templates. Chỉ gỡ fixture con khi phê duyệt archive/rebuild, không sửa lịch sử thành “đã kết thúc”.
3. WH-FB component tối thiểu đã thấy: stock_documents 3/8; stock_document_lines 3/4/9/10; stock_movements 3/4/10/11; stocktakes 3/7, stocktake_lines 4/5/9; stock_thresholds 13/14; material_fulfillments 3/8/9, fulfillment_lines 3/4/9/10; material_requests 3/8/9, request_lines 3/4/9/10, approvals 3/9/10. Đây là **phạm vi đánh giá ảnh hưởng**, không mặc định xóa mọi dòng trong danh sách. Request 3 và fulfillment 3 đã hoàn tất, là lịch sử cần giữ mặc định; đưa chúng vào rebuild cần phê duyệt riêng vì liên kết doc 8.
4. warehouses 4, departments 3/5/6/10 và các danh mục giữ ID, chuyển Inactive sau khi các component được xử lý. Không bắt buộc xóa bản ghi master.

Toàn bộ FK trực tiếp được kiểm tra trong những component này dùng NO_ACTION. Phải child-before-parent khi loại fixture đã duyệt và parent-before-child khi tạo fixture mới; material_fulfillments.stock_document_id, stocktakes.adjustment_document_id, reversal_of_document_id cần xử lý theo đồ thị thực tế. Không tắt FK để “reset cho nhanh”. SELECT hiện không thấy maintenance_work_logs trỏ docs 3/8; vẫn phải recheck khi thực thi.

Ngoài FK còn có stock_documents.reference_type/reference_id, operational_tasks.source_type/source_id, attachments.entity_type/entity_id, notifications.source_type/source_id, audit_logs.entity_type/entity_id. Có notification MATERIAL_REQUEST 13, **không phải INTERNAL_REQUEST 13**; attachments trùng số ID thuộc CLEANING_TASK/INCIDENT/INSPECTION/MAINTENANCE/ROOM, không thuộc component đang dọn. Phải giữ chúng. Reference PURCHASE_ORDER 1003 của receipt 3 cũng cần xác minh nguồn fixture, không tự nhận là FK có thể xóa.

Với B, mỗi row cần manifest: table + primary key + business code/branch + expected hash + provenance synthetic + reason + external references + archive location + approved disposition + old→new mapping. Nếu không phân biệt được row mẫu với dữ liệu cần DEV1/DEV2 giữ, dừng phần đó. Không dùng DROP/TRUNCATE hoặc DELETE toàn bảng; việc loại row theo manifest cũng chỉ được làm sau phê duyệt riêng.

## C. So sánh hai phương án

| Tiêu chí | A — Controlled Data Retirement | B — Controlled Seed/Test Reinitialization có giới hạn |
| --- | --- | --- |
| An toàn/history | Giữ ledger/history trong DB; cần bằng chứng nghiệp vụ để hoàn tất/correct | Chỉ giữ fixture đúng; archive và rebuild component sai. Có thể mất lịch sử synthetic trong working DB nên cần duyệt riêng và bản sao khôi phục đầy đủ |
| Khối lượng | Ít row hơn nhưng phải bổ sung nhiều workflow/mapping chưa có và kiểm thử SoD | Closure hàng chục row, thiết kế fixture và kiểm thử cross-module; không reset cả DB |
| Authentication | Deactivate sample accounts + revoke sessions đúng FE-01; không reset password | Vẫn bảo vệ users/password/lockout; không tái tạo account/session tùy tiện; chỉ revoke trong retirement đã duyệt |
| RBAC | Giữ exact catalog và phân công đã chốt | Giữ nguyên tương tự; không chạy seed catalog lại |
| Tính trung thực | Không thể tạo true check-out/stock-transfer history khi thiếu evidence | Phù hợp dữ liệu synthetic thiếu evidence: ghi rõ fixture thay thế, không tuyên bố sự kiện chưa xảy ra |
| Main HotelOpsDB | Phù hợp nhất với dữ liệu nghiệp vụ cần bảo toàn; vẫn cần migration approval riêng | Chỉ tái sử dụng khi chứng minh từng component trên main là synthetic và DEV1/DEV2 đồng ý. Không coi B là migration production tổng quát |
| Kiểm thử | Test transition/actor/SoD/quantity/idempotency, workflow gaps phải đóng | Test fixture provenance/closure/reference mappings, protected hashes, ledger quantities và cả positive/negative API cases |
| Rollback | Transaction rollback trước COMMIT; sau COMMIT cần compensating action hợp lệ hoặc phục hồi bản sao được duyệt | Backup current state + archive closure + replay manifest có kiểm chứng; không restore đè database đang có thay đổi khác |

**Ưu tiên đề xuất: B có giới hạn cho chính các component lỗi thời của trial, sau phê duyệt manifest và provenance.** Dữ liệu được mô tả chủ yếu là seed/test; ca thiếu actual end và request draft không có workflow chuẩn không thể được làm “hoàn thành thật” bằng suy đoán. Chuẩn hóa fixture sẽ tạo nền test đúng mô hình hơn việc viết thêm giả định chỉ để đóng seed cũ.

Đây là lựa chọn có điều kiện, không phải quyền reset vừa được cấp. Không rebuild component stock đã POSTED nếu nhóm chưa chấp nhận loại synthetic history đó khỏi working trial. Nếu bất kỳ row cần giữ như nghiệp vụ thật hoặc DEV2 cần giữ lịch sử hiện hữu, chuyển component đó sang A; thiếu chứng cứ thời gian/physical transfer thì A vẫn BLOCKED. Không hợp thức hóa dữ liệu giả bằng cách gọi thao tác là migration.

## D. Execution Plan và các quyết định phải duyệt trước

### D1. Hồ sơ quyết định cụ thể

| Quyết định | Nội dung cần người phụ trách chốt |
| --- | --- |
| D01 — Provenance | Toàn bộ request13, shift4/5 và WH-FB component nào là synthetic được phép archive/rebuild; row lịch sử nào DEV1/DEV2 bắt buộc giữ |
| D02 — Inventory | Chọn A hoặc B cho stock. Nếu A, chọn kho nhận hợp lệ, số lượng/đơn vị, chứng cứ xác nhận hoặc phê duyệt rõ test scenario; không mặc định WH-MAIN |
| D03 — Nhu cầu 8/9 | Hủy nhu cầu chưa issued theo requester, hay giữ để tạo fixture fulfillment hợp lệ tại kho được duyệt; chốt hủy PREPARING và actor/audit |
| D04 — Request13 | Chọn archive/rebuild fixture; hoặc phê duyệt rule draft cancel đầy đủ. Không duyệt chỉ một câu UPDATE |
| D05 — Shifts | Chọn archive/rebuild fixtures sai; hoặc cung cấp evidence actual-end và rule Workday→schema/WorkShift cancellation. Không dùng scheduled time thay actual |
| D06 — Accounts/history | Duyệt retirement users11/17/18/20, session revocation, disposition của inactive-role assignment và branch access; giữ manager FKs trên retired departments làm lịch sử hoặc clear trong batch audit rõ ràng. Giữ users1/4/5/19 và credential nguyên vẹn |
| D07 — Fixture thiết kế mới | Chọn scenarios, branch/department/warehouse mappings và users được giữ; số lượng/expected outcomes; chủ sở hữu BE1/BE2 và kiểm thử cần giữ |

Không có quyết định nào trong bảng được tự coi đã được Task02D phê duyệt. Với D06, Active→Inactive/session revocation là workflow đã có căn cứ; chỉ cần duyệt changeset và thứ tự thực thi, không cần invent trạng thái mới.

### D2. Thứ tự thực thi sau khi được duyệt

1. **Chốt D01–D07, review BE1/BE2.** Lưu exact row manifest, expected state/hashes và protected list. Không viết changeset destructively trước khi biết phạm vi được duyệt.
2. **Preflight chỉ đọc lại.** Exact server/DB guard, row versions/fingerprints, FK và polymorphic references, stock sums/approvals/zero-issued, assigned work/attendance, current sessions, no unreviewed triggers. Có thay đổi ngoài manifest thì dừng, không overwrite baseline để ép khớp.
3. **Recovery point mới cho trial hiện tại.** COPY_ONLY/CHECKSUM, tên file mới ngoài Git, VERIFYONLY và quy trình restore rehearsal vào DB tên mới nếu được phép. Backup Task02C là trước tám thay đổi, không thay thế checkpoint mới. Không tự restore đè trial/main.
4. **Archive components được duyệt.** Lưu ngoài Git có kiểm soát quyền; giữ bản đầy đủ để khôi phục FK và bằng chứng. Audit cũ không xóa; liên kết đa hình chỉ sửa theo type+ID manifest. Tài liệu Git chỉ chứa metadata không nhạy cảm.
5. **Xử lý inventory/request components.** A: actor/SoD + quantities/reason đã duyệt, hủy zero-issued demand/preparation hoặc hoàn thành thực tế; transfer có dispatch/receipt không giả; zero stock trước retire WH-FB; tắt thresholds/alerts cho kho retired. B: áp dụng child/parent order theo manifest đóng, tái tạo canonical fixture với expected stock arithmetic, không “nhận hàng” hay “đã giao” giả vào old history. Preserve request3/completed history nếu không được duyệt thay.
6. **Xử lý request13/shifts.** Chỉ theo D04/D05. B archive/rebuild không có yêu cầu tạo check-out cho dữ liệu cũ; A phải có evidence và rule đã duyệt. Không disable User4 hoặc thay DH assignment. Dừng nếu còn open work interval hoặc liên kết ra ngoài phạm vi.
7. **Retirement nguyên tử phần còn lại.** Lock user trước session; deactivate sample accounts khi đã hết work; revoke tất cả unrevoked sessions và audit, không chạm credentials; xử lý assignment/branch-access đúng D06; type6 inactive; WH-FB inactive khi đủ điều kiện; cập nhật bốn department flags cuối cùng, audit cùng transaction. Giữ manager links cho history chỉ khi department cũng đã Inactive theo D06. Nếu không thể đạt postconditions, rollback toàn retirement batch.
8. **Dry Run và ROLLBACK.** Guard không đổi, row counts/hashes đúng manifest, ledger arithmetic và negative stock checks, audit scope, no credential changes. Rollback có thể để lại identity gaps; không reseed để che giấu chúng.
9. **Review kết quả cụ thể rồi COMMIT trên trial.** Không có bước COMMIT trong Task02D. Apply phải chống replay bằng baseline và manifest, không xóa để chạy lại. Các workflow nhiều bước có ledger đã post cần checkpoint/compensation rõ; không tuyên bố một transaction bao trùm được sự kiện vật lý.
10. **Verify sau COMMIT.** Exact Release catalog hai chiều; unchanged OM/DH/Storekeeper; only HK/MT/WH/FO active; no active users/warehouses/catalog on retired departments; no open requests/shifts/tasks/assets responsibility; stocks/history theo disposition; sessions revoked của disabled users; DBCC CHECKDB/CHECKCONSTRAINTS, FK/unique indexes; protected data and source/main hashes. Kiểm thử API chỉ khi endpoints thực sự có, không gọi script verification là API test.

### D3. Điều kiện để SQL 52005 cho phép retirement

Theo script Task02B hiện tại: active users=0; active warehouses=0; open internal requests=0; open operational tasks=0; active/planned shifts=0; active request types=0; active work categories=0 cho legacy departments. Hiện lần lượt còn **4 / 1 / 1 / 0 / 2 / 1 / 0**.

Không sửa bỏ hoặc bypass guard. Có thể chuẩn bị thay đổi ở transaction rồi chạy cùng điều kiện preflight trước department UPDATE; nếu thất bại thì rollback changeset. Chạy nguyên script có @@TRANCOUNT guard cần ngoài transaction như hiện tại; không lồng thẳng script đó vào transaction khác hoặc bỏ guard để chạy được.

52005 là một gate, không phải toàn bộ UC-026. Phải kiểm tra thêm assets in custody, warehouse balances/open supply workflows, user-owned work và trạng thái không được script nhận diện. Những kiểm tra bổ sung độc lập không làm yếu guard. Nếu sau archive/rebuild vẫn còn một blocker, chưa được retire bất kỳ bộ phận để tuyên bố đạt toàn mô hình.

## E. Release Readiness

Chưa sẵn sàng áp dụng cleanup trên HotelOpsDB. Bộ Release hiện không chứa retirement dependency đầy đủ. Đề nghị bổ sung sau review (tên chỉ là đề xuất, chưa tạo/chạy):

| Artifact cần bổ sung | Phạm vi / giới hạn |
| --- | --- |
| Read-only legacy preflight | Đọc target được chỉ định; output closure, stock, work, effective role/user state; fail với unknown refs |
| Approved retirement manifest | Mapping theo business code + branch và exact reviewed keys; không có IDs nhân sự mẫu hard-code trong migration dùng chung |
| Inventory dependency resolution | BE2 review, valid cancel/preparation/transfer/receipt/stock guard/audit; không tự chọn warehouse hoặc zero ledger |
| Request/shift approved workflow mapping | BE1 review; chốt draft cancellation, Workday/Shift/Attendance mapping. Nếu B thì separate development-only fixture rebuild, không đưa reset vào migration nghiệp vụ dùng chung |
| Account retirement integration | FE-01 locking/session revocation/audit; preserve credentials và fixed personnel; không seed users/passwords lại |
| Department retirement + postflight | Sau dependency resolution và catalog normalization; zero blockers, exact flags, audit, Release04 read-only + checks bổ sung |
| Development fixture validation | Reject FB/LAU/SEC active defaults; positive/negative scenarios HK/MT/WH/FO; protected authentication/RBAC và dependency manifest tests |

Thứ tự tương lai: backup/rehearsal → catalog/personnel normalization đã duyệt → dependency resolution được duyệt → retirement → Release04/expanded verification. Nếu target đã RBAC thì không chạy Upgrade02 lại để xử lý department. Main cần kế hoạch được duyệt riêng dựa trên dữ liệu main thực tế; không copy trial IDs hoặc tự chạy B lên main.

## F. Final Conclusion

1. **Không thể chứng minh xử lý toàn bộ bằng workflow hiện có.** Material request zero-issued cancellation có căn cứ; fulfillment preparation cancellation, internal draft cancellation, mapping WorkShift/Workday và kết thúc ca thiếu thời gian chưa đầy đủ; backend business modules này cũng chưa triển khai.
2. **Cần duyệt quyết định D01–D07.** Không có thiếu business rule chung cấm inactive manager FK: điểm đó được điều chỉnh khỏi đánh giá bảo thủ trước. Cần giải quyết nghiệp vụ/fixture thực sự trước khi deactivate accounts/departments.
3. **Nên chuẩn hóa seed/test theo B có giới hạn** để đạt đúng mô hình mà không tạo sự kiện giả, chỉ khi synthetic provenance và manifest được duyệt. B không phải quyền xóa hàng loạt, reset passwords, chạy lại RBAC seed hoặc bỏ FK. Dữ liệu phải giữ thật thì dùng A và chờ evidence/workflow đầy đủ.
4. Hoàn tất retirement chỉ khi zero blockers, bốn legacy departments Inactive, tám canonical records Active, no orphan/active responsibility, exact RBAC/fixed personnel, authentication safety và source/main unchanged.
5. **Task02D hoàn tất tài liệu trình duyệt; database retirement vẫn BLOCKED chờ các quyết định trên. Không thay đổi nguồn hoặc trial.**

### Kiểm chứng và Git

Chỉ tạo `scripts/verification/RbacUpgradeTrial/Task02D/FINAL_RESOLUTION_PLAN.md`. Không chỉnh scripts/Release chuẩn, C#, EF entities/DbContext, CurrentUserAccessor hoặc connection strings. Không commit/push/merge/PR. Không chạy build/test vì không có thay đổi code; `git diff --check` được dùng để kiểm tra tài liệu. Hai lỗi công cụ SELECT/PowerShell trong quá trình phân tích (tên cột log_id và cách đếm thuộc tính JSON) đã sửa và chạy lại thành công; không có write hay thay đổi database do những lần đó.
