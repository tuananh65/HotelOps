
- Cập nhật tồn kho nguồn.
- Cập nhật tồn kho đích.
- Giữ thông tin lô và hạn sử dụng.
- Ghi lịch sử.
- Thông báo các bên.
- Hoàn thành phiếu.

### Consumables

Điều chuyển không phải tiêu hao toàn hệ thống.

Chỉ thay đổi vị trí, chi nhánh sở hữu và số lượng hàng đang vận chuyển theo chính sách.

### Reusable Items

Giữ tổng sở hữu toàn hệ thống khi điều chuyển giữa hai chi nhánh, trừ trường hợp ghi giảm hợp lệ.

Không sử dụng phiếu xuất tiêu hao cho hàng điều chuyển.

### Security

- OM nguồn chỉ xác nhận yêu cầu thuộc phạm vi của mình.
- Storekeeper nguồn chỉ xuất hàng kho được giao.
- Storekeeper đích chỉ xác nhận nhận hàng của kho được giao.
- Không sửa giao dịch đã commit.
- Không xuất âm.
- Không nhận trùng.
- Không bypass approval.
- Không mở scope ALL cho OM hoặc Storekeeper.

---

## 26. UNIFIED INTERNAL REQUEST

Giữ quyết định gộp các yêu cầu vào một workspace chung.

Tên:

Yêu cầu nội bộ.

Các loại có thể gồm:

| Request Type | Tên hiển thị |
|---|---|
| GENERAL_INTERNAL_REQUEST | Yêu cầu nội bộ |
| APPLICATION_REQUEST | Gửi đơn |
| CONSUMABLE_REFILL | Bổ sung tiêu hao |
| REUSABLE_ITEM_REQUEST | Bổ sung vật tư thay thế |
| WAREHOUSE_REPLENISHMENT | Bổ sung hàng cho kho |
| INTER_BRANCH_TRANSFER | Điều chuyển vật tư |
| ROOM_ITEM_MISSING | Ghi nhận thiếu vật dụng |
| ROOM_EQUIPMENT_DAMAGE | Ghi nhận thiết bị hỏng |
| MAINTENANCE_REMEDIATION | Khắc phục kỹ thuật |
| EQUIPMENT_REPLACEMENT | Đề xuất thay mới |
| ROOM_REINSPECTION | Kiểm tra lại phòng |
| ITEM_WRITE_OFF | Đề nghị ghi giảm vật tư |

Tên enum chỉ mang tính định hướng.

Đối chiếu Request Type hiện hành trước khi migration.

### Create Request

Dropdown loại yêu cầu.

Dynamic Form theo từng loại.

Chỉ hiển thị loại người dùng được phép tạo.

### Request Detail

Hiển thị:

- Người gửi.
- Người nhận.
- Chi nhánh.
- Trạng thái.
- Nội dung.
- Timeline.
- Phiếu liên quan.
- Lịch sử chuyển xử lý.
- Ghi chú.
- Lý do từ chối.
- Kết quả kiểm tra.
- Chứng từ kho.

### Routing

| Loại yêu cầu | Người gửi | Người xử lý |
|---|---|---|
| Bổ sung tiêu hao | HK Staff | HK Supervisor → Storekeeper |
| Bổ sung đồ thay thế | HK Staff/HK Supervisor theo quyền | Storekeeper |
| Thiếu vật dụng | HK Staff | HK Supervisor → Storekeeper |
| Thiết bị hỏng | HK Staff/Reception | HK Supervisor → Maintenance Supervisor |
| Phiếu sửa chữa | Maintenance Supervisor | Maintenance Staff |
| Nghiệm thu kỹ thuật | Maintenance Staff | Maintenance Supervisor |
| Kiểm tra lại phòng | Hệ thống | HK Supervisor |
| Thay mới thiết bị | Maintenance Supervisor | OM theo quyền tài sản |
| Bổ sung kho | Storekeeper | OM |
| Điều chuyển | OM đích | OM nguồn → Storekeeper nguồn/đích |

