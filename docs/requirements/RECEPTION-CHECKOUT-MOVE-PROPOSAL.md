# Đề xuất hoàn tất nghiệp vụ Lễ tân — 20/11/2026

Trạng thái: người dùng đã duyệt cả trả/chuyển phòng trong phiên làm việc ngày 20/11/2026. Nhận khách đã được duyệt riêng; đề xuất này bổ sung trả và chuyển phòng.

## Phạm vi và quy tắc

- **Trả phòng**: Lễ tân có ROOM_CHECKOUT và ROOM_VIEW tại chi nhánh được xác nhận phòng đang OCCUPIED thành DIRTY. Phòng bị khóa vẫn được trả; lệnh khóa không bị xóa. Ghi lịch sử CHECKOUT và audit cùng transaction.
- **Chuyển phòng**: Lễ tân có ROOM_OCCUPANCY_MOVE và ROOM_VIEW được chuyển giữa hai phòng khác nhau trong cùng chi nhánh. Phòng nguồn phải OCCUPIED; phòng đích hoạt động, READY/AVAILABLE, vệ sinh/kiểm tra đạt và không bị khóa. Nguồn thành DIRTY, đích thành OCCUPIED trong một transaction. Bắt buộc nhập lý do. Lịch sử nguồn CHECKOUT và đích CHECKIN có lý do chuyển phòng; hai audit dùng chung request_id, lưu số phòng nguồn/đích. Không tạo thêm mã trạng thái/permission/trigger.
- Các thao tác yêu cầu phiên hợp lệ, CSRF và phiên bản dữ liệu nguồn; chuyển phòng kiểm tra cả hai phiên bản, khóa phòng theo thứ tự ID để tránh khóa chéo. Gửi lặp/dữ liệu cũ bị từ chối, không tự gửi lại khi chưa rõ kết quả.
- Sau trả/chuyển, kết quả vệ sinh/kiểm tra trước thời điểm trả phòng chỉ dùng tra cứu, không chứng minh phòng sẵn sàng. Mức sẵn sàng là Chờ vệ sinh (hoặc Bị khóa nếu có khóa). Giám sát Buồng phòng tạo/phân công chu kỳ mới bằng luồng hiện có; Lễ tân không được tự tạo checklist, phân công, hoàn tất vệ sinh hoặc kiểm tra.
- Chặn trả/chuyển khi nguồn còn công việc vệ sinh/kiểm tra/làm lại chưa kết thúc, để người phụ trách xử lý theo luồng hiện hành. Không tự kết thúc hồ sơ hay giờ làm việc của nhân viên.
- Khi đang OCCUPIED, một kết quả kiểm tra vệ sinh không được ghi đè trạng thái sử dụng thành READY. Quyền và vai trò tài khoản không thay đổi sau thao tác.
- Không xử lý đặt phòng, thông tin khách, thanh toán, chuyển khác chi nhánh, hủy/đảo nhận khách trong gói này.

## API và giao diện dự kiến

- GET/POST `/api/branches/{branchId}/rooms/{roomId}/check-out`.
- GET `/api/branches/{branchId}/rooms/{roomId}/move-options`: phòng đích có thể nhận khách, số phòng/tầng/loại phòng, phiên bản từng phòng.
- POST `/api/branches/{branchId}/rooms/{roomId}/move`: phòng đích, phiên bản đích, lý do; If-Match cho nguồn.
- Nút Trả phòng / Chuyển phòng nằm trong popup chi tiết phòng, chỉ xuất hiện theo điều kiện API. Popup xác nhận nằm trong màn hình; chọn đích bằng số phòng/tên, không nhập ID.

## Tác động dùng chung và kiểm chứng

Thay đổi FE-03, cách đọc readiness và phần kết luận kiểm tra FE-05 để giữ đúng trạng thái có khách. Dùng các bảng/cột hiện có (`rooms`, `room_status_history`, `audit_logs`, các bảng chu kỳ vệ sinh); không migration, không sửa generated entities, không cấp thêm quyền. Dữ liệu quyền hiện có chỉ được dùng theo quy tắc đã chốt.

Kiểm thử: vai trò khác/chi nhánh khác bị chặn; nguồn không có khách; đích bị khóa/chưa đạt/đã có khách; cùng phòng; phiên bản cũ; thiếu lý do; gửi trùng; rollback toàn bộ khi một bước thất bại; trạng thái sau trả không dùng lại nghiệm thu cũ; nhận khách lại chỉ sau chu kỳ mới; số lịch sử/audit chính xác. Thao tác kiểm thử ghi dữ liệu chỉ trên các phòng DEMO-LT.
