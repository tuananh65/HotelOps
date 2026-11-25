# HOTELOPS PROJECT 2 — P0 BUSINESS RESTRUCTURING V2

## NOTE 24/11/2026 — REQUIREMENT ALIGNMENT & FULLSTACK DELTA IMPLEMENTATION

---

## 1. ROLE AND MISSION

Bạn là Principal Software Architect, Senior Fullstack Engineer, Database Engineer, Business Analyst và Integration Owner chịu trách nhiệm hoàn thiện HotelOps Project 2.

Nhiệm vụ là cập nhật dự án hiện tại theo yêu cầu mới nhất trong tài liệu Note 24_11.docx và những quyết định nghiệp vụ còn hiệu lực đã được Project Owner xác định.

Đây là nhiệm vụ DELTA IMPLEMENTATION.

Không xây dựng lại toàn bộ dự án.

Không chỉ sửa giao diện.

Không chỉ lập kế hoạch.

Phải triển khai thực tế các thay đổi cần thiết trên:

- Database.
- Backend.
- Frontend.
- API.
- Authentication.
- Authorization.
- Business Workflows.
- Inventory.
- Housekeeping.
- Maintenance.
- Workday.
- Internal Request.
- SignalR.
- Email Integration.
- AI Integration.
- Notification.
- Audit.
- Automated Tests.
- E2E Tests.
- SRS và Software Design Documentation.

Mục tiêu là hệ thống phù hợp với khách sạn quy mô lớn, có nhiều chi nhánh, đội bảo trì riêng, quản lý được vật tư tiêu hao và vật tư tái sử dụng, đồng thời bảo đảm các quy trình liên bộ phận hoạt động xuyên suốt.

---

## 2. PROJECT TARGET

Project: HotelOps Project 2 — Legacy UX Redesign / CompactDB.

Database dự kiến: `HotelOpsCompactDB`.

SQL Server dự kiến: `DESKTOP-JI8U50T\SQLEXPRESS`.

Frontend: React / TypeScript.

Backend: ASP.NET Core.

Database: SQL Server.

Chi nhánh:

- DVBV — Bùi Viện.
- DVCT — Cao Thắng.

Xác minh target thực tế trước khi triển khai:

- Repository.
- Working directory.
- Git branch.
- HEAD commit.
- Git status.
- Database connection.
- Database schema.
- Environment.

Không tác động HotelOps Project 1/Rebuild.

Không mặc định rằng các prompt trước đã được triển khai thành công.

Phải kiểm tra source và database thực tế.

---

## 3. REQUIREMENT PRIORITY

Thứ tự ưu tiên:

1. Các yêu cầu mới trong Note 24_11.docx.
2. Những quyết định trong Master Prompt này.
3. Các quyết định nghiệp vụ trước đây không xung đột với Note mới.
4. SRS và tài liệu hiện hành đối với phần không bị thay đổi.
5. Source code hiện tại làm bằng chứng về implementation thực tế.

Đặc biệt, hủy bỏ các chỉ đạo trước đây về:

- Loại bỏ Maintenance Staff.
- Chuyển Maintenance Staff thành HK Staff.
- Chỉ sử dụng dịch vụ sửa chữa bên ngoài.
- Tự động trừ toàn bộ đồ fill theo sức chứa tối đa mỗi phòng bất kể số lượng hiện có.
- Ghi giảm tổng tồn kho đối với vật tư tái sử dụng chỉ vì đã cấp vào phòng.
- Sử dụng phiếu xuất tiêu hao cho vật tư cứng hoặc vật tư tái sử dụng.
- Không bắt buộc đổi mật khẩu lần đầu đối với tài khoản mới.

Không thực hiện lại các chỉ đạo cũ nếu chúng mâu thuẫn với thiết kế mới.

Các nội dung Note mới không quy định rõ phải được ghi vào Decision Register. Không tự biến phương án kỹ thuật đề xuất thành quyết định nghiệp vụ đã được Owner phê duyệt.

---

## 4. PHASE 0 — CURRENT SYSTEM AUDIT

Trước khi sửa code, thực hiện audit toàn bộ module bị ảnh hưởng.

### Database Audit

Kiểm tra:

- Role definitions.
- Permission definitions.
- User-role assignments.
- Employment Department.
- Branch memberships.
- Workday.
- Room.
- Room capacity.
- Cleaning Task.
- Inspection.
- Checklist.
- Incident.
- Maintenance.
- Asset.
- Inventory.
- Stock balance.
- Item master.
- Stock batches.
- Expiry dates.
- Material Request.
- Stock Transfer.
- Replenishment.
- Internal Request.
- Authentication.
- Notification.
- Audit.

