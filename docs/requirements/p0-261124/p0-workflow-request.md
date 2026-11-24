# HOTELOPS PROJECT 2 — P0 MAJOR BUSINESS WORKFLOW RESTRUCTURING

## ROLE CONSOLIDATION, HOUSEKEEPING SUPPLY MANAGEMENT, INCIDENT REMEDIATION & UNIFIED INTERNAL REQUEST

## 1. VAI TRÒ VÀ NHIỆM VỤ

Bạn là Principal Software Architect, Senior Fullstack Engineer, Database Engineer và Business Analyst chịu trách nhiệm tái cấu trúc HotelOps Project 2.

Công nghệ hiện hành:

- Frontend: React / TypeScript.
- Backend: ASP.NET Core.
- Database: SQL Server.
- Database đích: `HotelOpsCompactDB`.
- Project: HotelOps Project 2 — Legacy UX Redesign / CompactDB.
- Chi nhánh: DVBV và DVCT.

Project Owner đã đưa ra thiết kế nghiệp vụ mới nhằm tinh gọn bộ máy quản lý, tự động hóa quản lý đồ fill cho nhân viên vệ sinh, đơn giản hóa Maintenance và thống nhất toàn bộ yêu cầu nội bộ.

Đây là nhiệm vụ thay đổi nghiệp vụ tổng thể, không phải chỉ sửa UI.

Phải thực hiện đồng bộ:

- Database Schema.
- Database Migration.
- Data Migration.
- Backend Domain.
- Services.
- API.
- RBAC.
- Frontend.
- Routes.
- Notification.
- Audit.
- Transaction.
- Concurrency.
- Business Workflow.
- Automated Tests.
- End-to-End Tests.
- SRS và Use Case documentation.

Không dừng lại ở việc phân tích hoặc đề xuất thiết kế.

Các quyết định nghiệp vụ trong prompt này thay thế các chỉ đạo cũ tại những phần có xung đột.

---

## 2. SOURCE AUDIT VÀ DATABASE PREFLIGHT

Trước khi triển khai, kiểm tra toàn bộ source hiện tại.

Xác định:

1. Các role hiện có.
2. Role definitions.
3. Permission definitions.
4. Role-permission mappings.
5. User-role assignments.
6. Branch và Department Membership.
7. Workday.
8. Cleaning Task.
9. Inspection và Rework.
10. Room Status.
11. Room Block.
12. Incident.
13. Maintenance.
14. Inventory.
15. Stock Transactions.
16. Material Request.
17. Replenishment.
18. Internal Request.
19. Notification.
20. Audit.
21. Các bảng, endpoint và test đang phụ thuộc vào role cũ.

Tạo báo cáo impact analysis:

`MODULE | CURRENT CONTRACT | NEW CONTRACT | DATABASE IMPACT | API IMPACT | UI IMPACT | MIGRATION | TEST`

Xác minh đúng HotelOps Project 2.

Không tác động HotelOps Project 1/Rebuild.

Tạo backup database trước khi migration.

Kiểm tra khả năng restore backup.

Thực hiện migration và regression tests trên database clone/fixture độc lập trước khi áp dụng vào database test chính.

Không áp dụng migration vào môi trường production.

---

## 3. TÁI CẤU TRÚC ROLE

### 3.1. Danh sách Actor mục tiêu

Sau thay đổi chỉ còn 8 role nghiệp vụ active:

1. `SYS_ADMIN`.
2. `OWNER`.
3. `OPERATION_MANAGER`.
4. `HOUSEKEEPING_SUPERVISOR`.
5. `HK_STAFF`.
6. `MAINTENANCE_SUPERVISOR`.
7. `RECEPTION`.
8. `STOREKEEPER`.

Các role cũ có thể tồn tại ở trạng thái inactive để bảo toàn dữ liệu lịch sử và FK.

Không hard-delete role nếu đang được dữ liệu lịch sử tham chiếu.

### 3.2. Gộp Department Head vào Operations Manager

Gộp:

`DEPT_MANAGER` → `OPERATION_MANAGER`.

Operations Manager tiếp quản:

- Quản lý lịch tuần.
- Phân công ca làm việc.
- Quản lý nhân sự phục vụ phân ca.
- Quản lý Operational Tasks.
- Giám sát hoạt động khách sạn.
- Theo dõi tình trạng phòng.
- Giám sát Housekeeping.
- Giám sát Maintenance.
- Quản lý tài sản.
- Quản lý Room Block theo workflow.
- Xem báo cáo.
- Quản lý cấu hình đồ fill.
- Quản lý định mức tồn kho.
- Xử lý yêu cầu bổ sung hàng cho kho.

Operations Manager không còn thực hiện phê duyệt trung gian đối với Internal Requests giữa các bộ phận.

Các yêu cầu giữa HK Staff, HK Supervisor, Storekeeper và Maintenance Supervisor phải được chuyển trực tiếp theo workflow.

OM chỉ được xem:

- Nội dung yêu cầu liên bộ phận.
- Người gửi.
- Người nhận.
- Trạng thái.
- Tiến độ.
- Lịch sử.
- Các yêu cầu quá hạn.
- Lý do từ chối.

Không được tự approve, reject, assign, resolve hoặc close các yêu cầu liên bộ phận thông thường chỉ vì có role OM.

Ngoại lệ được Owner phê duyệt:

Yêu cầu bổ sung hàng cho kho do Storekeeper gửi đến OM được xử lý bởi OM theo workflow riêng.

Không gộp permission bằng phép UNION một cách máy móc.

Phải lập ma trận permission mục tiêu và loại bỏ các quyền xử lý Internal Request không còn phù hợp.

Đối với tài khoản Department Head hiện có:

- Chuyển sang OPERATION_MANAGER.
- Giữ User ID.
- Giữ username, mật khẩu và thông tin cá nhân.
- Giữ chi nhánh.
- Thu hồi role assignment DEPT_MANAGER.
- Bảo toàn lịch sử.

Nếu một chi nhánh có nhiều OM sau migration, giữ các tài khoản hợp lệ, không tự xóa hoặc vô hiệu hóa nhân viên để ép số lượng về một.

### 3.3. Tái cấu trúc Maintenance

Gộp mô hình quản lý bảo trì thành một role:

`MAINTENANCE_SUPERVISOR`.

Chuyển:

`MAINTENANCE_LEAD` → `MAINTENANCE_SUPERVISOR`.

Maintenance Supervisor chịu trách nhiệm:

- Tiếp nhận yêu cầu khắc phục sự cố từ HK Supervisor.
- Kiểm tra thông tin sự cố.
- Liên hệ dịch vụ sửa chữa bên ngoài.
- Giám sát quá trình sửa chữa.
- Ghi nhận kết quả sửa chữa.
- Xác nhận đã hoàn thành công việc bảo trì.
- Tiếp nhận yêu cầu sửa lại.
- Theo dõi lịch sử bảo trì.

Không quản lý nhân viên kỹ thuật nội bộ nữa.

Không xây dựng module nhà cung cấp dịch vụ sửa chữa.

Không tạo account cho kỹ thuật viên bên ngoài.

Không quản lý hợp đồng, hóa đơn hoặc chi phí sửa chữa bên ngoài trong phạm vi này.

Việc gọi đơn vị sửa chữa được thực hiện ngoài hệ thống.

### 3.4. Chuyển Maintenance Staff sang HK Staff

Chuyển toàn bộ tài khoản có role:

`MAINTENANCE_STAFF` → `HK_STAFF`.

Thực hiện:

- Giữ User ID.
- Giữ thông tin cá nhân.
- Giữ mật khẩu.
- Giữ account status.
- Giữ chi nhánh.
- Thu hồi toàn bộ role assignment Maintenance Staff.
- Gán role HK Staff.
- Cập nhật Employment Department từ ENG sang HK khi phù hợp.
- Thu hồi quyền kỹ thuật không còn hợp lệ.
- Kiểm tra quyền hiệu lực từ các assignment khác.
- Cập nhật các lịch làm việc tương lai bị ảnh hưởng theo quy tắc an toàn.

