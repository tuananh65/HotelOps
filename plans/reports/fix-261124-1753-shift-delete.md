# Nút xóa ca — 24/11/2026

Đã thêm nút Xóa ca màu đỏ trong Cấu hình > Ca làm việc và màn cấu hình mở từ lịch. Xác nhận hiển thị tên/giờ/bộ phận/đối tượng; dùng PUT workday/shifts/{id} hiện có với IsActive=false. Ca rời danh sách đang dùng; bật Hiện ca đã xóa / ngừng sử dụng để xem lại hoặc sửa. Không xóa lịch sử/phân công/chấm công.

Quyền SHIFT_MANAGE, branch, row version và audit giữ nguyên. Backend từ chối ca có PLANNED hoặc WORKING; UI hiển thị lỗi và giữ dialog. Không đổi backend, schema, grants. Không thao tác xóa ca thật trên Production.

Kiểm chứng:
- Frontend TypeScript/Vite build Passed; cảnh báo outDir ngoài frontend có chủ đích để giữ bundle cũ.
- Compact unit tests 9 passed.
- Browser fixture develop và Production: xác nhận/hủy, backend lỗi, xóa thành công, xem lại inactive và bật lại, các tab/quyền và responsive 1440/390/320px đạt.
- Real API clone HotelOpsCompactWorkdayTestWarehouse261124 sau backup VERIFYONLY: tạo ca kiểm thử rồi ngừng, giữ bản ghi; HK/cross-branch/stale bị từ chối; ca còn phân công thực tế bị chặn và không đổi. Evidence artifacts/shift-delete-validation/api-results.json.
- Không chạy lại .NET build/test vì không sửa C#. API dùng binary đã build/kiểm thử từ lượt trước.
- Lần đầu browser test bị chặn bởi assertion chỉ cho GET của bộ test điều hướng cũ; bổ sung mock PUT, không cho request đi đến API thật, chạy lại đạt. API kiểm thử lịch tuần lần đầu thiếu date trả400; bổ sung ngày và chạy lại đạt. Không còn lỗi chưa xử lý.

Production frontend đã cập nhật https://127.0.0.1:5080/#configuration?tab=shifts. API giữ PID33756, frontend PID7680. Develop frontend5174 nhận source mới, API5774 giữ PID1700. API thử5775 đã dừng. Backup frontend tại artifacts/production-261124-shift-delete/frontend-before, phục hồi bằng copy assets trước index.html. Health Production ok schema3.0.0. Không commit/push.

Files:
- frontend/src/compact/workday-settings.tsx
- frontend/src/compact/workday.css
- frontend/tests/operations-configuration-browser.mjs
- scripts/verification/shift-deletion.mjs (mới)
- docs/ui/compact-ux.md
- Báo cáo này.

Browser evidence: artifacts/configuration-validation/browser-results.json và ảnh full/restricted-1440/390/320.png. Database TEST backup: artifacts/workday/backup-HotelOpsCompactWorkdayTestWarehouse261124.json. Không thay đổi dữ liệu/schema Production trong lượt này.