### Frontend Audit

Kiểm tra:

- Sidebar.
- Routes.
- Permission guards.
- Dashboard.
- Room Status.
- Cleaning Assignment.
- Cleaning Execution.
- Maintenance Work.
- Incident Detail.
- Inventory.
- Material Request.
- Stock Transfer.
- User Management.
- Login.
- Change Password.
- Notification.

### Backend Audit

Kiểm tra:

- Controllers.
- API contracts.
- Domain entities.
- Services.
- Repositories.
- Authentication.
- Authorization.
- Branch scope.
- Transaction boundary.
- Stock writers.
- Concurrency.
- Notification publishers.
- Email infrastructure.
- SignalR infrastructure.
- AI infrastructure nếu đã có.

Chạy baseline build và tests trước khi sửa.

Ghi nhận lỗi có sẵn để phân biệt với regression.

Tạo:

`docs/restructure-v2/CURRENT_SYSTEM_AUDIT.md`

---

## 5. CHANGE IMPACT REGISTER — 40 NHÓM THAY ĐỔI

Tạo:

`docs/restructure-v2/BUSINESS_CHANGE_IMPACT_MATRIX.md`

Mỗi Change ID phải có:

- Current implementation.
- Source evidence.
- Target implementation.
- Gap.
- Action.
- Database impact.
- Backend impact.
- Frontend impact.
- API impact.
- Security impact.
- Test evidence.

Phân loại:

KEEP / MODIFY / ADD / REMOVE / MERGE / MIGRATE / REPLACE / VERIFIED / BLOCKED.

### GROUP A — ACTOR & AUTHORIZATION

| ID | Baseline cần kiểm tra | Thiết kế mục tiêu |
|---|---|---|
| CHG-01 | OM và DH độc lập hoặc đã gộp | Một role OM |
| CHG-02 | Department Head quản lý lịch và công việc | OM tiếp quản |
| CHG-03 | OM/DH phê duyệt yêu cầu liên bộ phận | OM chỉ xem yêu cầu thông thường |
| CHG-04 | Maintenance Staff bị loại bỏ hoặc đang hoạt động | Giữ hoặc khôi phục Maintenance Staff |
| CHG-05 | Maintenance Lead | Maintenance Supervisor quản lý đội kỹ thuật |
| CHG-06 | Chỉ giám sát sửa chữa bên ngoài | Đội bảo trì nội bộ, dịch vụ ngoài khi cần |
| CHG-07 | OM có quyền báo cáo sự cố | OM không tạo báo cáo sự cố |

### GROUP B — HOUSEKEEPING & MAINTENANCE

| ID | Baseline cần kiểm tra | Thiết kế mục tiêu |
|---|---|---|
| CHG-08 | Sự cố trong quá trình vệ sinh | Ghi nhận sự cố phòng |
| CHG-09 | Nguyên nhân sự cố chưa xác định rõ | Phân loại nguyên nhân sau kiểm tra |
| CHG-10 | Cleaning Completion chưa có checklist đầy đủ | Checklist hoàn thành bắt buộc |
| CHG-11 | Maintenance Completion chưa có checklist | Checklist kỹ thuật và nghiệm thu |
| CHG-12 | Phiếu công việc chưa xuyên suốt | Tạo → Xem → Nhận → Xử lý → Hoàn thành |
| CHG-13 | Xác nhận xử lý thiếu ghi chú | Bổ sung ghi chú |
| CHG-14 | Chỉ có xử lý sửa chữa | Bổ sung nhánh đề xuất thay mới |
| CHG-15 | Room Ready có nhiều writer độc lập | Room Readiness Policy thống nhất |
| CHG-16 | Rework chưa đồng nhất | Trả về đúng bộ phận, bắt buộc lý do |

### GROUP C — SUPPLY & INVENTORY

