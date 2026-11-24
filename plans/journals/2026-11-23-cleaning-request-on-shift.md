# Yêu cầu dọn phòng trong ca

Theo yêu cầu mới, form tạo Cleaning chọn nhân viên thay bộ phận và tạo/phân công nguyên tử. WorkService kiểm tra người tạo có CLEANING_ASSIGN; nhân viên có HK_STAFF, CLEANING_EXECUTE, tài khoản và branch membership hợp lệ. Hai người phải có ca WORKING đã bắt đầu, chưa kết thúc và trong khung giờ địa phương. Tái sử dụng transaction, assignment, lịch sử và notifications hiện có; không sửa permission/schema.

Kiểm chứng bằng build FE/BE, 43 test .NET đạt/3 bỏ qua, 27 kiểm tra API fixture và UI mock tại 3 kích thước. Đã khắc phục khóa DLL do API đang chạy; không ghi test lên primary. Quyết định UX được cập nhật trong docs/ui/compact-ux.md. Báo cáo: plans/reports/implementation-261123-cleaning-on-shift.md.
