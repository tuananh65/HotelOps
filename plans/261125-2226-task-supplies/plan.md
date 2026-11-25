# Cấp vật tư theo công việc

Yêu cầu mới thay thế cấp đầu ca: cấp theo sức chứa tối đa khi nhân viên nhận công việc. Tiêu hao vẫn theo số thực tế bổ sung; giữ chứng từ và số dư cũ.

- [x] Đọc luồng cấp đầu ca, reservation, nhận việc và constraints hiện tại.
- [ ] Chuyển allocation thành sổ giữ vật tư của ca, cấp bổ sung tại nhận việc; giữ FEFO và vật tư tái sử dụng.
- [ ] Cập nhật phân công, nhận việc, bắt đầu và bàn giao; không cấp trùng.
- [ ] Cập nhật UI và các thông báo/cấu hình liên quan.
- [ ] Kiểm thử trên database clone; build/test; review độc lập.
- [ ] Bổ sung vật tư hai kho sau khi rõ số lượng/lô/hạn sử dụng. Đang chờ câu trả lời, chưa nhập kho.
- [ ] Cập nhật runtime Development và Production local sau kiểm chứng.

Runtime cần giữ: Development5173/API5081; Production5080. Đã kiểm tra metadata: cả hai đang chạy; health Production cần kiểm tra lại. Không commit/push/reset dữ liệu.