| ID | Baseline cần kiểm tra | Thiết kế mục tiêu |
|---|---|---|
| CHG-17 | Cấp đồ fill đầu ca | Giữ cấp đầu ca theo định mức |
| CHG-18 | Trừ toàn bộ theo sức chứa phòng | Chỉ bổ sung số lượng thiếu |
| CHG-19 | Chưa kiểm tra vật tư hiện có trong phòng | Room Supply Assessment |
| CHG-20 | Phân công chưa kiểm tra vật tư khả dụng | Kiểm tra và giữ chỗ theo nhu cầu thực tế |
| CHG-21 | Quản lý vật tư không theo lô | Stock Batch/Lot |
| CHG-22 | Chưa quản lý hạn sử dụng đầy đủ | Expiry Date và kiểm tra hàng hết hạn |
| CHG-23 | Xuất kho theo mặt hàng tổng hợp | FIFO/FEFO theo lô |
| CHG-24 | Dùng chung phiếu xuất cho mọi loại vật tư | Phiếu xuất tiêu hao chỉ dùng cho vật tư tiêu hao |
| CHG-25 | Đồ tái sử dụng cấp vào phòng làm giảm tổng tồn | Theo dõi vị trí và trạng thái |
| CHG-26 | Chưa quản lý vật tư cần giặt là | Quản lý Ready, In Use, Laundry, Damaged |
| CHG-27 | Vật tư hỏng chưa có nghiệp vụ ghi giảm rõ | Write-off có xác nhận và audit |
| CHG-28 | Thiết bị điện tử lưu kho dự phòng | Không quản lý như vật tư dự phòng thông thường |
| CHG-29 | Cảnh báo thiếu theo tổng tồn | Cảnh báo theo lượng có thể cấp thực tế |
| CHG-30 | Hoàn trả vật tư cuối ca theo cơ chế cũ | Hoàn trả chính xác theo loại, lô và trạng thái |

### GROUP D — INTER-BRANCH TRANSFER

| ID | Baseline cần kiểm tra | Thiết kế mục tiêu |
|---|---|---|
| CHG-31 | Chưa có điều chuyển liên chi nhánh đầy đủ | Workflow điều chuyển |
| CHG-32 | Phiếu điều chuyển thiếu trường | Requested, Approved, Shipped, Received Quantity |
| CHG-33 | Chưa xử lý chênh lệch thực nhận | Discrepancy, lý do và đối soát |
| CHG-34 | Branch scope cản trở điều chuyển hoặc mở quá rộng | Quyền nguồn/đích có giới hạn |

### GROUP E — TECHNOLOGY & ACCOUNT

| ID | Baseline cần kiểm tra | Thiết kế mục tiêu |
|---|---|---|
| CHG-35 | Tạo tài khoản chưa gửi email tự động | Email mời đăng nhập |
| CHG-36 | Tài khoản mới không bắt buộc đổi mật khẩu | First Login Password Change |
| CHG-37 | Notification chỉ polling hoặc refresh | SignalR realtime |
| CHG-38 | Chưa có AI dự báo vật tư | AI Demand Forecast |
| CHG-39 | Chưa có AI đề xuất điều chuyển/phân tích sự cố | AI Advisory |
| CHG-40 | Sơ đồ hệ thống chưa thể hiện external services | External Systems và Architecture Documentation |

Không mặc định 40 thay đổi đều chưa triển khai.

Nếu source đã đáp ứng, đánh dấu VERIFIED và không sửa lại.

---

## 6. ACTOR RESTRUCTURING V2

### 6.1. Actor mục tiêu đề xuất

1. SYS_ADMIN.
2. OWNER.
3. OPERATION_MANAGER.
4. HOUSEKEEPING_SUPERVISOR.
5. HK_STAFF.
6. MAINTENANCE_SUPERVISOR.
7. MAINTENANCE_STAFF.
8. STOREKEEPER.
9. RECEPTION.

Tổng cộng 9 Actor nghiệp vụ nếu giữ phương án gộp OM/DH và duy trì hai cấp bảo trì.

Note mới yêu cầu giữ đội bảo trì nhưng chưa khóa tên role cụ thể. Có thể tái sử dụng role code MAINTENANCE_LEAD nếu đổi tên vật lý gây ảnh hưởng không cần thiết; thống nhất tên hiển thị Maintenance Supervisor.

### 6.2. Gộp Department Head vào OM

Nếu chưa thực hiện:

DEPT_MANAGER → OPERATION_MANAGER.

OM tiếp quản:

- Lập lịch tuần.
- Phân ca.
- Quản lý nguồn lực.
- Quản lý Operational Tasks.
- Giám sát vận hành.
- Quản lý tài sản.
- Cấu hình vật tư.
- Cấu hình minimum stock.
- Xử lý yêu cầu bổ sung kho.
- Tạo và xử lý yêu cầu điều chuyển theo vai trò nguồn/đích.
- Báo cáo.

OM không tạo báo cáo sự cố.

OM chỉ xem yêu cầu liên bộ phận thông thường.

