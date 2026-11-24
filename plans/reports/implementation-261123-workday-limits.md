# Phân ca theo bộ phận, tuần và xác nhận vượt giờ

Ngày 23/11/2026 — Project 2 / CompactDB. Triển khai một agent, không thay schema, grants hoặc dữ liệu phân ca primary.

## Thay đổi

- Cấu hình ca yêu cầu chọn bộ phận trước. Đối tượng được lọc theo quan hệ employment/role còn hiệu lực của nhân sự đang hoạt động tại chi nhánh, có ATTENDANCE_RECORD. Đổi bộ phận xóa đối tượng cũ. Backend từ chối ghép sai bộ phận/role; không hardcode ID. Bộ phận chưa có nhân sự đủ điều kiện hiển thị thông báo, không tự suy diễn ánh xạ mới.
- Form phân ca giữ chọn nhiều người, bổ sung phạm vi ngày/tuần/tháng. Tuần từ thứ Hai đến Chủ nhật, hỗ trợ qua tháng/năm; bỏ ngày quá khứ, có thể chọn thứ/ngày và xác nhận ngày lễ. Lịch lễ của cả hai năm được tải nếu tuần qua năm.
- Tổng giờ phân ca = thời lượng ca hiện có chưa hủy + toàn bộ lượt đang thêm của từng nhân viên. Ca ENDED vẫn tính; CANCELLED không tính. Tính trên tất cả ca/chi nhánh của người đó, không trả chi tiết chi nhánh khác. Đây là giờ theo lịch, không phải timer công việc hay giờ attendance thực tế.
- Chỉ khi vượt 480 phút/ngày hoặc 2400 phút/tuần mới cảnh báo. Hiển thị giờ đã có, giờ thêm, tổng và giới hạn. Người phân ca phải tích xác nhận; đổi ngày/người hoặc lịch nền thay đổi làm xác nhận cũ hết hiệu lực.
- API single và batch cùng kiểm tra xác nhận qua fingerprint dữ liệu hiện tại trong transaction + Workday applock; vẫn kiểm tra scope, eligibility, capacity, overlap, RowVersion. Mã lỗi 409 được dịch tiếng Việt. Không bỏ validation để cho lưu vượt giờ. Xác nhận được lưu trong sự kiện và audit; mỗi sự kiện chỉ giữ chi tiết ngày/tuần liên quan, audit giữ đủ cảnh báo của cả lượt.
- Logo login căn giữa thẻ; lớp tối ảnh nền tăng lên 55%. Đã xem ảnh desktop/mobile.

## Kiểm chứng

- `dotnet build HotelOps.slnx`: PASS, 0 warning / 0 error.
- `dotnet test HotelOps.slnx --no-build`: 53 PASS, 3 SKIP (các integration fixture tùy chọn có sẵn: worker recurrence/low-stock, maintenance worker, account management). Không tính SKIP là PASS.
- Frontend `npm run build -- --outDir ../artifacts/production-261123-workday/frontend`: PASS, gồm `tsc -b` và Vite build.
- SQL/API fixture `Test-Backend.ps1 -WorkdayLimits`: 25 kiểm tra PASS trên `HotelOpsCompactApiTest11df6da2e7c2`. Gồm mapping bộ phận, scope, đúng 8/40 giờ, cảnh báo 9h/48h, thiếu/sai/stale xác nhận, single endpoint, audit/history, retry, hủy/đã kết thúc, nguyên tử nhiều người, tuần qua tháng và cả tháng vượt giờ không cắt lịch sử. Không tạo phân ca thử trên primary.
- Edge `compact-workday-scheduling.mjs`: PASS với API fixture. Kiểm tra phụ thuộc bộ phận/đối tượng, tuần/tháng/nhiều người, xác nhận giờ/holiday và retry409, tuần qua năm, lịch sử khóa định nghĩa, OWN view; layout 1440/768/375px. Login logo căn giữa, overlay55%, không tràn desktop/mobile, không có pageerror.
- Release publish PASS. Bản chạy ở `artifacts/production-261123-workday`; API môi trường Production, health200/schema2.1.0/40tables. Không đổi database schema/grants. Bản frontend là built preview cho Production cục bộ theo thiết lập hiện hữu.
- Bằng chứng: `artifacts/workday-limits-*.log`, `artifacts/compact-v2/backend/workday-limits-verification.json`, `artifacts/workday-scheduling/verification.json`, ảnh cùng thư mục; kiểm chứng truy cập Production riêng tại `artifacts/production-261123-workday/verification.json`.

## File thay đổi trong lượt này

Backend: `Compact/Contracts/WorkdayRequests.cs`; `Compact/Services/WorkdayService.cs`, `WorkdayService.Scheduling.cs`, **mới** `WorkdayService.Hours.cs` (đều dưới `src/HotelOps.Api`).

Frontend: `frontend/src/compact/workday-settings.tsx`, `workday-assignment.tsx`, `workday-scheduling.ts`, `compact.css`.

Kiểm thử: **mới** `tests/HotelOps.Compact.Tests/WorkdayHoursTests.cs`, **mới** `scripts/database/compact-v2/test-workday-limits.ps1`; cập nhật `scripts/database/compact-v2/Test-Backend.ps1`, `frontend/tests/compact-workday-scheduling.mjs`.

Tài liệu: `docs/ui/compact-ux.md`, `plans/261123-1332-workday-limits/plan.md`, báo cáo này. Launcher/log/build cục bộ trong `artifacts/`, không chứa giá trị secret. Các file khác đang dirty từ các lượt trước được giữ nguyên; không commit/push.

## Review và giới hạn kiểm chứng

Đã sửa lỗi LINQ không dịch được khi lọc record DepartmentRole, kiểm tra lại API trả400 đúng. Đã giới hạn nội dung reason theo ngày của từng sự kiện để lượt cả tháng không vượt cột 2000 ký tự; audit không mất dữ liệu. Giữ nguyên khóa toàn Workday để việc xem lại tổng giờ và lưu không bị hai request phân ca vượt qua nhau.

Không đăng nhập bằng tài khoản người dùng thật hoặc tạo phân ca trên Production để thử; các workflow ghi được kiểm chứng trên database độc lập. Git chỉ cảnh báo chuẩn hóa LF/CRLF theo cấu hình hiện có; không có lỗi whitespace. Ngưỡng giờ áp dụng cho phân ca mới, không tự sửa/xóa lịch đã tồn tại.
