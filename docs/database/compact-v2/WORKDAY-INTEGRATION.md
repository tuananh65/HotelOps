# HotelOps Compact 2.1 — Workday và tích hợp FE

Nguồn yêu cầu: `HotelOps_SRS_Tinh_Gon_Project2_V1.docx` và quyết định trực tiếp của người dùng ngày 22/11/2026. Chỉ các phần đã xác nhận được triển khai; không tự coi các mục OPEN trong SRS là quyền nghiệp vụ đã duyệt.

## Quyết định đã chốt

- Được thêm đúng 3 bảng: `workday_shifts`, `workday_assignments`, `workday_events`. Tổng 40 bảng, giữ 6 view; không có bảng migration.
- Giám sát buồng: 3 ca 00–08/08–16/16–24, mỗi ca 1 người. Nhân viên buồng: cùng giờ, mỗi ca 4 người.
- Trưởng kỹ thuật / nhân viên kỹ thuật / kho: sáng 08:00–11:30, chiều 13:00–17:00, sức chứa tương ứng 1/3/1 người mỗi ca. Đây là cách biểu diễn hai ca sáng/chiều theo câu trả lời của người dùng, không dùng hai ca 07:30–17:00/09:00–18:30 đề xuất trước đó.
- Lễ tân: 3 ca 00–08/08–16/16–24, mỗi ca 1 người.
- Start chỉ cho ca của chính nhân viên trong ngày hiện tại của chi nhánh; không ép bắt đầu đúng giờ. Một người không có hai ca hoạt động cùng lúc. End ghi thời gian thực tế, kể cả ca bắt đầu hôm trước.
- Thời gian công việc và thời gian ca độc lập. End ca không tự đóng công việc hay ghi nhận hoàn thành.

## API và an toàn dữ liệu

`GET /api/v2/workday?branchId=&date=` trả lịch Thứ Hai–Chủ Nhật, ngày hiện tại của chi nhánh, các định nghĩa ca, phân công trong phạm vi và ca của mình đang hoạt động. `SHIFT_MANAGE` xem/lập lịch trong chi nhánh; `ATTENDANCE_RECORD` xem/thao tác ca cá nhân. Admin không tự có quyền vận hành.

`GET workday/candidates?shiftId=&date=` chỉ dành cho người lập lịch. Nhân viên phải hoạt động, có quyền vào chi nhánh, bộ phận công tác đúng và vai trò tương ứng trong toàn bộ ca đã lên kế hoạch. Không đưa người trùng lịch vào danh sách.

`POST workday/assignments` nhận `shiftId`, `userId`, `workDate`, `rowVersion`, `operationKey`. `POST workday/assignments/{id}/start|end|cancel` nhận phiên bản phân công, khóa thao tác và lý do khi hủy. Chỉ hủy ca còn PLANNED. Lịch sử ở `GET workday/assignments/{id}/history` được kiểm tra phạm vi và bất biến ở SQL.

Phân ca và chuyển trạng thái dùng giao dịch Serializable cùng application lock, kiểm tra sức chứa/trùng lịch, rowversion và khóa thao tác. Unique index ngăn hai ca WORKING cho một người. Thu hồi quyền, phạm vi hoặc vô hiệu hóa nhân viên bị chặn khi còn ca PLANNED/WORKING; quản lý cần xử lý lịch trước.

`auth/me` bổ sung chi nhánh, vai trò và memberships hợp lệ; loại vai trò chưa đến ngày hiệu lực. `workspace/options` cung cấp dữ liệu chọn phục vụ các thao tác được phép. FE dùng bearer session và quyền thật; không chuyển vai trò giả lập.

## Tích hợp FE

Điểm vào React chuyển sang `frontend/src/compact/app.tsx`. Các module legacy vẫn giữ trong source để đối chiếu. Giao diện mới gồm đăng nhập/đổi mật khẩu, chi nhánh, phòng, công việc/yêu cầu/inspection, lịch tuần, chứng từ, tài sản, tổng quan/báo cáo, thông báo và nhân sự.