Không thay đổi lịch làm việc trong quá khứ.

Không xóa Maintenance History.

Không chuyển Maintenance Tasks đang mở thành Cleaning Tasks.

Đối với Maintenance Tasks đang mở:

- Kiểm tra trạng thái hiện tại.
- Chuyển trách nhiệm quản lý sang Maintenance Supervisor bằng migration có audit khi contract cho phép.
- Nếu không thể ánh xạ an toàn, ghi nhận danh sách cần xử lý riêng.
- Không để công việc đang mở không có người chịu trách nhiệm.

### 3.5. Xử lý role cũ

Sau migration:

- Không còn active assignment DEPT_MANAGER.
- Không còn active assignment MAINTENANCE_LEAD.
- Không còn active assignment MAINTENANCE_STAFF.
- Không còn khả năng đăng nhập bằng quyền hiệu lực của role cũ.
- Role cũ có thể giữ inactive vì mục đích lịch sử.
- Refresh hoặc revoke session/token bị ảnh hưởng.
- Không để JWT/session cũ tiếp tục sử dụng quyền đã thu hồi.

---

## 4. PHÂN LOẠI VẬT TƯ VÀ TÀI SẢN

Thiết kế mô hình phân loại rõ ràng.

### GROUP A — ROOM FILL SUPPLY

Đồ được cấp cho HK Staff đầu ca để bổ sung cho phòng.

Danh mục mặc định:

| Vật tư | Cấp đầu ca | Tồn kho tối thiểu | Định mức theo khách |
|---|---:|---:|---:|
| Bàn chải đánh răng | 10 | 50 | 1 |
| Kem đánh răng | 10 | 50 | 1 |
| Dầu gội | 10 | 50 | 1 |
| Sữa tắm | 10 | 50 | 1 |
| Nước đóng chai | 10 | 50 | 1 |
| Giấy vệ sinh | 10 | 50 | 1 |
| Khăn tắm | 10 | 50 | 1 |
| Khăn mặt | 10 | 50 | 1 |

Sử dụng đơn vị tính thực tế của danh mục vật tư.

Các giá trị trên là cấu hình mặc định, không hardcode trong service hoặc frontend.

Đối với khăn tắm và khăn mặt, phân biệt vật tư được cấp vào phòng với vật tư mất/hỏng. Không mặc định ghi nhận toàn bộ khăn đã cấp vào phòng là tài sản bị tiêu hủy.

### GROUP B — STOCKED DURABLE ROOM ITEMS

Vật dụng lâu bền có thể được lưu trữ tại kho để bổ sung cho phòng.

Bao gồm:

- Ấm đun nước.
- Máy sấy tóc.
- Đèn các loại.
- Móc treo quần áo.
- Thùng rác.
- Dép đi trong phòng.
- Gối.
- Ga giường.
- Chăn.

Không tự động cấp nhóm này khi HK Staff bắt đầu ca.

Không trừ nhóm này theo sức chứa khách khi dọn phòng bình thường.

Chỉ thực hiện cấp phát khi có nghiệp vụ bổ sung/thay thế hợp lệ.

### GROUP C — EXTERNAL REPAIR ASSETS

Tài sản hoặc thiết bị không lưu kho để cấp thay thế trực tiếp theo luồng đồ fill.

Bao gồm:

- Nội thất.
- Điều hòa.
- Tivi.
- Bình nóng lạnh.
- Thiết bị an ninh.
- Thiết bị báo cháy.
- Các tài sản cần dịch vụ sửa chữa bên ngoài.

Khi hỏng phải tạo Incident và chuyển đến Maintenance Supervisor sau khi HK Supervisor xác nhận.

Không tự động tạo Stock Issue đối với tài sản nhóm C.

### Quy tắc phân loại

- Cho phép cấu hình phân loại mặt hàng và thiết bị phù hợp thực tế.
- Không phân loại bằng cách so sánh tên tiếng Việt trong source.
- Sử dụng Item ID, Asset ID, Category hoặc Supply Type có cấu trúc.
- Kiểm tra trùng danh mục hiện hữu trước khi seed.
- Không tạo dữ liệu danh mục trùng lặp.

---

## 5. CẤU HÌNH ĐỊNH MỨC CỦA OPERATIONS MANAGER

Tạo màn hình:

**Cấu hình vật tư phòng**

Operations Manager có quyền cấu hình theo từng khách sạn/chi nhánh được phân quyền.

Các thông tin:

- Chi nhánh.
- Mã vật tư.
- Tên vật tư.
- Nhóm vật tư.
- Đơn vị tính.
- Số lượng cấp đầu ca.
- Số lượng tối thiểu trong kho.
- Số lượng cần cho mỗi khách.
- Trạng thái áp dụng.
- Ngày hiệu lực nếu cần.
- Người cập nhật.
- Thời gian cập nhật.

Ví dụ:

DVBV:

- Nước đóng chai: cấp đầu ca 10, minimum stock 50.
- Khăn tắm: cấp đầu ca 10, minimum stock 50.

DVCT:

- Có thể cấu hình cấp đầu ca 15.
- Minimum stock 100.

Không buộc hai chi nhánh sử dụng cùng một định mức.

### Quy tắc

- Chỉ OM có quyền cấu hình trong phạm vi branch.
- Storekeeper được xem cấu hình áp dụng tại kho.
- HK Supervisor được xem định mức phục vụ giao việc.
- HK Staff chỉ xem định mức và lượng vật tư liên quan bản thân.
- Owner chỉ xem nếu quyền đọc phù hợp.
- Thay đổi cấu hình không được viết lại giao dịch lịch sử.
- Ca đang mở phải lưu hoặc tham chiếu phiên bản định mức đầu ca.
- Không tự động cấp bù khi OM chỉnh cấu hình giữa ca.
- Không chấp nhận số lượng âm hoặc dữ liệu sai đơn vị tính.

Thiết kế permission phù hợp với naming convention hiện hữu.

Không tạo nhiều permission dư thừa hoặc cấp quyền ALL cho OM.

---

## 6. QUẢN LÝ VẬT TƯ HK STAFF ĐANG GIỮ

Bổ sung domain quản lý vật tư do nhân viên giữ trong một ca làm việc.

Không lưu lượng vật tư bằng các field rời trong bảng User.

Mô hình phải hỗ trợ:

- Staff ID.
- Branch ID.
- Workday/Shift ID.
- Item ID.
- Initial Issued Quantity.
- Additional Issued Quantity.
- Reserved Quantity.
- Used/Placed Quantity.
- Returned Quantity.
- Remaining Quantity.
- Allocation Status.
- RowVersion hoặc concurrency token.
- Transaction References.

Tên bảng và field phải phù hợp kiến trúc hiện hữu.

Ưu tiên tái sử dụng Inventory Ledger/Stock Movement nếu hỗ trợ.

### Công thức

Remaining Quantity:

Initial Issued + Additional Issued - Used/Placed - Returned.

Available Quantity:

Remaining Quantity - Reserved Quantity.

Reserved Quantity là phần vật tư được giữ chỗ cho Cleaning Tasks đã phân công nhưng chưa hoàn thành.

Không được:

- Remaining âm.
- Available âm.
- Reserved âm.
- Cấp phát trùng.
- Tiêu hao trùng.
- Hoàn trả trùng.

Mọi biến động phải có lịch sử và liên kết với ca làm việc hoặc công việc liên quan.

Phải đối soát được:

- Kho đang giữ.
- Nhân viên đang giữ.
- Vật tư đã sử dụng hoặc cấp vào phòng.
- Vật tư đã hoàn trả.

