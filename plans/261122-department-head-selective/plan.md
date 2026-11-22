# Department Head — áp dụng chọn lọc

Yêu cầu hiện tại ưu tiên UX đã chốt hơn bản P0 đính kèm. Không coi mọi đề xuất trong tài liệu là yêu cầu bắt buộc; quy tắc này tiếp tục áp dụng cho tài liệu tiếp theo.

Giữ: chi nhánh dưới logo, tiêu đề navbar, toolbar gọn sát navbar, lịch chọn một đối tượng (không Tất cả), dialog/drawer và phân trang hiện có. Không sửa role_permissions, staffing, schema hoặc dữ liệu vận hành.

- [x] Đọc tài liệu, đối chiếu quyền SQL thực: 14 quyền BRANCH; cả hai chi nhánh có HK/ENG/FO/STORE. Xác nhận Workday đã quản lý toàn chi nhánh.
- [x] Nhóm điều hướng DH theo quyền; tái sử dụng Work/Documents, tách yêu cầu nội bộ, thêm nhân sự ứng viên theo ca chỉ đọc. Các vai trò khác giữ bố cục hiện có.
- [x] Dữ liệu dashboard/báo cáo bổ sung có kiểm tra quyền miền dữ liệu; dùng thống kê thật. Bộ lọc dùng API và không bỏ sót trang.
- [x] Duyệt MR hiển thị theo thẩm quyền backend hiện hành; không thay đổi luật fallback.
- [x] Build FE/BE, kiểm thử UI hồi quy và backend fixture; báo rõ phần chưa kiểm chứng.
- [x] Rà soát diff và cập nhật chuẩn UX/báo cáo.

Không áp dụng nguyên xi: header thêm chi nhánh/breadcrumb, lịch Tất cả hoặc nhiều bộ lọc trùng nhau, quyền quản trị nhân sự, quyền chuyên môn/kho bổ sung. Màn Nhân sự chỉ trình bày ứng viên hợp lệ của ca/ngày đã chọn và lịch tuần, không giả làm danh sách toàn bộ tài khoản.

Process: tái sử dụng FE 5186 (node 6424/9324), API 5768 (dotnet 31332). Chưa tạo process mới. .claude/rules/process-management.md không tồn tại.

Kết thúc: build API bị DLL lock nên dừng đúng API 31332, build lại Passed 0 warnings, khởi động lại PID 40104 cùng cổng 5768 worker tắt; health ok. API fixture 5187 tự dừng. FE tiếp tục dùng process cũ. Báo cáo: `plans/reports/department-head-261122-selective.md`.
