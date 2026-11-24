# Admin: trạng thái và mô tả danh mục

Yêu cầu trước đã hoàn tất và xác minh Production18:22, xem báo cáo remediation. Đang xử lý yêu cầu tiếp theo.

- [ ] Công tắc xanh/đỏ có nhãn trợ năng, cập nhật qua API/version hiện có; không checkbox trạng thái trong form Admin.
- [ ] Cột mô tả/trạng thái bộ phận và loại phòng; Dept description thiếu trong DB, proposal đã chuẩn bị và đang chờ người dùng duyệt.
- [ ] Ẩn BranchContext chỉ SYS_ADMIN, giữ cho vai trò khác.
- [ ] Build, browser, API fixture nếu thay backend, sao lưu trước schema; cập nhật Production trong phạm vi đã duyệt.

Không thay trạng thái vai trò hoặc quyền mới. Không xóa chi nhánh hoặc menu cấu trúc khách sạn. Những thay đổi có sẵn trên nhánh được giữ nguyên.

18:26 user approved department description schema/mapping/API; test isolated then backup/apply Production. Scope is one nullable nvarchar(2000) column only.

18:42: implementation, build, unit, API and browser checks passed (13 API groups, 66 unit passed/3 skipped, 2 browser suites). Backup verified before primary column change. API31636 started; frontend switch first attempt stopped on health readiness, retry after startup. Publish OFL retry warning resolved with matching source/destination hashes. No config-internal implementation started yet.

18:43: COMPLETED. Production HTTPS5080 verified with new bundle, trusted TLS, API401 without session, no browser errors. All items above completed, including approved description schema.