---

## 7. TỰ ĐỘNG CẤP VẬT TƯ KHI BẮT ĐẦU CA

### Trigger

Khi HK Staff nhấn:

`Bắt đầu ca`.

Hệ thống thực hiện workflow tự động.

### Workflow

1. Kiểm tra ca làm việc hợp lệ.
2. Kiểm tra ca thuộc nhân viên đang đăng nhập.
3. Kiểm tra đúng chi nhánh.
4. Kiểm tra chưa bắt đầu ca.
5. Đọc cấu hình đồ fill của chi nhánh.
6. Kiểm tra tồn kho từng mặt hàng.
7. Tạo giao dịch cấp phát đầu ca.
8. Giảm tồn vật tư tại kho.
9. Tăng lượng vật tư HK Staff đang giữ.
10. Liên kết giao dịch với Workday Session.
11. Thông báo cho HK Staff.
12. Ghi lịch sử cho Storekeeper.

Đây là giao dịch cấp phát tự động của hệ thống, không yêu cầu Storekeeper xác nhận thủ công từng ca.

Không cấp quyền xuất kho trực tiếp cho HK Staff để thực hiện bước này.

Backend thực hiện automation trong phạm vi nghiệp vụ được phê duyệt.

### Xử lý kho thiếu khi bắt đầu ca

Owner chưa quy định rõ tình huống kho không đủ bộ đồ cấp đầu ca.

Áp dụng phương án an toàn mặc định:

- Cho phép nhân viên ghi nhận bắt đầu ca hợp lệ.
- Không xuất âm kho.
- Không giả lập vật tư đã được cấp.
- Nếu không thể cấp đủ bộ định mức, ghi nhận trạng thái cấp phát thiếu.
- Thông báo HK Staff, HK Supervisor và Storekeeper.
- Không cho phân công phòng nếu lượng vật tư thực tế không đáp ứng.
- Cho phép cấp bổ sung khi kho có đủ hàng.

Không tự động phát sinh nhiều giao dịch cho cùng một ca.

Nếu lựa chọn cấp một phần, phải ghi rõ số lượng thực cấp và phần còn thiếu, xử lý trong transaction nhất quán.

### Idempotency

Nhấn Start Shift nhiều lần hoặc retry API không được cấp thêm nhiều bộ vật tư.

Kiểm tra bằng Shift ID và transaction reference duy nhất.

---

## 8. KIỂM TRA ĐỦ VẬT TƯ TRƯỚC KHI PHÂN CÔNG DỌN PHÒNG

Khi HK Supervisor phân công Cleaning Task, backend phải kiểm tra lượng vật tư HK Staff đang có.

### Công thức

Required Quantity của mỗi Item:

`Room Max Guest Capacity × Item Quantity Per Guest`

Ví dụ:

Phòng có sức chứa tối đa 3 khách.

Mỗi vật tư yêu cầu 1 đơn vị/khách.

Nhân viên cần ít nhất:

- 3 bàn chải.
- 3 kem đánh răng.
- 3 dầu gội.
- 3 sữa tắm.
- 3 nước.
- 3 giấy vệ sinh.
- 3 khăn tắm.
- 3 khăn mặt.

Không sử dụng số khách đang lưu trú thay cho Max Guest Capacity.

Nếu Room chưa có sức chứa tối đa hợp lệ, báo lỗi cấu hình; không tự mặc định sức chứa bằng 0.

### Điều kiện phân công

Nhân viên phải:

- ACTIVE.
- Thuộc đúng chi nhánh.
- Có role HK_STAFF.
- Có ca làm việc hợp lệ và đang mở.
- Có đủ Available Quantity cho tất cả đồ fill bắt buộc.
- Đáp ứng điều kiện phân công Cleaning Task hiện hành.

Nếu chỉ thiếu một loại vật tư thì không được phân công phòng.

### Giữ chỗ khi phân công

Khi phân công thành công:

- Tạo reservation cho số lượng vật tư cần dùng.
- Giảm Available Quantity.
- Không ghi nhận đã tiêu hao ngay.
- Giữ snapshot sức chứa phòng và định mức vật tư tại thời điểm giao việc.

Khi đổi nhân viên:

- Kiểm tra nhân viên mới đủ vật tư.
- Giải phóng reservation của nhân viên cũ.
- Tạo reservation cho nhân viên mới.
- Thực hiện toàn bộ trong một transaction.

Khi hủy Cleaning Task hợp lệ:

- Giải phóng phần reservation chưa sử dụng.
- Không tự hoàn lại vật tư thực tế đã sử dụng.

### Ví dụ kiểm thử

Nhân viên có 10 đơn vị của mỗi vật tư.

Được giao 3 phòng:

- Phòng A: 2 khách.
- Phòng B: 3 khách.
- Phòng C: 4 khách.

Tổng giữ chỗ: 9 đơn vị mỗi loại.

Available còn 1.

Không thể giao thêm phòng có sức chứa 2 khách.

Không chỉ disable button frontend.

Backend phải từ chối assignment không đủ vật tư ngay cả khi gọi API trực tiếp.

---

## 9. GHI NHẬN VẬT TƯ KHI HOÀN THÀNH DỌN PHÒNG

Khi HK Staff hoàn thành Cleaning Task hợp lệ:

1. Đọc reservation tương ứng.
2. Ghi nhận lượng vật tư đã sử dụng/cấp vào phòng.
3. Giảm Remaining Quantity.
4. Giải phóng reservation đã thực hiện.
5. Tạo bản ghi vật tư sử dụng theo phòng.
6. Liên kết Cleaning Task.
7. Chuyển công việc sang bước chờ Inspection.
8. Ghi audit.

Không trừ vật tư trực tiếp tại thời điểm tạo Cleaning Task.

Không giảm tồn kho trung tâm lần thứ hai khi hoàn thành Cleaning Task, vì vật tư đã được chuyển từ kho sang nhân viên khi cấp phát.

Không trừ lặp khi:

- Nhấn Complete nhiều lần.
- Retry API.
- F5.
- Reconnect.
- Inspection lại.
- Cleaning Rework.

Đối với Rework, không tự động trừ thêm một bộ đồ fill nếu chỉ làm lại vệ sinh. Nếu thực tế cần thêm vật tư thay thế, phải có nghiệp vụ ghi nhận bổ sung riêng được xác thực và có audit.

Phải bảo đảm Stock Ledger, Staff Supply Balance và Cleaning Task luôn nhất quán.

---

## 10. YÊU CẦU BỔ SUNG VẬT TƯ TIÊU HAO

Tạo loại Internal Request:

`CONSUMABLE_REFILL_REQUEST`.

Tên tiếng Việt:

Yêu cầu bổ sung tiêu hao.

### Người tạo

HK Staff đang trong ca làm việc.

### Nội dung

- Mã yêu cầu.
- Chi nhánh.
- Người yêu cầu.
- Ca làm việc.
- Danh sách vật tư.
- Số lượng đang giữ.
- Số lượng khả dụng.
- Số lượng đề nghị bổ sung.
- Lý do.
- Công việc liên quan nếu có.
- Trạng thái.
- Người xử lý hiện tại.
- Lịch sử.

Cho phép yêu cầu khi không đủ vật tư để thực hiện công việc tiếp theo, không bắt buộc tất cả vật tư phải bằng 0.

### STEP 1 — HK Staff gửi yêu cầu

Request được chuyển đến HK Supervisor cùng chi nhánh.

Trạng thái logic:

`PENDING_HK_SUPERVISOR`.

### STEP 2 — HK Supervisor xác nhận

HK Supervisor xem:

- Lượng vật tư thực tế nhân viên đang giữ.
- Reservation.
- Công việc được giao.
- Nhu cầu bổ sung.
- Lịch sử cấp phát.
- Số lượng đề nghị.

