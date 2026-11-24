# Cấu hình vận hành hợp nhất

## Đã triển khai

OM có một menu Cấu hình với bốn tab: Ca làm việc, Vật tư phòng, Yêu cầu nội bộ, Báo cáo phòng. Tab hiện theo quyền hiện có; truy cập tab không có quyền không tải dữ liệu của tab đó. Mỗi tab dùng lại component/API hiện có; giữ các nhóm A/B/C của vật tư. Nút Cấu hình ca từ lịch đi thẳng tab Ca làm việc, có nút quay lại lịch. Giữ URL cũ supply-config/internal-settings và tab báo cáo phòng. Tải lại giữ tab; đổi chi nhánh remount dữ liệu và form. Navbar chỉ có một tiêu đề. Bảng cấu hình ca trên điện thoại chuyển thành dòng có nhãn.

## Kiểm chứng

- Frontend TypeScript + Vite production build: Passed. Cảnh báo output ngoài frontend không tự xóa là có chủ đích để giữ bản trước.
- Compact unit tests: 9 Passed.
- Browser trên develop 5174 và bundle Production 5080: Passed. Kiểm tra shortcut từ lịch/quay lại, bốn tab, tải lại, URL cũ, tiêu đề duy nhất, chọn bộ phận trước đối tượng, đổi chi nhánh tải đúng branchId, vai trò chỉ có SHIFT_MANAGE không tải cấu hình vật tư, responsive 1440/390/320px.
- Browser hồi quy cấu hình vật tư A/B/C: Passed.
- Các browser test intercept API bằng fixture, không ghi dữ liệu thật. Không đổi endpoint/backend/schema/grants nên không chạy lại test C# hoặc migration trong lượt này. Không tuyên bố đã kiểm thử ghi API bằng browser fixture.
- Lần chạy đầu dùng selector exact label Đối tượng/Bộ phận không khớp nhãn có hướng dẫn; sửa selector test và chạy lại toàn bộ thành công. Ảnh mobile chờ hiệu ứng sidebar kết thúc trước khi chụp.

## Bản chạy

Frontend đã cập nhật ở https://127.0.0.1:5080/#configuration?tab=shifts. Artifact mới: artifacts/production-261124-configuration-final/frontend; bản frontend trước được lưu tại frontend-before cùng thư mục cha. Không đổi process API, không chạy DDL hoặc ghi dữ liệu. Production API giữ PID 28312, frontend giữ PID 7680. Có thể phục hồi frontend-before vào thư mục artifacts/production-261124-p0/frontend, index.html sau cùng. Không có server kiểm thử lâu dài mới.

## Files

- frontend/src/compact/operations-configuration.tsx (mới)
- frontend/src/compact/operations-configuration.css (mới)
- frontend/src/compact/app.tsx
- frontend/src/compact/internal-settings.tsx
- frontend/src/compact/workday.tsx
- frontend/src/compact/workday-settings.tsx
- frontend/tests/operations-configuration-browser.mjs (mới)
- docs/ui/compact-ux.md
- Báo cáo này.

Bằng chứng: artifacts/configuration-validation/browser-results.json và ảnh full/restricted-1440/390/320.png. Các thay đổi vật tư chưa commit của lượt trước được giữ. Không commit/push trong lượt này. Không có câu hỏi nghiệp vụ còn chờ trong phạm vi tổ chức trang cấu hình.
