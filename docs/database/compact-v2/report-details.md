## C. Simplified Business Workflows

### C1. Reception, housekeeping và inspection

Checkout/chuyển occupancy ra → phòng DIRTY, tăng turnover, xóa con trỏ bằng chứng cleaning cũ → tạo và giao cleaning → Start/tiến độ/Pause/Resume ghi khoảng thời gian → Complete đóng khoảng, gửi inspection → HK Supervisor kiểm tra độc lập → đạt thì đủ điều kiện READY nếu không có block; không đạt thì findings → rework → reinspection. Check-in chỉ khi readiness hiệu lực READY; occupancy vẫn là một thông tin riêng. Inspection không được đổi phòng OCCUPIED thành READY.

Nguồn đối chiếu legacy: `Services/Rooms/EffectiveRoomReadiness.cs`, `RoomCheckInService.cs`, các controller RoomDeparture và Change Notes ngày 20/11. V2 dùng turnover rõ ràng; thuật toán/giao dịch Reception hoàn chỉnh sẽ phải được port và test khi chuyển backend. View readiness không tự cấp quyền hoặc ghi check-in/check-out.

### C2. Incident và maintenance

Báo cáo một incident → phân loại/triage/priority/deadline → giao maintenance có parent incident → thực hiện với evidence, thời gian và progress → gửi kết quả → người khác xác minh → đóng incident hoặc reopen có lý do. Room block là điều kiện riêng; không tự bỏ block chỉ vì maintenance ghi Complete. Preventive occurrence vẫn có lịch/mẫu công việc, không đồng nghĩa lịch ca nhân sự.

### C3. Material/inventory/replenishment

Request → DH duyệt số lượng theo dòng → Storekeeper khác người duyệt cấp một phần/toàn phần → ledger ghi số lượng thực tế, view tính thiếu → liên kết replenishment khi cần → nhận từng phần → cập nhật khả năng cấp phát. Kiểm kê/đối chiếu/tách người duyệt điều chỉnh vẫn giữ; sửa giao dịch đã post bằng reversal có nguồn, không sửa ledger. Backend phải chọn đúng DH và ngoại lệ duyệt đã phê duyệt; database không tự cấp role.

### C4. Operational Task

Create → Assign → Accept → Start → In Progress → Complete/Pending Verification → Verify/Closed. Pause/Resume/Reopen ghi reason và lịch sử; thời gian gắn nhiệm vụ. Người làm không tự xác minh. Không còn check-in ngày làm việc trước khi nhận nhiệm vụ.

### C5. Internal Request và backlog

Create → Route → Accept/Reject → Process → Complete → Confirm/Closed hoặc Reopen. Phòng ban gửi/nhận còn riêng. Chỉ tạo linked work cần thiết; liên kết `DEPENDS_ON` chặn Complete khi việc liên quan chưa đạt. Bàn giao backlog có `HANDOVER_OFFERED` gửi người cụ thể; đúng recipient mới được `HANDOVER_ACCEPTED`/`HANDOVER_REJECTED`, chỉ một quyết định. Đây chưa phải phê duyệt bỏ UC-140.

### C6. Notifications, dashboard, reports

Sự kiện cần hành động → outbox/notification có recipient/event dedup → truy cập đối tượng có kiểm quyền → read/ack. Các cảnh báo assignment, pending approval, failed inspection, severe incident, overdue và low stock được giữ ở thiết kế. Dashboard dùng readiness, workload/duration, incident và ledger/material remainder; báo cáo/audit không nhân đôi nguồn số liệu. Việc lọc loại thông báo cần làm trong backend, không âm thầm xóa dữ liệu hoặc alert trong DB.

## D. Removed/Reduced Functional Scope