Có hai lựa chọn:

- Xác nhận.
- Từ chối.

Nếu từ chối:

- Bắt buộc nhập lý do.
- Kết thúc yêu cầu ở trạng thái phù hợp.
- Thông báo HK Staff.
- Không tăng lượng vật tư của nhân viên.
- Không giảm tồn kho.
- Không chuyển sang Storekeeper.

Nếu xác nhận:

- Tự động thay đổi người/bộ phận nhận thành Storekeeper.
- Giữ nguyên Request ID.
- Giữ lịch sử xác nhận của HK Supervisor.
- Thông báo Storekeeper.
- Chuyển trạng thái logic sang `PENDING_STOREKEEPER`.

Không tạo một yêu cầu độc lập làm mất liên kết với yêu cầu gốc.

### STEP 3 — Storekeeper xử lý

Storekeeper xem:

- Người yêu cầu.
- Ca làm việc.
- Chi nhánh.
- Vật tư yêu cầu.
- Số lượng được HK Supervisor xác nhận.
- Tồn kho khả dụng.
- Kết quả kiểm tra đủ/thiếu theo từng mặt hàng.

Có hai lựa chọn:

- Xác nhận cấp phát.
- Từ chối.

### STEP 4 — Kiểm tra tồn kho

Khi Storekeeper nhấn xác nhận:

Backend kiểm tra lại tồn kho trong transaction.

Nếu tất cả mặt hàng đủ:

- Tạo Stock Issue hoặc giao dịch chuyển vật tư hợp lệ.
- Giảm tồn kho.
- Tăng Additional Issued Quantity của nhân viên.
- Ghi giao dịch cấp phát.
- Chuyển Request thành Fulfilled/Completed theo lifecycle.
- Thông báo HK Staff.
- Thông báo HK Supervisor.

Nếu không đủ:

- Không xuất âm kho.
- Không tạo giao dịch cấp phát.
- Hiển thị rõ mặt hàng thiếu và số lượng thiếu.
- Yêu cầu Storekeeper thực hiện từ chối kèm lý do.

Không tự động cấp một phần đối với yêu cầu bổ sung đã được xác nhận nếu chưa có chính sách partial fulfillment.

### STEP 5 — Kiểm tra lại trước khi cấp

Nếu ca làm việc đã kết thúc:

- Không cấp vật tư vào ca đã đóng.
- Yêu cầu Storekeeper từ chối hoặc kết thúc request theo contract.
- Ghi rõ lý do.

Mọi trường hợp từ chối đều phải nhập lý do.

Không cho OM xác nhận thay HK Supervisor hoặc Storekeeper.

---

## 11. TỰ ĐỘNG HOÀN TRẢ VẬT TƯ CUỐI CA

Khi HK Staff nhấn:

`Kết thúc ca`.

Backend thực hiện:

1. Kiểm tra Workday Session đang mở.
2. Kiểm tra Cleaning Tasks còn dang dở.
3. Kiểm tra reservation chưa giải phóng.
4. Kiểm tra yêu cầu bổ sung đang chờ.
5. Xác định số lượng vật tư còn thực tế.
6. Tạo Stock Return.
7. Tăng tồn kho trung tâm.
8. Giảm Staff Remaining Quantity.
9. Đóng Staff Supply Allocation.
10. Đóng ca.
11. Tạo thông báo nhật ký cho Storekeeper.
12. Ghi audit.

### Quy tắc

- Chỉ hoàn trả đồ chưa sử dụng.
- Không hoàn trả đồ đã cấp vào phòng.
- Không hoàn trả đồ đã ghi nhận tiêu hao.
- Không hoàn trả hai lần.
- Không cho tồn vật tư nhân viên âm.
- Không tự động xóa lịch sử cấp phát.

Đối với Cleaning Task còn mở hoặc reservation chưa giải quyết:

Không được âm thầm hủy hoặc mất assignment.

Phải yêu cầu xử lý/reassign/hủy đúng workflow trước khi đóng ca hoặc thực hiện quy trình bàn giao hợp lệ đã được định nghĩa.

### Yêu cầu bổ sung còn chờ

Nếu ca đã kết thúc, yêu cầu không còn được phép cấp vào ca đó.

Sử dụng trạng thái kết thúc phù hợp, ghi lý do và thông báo cho người xử lý.

Không tự chuyển lượng vật tư sang ca tiếp theo.

### Notification cho Storekeeper

Nội dung:

- Nhân viên.
- Mã ca.
- Chi nhánh.
- Thời gian hoàn trả.
- Danh sách mặt hàng.
- Số lượng hoàn trả từng mặt hàng.
- Mã chứng từ.
- Nguồn giao dịch: tự động hoàn trả cuối ca.

Storekeeper có thể mở chứng từ chi tiết.

Không yêu cầu Storekeeper xác nhận lần nữa đối với giao dịch hoàn trả tự động đã hoàn tất.

---

## 12. CẢNH BÁO TỒN KHO TỐI THIỂU

Operations Manager được cấu hình Minimum Stock theo từng vật tư và chi nhánh.

Đồ fill mặc định:

`Minimum Stock = 50`.

### Trigger

Kiểm tra lại tồn kho sau:

- Cấp đầu ca.
- Cấp bổ sung.
- Xuất kho.
- Nhập kho.
- Hoàn trả cuối ca.
- Điều chỉnh tồn.
- Chuyển kho.
- Cập nhật Minimum Stock.

### Điều kiện

Nếu Stock Quantity của mặt hàng nhỏ hơn Minimum Stock:

- Hiển thị trạng thái Low Stock.
- Thông báo Storekeeper.
- Hiển thị số lượng hiện tại.
- Hiển thị định mức tối thiểu.
- Hiển thị số lượng thiếu so với ngưỡng.
- Cho phép tạo yêu cầu bổ sung kho.

Ví dụ:

Minimum Stock: 50.

Current Stock: 43.

Thiếu so với ngưỡng: 7.

Nếu tồn đúng 50 thì không phát sinh cảnh báo dưới ngưỡng.

Không tạo notification trùng sau mọi lần refresh hoặc mỗi lần scheduler chạy.

Quản lý một cảnh báo đang mở cho mỗi Item/Branch hoặc sử dụng cơ chế deduplication tương đương.

Khi tồn kho được bổ sung đủ, cập nhật cảnh báo đã giải quyết.

Khi thiếu trở lại, cho phép phát sinh cảnh báo mới.

---

## 13. YÊU CẦU BỔ SUNG HÀNG CHO KHO

Tạo loại yêu cầu:

`WAREHOUSE_REPLENISHMENT_REQUEST`.

Tên tiếng Việt:

Yêu cầu bổ sung hàng cho kho.

### Workflow

Storekeeper nhận cảnh báo thiếu hàng.

Storekeeper tạo yêu cầu bổ sung.

Chọn:

- Vật tư cần bổ sung.
- Tồn hiện tại.
- Minimum Stock.
- Số lượng đề nghị.
- Lý do.
- Mức độ ưu tiên nếu có.

Yêu cầu được gửi đến Operations Manager cùng chi nhánh.

OM có thể:

- Xem chi tiết.
- Xác nhận đề nghị bổ sung.
- Từ chối kèm lý do.
- Theo dõi tiến độ bổ sung.

Đây là yêu cầu gửi trực tiếp đến OM, không phải bước phê duyệt trung gian giữa các bộ phận.

### Nhập kho sau bổ sung

Khi hàng thực tế được đưa đến kho:

Storekeeper thực hiện nhận hàng theo Replenishment/Stock Receipt workflow hiện hành.

Chỉ tăng tồn kho sau khi có giao dịch nhập kho thành công.

Không tăng tồn kho khi OM vừa xác nhận yêu cầu.

Không xây dựng module mua hàng hoặc quản lý nhà cung cấp mới nếu không thuộc phạm vi nghiệp vụ.