Không để OM tham gia mọi bước xử lý yêu cầu.

Không gộp toàn bộ domain entities thành một bảng JSON thiếu ràng buộc.

Unified Request là workspace và lớp điều phối, còn các giao dịch kho, Maintenance Work Order và Incident vẫn có domain contract riêng.

---

## 27. ROOM READINESS POLICY

Phòng chỉ Ready khi đáp ứng đầy đủ điều kiện.

- Occupancy phù hợp.
- Cleaning hoàn thành.
- Checklist bắt buộc hoàn thành.
- Inspection đạt.
- Không còn thiếu vật tư bắt buộc ảnh hưởng sử dụng.
- Không còn sự cố kỹ thuật chưa xử lý.
- Không còn yêu cầu sửa lại.
- Không còn blocker hoặc Room Block hợp lệ.

Maintenance Staff hoàn thành không đồng nghĩa Room Ready.

Maintenance Supervisor nghiệm thu kỹ thuật không đồng nghĩa Room Ready.

Storekeeper bổ sung vật dụng không đồng nghĩa Room Ready.

HK Supervisor chịu trách nhiệm xác nhận chất lượng phòng cuối cùng.

Nếu phòng có nhiều sự cố, giải quyết một sự cố không được tự động đóng mọi blocker.

Đối với thiết bị an ninh, thiết bị báo cháy hoặc sự cố an toàn nghiêm trọng, áp dụng guard phù hợp để không đưa phòng vào sử dụng khi chưa bảo đảm điều kiện.

Không tự thay đổi Occupancy.

Giữ Reception contract đã được phê duyệt.

---

## 28. EMAIL ACCOUNT INVITATION

Theo Note 24/11, sau khi tạo tài khoản, nhân viên phải nhận thông tin và hướng dẫn đăng nhập qua email.

### Workflow

Administrator tạo tài khoản.

→ Backend tạo User và Role Assignment.

→ Tạo lời mời hoặc thông tin kích hoạt an toàn.

→ Gửi email.

→ Nhân viên truy cập hệ thống.

→ Thực hiện thiết lập/đổi mật khẩu lần đầu.

→ Truy cập chức năng nghiệp vụ.

### Email content

- Tên hệ thống.
- Username hoặc thông tin nhận diện tài khoản.
- Địa chỉ đăng nhập.
- Hướng dẫn thiết lập mật khẩu.
- Thông tin hỗ trợ.
- Thời hạn lời mời nếu sử dụng token.

Ưu tiên activation link hoặc setup-password token:

- Dùng một lần.
- Có thời hạn.
- Lưu token dạng hash khi phù hợp.
- Không thể sử dụng lại sau khi thành công.

Không gửi password hash qua email.

Không lưu plaintext password.

Không ghi mật khẩu hoặc token vào log.

Nếu dự án bắt buộc sử dụng mật khẩu tạm thời, cần policy bảo mật phù hợp và yêu cầu đổi ngay lần đầu; ưu tiên phương án không gửi mật khẩu rõ qua email.

### Email failure

Không báo gửi email thành công nếu nhà cung cấp chưa xác nhận.

Lưu trạng thái:

- Pending.
- Sent.
- Failed.

Cho phép Administrator gửi lại lời mời hợp lệ.

Không tạo tài khoản trùng khi retry email.

Không rollback tài khoản đã tạo chỉ vì dịch vụ email gặp lỗi nếu có cơ chế retry và trạng thái lời mời rõ ràng.

---

## 29. FORCE PASSWORD CHANGE ON FIRST LOGIN

Đây là yêu cầu mới bắt buộc đối với tài khoản mới.

Bổ sung hoặc tái sử dụng field logic:

MustChangePassword.

PasswordChangedAt.

InvitationStatus nếu cần.

### Workflow