| Nhóm FE | Loại bỏ/giảm | Gộp, tự động hóa | Bắt buộc giữ và ảnh hưởng liên phòng ban |
|---|---|---|---|
| FE-01 Auth/RBAC | Giảm màn hình thao tác lặp; không bỏ UC kiểm soát | Hồ sơ user/role/scope chung | Effective grants, scope, last admin, session, audit; mọi bộ phận |
| FE-02 Structure | Không đề xuất bỏ thực thể cốt lõi | Tầng/area chung locations; danh mục chung | Dependency trước deactivation; phạm vi phòng/asset/kho |
| FE-03 Rooms | Giảm chỉnh status tay | Sinh trạng thái từ sự kiện | Occupancy/readiness, block, turnaround, Reception/HK/maintenance |
| FE-04 Housekeeping | Bỏ bước nhập giờ trùng, không bỏ execution | Create+assign, complete+submit | Assignee, blocker, time, evidence; HK Staff chịu trách nhiệm |
| FE-05 Inspection | Giảm xác nhận trung gian | Checklist/version/result chung | Supervisor độc lập; findings/rework/reinspection; chỉ đạt mới Ready |
| FE-06 Incident/Maintenance | Giảm nhập lại incident trong maintenance | Một hồ sơ nguồn + phần thực hiện | Priority/SLA, assigned technician, verify/reopen/evidence, block |
| FE-07 Asset | Không có yêu cầu bỏ UC tài sản | Profile/history/condition/custody cùng hồ sơ | Transfer/handover/retire có dependency và lịch sử |
| FE-08 Inventory | Giảm nhập lại chứng từ nguồn | Header/line/ledger chung | Tồn, kiểm kê, approved adjustment, reversal, Storekeeper/phê duyệt độc lập |
| FE-09 Materials | Giảm các bước nhập/chuyển phiếu | Request→approve→issue, tính thiếu | DH duyệt khác người cấp; partial fulfillment, traceability |
| FE-10 Replenishment | Giảm module độc lập phức tạp; không xóa UC-100/101 | Chứng từ liên quan trong kho | Nguồn yêu cầu, thiếu hàng, nhiều lần nhận |
| FE-11 Operational Task | Giảm nhập giờ thủ công; pause/reopen có lý do | Start/complete tự ghi interval | Accept/verify, assignee, deadlines; không đồng nhất Internal Request |
| FE-12 Work-time | UC-138/139 PROPOSE_REMOVE; UC-140 NEEDS_APPROVAL | Khoảng giờ theo task; backlog reception | Không bỏ lịch sử thực hiện hoặc xác nhận handover khi chưa duyệt |
| FE-13 Internal Requests | Giảm chuyển giao không thêm giá trị | Route/accept/process/confirm rõ ràng | Sender/receiver departments, linked-work completion, reopen/escalation |
| FE-14 Notification/Dashboard | Giảm trùng lặp, không bỏ mandatory alerts | Dedup recipient/event, dùng chung projections | Readiness/workload/incident/inventory, quyền xem khi mở đối tượng |
| FE-15 Reports/Audit | Chỉ giảm báo cáo/KPI trùng sau rà soát | Tái sử dụng nguồn read models | Audit/history bắt buộc bất biến, báo cáo vận hành |

Không chuyển dữ liệu từ database cũ. Với dữ liệu lịch sử workday/shift/correction đang tồn tại ở HotelOpsDB, cần quy trình lưu trữ/đối chiếu được duyệt nếu sau này cắt chuyển; không được xóa chúng chỉ vì schema v2 không có các bảng đó.

## E. Preserved Critical Business Rules

1. Không cấp thêm quyền, không gộp Department Head với quản lý duy nhất một phòng ban, không bỏ giới hạn branch/department.
2. Không dùng Ready lưu trên room thay cho inspection hiện hành, turnover và block. Departure làm bằng chứng chu kỳ trước hết hiệu lực.
3. Người dọn/rework không tự xác minh inspection; người thực hiện task/maintenance không tự verify; incident closure phải độc lập với người sửa.
4. Passed inspection cần đủ tiêu chí bắt buộc và không còn finding chưa xử lý trong chu kỳ; kết quả chốt bất biến. Không bỏ rework hoặc historical evidence.
5. Complete và Verify là hai ý nghĩa khác nhau. Không đóng Internal Request khi `DEPENDS_ON` còn chưa hoàn tất/verified.
6. Không cần workday nhưng vẫn cần assignment đúng người/branch/work; mỗi người chỉ có một interval đang mở; không overlap, không sửa/xóa khoảng đã chốt. Correction ngoại lệ cần hợp đồng audit và phê duyệt riêng, chưa triển khai bằng bảng khác.
7. Người cấp vật tư khác người duyệt; dùng đúng source document/line; lượng cấp không vượt lượng duyệt hoặc tồn thực tế. Ledger append-only; reversal trỏ giao dịch gốc và không đảo trùng.
8. Notifications không bỏ cảnh báo bắt buộc; dedup không thay authorization. Audit và lịch sử domain không bị cắt để giảm số bảng.
9. Giữ xác nhận backlog handover; chưa có quyết định cho phép bỏ tất cả xác nhận bàn giao.
10. Constraint/trigger bảo vệ dữ liệu không thay cho service authorization, đầy đủ state machine, dependency khi deactivate, last-admin và giao dịch nghiệp vụ. Các kiểm tra đó vẫn là yêu cầu backend khi chuyển sang v2.