---

## 14. SỰ CỐ THIẾU VẬT DỤNG LÂU BỀN TRONG PHÒNG

Tạo loại yêu cầu:

`ROOM_DURABLE_ITEM_MISSING`.

Tên tiếng Việt:

Báo cáo thiếu vật dụng lâu bền.

### STEP 1 — HK Staff báo cáo

Trong quá trình kiểm tra hoặc dọn phòng, HK Staff phát hiện thiếu:

- Ấm đun nước.
- Máy sấy tóc.
- Đèn.
- Móc treo quần áo.
- Thùng rác.
- Dép.
- Gối.
- Ga giường.
- Chăn.

HK Staff tạo báo cáo từ Cleaning Task hoặc Room Detail.

Thông tin:

- Phòng.
- Chi nhánh.
- Cleaning Task.
- Loại vật dụng.
- Số lượng thiếu.
- Mô tả.
- Bằng chứng nếu chức năng hiện có hỗ trợ.
- Người báo cáo.
- Thời điểm báo cáo.

Gửi đến HK Supervisor.

### STEP 2 — HK Supervisor kiểm tra

HK Supervisor nhận yêu cầu kiểm tra.

Có hai lựa chọn:

A. Không xác nhận sự cố:

- Ghi lý do.
- Thông báo người báo cáo.
- Kết thúc yêu cầu kiểm tra.
- Không tạo giao dịch kho.

B. Xác nhận thiếu vật dụng:

- Ghi kết quả kiểm tra.
- Xác định vật dụng và số lượng cần bổ sung.
- Tạo yêu cầu khắc phục gửi Storekeeper.
- Liên kết yêu cầu gốc.
- Đánh dấu phòng còn vấn đề chưa giải quyết.

Không yêu cầu OM phê duyệt.

### STEP 3 — Storekeeper tiếp nhận

Storekeeper xem:

- Phòng cần bổ sung.
- Vật dụng thiếu.
- Số lượng yêu cầu.
- Người xác nhận.
- Tồn kho hiện tại.

Storekeeper thực hiện bổ sung vật dụng vào đúng phòng.

Khi xác nhận đã bổ sung:

- Kiểm tra tồn kho.
- Tạo Stock Issue.
- Ghi nhận đích cấp phát là phòng.
- Cập nhật số lượng vật dụng được bổ sung.
- Ghi lịch sử.
- Gửi thông báo đã khắc phục đến HK Supervisor.

Không cộng vật dụng nhóm B vào Staff Supply Balance của HK Staff.

Nếu kho không đủ:

- Không xuất âm kho.
- Không đánh dấu đã khắc phục.
- Cho phép Storekeeper từ chối hoặc báo thiếu theo workflow, bắt buộc lý do.
- Giữ sự cố gốc chưa giải quyết.
- Cho phép tạo yêu cầu bổ sung hàng cho kho.

### STEP 4 — HK Supervisor kiểm tra lại

HK Supervisor nhận yêu cầu kiểm tra kết quả bổ sung.

Nếu đạt:

- Xác nhận sự cố đã được khắc phục.
- Đóng workflow.
- Đánh giá lại Room Readiness.
- Phòng trở về Ready nếu tất cả điều kiện được đáp ứng.

Nếu không đạt:

- Bắt buộc nhập lý do.
- Tạo yêu cầu bổ sung lại gửi Storekeeper.
- Giữ liên kết với sự cố gốc.
- Lưu lịch sử lần kiểm tra.
- Không chuyển phòng Ready.

### Chống cấp phát trùng

Khi yêu cầu bổ sung lại, không mặc định xuất lại toàn bộ vật dụng đã cấp ở lần trước.

HK Supervisor phải xác định phần còn thiếu hoặc vấn đề chưa được khắc phục.

Không tạo Stock Issue trùng chỉ vì gửi lại yêu cầu.

---

## 15. SỰ CỐ THIẾT BỊ VÀ TÀI SẢN BỊ HỎNG

Tạo loại yêu cầu:

`ROOM_EQUIPMENT_DAMAGE`.

Tên tiếng Việt:

Báo cáo thiết bị phòng bị hỏng.

Áp dụng cho:

- Nội thất.
- Điều hòa.
- Tivi.
- Bình nóng lạnh.
- Thiết bị an ninh.
- Thiết bị báo cháy.
- Các thiết bị cần sửa chữa bên ngoài.

### STEP 1 — Báo cáo sự cố

Người báo cáo:

- HK Staff.
- Reception.

Tạo báo cáo bao gồm:

- Phòng.
- Tài sản hoặc thiết bị.
- Mô tả sự cố.
- Mức độ ảnh hưởng.
- Bằng chứng nếu hỗ trợ.
- Thời gian báo cáo.
- Người báo cáo.

Chuyển yêu cầu đến HK Supervisor.

Không chuyển trực tiếp đến nhân viên Maintenance Staff vì role này không còn tồn tại trong mô hình nghiệp vụ mới.

### STEP 2 — HK Supervisor kiểm tra

HK Supervisor nhận yêu cầu kiểm tra sự cố.

Nếu không phát hiện sự cố:

- Từ chối xác nhận.
- Bắt buộc ghi lý do.
- Thông báo người báo cáo.
- Kết thúc yêu cầu kiểm tra.

Nếu xác nhận sự cố:

- Ghi kết quả kiểm tra.
- Ghi mức độ ảnh hưởng đến khả năng sử dụng phòng.
- Tạo yêu cầu khắc phục gửi Maintenance Supervisor.
- Liên kết Incident gốc.
- Đánh dấu phòng có vấn đề cần khắc phục.

Không yêu cầu OM phê duyệt.

### STEP 3 — Maintenance Supervisor tiếp nhận

Maintenance Supervisor được xem:

- Phòng.
- Thiết bị.
- Nội dung sự cố.
- Kết quả kiểm tra của HK Supervisor.
- Người báo cáo.
- Thời gian.
- Mức độ ảnh hưởng.
- Lịch sử sửa chữa.

Maintenance Supervisor thực hiện liên hệ đơn vị sửa chữa bên ngoài.

Hoạt động liên hệ và thông tin nhà cung cấp không được quản lý trong hệ thống ở phạm vi hiện tại.

Maintenance Supervisor có thể ghi nhận:

- Đã tiếp nhận.
- Đang khắc phục.
- Đang giám sát sửa chữa.
- Đã hoàn thành.

Sử dụng lifecycle thích hợp từ source hoặc migration trạng thái có kiểm soát nếu cần.

Không tạo assignment cho kỹ thuật viên nội bộ.

### STEP 4 — Maintenance Supervisor xác nhận hoàn thành

Sau khi giám sát quá trình sửa chữa, Maintenance Supervisor nhấn xác nhận hoàn thành.

Backend:

- Lưu kết quả khắc phục.
- Lưu thời gian hoàn thành.
- Lưu người xác nhận.
- Tạo yêu cầu kiểm tra kết quả cho HK Supervisor.
- Gửi notification.
- Giữ Incident ở trạng thái chờ kiểm tra cuối cùng.

Không tự chuyển phòng Ready.

### STEP 5 — HK Supervisor kiểm tra lại

Nếu đạt:

- Xác nhận kết quả sửa chữa.
- Đóng Incident và yêu cầu liên quan theo workflow.
- Giải quyết room readiness blocker tương ứng.
- Đánh giá lại khả năng sử dụng phòng.
- Chuyển phòng Ready nếu đủ toàn bộ điều kiện.

Nếu không đạt:

- Bắt buộc nhập lý do.
- Ghi nhận kết quả kiểm tra không đạt.
- Tạo yêu cầu sửa lại cho Maintenance Supervisor.
- Giữ Incident gốc.
- Tăng số lần xử lý/rework.
- Gửi notification.
- Không chuyển phòng Ready.