1. Nhân viên đăng nhập bằng cơ chế hợp lệ.
2. Backend xác định MustChangePassword.
3. Nếu TRUE, chỉ cấp quyền truy cập cần thiết cho đổi mật khẩu và đăng xuất.
4. Frontend chuyển đến Change Password.
5. Nhân viên nhập mật khẩu mới.
6. Backend kiểm tra password policy.
7. Lưu password hash mới.
8. Đánh dấu MustChangePassword = FALSE.
9. Thu hồi session/token cũ khi cần.
10. Cho phép truy cập nghiệp vụ.

### Security requirements

Không chỉ redirect frontend.

Backend phải từ chối endpoint nghiệp vụ khi tài khoản chưa hoàn tất đổi mật khẩu.

Phải kiểm tra:

- Direct URL.
- Direct API.
- Token cũ.
- Browser refresh.
- Nhiều tab.
- SignalR connection.
- Session refresh.
- Replay activation token.

Không mặc định đổi mật khẩu tất cả tài khoản cũ trong database.

Note yêu cầu bắt buộc lần đầu, chưa yêu cầu cưỡng chế toàn bộ tài khoản đang sử dụng đổi mật khẩu.

Giữ password hash hiện tại ngoài các thao tác đổi mật khẩu hợp lệ.

---

## 30. SIGNALR REALTIME NOTIFICATION

Bổ sung SignalR vào hệ thống ASP.NET Core và React hiện tại.

Không thay thế toàn bộ Notification database bằng kết nối realtime.

Notification phải được lưu để người dùng offline vẫn đọc được sau khi đăng nhập.

### Event types

- NewRequestCreated.
- RequestAssigned.
- RequestApproved.
- RequestRejected.
- CleaningAssigned.
- CleaningStatusChanged.
- MaintenanceAssigned.
- MaintenanceCompleted.
- ReinspectionRequired.
- SupplyAllocated.
- SupplyRefillRequested.
- SupplyRefillFulfilled.
- SupplyReturned.
- LowStockDetected.
- ExpiryWarning.
- TransferRequested.
- TransferApproved.
- TransferShipped.
- TransferReceived.
- TransferDiscrepancy.
- AccountInvitationStatus.

Tên event có thể điều chỉnh theo conventions.

### Backend

- Authenticated Notification Hub.
- Kiểm tra user identity.
- Kiểm tra branch và permission.
- Server quản lý membership của group.
- Không tin group hoặc user ID client yêu cầu tùy ý.
- Gửi event sau khi transaction thành công.
- Có event ID chống duplicate.
- Có fallback nếu realtime delivery thất bại.

### Frontend

- Khởi tạo kết nối sau đăng nhập hợp lệ.
- Tự reconnect.
- Disconnect khi logout.
- Không kết nối bằng quyền nghiệp vụ khi chưa đổi mật khẩu lần đầu.
- Cập nhật notification badge.
- Hiển thị popup/toast.
- Invalidate query liên quan.
- Không reload toàn bộ trang khi có một notification.
- Không tạo popup trùng.

### Ví dụ

HK Staff báo thiết bị hỏng.

HK Supervisor đang mở website nhận thông báo realtime.

Khi HK Supervisor tạo phiếu bảo trì, Maintenance Supervisor nhận thông báo.

Khi Maintenance Staff được phân công, nhân viên nhận thông báo.

Khi sửa chữa hoàn thành, người có quyền nghiệm thu nhận thông báo.

Khi Storekeeper nhận phiếu điều chuyển, trang kho cập nhật dữ liệu liên quan.

SignalR chỉ là cơ chế truyền sự kiện, không thay thế authorization và business transaction.

---

## 31. AI INTEGRATION

Theo Note mới, bổ sung AI hỗ trợ:

1. Dự báo nhu cầu vật tư.
2. Đề xuất điều chuyển giữa chi nhánh.
3. Phân tích sự cố.

