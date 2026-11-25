# Cấp vật tư theo công việc

Yêu cầu mới thay thế cấp đầu ca: cấp theo sức chứa tối đa khi nhân viên nhận công việc. Tiêu hao vẫn theo số thực tế bổ sung; giữ chứng từ và số dư cũ.

- [x] Đọc luồng cấp đầu ca, reservation, nhận việc và constraints hiện tại.
- [x] Chuyển allocation thành sổ giữ vật tư của ca, cấp bổ sung tại nhận việc; giữ FEFO và vật tư tái sử dụng.
- [x] Cập nhật phân công, nhận việc, bắt đầu và bàn giao; không cấp trùng.
- [x] Cập nhật UI và các thông báo/cấu hình liên quan.
- [x] Kiểm thử service trên database clone; build/test; review độc lập. SQL1pass, rollback fixture; chưa kiểm thử handover HTTP.
- [x] Nhập hai kho: chứng từ450/451, 8 loại x100 mỗi kho. Owner cho phép tự đặt hạn nội bộ; đã có policy chặn reusable quá hạn.
- [x] Cập nhật runtime Development và Production local; health57 và Edge smoke cả haiPASS.

Runtime cần giữ: Development5173/API5081; Production5080. Owner bổ sung lúc22:56: đổi tên Maintenance Lead, push source và database lên main. Đã commit source và gói backup mã hóa, merge lịch sử main không xung đột; chưa push. Khóa và backup thô giữ ngoài Git vì repository public. Không reset dữ liệu.

Build cuối0warning0error; tests166pass4skip, frontend9pass và buildpass; SQL deployment test57bảng234FK116CHECK. Luồng task-supply SQL integration đang kiểm chứng riêng. Backup mã hóa thử roundtrip khớp SHA256 và VERIFYONLY.
