# Bàn giao frontend C + 8

Nhánh đang làm: feature/front-end-new-cacao, nền integration fb2c46cc. Nhánh này đã tồn tại và đang được chọn khi bắt đầu lượt sửa; không tạo thêm nhánh trùng, không commit/push/deploy.

## Đã sửa

- Áp dụng C + 8 vào frontend React thật qua main.tsx: thanh nhóm nghiệp vụ và menu chức năng hai tầng, cacao–kem, menu mobile.
- Bổ sung CSS cho markup của các màn tích hợp API; xử lý các lớp màu cũ và bảng/biểu mẫu trước đây không khớp CSS prototype.
- Chuẩn hóa bảng, bộ lọc, fieldset, form đăng nhập/đổi mật khẩu, kho, cấu trúc và workspace sự cố/vật tư. Fieldset dùng toàn bộ chiều rộng để lưới trường co giãn đúng.
- Trang người dùng gom trường theo nhóm, giữ ID/mã nhân viên/tên đăng nhập/email/chi nhánh/phòng ban/vai trò/trạng thái; thêm xem nhanh từ kết quả hiện hành và liên kết hồ sơ đầy đủ.
- Điều hướng lấy từ API hiện có, giữ badge thông báo, bỏ menu lặp và không cấp quyền mới. Khi API điều hướng lỗi, kết thúc loading và ẩn liên kết nghiệp vụ.
- Giữ cây route ổn định qua thay đổi trạng thái xác thực, không làm mất thông báo đăng xuất chưa xác nhận.

## Tệp của lượt sửa

Sửa:
- frontend/src/main.tsx
- frontend/src/shared-host/HomeNavigation.tsx
- frontend/src/users/UserDirectoryPage.tsx

Thêm:
- frontend/src/shared-host/CacaoShell.tsx
- frontend/src/styles/cacao.css
- frontend/tests/cacao-shell.test.mjs
- DESIGN.md
- .impeccable/design.json
- docs/frontend-redesign/cacao-direction.md
- docs/frontend-redesign/cacao-review.md
- docs/frontend-redesign/cacao-handoff.md

Các preview và PRODUCT.md có từ lượt trước. package-lock.json hiện có dấu modified nhưng git diff không có thay đổi nội dung; không thay dependency. Các thư mục .agents/.codex/.vscode không thuộc thay đổi frontend này.

## Kiểm chứng

- Build: Passed — npm run build (tsc -b và Vite).
- Test mới: Passed — 4/4 trong tests/cacao-shell.test.mjs, dựng từ entrypoint ứng dụng thật và CSS thật, API được mock tại browser. Bao gồm CSS được tải, 2 tầng điều hướng, quick view, lọc/reset, mobile, lỗi điều hướng, đăng nhập ẩn shell và giữ feedback khi logout thất bại.
- Browser smoke: 7 màn ở 1440px và 390px, cộng login mobile; không tràn ngang toàn trang, không có pageerror. Có đọc ảnh chụp đại diện sau render. Các màn không có fixture dữ liệu thành công chỉ được kiểm tra trạng thái đầu hoặc unavailable, không phải toàn bộ workflow.
- Route inventory: giữ nguyên 163 khai báo route trong 5 file route host so với HEAD. Đây là chứng cứ bảo toàn route, không đồng nghĩa 80 màn đã được kiểm thử toàn chức năng.
- Prettier trên tệp sửa: Passed. git diff --check: Passed; Git có cảnh báo chuyển LF sang CRLF.
- Impeccable detect trên shell/CSS/directory: [] (không có finding).
- Test cũ: Failed khi chạy shared-auth-host.test.mjs và users-api.test.mjs. Hai lỗi đại diện được chạy lại trên bản HEAD nguyên trạng trích xuất riêng: canonical operations navigation reaches integrated Users and Structure reads (thiếu link Users trong fixture), concurrent reads stay independent (fixture response không được decoder chấp nhận). Cả hai vẫn fail trên bản gốc. Không tuyên bố mọi lỗi test cũ đều đã phân loại hay đã sửa.
- API thật: sau lượt nhập dbdemo.sql, backend và proxy đã kết nối; session trả 401 khi chưa đăng nhập, CSRF trả 200. SQL Server đã kiểm chứng. Chưa xác minh đăng nhập và workflow có quyền; xem database-import-handoff.md để biết chi tiết và lỗi tác vụ nền còn lại.
- Không sửa C#; không chạy dotnet build/test cho lượt UI này.

## Cảnh báo còn lại

Vite vẫn cảnh báo React Router bỏ qua directive use client và bundle JS khoảng 1.60 MB vượt ngưỡng 500 kB. Chưa thay chiến lược chia bundle. Copy Anh/Việt và một số nhãn module cũ còn giữ nguyên. Browser evidence dùng dữ liệu minh họa trong test, không thêm mock vào production.

## Chạy và xem

Frontend dev đang phục vụ http://127.0.0.1:5175/. Cần backend đúng API_PROXY_TARGET và phiên đăng nhập hợp lệ để mở các màn có quyền. Có thể chạy lại trong frontend: npm run dev -- --port 5175. Build output nằm tại frontend/dist.

Review giao diện được thực hiện trực tiếp theo quy trình dự phòng Impeccable, không có reviewer agent độc lập. Verdict ship chỉ áp dụng phần trình bày đã kiểm tra và lỗi fieldset đã sửa. Ảnh và log cục bộ: .impeccable/review/cacao/ và .impeccable/review/cacao-baseline/baseline-test-results.txt.