Mỗi màn hình tải riêng bằng lazy import. Các thao tác có trạng thái chờ, khóa nút gửi, lỗi và tải lại phiên bản; không tự thử lại mutation. Phòng tách occupancy khỏi readiness; nhân viên không tự chuyển phòng sang sẵn sàng. Thao tác công việc bám trạng thái, người được giao và quyền; checklist gửi rowversion của từng kết quả. Duyệt vật tư và ghi sổ kho vẫn tách biệt.

## Cài đặt local

Không chạy lại installer v2.0 trên database có dữ liệu. Dùng `scripts/database/compact-v2/Upgrade-Workday.ps1 -Database HotelOpsCompactDB`. Script tạo backup COPY_ONLY + CHECKSUM, RESTORE VERIFYONLY rồi mới thực hiện nâng cấp trong giao dịch. Nếu có lỗi, rollback; database legacy không được chọn làm đích.

Connection string giữ trong .NET User Secrets. Không lưu tên SQL Server, mật khẩu hoặc token vào cấu hình dùng chung. `Start-WorkdayApi.ps1 -Database HotelOpsCompactDB -Port 5768` là helper preview local, tắt worker nền và chờ health trước khi trả kết quả.

FE: tại `frontend`, đặt `$env:API_PROXY_TARGET='http://127.0.0.1:5768'`, chạy `npm run dev -- --port 5178`. Không đổi cấu hình proxy mặc định của các developer khác.

Kiểm chứng ngày 22/11/2026: [báo cáo FE/BE và Workday](../../../plans/reports/test-261122-1319-fe-be-workday.md). Các lệnh FE riêng cho Compact là `npm run test:compact`, `npm run test:compact:e2e`, `npm run test:compact:visual`, `npm run test:compact:states`. Bộ E2E ghi dữ liệu nên chỉ chạy trên bản sao kiểm thử sạch, sau khi backup và trỏ API proxy vào bản sao đó. Không chạy E2E ghi dữ liệu trên database chính.

Bảng `sysdiagrams` rỗng phát sinh sau nâng cấp đã được xóa sau backup và xác nhận riêng của người dùng để giữ đúng 40 bảng. `Remove-EmptyDiagram.ps1` từ chối nếu bảng có dữ liệu, có FK hoặc số bảng/schema không đúng điều kiện; không dùng để xóa dữ liệu sơ đồ đã lưu.

## Phạm vi dữ liệu mẫu và giới hạn

Nâng cấp chỉ thêm định nghĩa ca cho các chi nhánh DEMO đang tồn tại và ánh xạ theo mã vai trò, không hard-code ID. Sau bảng V10 được duyệt ngày 22/11/2026, `Apply-V10Roles.ps1` backup rồi chuyển ca giám sát HK sang HOUSEKEEPING_SUPERVISOR, ca trưởng kỹ thuật sang MAINTENANCE_LEAD; giữ giờ/định mức. Chỉ chuyển khi không có ca liên quan PLANNED/WORKING. Nhân viên cần EMPLOYMENT đúng bộ phận và role phù hợp; danh sách ứng viên trống nếu chưa có người đáp ứng. Lịch mới không được tự áp thành lịch nhân sự sản xuất.

Đợt V10 bổ sung tổng quan quản trị, danh mục/chi nhánh/tầng/phòng, cấu hình quyền, tồn kho, checklist/lịch định kỳ, hàng đợi giám sát và bốn actor riêng. [Bảng quyền đã duyệt](../../requirements/V10-ROLE-MAPPING-PROPOSAL.md) là nguồn cho thay đổi này; [báo cáo V10](../../../plans/reports/test-261122-1359-v10-actor-flows.md) ghi kết quả và giới hạn. Đây là tích hợp local, chưa publish production; các quy tắc SRS còn OPEN ngoài bảng duyệt không tự trở thành yêu cầu cuối cùng.
