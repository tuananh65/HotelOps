# Housekeeping Staff và trải nghiệm công việc — bản commit 20/11/2026

Gói mã nguồn này bao gồm các sửa cho `lan.dang` (Housekeeping Staff), các phần liên quan của `mai.tran` và những lỗi giao diện được yêu cầu trong ảnh: lọc tầng/hành lang; nhập số phòng và tên chi nhánh; popup tạo việc/phân công kiểm tra; tự tải lịch sử/làm lại; đồng bộ trạng thái lọc; bố cục thông báo/vật tư; đóng popup không trắng trang.

Bao gồm các sửa tiếp theo cho menu và liên kết theo quyền, hồ sơ cũ chỉ xem, popup trong viewport, nút thao tác, tên thay ID tham chiếu, trạng thái/thông báo tiếng Việt, tìm kiếm có lựa chọn, lọc bộ phận nhận yêu cầu, giao diện ca làm việc và form điều chỉnh giờ.

Đây là bản chọn theo phạm vi từ working tree chung. Các thay đổi quản trị/cấu trúc, cấu hình hệ thống và luồng yêu cầu điều chuyển vật tư giữa cơ sở còn lại không nằm trong commit này. Một số file dùng chung được stage từng phần; working tree của các phần khác được giữ nguyên.

## Database và cấu hình

GitHub lưu source và script, không đồng bộ database đang chạy. Không commit User Secrets, mật khẩu, cookie, backup, binary, dữ liệu bằng chứng riêng hay log runtime.

- `artifacts/mai-tran-access-review/Install-InspectionWriter.sql`: writer phân công kiểm tra đã được duyệt/cài local. Script yêu cầu transaction và SESSION_CONTEXT pin đúng database/GUID/source commit; không chạy như migration tự động.
- `artifacts/mai-tran-rework-writers/0-*.sql` đến `8-*.sql`: phần writer làm lại/kiểm tra lại đã được duyệt/cài. Giữ thứ tự, đọc precondition ở từng script, chỉ chạy trên baseline phù hợp sau backup/rollback. Không chạy lại trên database đã cài.
- API dùng hai kết nối hạn quyền qua User Secrets: `ConnectionStrings:InspectionAssignmentDb` và `ConnectionStrings:ReworkWorkflowDb`, cùng database với `ConnectionStrings:HotelOpsDb`. Tài khoản SQL riêng phải được quản trị cấu hình theo các role/issuer trong script; không dùng kết nối quyền cao làm fallback.
- `artifacts/worktime-repair/repair-draft.sql`: gói phục hồi local đã duyệt cho hai hồ sơ cũ cụ thể của Lan, bao gồm constraint ON_HOLD; bản trong Git kết thúc bằng ROLLBACK để xem trước. Không áp dụng vào dữ liệu môi trường khác nếu chưa đối chiếu đúng tài khoản/hồ sơ/schema.
- `artifacts/worktime-repair/demo.sql`: dữ liệu [DEMO] để so sánh nhật ký, cũng mặc định ROLLBACK. Không đưa dữ liệu mẫu vào môi trường thật nếu chưa được duyệt.

Ca thật của Lan chưa bị tự đóng. Hai hồ sơ cũ tạm giữ không được coi là có checklist/nguồn hợp lệ để tiếp tục thực hiện.

## Báo cáo chi tiết

- [Các lỗi trong ảnh](../../artifacts/workspace-usability-fix/REPORT.md)
- [Quyền và chức năng Housekeeping Staff](../../artifacts/lan-dang-access-review/REPORT.md)
- [Quét sidebar và popup](../../artifacts/lan-sidebar-sweep/REPORT.md)
- [Hồ sơ cũ và công việc thực hành](../../artifacts/lan-workflow-demo/REPORT.md)
- [Đồng bộ giao diện](../../artifacts/ui-consistency/REPORT.md)
- [Database, nhật ký và form điều chỉnh giờ](../../artifacts/worktime-repair/REPORT.md)

Các báo cáo trên ghi kết quả tại thời điểm từng lượt. Log, ảnh, backup và runner local được nhắc trong báo cáo không được đưa toàn bộ lên GitHub. Kiểm chứng bản nội dung đã stage được cập nhật dưới đây trước commit.

## Kiểm chứng gói commit

- Build từ bản tách riêng tạo bằng `git write-tree` + `git archive` của nội dung staged, không dùng các file ngoài phạm vi trong working tree.
- Backend `dotnet build HotelOps.slnx`: Passed, 0 lỗi; 2 cảnh báo NU1900 do sandbox không tải được dữ liệu vulnerability của NuGet. Không tắt kiểm tra audit để che cảnh báo.
- `dotnet test HotelOps.slnx --no-restore`: exit 0; solution không chứa regression project. Chạy riêng regression liên quan: 54 Passed, 0 Failed/Skipped.
- Frontend: bộ 103 ca đạt 101 ngay; 2 ca thông báo lỗi vì nhãn chọn vẫn kỳ vọng ID. Đã sửa nhãn thành tiêu đề thông báo và chạy lại riêng 2/2 Passed. Giữ assertion về ghi đúng ID, CSRF và số request.
- Tách attribute opt-in cho các kiểm thử writer buồng phòng để không phụ thuộc vào file test điều chuyển vật tư chưa thuộc gói commit; giữ yêu cầu clone riêng, không thay điều kiện SQL test.
- Kiểm thử SQL thực tế ở các lượt trước được ghi trong các báo cáo liên kết. Lượt commit không cài SQL hay thay dữ liệu mới.
- Quét các file staged: không thấy credential/private key/backup/binary/session cookie. Logs kiểm chứng cuối ở `artifacts/housekeeping-commit/` local, không đưa lên GitHub.
- Build frontend cuối trên snapshot: Passed (TypeScript + Vite). Còn cảnh báo bundle >500 kB và `use client` của thư viện bị bỏ qua; không có lỗi build.
