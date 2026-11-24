# Checklist OM và áp dụng theo hạng phòng

**CANCELLED theo quyết định người dùng 23/11/2026 14:40:** checklist dùng bản giấy, người dùng quyết định toàn bộ; không xây dựng cấu hình/gán mẫu trên hệ thống. Các đầu việc chưa hoàn tất bên dưới đã hủy, không tự tiếp tục. Đã hoàn nguyên đúng các chỉnh sửa source của lượt này, giữ các thay đổi trước đó. Không cập nhật DB/grants/Production. Ràng buộc checklist trong workflow cũ cần yêu cầu chuyển đổi riêng, không tự bỏ validation hoặc xóa lịch sử.

- [x] Audit source và database read-only: có mẫu/phiên bản/snapshot nhưng thiếu liên kết hạng phòng; OM thiếu CHECKLIST_MANAGE.
- [ ] Chuẩn bị phương án quyền/schema có thể review; xin duyệt phần thay đổi database theo AGENTS.md.
- [ ] Hoàn thiện UI quản lý mẫu và gán CLEANING/INSPECTION theo hạng phòng, theo quyền/branch.
- [ ] Backend CRUD liên kết, validation/concurrency/audit; tự chọn đúng mẫu khi tạo công việc mới, giữ snapshot cũ.
- [ ] Tests trên database độc lập, build FE/BE, browser; chỉ áp dụng Production sau phê duyệt.

Không sub-agent. Không đổi quyền vai trò khác. Không dùng ROOM_VIEW để thay thế quyền quản lý checklist. Không lưu mapping vào description hoặc browser storage.