## F. Impact by Role

| Vai trò | Trách nhiệm sau giản lược | Ranh giới cần giữ |
|---|---|---|
| Administrator | Hồ sơ user, danh mục, role/scope | Không mặc định có quyền tác nghiệp; bảo vệ admin cuối |
| Reception | Nhận/trả/chuyển occupancy theo điều kiện | Không thực hiện cleaning/rework, không tự xác minh Ready |
| HK Staff | Nhận, thực hiện, pause/resume, hoàn thành cleaning/rework | Không cần workday; không tự pass inspection |
| HK Supervisor | Phân công, inspection, rework/reinspection | Kiểm tra độc lập với người làm; evidence/checklist vẫn giữ |
| Maintenance Staff | Công việc sửa chữa, evidence, execution time | Không tự verify/close phần mình làm |
| Maintenance Lead | Triage/phân công/xác minh kỹ thuật trong scope | Không tự xác minh khi đã là performer |
| Department Head | Điều phối bộ phận trong branch, phê duyệt vật tư | Không mở scope toàn hệ thống; không tự approve request của mình |
| Storekeeper | Kiểm tồn và cấp/nhập/hoàn/chuyển theo chứng từ | Không thay người phê duyệt; không sửa ledger đã post |
| Operations Manager | Escalation, ngoại lệ/adjustment/reversal theo quyền | Không mở rộng role do gộp màn hình/quy trình |
| Owner | Xem dashboard/report được phép | Không tự có quyền mutation |
| Nhân viên gửi/nhận Internal Request | Create, tiếp nhận, xử lý, confirm/reopen | Giữ phòng ban gửi/nhận, linked work và lịch sử trách nhiệm |

## G. Approval Required

- Quyết định cuối cùng bỏ UC-138/139 trên sản phẩm và cách giữ dữ liệu ca/ngày làm việc lịch sử. Schema mới là phương án kỹ thuật theo yêu cầu hiện tại, chưa thay sản phẩm đang chạy.
- UC-140: mức xác nhận bàn giao bắt buộc, ai được từ chối/thu hồi; hiện vẫn giữ cơ chế xác nhận backlog.
- Danh sách KPI/report/notification cụ thể được bỏ, retention và các alert bắt buộc; không lấy từ “giảm trùng” để xóa hàng loạt.
- Exception manual room correction, occupancy reversal/cross-branch move và các phần Permission Matrix còn pending trong Change Notes.
- Hợp đồng điều chỉnh giờ đã chốt khi không còn FE-12; không sửa interval bất biến hoặc tự cấp quyền sửa giờ.
- Nếu thay state machine/status ngoài các giá trị hiện có, cần thống nhất contract và cập nhật backend/tests, không tự suy ra quyền từ tên vai trò.

## H. Database và kiểm chứng

Xem `docs/database/compact-v2/README.md` để biết 37 bảng, thay đổi cột/FK, script cài đặt, kiểm thử và giới hạn. Lượt thiết kế database ban đầu không thay code ứng dụng. Theo yêu cầu tiếp theo “Chuyển đổi Backend theo database mới”, backend tại `src/HotelOps.Api/Compact` đã được scaffold lại và nối vào ứng dụng với schema 2.0.0; API mới dùng `/api/v2`. Hợp đồng, hướng dẫn chạy và kiểm chứng riêng cho backend nằm tại `docs/database/compact-v2/BACKEND.md`. Các quyết định nghiệp vụ pending trong báo cáo này không tự trở thành quyền tác nghiệp.
