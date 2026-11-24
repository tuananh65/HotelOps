# Tạo và giao việc dọn phòng trong ca

Phạm vi: Project 2 Compact; một agent, không đổi schema/grants, không ghi thử primary.

- [x] Rà soát form, WorkService, Workday, mappings và quyền hiện tại.
- [x] Tạo và phân công trong một transaction; nhân viên HK_STAFF + CLEANING_EXECUTE + ACTIVE + đúng chi nhánh.
- [x] Người tạo và người được giao có ca WORKING đã bắt đầu, chưa kết thúc, trong giờ ca theo timezone chi nhánh.
- [x] Form chọn nhân viên thay bộ phận; trạng thái lỗi/rỗng/chưa vào ca bằng tiếng Việt.
- [x] Frontend/backend build; 43 test .NET đạt, 3 bỏ qua; 27 kiểm tra API fixture đạt; browser responsive 1440/768/375 đạt.
- [x] Chạy lại 27 kiểm tra API trên HotelOpsCompactApiTestffbfbce2d429 sau build cuối; API phát triển 5768 trả health OK, PID 34228.
- [x] Hoàn tất báo cáo, nhật ký; không commit/push hoặc ghi dữ liệu thử lên primary.
