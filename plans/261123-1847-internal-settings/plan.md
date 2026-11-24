# Cấu hình nội bộ — Trưởng bộ phận

Phần remediation và Admin đã hoàn tất, Production Admin xác minh thành công 18:43.

Quyết định người dùng: tên loại yêu cầu, mô tả, bộ phận nhận mặc định và bật/tắt. Bỏ modal cấu hình báo cáo phòng dạng thẻ lớn/checkbox, chuyển sang tab bảng gọn trên trang Cấu hình nội bộ.

- [x] Kiểm tra FE-13, code, ReferenceValue và mapping. Dùng WORK_CATEGORY/SettingsJson hiện có, không sửa schema, entity hay grants.
- [x] API cấu hình theo branch, DEPT_MANAGER + IR_PROCESS, version và audit. Staff đọc loại hoạt động, tạo yêu cầu dùng loại hợp lệ.
- [x] Trang riêng, hai tab, công tắc xanh/đỏ; form chỉ tên/mô tả/bộ phận, mobile không tràn ngang.
- [x] Build, unit, API fixture, browser. Xác minh Production sau cập nhật trong phạm vi đã được yêu cầu.

Giữ các yêu cầu cũ; chi nhánh chưa cấu hình vẫn tạo như trước. Khi đã cấu hình loại, yêu cầu mới bắt buộc chọn loại đang hoạt động. Bộ phận nhận là mặc định có thể đổi khi tạo theo phạm vi hiện hữu.

Validation: build 0 warnings/errors; 66 unit passed, 3 skipped. Isolated API fixture HotelOpsCompactApiTestfd0e2c92d9d8 passed 15 groups. Browser internal-settings and room-remediation passed; final tab accessibility marker and release packaging pending verification. Readonly review found inactive default-department and stale edit-version issues, both addressed. Early fixture failures were incorrect synthetic role setup and nonexistent revoked_by column; fixed test setup. Early form label test timeout resolved with explicit label/control associations.

COMPLETED: final release browser passed; Production5080 verified with index-B6vfyJWG.js, API40444/frontend21612, trusted TLS and health200. Backup verified before switch. Temporary previews5082/5083 stopped. Full report: plans/reports/implementation-261123-1847-internal-settings.md.
