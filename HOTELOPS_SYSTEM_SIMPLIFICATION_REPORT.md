# HOTELOPS — SYSTEM SIMPLIFICATION REPORT

Ngày: **21/11/2026**. Nguồn: tài liệu “SYSTEM SIMPLIFICATION CONSOLIDATION” người dùng gửi; `docs/requirements/HotelOps_SRS_DRAFT.md`; `SRS_CHANGE_NOTES.md`; code Reception/readiness legacy; schema Compact v1 và SQL Server local.

## A. Executive Summary

Thực hiện phương án **Level 2 — Balanced Simplification** trên database độc lập **HotelOpsCompactDB**, schema **2.0.0**, **37 bảng**, giảm từ 40 bảng của Compact v1 và vẫn dưới giới hạn 40. Database HotelOpsDB 149 bảng không bị sửa.

Yêu cầu chat mới “làm lại database vừa tạo theo giản lược này” cho phép thay database dù tài liệu nguồn vốn là đề bài phân tích có ghi “không thay đổi database”. Phạm vi áp dụng là **database thử nghiệm mới**; các dòng `PROPOSE_REMOVE`/`NEEDS_APPROVAL` vẫn không được diễn giải thành phê duyệt xóa UC trên sản phẩm đang chạy. Không chỉnh mã ứng dụng, seed quyền hoặc deploy frontend trong lượt sửa database này.

Thay đổi chính:

- Bỏ `shifts`, `workdays`, `time_corrections` khỏi schema mới; không yêu cầu bắt đầu workday để thực hiện nhiệm vụ. Việc bỏ UC-138/139 trên sản phẩm vẫn được ghi là đề xuất; nếu PO giữ chúng, cần thiết kế lại trước khi chuyển backend.
- Giữ `work_intervals` theo **work + assignment + user + branch**, ghi khoảng Start/Pause/Resume/Complete, chống overlap và bảo toàn thời gian đã chốt. Backend sẽ tự ghi thời gian từ thao tác; schema không tự thực hiện thao tác ứng dụng.
- Giữ xác nhận bàn giao backlog bằng sự kiện có recipient, đề nghị và một quyết định nhận/từ chối; không cần bảng ca hoặc bảng handover mới.
- Giữ lifecycle riêng cho Cleaning, Inspection, Rework, Incident, Maintenance, Operational Task và Internal Request. Dùng chung bảng lưu trữ không có nghĩa gộp chúng thành cùng một nghiệp vụ.
- Incident là hồ sơ nguồn; maintenance tham chiếu incident, tránh nhập lại thông tin ở UI/backend sau này. Vật tư dùng source document/line; view tự tính lượng còn thiếu, replenishment nằm trong module kho.
- Giữ scope, rowversion, lịch sử bất biến, approval, ledger/reversal, kiểm tra độc lập và readiness theo inspection hiện hành + block.

Schema v1 đã được kiểm tra **rỗng** trước khi thay. Installer thử tạo v2 rồi rollback về v1; kiểm tra độc lập vẫn đủ 40 bảng/version 1.0.0; sau đó mới áp dụng v2. Không có dữ liệu người dùng bị xóa hoặc được chuyển ngầm.


## B. UC Simplification Matrix

Đối chiếu đủ **144/144 UC** trong SRS; đây là phân loại thiết kế, không chứng nhận chức năng đã triển khai.