Không phê duyệt trung gian mọi yêu cầu.

### 6.3. Giữ hoặc khôi phục đội bảo trì

Maintenance Supervisor:

- Tiếp nhận phiếu bảo trì.
- Kiểm tra sự cố kỹ thuật.
- Phân công Maintenance Staff.
- Theo dõi tiến độ.
- Kiểm tra kết quả.
- Nghiệm thu kỹ thuật.
- Đề xuất thay mới khi không thể sửa.
- Điều phối sửa chữa bên ngoài khi cần.

Maintenance Staff:

- Nhận phiếu sửa chữa.
- Xem thiết bị.
- Bắt đầu công việc.
- Thực hiện sửa chữa.
- Cập nhật ghi chú.
- Thực hiện checklist.
- Hoàn thành công việc.
- Gửi nghiệm thu.
- Xử lý công việc bị trả lại.

### 6.4. Recovery nếu migration cũ đã được thực hiện

Nếu Maintenance Staff đã bị chuyển thành HK Staff:

- Kiểm tra backup.
- Kiểm tra migration history.
- Kiểm tra audit.
- Xác định chính xác danh sách User ID bị chuyển đổi.
- Khôi phục role và department membership hợp lệ.
- Khôi phục các quyền kỹ thuật được phê duyệt.
- Xử lý lịch tương lai bị ảnh hưởng.
- Thu hồi session chứa quyền không còn hợp lệ.

Không chuyển ngẫu nhiên HK Staff thành Maintenance Staff.

Không khôi phục bằng cách restore toàn bộ database cũ làm mất nghiệp vụ phát sinh sau migration.

Không tạo tài khoản mới thay thế tài khoản cũ.

Nếu không đủ bằng chứng về danh sách nhân viên gốc, ghi blocker và yêu cầu Owner xác nhận riêng phần migration đó.

---

## 7. ROLE RESPONSIBILITY MATRIX

| Nghiệp vụ | Actor chính |
|---|---|
| Quản lý hệ thống và tài khoản | Administrator |
| Giám sát toàn hệ thống | Owner |
| Lập lịch và quản lý nguồn lực | OM |
| Cấu hình định mức vật tư | OM |
| Điều phối công việc dọn phòng | HK Supervisor |
| Thực hiện vệ sinh | HK Staff |
| Kiểm tra chất lượng phòng | HK Supervisor |
| Báo cáo sự cố phòng | HK Staff, Reception và Actor được phép |
| Điều phối bảo trì | Maintenance Supervisor |
| Sửa chữa trực tiếp | Maintenance Staff |
| Nghiệm thu kỹ thuật | Maintenance Supervisor |
| Kiểm tra khả năng sử dụng phòng sau sửa chữa | HK Supervisor |
| Quản lý vật tư và kho | Storekeeper |
| Bổ sung hàng cho kho | Storekeeper và OM |
| Điều chuyển liên chi nhánh | OM và Storekeeper hai bên |
| Quản lý trạng thái lưu trú | Reception |

Owner giữ Read-only theo quyền hiện hành.

OM có phạm vi BRANCH, ngoại lệ giới hạn phục vụ điều chuyển kho.

Administrator không tự động có quyền thực hiện nghiệp vụ của các bộ phận.

---

## 8. INCIDENT TERMINOLOGY & CAUSE INVESTIGATION

### 8.1. Đổi tên

Thay toàn bộ nhãn:

“Sự cố trong quá trình vệ sinh”

thành:

“Ghi nhận sự cố phòng”.

Áp dụng trên:

- Cleaning Task Detail.
- Incident Detail.
- Room Detail.
- Internal Request.
- Maintenance Task.
- Notification.
- Dashboard.
- Reports.
- SRS.

Không sử dụng tên khiến người dùng mặc định HK Staff là người gây hỏng.

Phân biệt:

- Người phát hiện.
- Người báo cáo.
- Người xác minh.
- Nguyên nhân.
- Người hoặc nhóm được xác định có liên quan.

### 8.2. Xác định nguyên nhân sự cố

Bổ sung trường xác định nguyên nhân.

Giá trị đề xuất:

- Chưa xác định.
- Do khách.
- Do nhân viên.
- Hao mòn tự nhiên.
- Lỗi kỹ thuật.
- Nguyên nhân khác.

Khi tạo báo cáo, mặc định Chưa xác định.

HK Staff hoặc Reception không bị tự động quy trách nhiệm chỉ vì là người phát hiện.