### STEP 6 — Sửa lại

Maintenance Supervisor tiếp nhận yêu cầu sửa lại.

Tiếp tục giám sát đơn vị sửa chữa bên ngoài.

Xác nhận hoàn thành lần tiếp theo.

Hệ thống tiếp tục tạo yêu cầu kiểm tra cho HK Supervisor.

Lặp lại đến khi HK Supervisor xác nhận đạt.

Không xóa hoặc ghi đè lịch sử các lần sửa chữa trước.

---

## 16. ROOM READINESS — ĐIỀU KIỆN PHÒNG SẴN SÀNG

Tạo hoặc chuẩn hóa một domain policy chung đánh giá khả năng sử dụng phòng.

Không cho từng module tự cập nhật Ready bằng logic riêng.

Phòng chỉ được chuyển sang Ready khi:

1. Không có khách đang lưu trú.
2. Đã hoàn thành vệ sinh bắt buộc.
3. Inspection đạt nếu workflow yêu cầu.
4. Không còn Cleaning Rework chưa hoàn thành.
5. Không còn thiếu vật dụng lâu bền được xác định là readiness blocker.
6. Không còn sự cố thiết bị ảnh hưởng đến khả năng sử dụng phòng.
7. Đã kiểm tra đạt sau khi khắc phục các blocker.
8. Không còn Room Block hợp lệ.
9. Đáp ứng các điều kiện hiện hữu khác của Room Status.

Phải phân biệt:

- Occupancy.
- Cleaning Status.
- Inspection Status.
- Incident Status.
- Room Readiness Blockers.
- Operational Room Block.

Nếu một phòng có nhiều sự cố cùng lúc, xử lý xong một sự cố không được tự động đưa phòng Ready khi sự cố khác còn mở.

Đối với phòng đang có khách:

Không tự đổi Occupied thành Vacant khi báo cáo sự cố hoặc khắc phục hoàn tất.

Nếu sự cố thuộc nhóm an toàn nghiêm trọng, như thiết bị báo cháy hoặc thiết bị an ninh, phải ngăn sử dụng phòng theo chính sách an toàn phù hợp; không cho Ready chỉ dựa trên xác nhận sửa chữa.

Giữ quyền Reception cập nhật occupancy và quyền HK Supervisor kiểm soát chất lượng phòng theo workflow được duyệt.

Không tự mở quyền chỉnh trạng thái tùy ý.

---

## 17. UNIFIED INTERNAL REQUEST

Gộp các nhóm giao diện hiện tại:

- Báo cáo sự cố.
- Yêu cầu nội bộ.
- Gửi đơn.
- Yêu cầu bổ sung vật tư.
- Yêu cầu khắc phục.
- Yêu cầu kiểm tra.

Thành một workspace duy nhất:

**Yêu cầu nội bộ**

English: Internal Requests.

### Nguyên tắc kiến trúc

Gộp trải nghiệm người dùng và lớp điều phối workflow.

Không bắt buộc xóa ngay toàn bộ bảng Incident, Material Request, Maintenance hoặc Stock Documents đang tồn tại.

Có thể duy trì domain entities chuyên biệt và liên kết chúng với Unified Internal Request.

Mỗi loại yêu cầu phải sử dụng validation và state machine riêng.

Không dùng một bảng JSON không có ràng buộc để thay thế toàn bộ mô hình nghiệp vụ có cấu trúc.

Không tạo nhiều bản ghi trùng giữa các module mà không có correlation hoặc parent-child relationship.

### Màn hình danh sách

Các tab:

- Yêu cầu của tôi.
- Yêu cầu cần xử lý.
- Đang xử lý.
- Đã hoàn thành.
- Đã từ chối.
- Tất cả yêu cầu trong phạm vi được phép.

Operations Manager chỉ xem các yêu cầu liên bộ phận, ngoài loại bổ sung hàng cho kho được phép xử lý.

### Các cột

- Mã yêu cầu.
- Loại yêu cầu.
- Tiêu đề.
- Chi nhánh.
- Phòng liên quan.
- Người gửi.
- Bộ phận gửi.
- Người/bộ phận nhận hiện tại.
- Trạng thái.
- Mức độ ưu tiên.
- Thời gian tạo.
- Cập nhật gần nhất.
- Hành động.

### Popup tạo yêu cầu

Button:

`Tạo yêu cầu`.

Hiển thị Dropdown:

`Loại yêu cầu`.

Các loại yêu cầu có thể gồm:

| Request Type | Tên hiển thị |
|---|---|
| `GENERAL_INTERNAL_REQUEST` | Yêu cầu nội bộ thông thường |
| `CONSUMABLE_REFILL_REQUEST` | Yêu cầu bổ sung tiêu hao |
| `WAREHOUSE_REPLENISHMENT_REQUEST` | Yêu cầu bổ sung hàng cho kho |
| `ROOM_DURABLE_ITEM_MISSING` | Báo cáo thiếu vật dụng lâu bền |
| `ROOM_EQUIPMENT_DAMAGE` | Báo cáo thiết bị bị hỏng |
| `DURABLE_ITEM_REMEDIATION` | Yêu cầu bổ sung vật dụng lâu bền |
| `MAINTENANCE_REMEDIATION` | Yêu cầu khắc phục sự cố kỹ thuật |
| `ROOM_REINSPECTION` | Yêu cầu kiểm tra lại phòng |
| `APPLICATION_REQUEST` | Gửi đơn |

Tên enum chỉ mang tính định hướng.

Đối chiếu các Request Type hiện hữu trước khi quyết định migration.

Giữ những loại gửi đơn đang được hệ thống hỗ trợ; không tự phát minh thêm nghiệp vụ nghỉ phép hoặc nhân sự ngoài yêu cầu.

### Dynamic Form

Khi chọn loại yêu cầu, hiển thị các trường tương ứng.

Ví dụ:

Bổ sung tiêu hao:

- Danh sách vật tư.
- Số lượng đề nghị.
- Ca làm việc.
- Lý do.

Báo thiếu vật dụng:

- Phòng.
- Vật dụng.
- Số lượng thiếu.
- Mô tả.

Báo thiết bị hỏng:

- Phòng.
- Thiết bị.
- Mô tả.
- Mức độ ảnh hưởng.

Bổ sung kho:

- Mặt hàng.
- Tồn hiện tại.
- Minimum Stock.
- Số lượng đề nghị.

### Permission-based Request Types

Dropdown chỉ hiển thị các loại yêu cầu mà Actor được phép tạo.

Không cho frontend tự gửi bất kỳ Request Type nào rồi backend chấp nhận.

Các loại yêu cầu sinh tự động từ workflow, như yêu cầu kiểm tra sau sửa chữa, không cần xuất hiện dưới dạng yêu cầu được tạo tùy ý.

### Routing

Hệ thống tự xác định người/bộ phận nhận theo Request Type và Branch.

Không cho HK Staff tự chuyển yêu cầu đến OM để bỏ qua HK Supervisor.

Không cho người gửi tùy ý chọn chi nhánh ngoài quyền.

### Request Details

Hiển thị Drawer:

- Thông tin yêu cầu.
- Nội dung.
- Trạng thái hiện tại.
- Người chịu trách nhiệm.
- Timeline toàn bộ quá trình.
- Lịch sử chuyển người xử lý.
- Lý do từ chối.
- Các yêu cầu con.
- Incident gốc.
- Chứng từ kho.
- Cleaning Task liên quan.
- Kết quả khắc phục.
- Kết quả kiểm tra.
- Thông báo liên quan.

Tất cả dữ liệu phải được lọc theo quyền thực tế.

---

## 18. REQUEST ROUTING MATRIX

Triển khai quy tắc routing rõ ràng.