AI là công cụ hỗ trợ quyết định, không phải người phê duyệt nghiệp vụ.

Không cho AI tự sửa tồn kho, tạo stock movement hoặc tự thay đổi trạng thái phòng.

### 31.1. AI Demand Forecast

Nguồn dữ liệu:

- Lịch sử cấp vật tư.
- Lịch sử sử dụng thực tế.
- Lịch sử hoàn trả.
- Tồn khả dụng.
- Minimum Stock.
- Số phòng cần dọn.
- Room capacity.
- Xu hướng sử dụng.
- Hạn sử dụng.

Kết quả:

- Nhu cầu dự kiến theo mặt hàng.
- Khoảng thời gian dự báo.
- Lượng tồn khả dụng.
- Nguy cơ thiếu hụt.
- Số lượng đề xuất bổ sung.
- Lý do đề xuất.
- Mức độ tin cậy hoặc giới hạn dữ liệu nếu phù hợp.

Không bịa dữ liệu lịch sử.

Không trình bày dự báo như dữ liệu tồn thực tế.

Nếu lịch sử chưa đủ, hiển thị cảnh báo dữ liệu hạn chế.

### 31.2. AI Transfer Recommendation

Phân tích:

- Chi nhánh thiếu hàng.
- Chi nhánh có hàng dư.
- Tồn khả dụng.
- Minimum Stock hai bên.
- Nhu cầu dự kiến.
- Hạn sử dụng.
- Yêu cầu đang chờ.
- Hàng đang điều chuyển.

Đề xuất:

- Chi nhánh nguồn.
- Chi nhánh đích.
- Mặt hàng.
- Số lượng.
- Lý do.

Không đề xuất hàng hết hạn.

Không đề xuất chuyển vượt lượng khả dụng.

Không tự động tạo Stock Transfer thành công.

OM có thể xem đề xuất và tạo yêu cầu điều chuyển.

Workflow phê duyệt và nhận hàng vẫn giữ nguyên.

### 31.3. AI Incident Analysis

Hỗ trợ:

- Tóm tắt mô tả sự cố.
- Gợi ý nhóm sự cố.
- Gợi ý mức độ ưu tiên để người xử lý xem xét.
- Tìm các sự cố tương tự.
- Phân tích thiết bị hỏng lặp lại.
- Gợi ý hướng xử lý chuyên môn để đội bảo trì đánh giá.

Không tự xác định khách hoặc nhân viên là người gây hỏng.

Không tự đóng Incident.

Không tự nghiệm thu kỹ thuật.

Không tự đưa phòng Ready.

Đối với sự cố ảnh hưởng an toàn, AI chỉ hỗ trợ thông tin, không thay thế kiểm tra chuyên môn.

### 31.4. AI architecture

Tạo abstraction phù hợp:

- AI Provider Interface.
- Demand Forecast Service.
- Transfer Recommendation Service.
- Incident Analysis Service.

Không bắt buộc tên class trên.

Kiểm tra provider hiện có trước khi chọn công nghệ.

Không tự mua dịch vụ hoặc sử dụng API key ngoài phạm vi được cấp.

Credentials phải nằm trong secret/configuration an toàn.

Không commit API key.

Không gửi dữ liệu cá nhân không cần thiết đến AI provider.

### 31.5. Fallback

Nếu chưa có AI API key hoặc provider:

- Có thể triển khai giao diện, abstraction và contract.
- Có thể triển khai dự báo thống kê/rule-based riêng, ghi nhãn đúng.
- Không giả lập AI response rồi báo AI đã hoạt động.
- Không gọi rule-based recommendation là AI integration đã hoàn thành.
- Báo BLOCKED hoặc PARTIAL đối với external AI runtime nếu thiếu credentials.

Tách rõ việc chưa có AI provider với khả năng vận hành bình thường của HotelOps.

---

## 32. EXTERNAL SYSTEM ARCHITECTURE

