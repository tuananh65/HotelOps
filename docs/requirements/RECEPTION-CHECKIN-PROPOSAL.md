# Nhận khách cho Lễ tân — phạm vi đã duyệt

Người dùng duyệt ngày 20/11/2026, từ đề xuất ban đầu tại `artifacts/reception-room-demo/CHECKIN-PROPOSAL.md`. Bản này lưu quy tắc trong Git để các thành viên có thể đối chiếu.

- RECEPTION đang hoạt động, có ROOM_CHECKIN và ROOM_VIEW đúng chi nhánh, được nhận khách cho phòng hoạt động có trạng thái READY/AVAILABLE, vệ sinh/kiểm tra hiện tại đạt và không có khóa hiệu lực hoặc công việc đang mở.
- GET/POST `/api/branches/{branchId}/rooms/{roomId}/check-in`; POST yêu cầu phiên hợp lệ, CSRF và If-Match. Transaction SERIALIZABLE ghi OCCUPIED, lịch sử CHECKIN và audit cùng nhau; từ chối phiên bản cũ/gửi trùng.
- Hiển thị Có khách riêng với mức sẵn sàng vệ sinh. Không đổi vai trò tài khoản; không cho nhận khách lần nữa khi đang có khách.
- Vệ sinh, phân công, kiểm tra và làm lại vẫn do vai trò phụ trách. Lễ tân không truy cập hộp việc hoặc thực hiện vệ sinh/làm lại chỉ nhờ CLEANING_VIEW.
- Trả/chuyển phòng được duyệt sau, theo [đề xuất bổ sung](RECEPTION-CHECKOUT-MOVE-PROPOSAL.md). Không bao gồm đặt phòng, dữ liệu khách hoặc thanh toán.

Không thêm trạng thái, mã quyền, schema hoặc thay đổi generated entities cho thao tác nhận khách.