| UC ID | Chức năng gốc | Đề xuất | Lý do | Tác động | Trạng thái quyết định |
|---|---|---|---|---|---|
| UC-001 | Login | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-002 | Logout | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-003 | Change Password | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-004 | Create User Account | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-005 | Update User Account | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-006 | Activate/Deactivate User Account | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-007 | Assign User to Branch/Department | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-008 | Create/Update User Role | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-009 | Configure Role Permissions | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-010 | Assign/Revoke User Roles | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-011 | Review User Access Permissions | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-012 | Update Branch Information | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-013 | View Branch Structure | MERGE | Dùng chung tra cứu có bộ lọc và phân trang | Giữ dữ liệu được phép xem theo vai trò | Phương án Level 2; không tự cấp thêm quyền |
| UC-014 | Create Floor/Area | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-015 | Update Floor/Area | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-016 | Deactivate Floor/Area | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-017 | Create Room Type | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-018 | Update Room Type | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-019 | Deactivate Room Type | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-020 | Create Room | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-021 | Update Room Information | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-022 | Deactivate Room | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-023 | View Room Directory | MERGE | Dùng chung tra cứu có bộ lọc và phân trang | Giữ dữ liệu được phép xem theo vai trò | Phương án Level 2; không tự cấp thêm quyền |
| UC-024 | Create Department | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-025 | Update Department | SIMPLIFY | Một màn hình/command quản trị liên quan; không gộp quyền | Giảm nhập lặp; giữ scope, concurrency và audit | Phương án Level 2; không tự cấp thêm quyền |
| UC-026 | Deactivate Department | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-027 | Manage Operational Room Status | AUTOMATE | Trạng thái sinh từ checkout/cleaning/inspection/check-in | Giữ lịch sử, turnover và đối chiếu Reception legacy | Phương án Level 2; không tự cấp thêm quyền |
| UC-028 | Create Cleaning Task | MERGE | Tạo và giao cleaning trong một thao tác nghiệp vụ | Vẫn ghi assignment và kiểm tra người nhận | Phương án Level 2; không tự cấp thêm quyền |
| UC-029 | Assign Cleaning Task | MERGE | Tạo và giao cleaning trong một thao tác nghiệp vụ | Vẫn ghi assignment và kiểm tra người nhận | Phương án Level 2; không tự cấp thêm quyền |
| UC-030 | Reassign Cleaning Task | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-031 | Start Cleaning Task | AUTOMATE | Bắt đầu/tiến độ/hoàn thành đồng thời ghi thời gian và sự kiện | Không cần bắt đầu workday; Complete chưa Ready | Phương án Level 2; không tự cấp thêm quyền |
| UC-032 | Update Cleaning Progress | AUTOMATE | Bắt đầu/tiến độ/hoàn thành đồng thời ghi thời gian và sự kiện | Không cần bắt đầu workday; Complete chưa Ready | Phương án Level 2; không tự cấp thêm quyền |
| UC-033 | Report Cleaning Blocker | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-034 | Complete Cleaning Task | AUTOMATE | Bắt đầu/tiến độ/hoàn thành đồng thời ghi thời gian và sự kiện | Không cần bắt đầu workday; Complete chưa Ready | Phương án Level 2; không tự cấp thêm quyền |
| UC-035 | Receive Rework Task | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-036 | Complete Cleaning Rework | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-037 | Monitor Cleaning Tasks | MERGE | Một dòng thời gian vận hành phòng có bộ lọc | Không bỏ lịch sử hay quyền xem | Phương án Level 2; không tự cấp thêm quyền |
| UC-038 | View Room Turnover History | MERGE | Một dòng thời gian vận hành phòng có bộ lọc | Không bỏ lịch sử hay quyền xem | Phương án Level 2; không tự cấp thêm quyền |
| UC-039 | Create Room Inspection Checklist | SIMPLIFY | Dùng checklist/version chung | Không sửa tiêu chí đã công bố hoặc kết quả đã chốt | Phương án Level 2; không tự cấp thêm quyền |
| UC-040 | Update Room Inspection Checklist | SIMPLIFY | Dùng checklist/version chung | Không sửa tiêu chí đã công bố hoặc kết quả đã chốt | Phương án Level 2; không tự cấp thêm quyền |
| UC-041 | Assign Room Inspection | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-042 | Perform Room Inspection | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-043 | Record Inspection Defects | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-044 | Approve Room Inspection | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-045 | Reject Room Inspection | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-046 | Request Cleaning Rework | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-047 | Reinspect Room | SIMPLIFY | Chu kỳ cleaning → inspection → rework → reinspection rõ ràng | Giữ HK Staff khác người kiểm tra; không bỏ rework | Phương án Level 2; không tự cấp thêm quyền |
| UC-048 | View Room Inspection History | MERGE | Một dòng thời gian vận hành phòng có bộ lọc | Không bỏ lịch sử hay quyền xem | Phương án Level 2; không tự cấp thêm quyền |
| UC-049 | Report Incident | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-050 | Classify Incident | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-051 | Attach Incident Evidence | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-052 | Triage Incident | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-053 | Assign Maintenance Work | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-054 | Accept and Start Maintenance | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-055 | Update Maintenance Progress | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-056 | Block Room Due to Incident | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-057 | Escalate Critical Incident | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-058 | Resolve Maintenance Incident | MERGE | Một hồ sơ sự cố với phần phân công/thực hiện sửa chữa | Tham chiếu incident gốc; không nhập lại mô tả | Phương án Level 2; không tự cấp thêm quyền |
| UC-059 | Verify and Close Incident | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-060 | Reopen Incident | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-061 | Create Preventive Maintenance Schedule | SIMPLIFY | Mẫu lịch lặp và danh mục dùng chung | Giữ occurrence, phân công và dấu vết thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-062 | Execute Scheduled Maintenance | SIMPLIFY | Mẫu lịch lặp và danh mục dùng chung | Giữ occurrence, phân công và dấu vết thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-063 | Manage Work Categories | SIMPLIFY | Mẫu lịch lặp và danh mục dùng chung | Giữ occurrence, phân công và dấu vết thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-064 | Manage Operational Reference Data | SIMPLIFY | Mẫu lịch lặp và danh mục dùng chung | Giữ occurrence, phân công và dấu vết thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-065 | Register Asset/Equipment | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-066 | Update Asset Information | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-067 | Assign Asset to Location | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-068 | Transfer Asset Location | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-069 | Record Asset Handover | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-070 | Update Asset Condition | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-071 | View Asset Profile | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-072 | Review Asset Maintenance History | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-073 | Review Assets by Location | SIMPLIFY | Một hồ sơ tài sản và dòng thời gian | Giữ condition, custody, location, lịch sử bảo trì | Phương án Level 2; không tự cấp thêm quyền |
| UC-074 | Retire/Deactivate Asset | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-075 | Manage Warehouses | MERGE | Quản lý kho/vật tư/đơn vị/ngưỡng trong nhóm danh mục | Không bỏ đơn vị gốc và ngưỡng cảnh báo | Phương án Level 2; không tự cấp thêm quyền |
| UC-076 | Manage Inventory Items | MERGE | Quản lý kho/vật tư/đơn vị/ngưỡng trong nhóm danh mục | Không bỏ đơn vị gốc và ngưỡng cảnh báo | Phương án Level 2; không tự cấp thêm quyền |
| UC-077 | Manage Units of Measurement | MERGE | Quản lý kho/vật tư/đơn vị/ngưỡng trong nhóm danh mục | Không bỏ đơn vị gốc và ngưỡng cảnh báo | Phương án Level 2; không tự cấp thêm quyền |
| UC-078 | Configure Minimum Stock Levels | MERGE | Quản lý kho/vật tư/đơn vị/ngưỡng trong nhóm danh mục | Không bỏ đơn vị gốc và ngưỡng cảnh báo | Phương án Level 2; không tự cấp thêm quyền |
| UC-079 | Create Stock Receipt | SIMPLIFY | Chứng từ/dòng nguồn dùng lại dữ liệu đã nhập | Giữ phê duyệt, phân nhiệm, ledger và truy nguyên | Phương án Level 2; không tự cấp thêm quyền |
| UC-080 | Verify and Post Stock Receipt | SIMPLIFY | Chứng từ/dòng nguồn dùng lại dữ liệu đã nhập | Giữ phê duyệt, phân nhiệm, ledger và truy nguyên | Phương án Level 2; không tự cấp thêm quyền |
| UC-081 | Issue Stock | SIMPLIFY | Chứng từ/dòng nguồn dùng lại dữ liệu đã nhập | Giữ phê duyệt, phân nhiệm, ledger và truy nguyên | Phương án Level 2; không tự cấp thêm quyền |
| UC-082 | Return Materials to Stock | SIMPLIFY | Chứng từ/dòng nguồn dùng lại dữ liệu đã nhập | Giữ phê duyệt, phân nhiệm, ledger và truy nguyên | Phương án Level 2; không tự cấp thêm quyền |
| UC-083 | Create Stock Transfer | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-084 | Confirm Stock Transfer Receipt | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-085 | Conduct Stocktake | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-086 | Reconcile Stocktake Discrepancies | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-087 | Adjust Stock Balance | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-088 | Reverse Stock Transaction | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-089 | View Stock Balance and Movement Ledger | MERGE | Tồn và lịch sử truy vấn từ một ledger | Không lưu hai nguồn tồn có thể lệch nhau | Phương án Level 2; không tự cấp thêm quyền |
| UC-090 | Return Unused Materials | SIMPLIFY | Chứng từ/dòng nguồn dùng lại dữ liệu đã nhập | Giữ phê duyệt, phân nhiệm, ledger và truy nguyên | Phương án Level 2; không tự cấp thêm quyền |
| UC-091 | Create Material Request | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-092 | Link Material Request to Work | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-093 | Update/Cancel Material Request | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-094 | Review Material Request | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-095 | Approve/Reject Material Request | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-096 | Check Material Availability | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-097 | Issue Approved Materials | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-098 | Manage Partial Material Fulfillment | SIMPLIFY | Yêu cầu → duyệt → cấp phát, tự tính thiếu | DH duyệt khác Storekeeper cấp; không vượt lượng duyệt | Phương án Level 2; không tự cấp thêm quyền |
| UC-099 | Track Material Request History | MERGE | Lịch sử duyệt/cấp phát cùng hồ sơ yêu cầu | Snapshot từng revision và audit vẫn giữ | Phương án Level 2; không tự cấp thêm quyền |
| UC-100 | Create Replenishment Request | MERGE | Replenishment thành chứng từ liên quan trong module kho | Giữ nhiều lần nhận/thiếu hàng, không xóa FE-10 | Phương án Level 2; không tự cấp thêm quyền |
| UC-101 | Track Material Replenishment | MERGE | Replenishment thành chứng từ liên quan trong module kho | Giữ nhiều lần nhận/thiếu hàng, không xóa FE-10 | Phương án Level 2; không tự cấp thêm quyền |
| UC-102 | Create Operational Task | MERGE | Tạo, deadline và phân công chung màn hình | Create/Assign vẫn là sự kiện có trách nhiệm | Phương án Level 2; không tự cấp thêm quyền |
| UC-103 | Set Task Priority and Deadline | MERGE | Tạo, deadline và phân công chung màn hình | Create/Assign vẫn là sự kiện có trách nhiệm | Phương án Level 2; không tự cấp thêm quyền |
| UC-104 | Assign/Reassign Task | MERGE | Tạo, deadline và phân công chung màn hình | Create/Assign vẫn là sự kiện có trách nhiệm | Phương án Level 2; không tự cấp thêm quyền |
| UC-105 | Review and Verify Task Completion | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-106 | Reopen Assigned Task | SIMPLIFY | Reopen và Pause/Resume chỉ khi có lý do | Giữ quyền, reason và từng khoảng thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-107 | Accept Assigned Task | SIMPLIFY | Accept → Start → In Progress → Complete | Ghi giờ từ thao tác, vẫn chờ Verify | Phương án Level 2; không tự cấp thêm quyền |
| UC-108 | Start Assigned Task | SIMPLIFY | Accept → Start → In Progress → Complete | Ghi giờ từ thao tác, vẫn chờ Verify | Phương án Level 2; không tự cấp thêm quyền |
| UC-109 | Update Task Progress | SIMPLIFY | Accept → Start → In Progress → Complete | Ghi giờ từ thao tác, vẫn chờ Verify | Phương án Level 2; không tự cấp thêm quyền |
| UC-110 | Pause/Resume Task | SIMPLIFY | Reopen và Pause/Resume chỉ khi có lý do | Giữ quyền, reason và từng khoảng thực hiện | Phương án Level 2; không tự cấp thêm quyền |
| UC-111 | Record Actual Working Time | AUTOMATE | Ghi khoảng thời gian theo Start/Pause/Resume/Complete | Thay phụ thuộc workday bằng assignment của nhiệm vụ | Phương án Level 2; không tự cấp thêm quyền |
| UC-112 | Complete Assigned Task | SIMPLIFY | Accept → Start → In Progress → Complete | Ghi giờ từ thao tác, vẫn chờ Verify | Phương án Level 2; không tự cấp thêm quyền |
| UC-113 | Review Task Workload and Time Logs | MERGE | Workload và duration từ cùng nguồn công việc | Chưa thêm chấm công/lương | Phương án Level 2; không tự cấp thêm quyền |
| UC-114 | Create Internal Request | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-115 | Route and Assign Internal Request | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-116 | Accept/Reject Internal Request | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-117 | Update Internal Request Progress | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-118 | Create Linked Operational Work | SIMPLIFY | Chỉ tạo linked work khi cần một người/quy trình xử lý riêng | Dùng liên kết có FK, không tự nhân đôi nhiệm vụ | Phương án Level 2; không tự cấp thêm quyền |
| UC-119 | Record Request Handover | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-120 | Complete Internal Request | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-121 | Reopen Internal Request | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-122 | Escalate Overdue Internal Request | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-123 | View Internal Request History | SIMPLIFY | Create → Route → Accept/Reject → Process → Complete → Confirm/Reopen | Internal Request vẫn khác Task; không đóng khi linked work chưa đạt | Phương án Level 2; không tự cấp thêm quyền |
| UC-124 | Receive Assignment Notifications | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-125 | Receive Approval Notifications | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-126 | Receive Inspection and Incident Alerts | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-127 | Receive Overdue Work Alerts | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-128 | Receive Low Stock Alerts | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-129 | View Notification Center | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-130 | Acknowledge Notifications | SIMPLIFY | Chỉ nhắc sự kiện có hành động; dedup theo recipient/event | Giữ alert bắt buộc, ack và liên kết đối tượng | Phương án Level 2; không tự cấp thêm quyền |
| UC-131 | View Room Readiness Dashboard | MERGE | Dashboard/report dùng cùng truy vấn và bộ lọc | Giữ readiness/workload/incident/inventory, chưa xóa KPI chưa rà soát | Phương án Level 2; không tự cấp thêm quyền |
| UC-132 | View Task Workload Dashboard | MERGE | Dashboard/report dùng cùng truy vấn và bộ lọc | Giữ readiness/workload/incident/inventory, chưa xóa KPI chưa rà soát | Phương án Level 2; không tự cấp thêm quyền |
| UC-133 | View Incident and Maintenance Dashboard | MERGE | Dashboard/report dùng cùng truy vấn và bộ lọc | Giữ readiness/workload/incident/inventory, chưa xóa KPI chưa rà soát | Phương án Level 2; không tự cấp thêm quyền |
| UC-134 | View Inventory and Material Dashboard | MERGE | Dashboard/report dùng cùng truy vấn và bộ lọc | Giữ readiness/workload/incident/inventory, chưa xóa KPI chưa rà soát | Phương án Level 2; không tự cấp thêm quyền |
| UC-135 | Generate and Export Operational Reports | MERGE | Dashboard/report dùng cùng truy vấn và bộ lọc | Giữ readiness/workload/incident/inventory, chưa xóa KPI chưa rà soát | Phương án Level 2; không tự cấp thêm quyền |
| UC-136 | View Audit Logs | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-137 | Inspect Record Change History | KEEP | Kiểm soát cốt lõi hoặc truy nguyên bắt buộc | Giữ quyền, phạm vi, phân nhiệm và lịch sử | Phương án Level 2; không tự cấp thêm quyền |
| UC-138 | Manage Operational Workday and Shift Records | PROPOSE_REMOVE | Bỏ lịch ca/workday và check-in/out workday khỏi phương án Level 2 | DB mới không có bảng ca/ngày làm việc; loại UC ở sản phẩm còn cần PO xác nhận | Chưa phê duyệt bỏ UC; chỉ thiết kế DB mới đã được yêu cầu |
| UC-139 | Record Workday Start and End | PROPOSE_REMOVE | Bỏ lịch ca/workday và check-in/out workday khỏi phương án Level 2 | DB mới không có bảng ca/ngày làm việc; loại UC ở sản phẩm còn cần PO xác nhận | Chưa phê duyệt bỏ UC; chỉ thiết kế DB mới đã được yêu cầu |
| UC-140 | Manage Operational Shift Handover | NEEDS_APPROVAL | Thay bàn giao ca bằng tiếp nhận backlog | Giữ HANDOVER_OFFERED/ACCEPTED/REJECTED; chưa bỏ xác nhận | Chưa phê duyệt bỏ UC; chỉ thiết kế DB mới đã được yêu cầu |
| UC-141 | View User List | MERGE | Dùng chung tra cứu có bộ lọc và phân trang | Giữ dữ liệu được phép xem theo vai trò | Phương án Level 2; không tự cấp thêm quyền |
| UC-142 | View Room Types List | MERGE | Dùng chung tra cứu có bộ lọc và phân trang | Giữ dữ liệu được phép xem theo vai trò | Phương án Level 2; không tự cấp thêm quyền |
| UC-143 | View Incidents List | MERGE | Một màn hình sự cố/bảo trì với bộ lọc trạng thái | Không bỏ verification hoặc scope | Phương án Level 2; không tự cấp thêm quyền |
| UC-144 | View Maintenance List | MERGE | Một màn hình sự cố/bảo trì với bộ lọc trạng thái | Không bỏ verification hoặc scope | Phương án Level 2; không tự cấp thêm quyền |

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