Cập nhật sơ đồ kiến trúc hệ thống và External System trong tài liệu.

Các thành phần dự kiến:

- React Frontend.
- ASP.NET Core Web API.
- SQL Server.
- Authentication/Authorization.
- SignalR Hub.
- Notification Service.
- Email Provider hoặc SMTP Service.
- AI Provider.
- Background Processing nếu source sử dụng.

Phân biệt:

- SignalR Hub là thành phần ứng dụng.
- Email Provider là dịch vụ tích hợp bên ngoài nếu được triển khai như vậy.
- AI Provider là dịch vụ bên ngoài nếu sử dụng API của nhà cung cấp.
- Đơn vị sửa chữa bên ngoài là tác nhân nghiệp vụ thực tế, không nhất thiết là hệ thống phần mềm được tích hợp.

Không vẽ thêm External System không tồn tại chỉ để làm sơ đồ phức tạp hơn.

Mọi công nghệ được ghi trong Report 4 phải phản ánh implementation thực tế hoặc được đánh dấu planned.

---

## 33. FRONTEND CHANGE REGISTER

### OM

Cập nhật:

- Dashboard.
- Lịch làm việc.
- Công việc và yêu cầu.
- Cấu hình định mức.
- Tồn kho.
- Vật tư sắp hết hạn.
- Yêu cầu bổ sung kho.
- Điều chuyển liên chi nhánh.
- Đề xuất thay mới.
- AI Recommendation.
- Báo cáo.

OM không có chức năng báo cáo sự cố.

OM không có nút duyệt các yêu cầu liên bộ phận thông thường.

### HK Supervisor

Cập nhật:

- Phân công dọn phòng.
- Kiểm tra vật tư nhân viên.
- Nhu cầu bổ sung thực tế theo phòng.
- Checklist.
- Ghi nhận và xác minh sự cố.
- Yêu cầu khắc phục.
- Kiểm tra lại phòng.
- Rework.
- Nguyên nhân sự cố.

### HK Staff

Cập nhật:

- Công việc dọn phòng.
- Nhận phiếu.
- Checklist hoàn thành.
- Room Supply Assessment.
- Vật tư tiêu hao đang giữ.
- Vật tư thay thế đang giữ.
- Yêu cầu bổ sung.
- Ghi nhận sự cố phòng.
- Ghi chú.
- Đề nghị bàn giao theo policy hiện hành.

### Maintenance Supervisor

Cập nhật:

- Dashboard bảo trì.
- Phiếu cần xử lý.
- Phân công kỹ thuật viên.
- Theo dõi tiến độ.
- Nghiệm thu.
- Đề xuất thay mới.
- Lịch sử sửa chữa.
- Rework.

### Maintenance Staff

Giữ hoặc khôi phục:

- Công việc được giao.
- Xem phiếu.
- Nhận phiếu.
- Bắt đầu.
- Checklist.
- Ghi chú.
- Hoàn thành.
- Đề xuất không thể sửa.
- Kết quả nghiệm thu.
- Công việc cần sửa lại.

### Storekeeper

Cập nhật:

- Danh mục phân loại vật tư.
- Tồn kho tiêu hao.
- Quản lý lô.
- Hạn sử dụng.
- FEFO/FIFO.
- Tồn khả dụng.
- Vật tư tái sử dụng theo trạng thái.
- Vật tư cần giặt.
- Vật tư hỏng.
- Phiếu xuất tiêu hao.
- Chứng từ luân chuyển.
- Phiếu điều chuyển.
- Requested Quantity.
- Shipped Quantity.
- Received Quantity.
- Discrepancy.
- Cảnh báo thiếu hàng.
- Yêu cầu bổ sung.

### Reception

Giữ workflow phòng hiện hành.

Cập nhật:

- Ghi nhận sự cố phòng.
- Theo dõi sự cố.
- Xem trạng thái khắc phục.
- Xem khả năng sử dụng phòng.

