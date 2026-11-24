# Điều chỉnh workspace vệ sinh — 24/11/2026

## Phạm vi đã chốt

- Hai tab công việc hiện tại/lịch sử vệ sinh cho nhân viên và giám sát; lịch sử theo ngày/tuần với tìm kiếm và lọc.
- Giám sát không được xem trang Tình trạng phòng (đã được người dùng xác nhận); giữ API dữ liệu tối thiểu phục vụ giao việc.
- Thời lượng 20/25/30 phút; dọn lại chọn nhân viên như giao việc và đóng không đổi trang nền.
- Tách Đã giao khỏi Đang dọn; Tất cả đầu tiên; các tab trạng thái trên khung tìm kiếm/sắp xếp.
- Báo sự cố màu cam, chỉ trong bước thực hiện vệ sinh/kiểm tra; chỉnh chi tiết người giao/nhận; bỏ hủy và minh chứng trong luồng vệ sinh.

## Tiến độ

- [x] Đọc UX/source, xác nhận ý ở ảnh 2, kiểm tra tiến trình đang dùng.
- [x] Backend: hàng đợi, lịch sử cá nhân/ngày/tuần, phân công dọn lại, bảo vệ trạng thái và quyền.
- [x] Frontend: hai tab, biểu mẫu, hàng đợi, chi tiết, điều hướng.
- [x] Build/test, kiểm chứng API TEST và trình duyệt desktop/mobile.
- [x] Cập nhật dịch vụ local đã được cho phép; kiểm tra, báo cáo giới hạn.

Không dùng sub-agent theo lựa chọn gần nhất của người dùng. Giữ nguyên thay đổi đang có, không commit/push. Production API 8040:5769; frontend 7680:5080; Develop API 23404:5774; frontend 48136:5174 đang cần dùng. Không còn API validation 5775 cũ.

Sau cập nhật: API Production PID 49196, Develop PID 3876; frontend giữ nguyên PID. Báo cáo chi tiết: `plans/reports/implementation-261124-1833-housekeeping-workspace.md`.