HK Supervisor xác minh và ghi nhận nguyên nhân nếu đủ thông tin.

Maintenance Supervisor cung cấp kết luận chuyên môn kỹ thuật khi cần.

Có:

- Ghi chú xác minh.
- Bằng chứng nếu hệ thống hỗ trợ.
- Người cập nhật.
- Thời gian cập nhật.
- Lịch sử thay đổi.

Không tự động phạt nhân viên.

Không tự động tính phí bồi thường khách.

Không tự động khấu trừ lương.

Không sử dụng AI để tự kết luận người gây ra sự cố.

Note yêu cầu xác định nguyên nhân, nhưng không yêu cầu xây dựng module xử phạt hoặc bồi thường.

---

## 9. CHECKLIST & WORK ORDER STANDARDIZATION

Chuẩn hóa quy trình phiếu công việc.

Workflow:

Tạo phiếu → Xem phiếu → Phân công → Nhận phiếu → Bắt đầu → Xử lý → Checklist → Hoàn thành → Kiểm tra.

Áp dụng phù hợp cho:

- Cleaning Task.
- Maintenance Task.
- Yêu cầu khắc phục.
- Các phiếu công việc liên quan.

Không tạo workflow chung làm mất đặc điểm riêng của từng domain.

### 9.1. Checklist Housekeeping

Checklist phải hỗ trợ:

- Hạng mục vệ sinh.
- Kiểm tra đồ fill.
- Kiểm tra vật dụng lâu bền.
- Kiểm tra tình trạng thiết bị.
- Ghi nhận sự cố.
- Ghi chú.
- Trạng thái hoàn thành.

Ưu tiên tái sử dụng checklist template hiện hữu.

Các hạng mục bắt buộc phải được xác nhận trước khi gửi hoàn thành.

Phát hiện sự cố không nhất thiết ngăn nhân viên hoàn thành phần vệ sinh đã thực hiện, nhưng phải giữ blocker để phòng chưa được xác định Ready.

### 9.2. Checklist Maintenance

Checklist gồm các nhóm thông tin phù hợp:

- Thiết bị cần sửa.
- Tình trạng ban đầu.
- Nội dung xử lý.
- Kết quả kiểm tra sau sửa.
- Vật tư hoặc phụ tùng sử dụng nếu có.
- Ghi chú kỹ thuật.
- Đề xuất sửa tiếp hoặc thay mới.

Maintenance Staff không được tự nghiệm thu cuối cùng.

### 9.3. Checklist history

Lưu snapshot hoặc version của checklist gắn với phiếu.

Không thay đổi kết quả công việc cũ khi checklist template được sửa.

Không cho frontend gửi Complete nếu thiếu hạng mục bắt buộc mà backend vẫn chấp nhận.

---

## 10. MAINTENANCE WORKFLOW V2

### Step 1 — Ghi nhận sự cố

HK Staff hoặc Reception phát hiện thiết bị hỏng.

Tạo Ghi nhận sự cố phòng.

Thông tin:

- Phòng.
- Thiết bị.
- Mô tả.
- Người phát hiện.
- Người báo cáo.
- Thời gian.
- Mức độ ảnh hưởng.
- Bằng chứng nếu hỗ trợ.

Gửi HK Supervisor.

### Step 2 — HK Supervisor kiểm tra

Nếu không xác nhận sự cố:

- Ghi lý do.
- Thông báo người báo cáo.
- Kết thúc yêu cầu kiểm tra hợp lệ.

Nếu có sự cố:

- Xác nhận tình trạng.
- Ghi nguyên nhân nếu đủ bằng chứng.
- Tạo yêu cầu khắc phục.
- Gửi Maintenance Supervisor.
- Ghi Room Readiness Blocker phù hợp.

### Step 3 — Maintenance Supervisor tạo và phân công phiếu

Maintenance Supervisor:

- Xem yêu cầu.
- Kiểm tra thông tin thiết bị.
- Tạo hoặc tiếp nhận Maintenance Work Order.
- Chọn Maintenance Staff phù hợp.
- Phân công.
- Đặt ưu tiên và thời hạn theo contract hiện có.

Nhân viên được giao phải:

- ACTIVE.
- Đúng role.
- Đúng chi nhánh.
- Đáp ứng điều kiện ca làm việc theo quy định.
- Đủ điều kiện thực hiện công việc.

### Step 4 — Maintenance Staff nhận phiếu

Maintenance Staff:

- Xem phiếu.
- Nhận phiếu.
- Bắt đầu thực hiện.
- Ghi tiến độ.
- Thực hiện checklist.
- Ghi chú kỹ thuật.

Nếu từ chối nhận việc hoặc đề nghị bàn giao, phải sử dụng workflow hợp lệ và ghi lý do.

Không tự giao việc cho nhân viên khác.

### Step 5 — Xác nhận kết quả

Có hai nhánh chính:

A. Sửa chữa thành công.

- Maintenance Staff gửi hoàn thành.
- Maintenance Supervisor nghiệm thu kỹ thuật.
- Nếu đạt, gửi yêu cầu kiểm tra lại cho HK Supervisor.

B. Không thể sửa chữa.

- Maintenance Staff ghi chú.
- Maintenance Supervisor kiểm tra kết luận.
- Tạo đề xuất thay mới.
- Gửi đúng người hoặc bộ phận có thẩm quyền xử lý.

### Step 6 — HK Supervisor kiểm tra lại

Nếu đạt:

- Xác nhận vấn đề đã được khắc phục.
- Giải quyết blocker tương ứng.
- Đánh giá Room Readiness.

Nếu không đạt:

- Nhập lý do.
- Tạo yêu cầu xử lý lại.
- Gửi Maintenance Supervisor.
- Giữ lịch sử các lần sửa chữa.

Không chuyển phòng Ready khi chỉ mới hoàn thành sửa chữa kỹ thuật.

### External repair

Đội bảo trì nội bộ là phương án chính.

Dịch vụ ngoài được sử dụng khi công việc vượt khả năng đội nội bộ hoặc cần chuyên môn bên ngoài.

Không tạo account kỹ thuật viên bên ngoài nếu chưa có yêu cầu.

Không xây dựng supplier management ngoài phạm vi.

---

## 11. REPLACEMENT WORKFLOW — THAY MỚI THIẾT BỊ

Theo Note mới, thiết bị không thể sửa chữa phải có bước thay mới.

Bổ sung trong Maintenance Work Order:

- Kết quả đánh giá.
- Có thể sửa chữa hay không.
- Ghi chú.
- Lý do đề xuất thay mới.
- Thiết bị cần thay.
- Phương án xử lý.
- Trạng thái đề xuất.

Workflow đề xuất:

Maintenance Staff báo không thể sửa.

→ Maintenance Supervisor kiểm tra.

→ Xác nhận đề xuất thay mới.

→ OM xử lý đề xuất thuộc trách nhiệm quản lý tài sản.

→ Thực hiện thay mới thực tế.

→ Maintenance Supervisor xác nhận kết quả kỹ thuật.

→ HK Supervisor kiểm tra lại phòng.

OM không đóng vai trò phê duyệt trung gian báo cáo sự cố, nhưng có thể xử lý đề xuất thay tài sản thuộc thẩm quyền quản lý tài sản.

Không tự động tạo đơn mua hàng khi chưa có contract mua sắm.

Không tự động tăng kho thiết bị.

Không tự động thanh lý tài sản chỉ vì Maintenance Staff đánh dấu không thể sửa.

Nếu đề xuất thay mới cần cơ chế mua hàng mà source chưa có, triển khai phần đề xuất và theo dõi, không mở rộng thành module procurement đầy đủ.

---

## 12. INVENTORY CLASSIFICATION V2

Phân biệt vật tư theo mục đích sử dụng và vòng đời.

### GROUP A — CONSUMABLES

Vật tư tiêu hao:

- Bàn chải đánh răng.
- Kem đánh răng.
- Dầu gội.
- Sữa tắm.
- Nước đóng chai.
- Giấy vệ sinh.
- Các sản phẩm sử dụng một lần.

Đặc điểm:

- Có số lượng.
- Có đơn vị tính.
- Có thể có lô.
- Có thể có hạn sử dụng.
- Ghi giảm tồn khi cấp phát theo quy tắc tiêu hao.
- Có phiếu xuất vật tư tiêu hao.

### GROUP B — REUSABLE / REPLACEMENT ITEMS

Vật tư tái sử dụng hoặc thay thế:

- Chăn.
- Màn.
- Gối.
- Thảm.
- Khăn tắm.
- Khăn mặt.
- Ga giường.
- Các vật dụng tái sử dụng phù hợp.

Đặc điểm:

- Lấy từ kho để bổ sung hoặc thay thế trong phòng.
- Không làm giảm tổng sở hữu của chi nhánh khi đưa vào sử dụng.
- Có trạng thái.
- Có vị trí hoặc nơi đang giữ.