### Administrator

Cập nhật:

- Quản lý 9 role mục tiêu nếu được xác nhận.
- Tạo tài khoản.
- Trạng thái lời mời email.
- Gửi lại lời mời.
- Trạng thái đổi mật khẩu lần đầu.
- Role và branch assignment.

### Owner

Giữ Global Read-only.

Bổ sung báo cáo trong phạm vi quyền:

- Tiêu hao theo chi nhánh.
- Vật tư sắp hết hạn.
- Vật tư tái sử dụng.
- Tồn khả dụng.
- Điều chuyển.
- Sự cố.
- Hiệu suất bảo trì.
- Đề xuất AI.

---

## 34. UI/UX PRESERVATION

Giữ Design System HotelOps đã được thống nhất.

- Lucide icons.
- Permission-based sidebar.
- Data table thống nhất.
- Search.
- Sorting.
- Pagination.
- Filter mặc định ẩn.
- Modal/Popup cho thao tác.
- Drawer cho chi tiết.
- Loading/Empty/Error state.
- Notification popup khi có thay đổi.

Không redesign các trang không thuộc phạm vi thay đổi.

Đối với yêu cầu chỉ có trong Note cũ như:

- Nút Hủy màu đỏ.
- Calendar View.
- Màu ca làm việc.
- Deadline xin nghỉ.
- Giới hạn số người nghỉ cùng ca.
- Đổi nhãn Room Block.
- KPI đặt đầu Dashboard.

Kiểm tra xem đây có phải chức năng đã triển khai hoặc đã được Owner phê duyệt độc lập hay không.

Nếu đã triển khai hợp lệ, bảo toàn.

Nếu chưa triển khai và không nằm trong Note mới, đưa vào danh sách REVIEW_REQUIRED thay vì tự mở rộng scope.

---

## 35. DATABASE SCHEMA DELTA

Kiểm tra khả năng tái sử dụng schema hiện tại.

Có thể cần bổ sung các cấu trúc logic:

- Stock Batch.
- Batch Expiry.
- Staff Supply Allocation.
- Room Supply Assessment.
- Supply Usage.
- Reusable Item Balance.
- Reusable Item Movement.
- Item Condition.
- Item Location.
- Laundry Status History.
- Write-off Request.
- Transfer Request.
- Transfer Line.
- Transfer Shipment.
- Transfer Receipt.
- Transfer Discrepancy.
- Checklist Template.
- Checklist Result.
- Incident Cause History.
- Equipment Replacement Request.
- Account Invitation.
- Password Change Requirement.
- Notification Event.
- AI Recommendation History nếu cần.

Không bắt buộc tạo tất cả thành bảng riêng.

Thiết kế tối thiểu cần thiết dựa trên source.

Không lưu mật khẩu rõ.

Không bịa Batch Number hoặc Expiry Date.

Không làm mất inventory balance cũ.

Không tạo tồn kho giả để kiểm thử thành công.

### Migration

- Backup.
- Restore validation.
- Fixture migration.
- FK validation.
- Before/after comparison.
- Transaction.
- Rollback/recovery.
- Inventory reconciliation.
- Role reconciliation.

Chỉ thực hiện migration vào đúng database test sau khi xác minh an toàn và có phạm vi thực thi được phép.

Không chạy migration vào production.

---

## 36. TRANSACTION & CONCURRENCY

Các workflow sau phải có transaction boundary rõ:

- Start Shift allocation.
- Cleaning supply reservation.
- Cleaning completion.
- Consumable refill.
- Reusable replacement.
- End Shift return.
- Write-off.
- Stock transfer shipment.
- Stock transfer receipt.
- Maintenance completion.
- Reinspection.
- Password change.

Phải chống:

- Âm tồn kho.
- Âm lượng sẵn sàng.
- Âm staff balance.
- Double allocation.
- Double consumption.
- Double return.
- Double fulfillment.