| Loại yêu cầu | Người tạo | Người xử lý |
|---|---|---|
| Bổ sung tiêu hao | HK Staff | HK Supervisor |
| Bổ sung tiêu hao đã xác nhận | Hệ thống chuyển tiếp | Storekeeper |
| Bổ sung hàng cho kho | Storekeeper | Operations Manager |
| Báo thiếu vật dụng lâu bền | HK Staff | HK Supervisor |
| Khắc phục thiếu vật dụng | HK Supervisor | Storekeeper |
| Kiểm tra sau bổ sung | Hệ thống | HK Supervisor |
| Báo thiết bị hỏng | HK Staff / Reception | HK Supervisor |
| Khắc phục sự cố kỹ thuật | HK Supervisor | Maintenance Supervisor |
| Kiểm tra sau sửa chữa | Hệ thống | HK Supervisor |
| Yêu cầu bổ sung lại | HK Supervisor | Storekeeper |
| Yêu cầu sửa lại | HK Supervisor | Maintenance Supervisor |
| Yêu cầu nội bộ thông thường | Actor hợp lệ | Bộ phận đích theo contract |
| Gửi đơn | Actor hợp lệ | Người xử lý theo loại đơn hiện hành |

Không có bước trung gian OM phê duyệt tất cả yêu cầu.

### Trường hợp nhiều người cùng role

Nếu có nhiều HK Supervisor hoặc Storekeeper trong cùng chi nhánh:

- Ưu tiên quy tắc người đang trực/được phân công nếu dữ liệu Workday hỗ trợ.
- Nếu không có người trực, đưa vào hàng đợi bộ phận phù hợp.
- Không làm mất yêu cầu.
- Không để nhiều người đồng thời xử lý thành công cùng một transition.
- Khi một người tiếp nhận, xác định handler rõ ràng.
- Dùng concurrency control để chống xử lý trùng.

Không tự định tuyến sang chi nhánh khác khi không có người xử lý.

---

## 19. TRẠNG THÁI VÀ LỊCH SỬ REQUEST

Thiết kế state machine riêng cho từng loại.

Các trạng thái logic có thể gồm:

- Created.
- Pending Supervisor.
- Pending Storekeeper.
- Pending Maintenance Supervisor.
- Pending Operations Manager.
- Accepted.
- In Progress.
- Pending Reinspection.
- Rework Required.
- Fulfilled.
- Completed.
- Rejected.
- Cancelled.

Không bắt buộc tạo tất cả trạng thái nếu có thể ánh xạ hợp lệ với schema hiện hữu.

Mọi transition phải có:

- Request ID.
- Previous Status.
- New Status.
- Previous Handler.
- New Handler.
- Actor ID.
- Actor Role.
- Timestamp.
- Reason.
- Related Entity.
- Correlation ID.
- RowVersion.

Mọi hành động từ chối, không xác nhận hoặc yêu cầu làm lại đều bắt buộc có lý do.

Không cho xóa lịch sử.

Không ghi đè kết quả kiểm tra cũ.

Không cho client tự truyền New Status tùy ý.

Backend quyết định trạng thái tiếp theo dựa trên action hợp lệ.

---

## 20. GIAO DIỆN THEO ROLE MỚI

### A. Operations Manager

Sidebar mục tiêu:

1. Tổng quan vận hành.
2. Tình trạng phòng.
3. Lịch làm việc.
4. Nhân sự & Công việc.
5. Housekeeping & Maintenance.
6. Yêu cầu nội bộ.
7. Tài sản.
8. Kho & Vật tư.
9. Cấu hình định mức.
10. Báo cáo & Nhật ký.

Cho phép:

- Quản lý lịch tuần.
- Quản lý nguồn lực.
- Cấu hình định mức đầu ca.
- Cấu hình lượng tồn tối thiểu.
- Cấu hình vật tư theo sức chứa.
- Xử lý yêu cầu bổ sung hàng cho kho.
- Theo dõi yêu cầu liên bộ phận ở chế độ đọc.

Không hiển thị nút duyệt yêu cầu liên bộ phận thông thường.

### B. HK Supervisor

Sidebar mục tiêu:

1. Tổng quan Housekeeping.
2. Tình trạng phòng.
3. Phân công dọn phòng.
4. Kiểm tra & Làm lại.
5. Nhân viên & Vật tư.
6. Yêu cầu nội bộ.
7. Lịch làm việc cá nhân.

Màn hình phân công phải hiển thị:

- Nhân viên đang trực.
- Số lượng vật tư còn giữ.
- Số lượng đang reservation.
- Số lượng khả dụng.
- Danh sách phòng đã giao.
- Trạng thái đủ/thiếu vật tư.
- Lý do không thể phân công.

Màn hình Request cho phép:

- Xác nhận bổ sung tiêu hao.
- Từ chối.
- Kiểm tra báo thiếu vật dụng.
- Kiểm tra thiết bị bị hỏng.
- Gửi yêu cầu khắc phục.
- Xác nhận kết quả khắc phục.
- Yêu cầu làm lại.

### C. HK Staff

Sidebar mục tiêu:

1. Tổng quan cá nhân.
2. Công việc dọn phòng.
3. Vật tư của tôi.
4. Yêu cầu nội bộ.
5. Lịch làm việc.
6. Lịch sử công việc.

Trang Vật tư của tôi:

- Ca hiện tại.
- Vật tư được cấp đầu ca.
- Vật tư được bổ sung.
- Vật tư đang giữ chỗ.
- Vật tư đã sử dụng.
- Vật tư còn lại.
- Vật tư khả dụng.
- Lịch sử cấp phát.
- Lịch sử hoàn trả.

Button:

`Yêu cầu bổ sung`.

Trong Cleaning Task Detail:

- Báo thiếu vật dụng.
- Báo thiết bị hỏng.
- Xem định mức vật tư của phòng.
- Xem checklist.
- Bắt đầu.
- Hoàn thành.

Không cho HK Staff:

- Tự cộng vật tư.
- Tự xuất kho.
- Tự duyệt yêu cầu.
- Tự chuyển phòng Ready.
- Tự phân công Cleaning Task.

### D. Storekeeper

Sidebar mục tiêu:

1. Tổng quan kho.
2. Danh mục vật tư.
3. Tồn kho.
4. Nhập / Xuất / Chuyển kho.
5. Cấp phát cho nhân viên.
6. Kiểm kê & Điều chỉnh.
7. Yêu cầu nội bộ.
8. Lịch sử & Chứng từ.
9. Lịch làm việc.

Trang cấp phát cho nhân viên hiển thị:

- Danh sách nhân viên đang trong ca.
- Lượng vật tư đã cấp đầu ca.
- Lượng vật tư cấp bổ sung.
- Lượng nhân viên đang giữ.
- Lượng đã sử dụng.
- Lượng hoàn trả.
- Chứng từ liên quan.

Cho phép:

- Xác nhận/từ chối yêu cầu bổ sung tiêu hao.
- Bổ sung vật dụng lâu bền cho phòng.
- Xem lịch sử cấp phát tự động.
- Xem lịch sử hoàn trả tự động.
- Tạo yêu cầu bổ sung hàng cho kho khi thiếu.

Không cho sửa trực tiếp Staff Supply Balance.

### E. Maintenance Supervisor

Sidebar mục tiêu:

1. Tổng quan bảo trì.
2. Yêu cầu khắc phục.
3. Công việc đang xử lý.
4. Tài sản & Thiết bị.
5. Lịch sử sửa chữa.
6. Yêu cầu nội bộ.
7. Lịch làm việc.

Xóa hoặc ẩn:

- Quản lý Maintenance Staff.
- Phân công kỹ thuật viên nội bộ.
- Danh sách kỹ thuật viên nhận việc.
- Các chức năng dành riêng cho role Maintenance Staff cũ.

Cho phép:

- Tiếp nhận yêu cầu.
- Theo dõi sửa chữa.
- Xác nhận hoàn thành.
- Xem kết quả kiểm tra của HK Supervisor.
- Tiếp nhận yêu cầu sửa lại.

Không tự xác nhận Room Ready.

### F. Reception

Giữ chức năng Reception hiện hành đã được cấp quyền.

Bổ sung:

- Báo thiết bị phòng bị hỏng.
- Theo dõi yêu cầu đã gửi.
- Xem kết quả khắc phục.
- Xem tình trạng phòng.

Không tự chuyển yêu cầu trực tiếp sang Maintenance Supervisor để bỏ qua kiểm tra của HK Supervisor.

Không xác nhận chất lượng sửa chữa hoặc vệ sinh thay HK Supervisor.

### G. Owner

Giữ Global Read-only Scope.

Bổ sung khả năng giám sát trong phạm vi permission phù hợp:

- Tiêu hao vật tư theo chi nhánh.
- Vật tư cấp đầu ca.
- Vật tư bổ sung.
- Vật tư hoàn trả.
- Tồn kho dưới định mức.
- Số lượng sự cố.
- Thời gian xử lý sự cố.
- Tỷ lệ kiểm tra đạt.
- Số lần yêu cầu làm lại.
- Yêu cầu liên bộ phận.

Không cấp quyền mutation nghiệp vụ cho Owner.

### H. Administrator

- Quản lý 8 role nghiệp vụ mới.
- Hiển thị role cũ dưới dạng inactive nếu cần xem lịch sử.
- Quản lý tài khoản.
- Quản lý role assignment.
- Quản lý cấu hình hệ thống theo quyền.
- Không mặc định được xử lý thay các role nghiệp vụ.

---

## 21. WORKDAY RESTRUCTURING

Operations Manager tiếp quản chức năng lập lịch tuần của Department Head.

Cập nhật:

- Navigation.
- Permission guards.
- Workday API.
- Role eligibility.
- Shift templates.
- Staff candidate query.
- Department filters.

Các nhóm nhân sự cần được phân ca:

- HK Staff.
- HK Supervisor.
- Maintenance Supervisor.
- Reception.
- Storekeeper.

Không còn ca dành riêng cho Maintenance Staff nội bộ.

Điều chỉnh lịch tương lai chịu ảnh hưởng bởi role migration một cách có kiểm soát.

Không tự động xóa lịch quá khứ.

Không tự động tạo lịch tuần mới khi chưa có yêu cầu.

Cần kiểm tra lại định mức nhân sự theo ca do số lượng HK Staff tăng và Maintenance Staff bị loại bỏ.

Chỉ tự động cấp vật tư đầu ca đối với HK Staff.

Không tự cấp bộ đồ fill cho HK Supervisor, Maintenance Supervisor, Reception hoặc Storekeeper.

---

## 22. NOTIFICATION SYSTEM

Tích hợp các notification event mới vào hệ thống hiện hành.

Các sự kiện bắt buộc:

| Event | Người nhận |
|---|---|
| Cấp đồ fill đầu ca thành công | HK Staff, Storekeeper |
| Không đủ đồ cấp đầu ca | HK Staff, HK Supervisor, Storekeeper |
| Yêu cầu bổ sung tiêu hao mới | HK Supervisor |
| Supervisor từ chối | HK Staff |
| Supervisor xác nhận | Storekeeper |
| Storekeeper từ chối | HK Staff, HK Supervisor |
| Cấp bổ sung thành công | HK Staff, HK Supervisor |
| Hoàn trả vật tư cuối ca | Storekeeper |
| Tồn kho dưới định mức | Storekeeper |
| Yêu cầu bổ sung kho mới | OM |
| OM xác nhận/từ chối bổ sung kho | Storekeeper |
| Báo thiếu vật dụng | HK Supervisor |
| Yêu cầu bổ sung vật dụng | Storekeeper |
| Đã bổ sung vật dụng | HK Supervisor |
| Báo thiết bị hỏng | HK Supervisor |
| Yêu cầu sửa chữa | Maintenance Supervisor |
| Xác nhận sửa chữa hoàn thành | HK Supervisor |
| Yêu cầu sửa lại | Maintenance Supervisor |
| Yêu cầu bổ sung lại | Storekeeper |
| Sự cố đã xử lý đạt | Người báo cáo và bên liên quan |

Yêu cầu:

- Đúng recipient.
- Đúng branch.
- Có deep link hợp lệ.
- Không rò rỉ thông tin.
- Không tạo notification trùng.
- Chỉ gửi sự kiện thành công sau khi transaction đã commit.
- Hỗ trợ retry an toàn nếu hệ thống có outbox hoặc cơ chế tương đương.

---

## 23. DATABASE MIGRATION

Project Owner phê duyệt thay đổi database schema và data migration cần thiết cho thiết kế mới trong môi trường test của HotelOps Project 2.

Không được sử dụng phê duyệt này cho Project 1 hoặc production.

Ưu tiên tái sử dụng các bảng hiện hữu.

Bổ sung cấu trúc dữ liệu tối thiểu nếu cần cho:

1. Branch Supply Configuration.
2. Staff Shift Supply Allocation.
3. Staff Supply Balance/Ledger.
4. Cleaning Supply Reservation.
5. Cleaning Supply Usage.
6. Request Routing.
7. Request Transition History.
8. Request Relationship.
9. Room Readiness Blocker.
10. Low Stock Alert.
11. Remediation/Inspection History.
12. Idempotency Keys hoặc transaction reference phù hợp.

Tên trên là mô hình logic đề xuất, không bắt buộc tạo 12 bảng mới.

Kiểm tra schema hiện hành và lựa chọn thiết kế phù hợp nhất.

### Migration requirements

- Backup.
- Restore validation.
- Transaction.
- FK consistency.
- Unique constraints.
- Non-negative quantity constraints.
- Concurrency.
- Data preservation.
- Audit.
- Idempotency.
- Rollback hoặc recovery plan.

Không truncate database.

Không drop database.

Không reset toàn bộ dữ liệu.

Không đổi User ID.

Không đổi mật khẩu.

Không tạo tồn kho giả để test PASS.

Không tự cấp thêm vật tư vật lý vào database khi chưa có chứng từ nhập kho.

Bảo toàn dữ liệu Staffing V3/V4 ngoài các role và department membership đã được yêu cầu thay đổi.

---

## 24. BACKEND ARCHITECTURE

Thiết kế các domain service phù hợp, có thể gồm:

- RoleMigrationService.
- BranchSupplyConfigurationService.
- StaffSupplyService.
- ShiftSupplyAllocationService.
- CleaningSupplyReservationService.
- InventoryTransactionService.
- RequestRoutingService.
- RequestWorkflowService.
- RoomReadinessService.
- LowStockAlertService.
- MaintenanceRemediationService.

Đây là gợi ý trách nhiệm service.

Không tạo class trùng chức năng nếu source hiện có service phù hợp.

### Yêu cầu kiến trúc

- Controller không chứa toàn bộ business logic.
- Domain validation tập trung.
- Transaction boundary rõ ràng.
- Không có duplicate business writers.
- Không bypass authorization.
- Không dùng frontend làm nguồn xác định số tồn.
- Không cho client tự xác định actor hoặc recipient trái quy tắc.
- Kiểm tra stock và reservation phía server.
- Tái sử dụng Inventory Ledger.
- Không cập nhật trực tiếp số tồn ngoài transaction hợp lệ.
- Không đồng bộ trạng thái bằng cách gọi vòng tròn giữa nhiều service.

### Concurrency bắt buộc

Phải kiểm thử các trường hợp:

- Hai HK Staff bắt đầu ca đồng thời.
- Hai Supervisor giao phòng cho cùng một nhân viên.
- Hai Storekeeper xác nhận cùng một request.
- Hai giao