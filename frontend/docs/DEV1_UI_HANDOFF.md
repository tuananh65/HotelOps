# Bàn giao frontend đã nối màn hình DEV1

Tài liệu này thay thế trạng thái "chưa nối màn hình" trong DEV1_CONTROLLER_HANDOFF.md và DOC_TRUOC_KHI_CHAY.txt của gói trước.

## Phạm vi

Frontend gốc FE+BE/inventory-authz commit 1cf8d71692207e01f34b57d1daf5cec3b5b1e40b; backend đối chiếu integration/dev1-fe-handoff commit cf8fb020362f8f64192e7f2f14a10547cebe5b91.

- Sửa src/main.tsx: đưa route DEV1 qua màn hình dùng DTO thật, bỏ luồng screen-context cũ chỉ cho các route được chuyển; thêm navigation trên Profile.
- Thêm src/screens/dev1/model.ts, Dev1Screen.tsx, HistoryScreen.tsx, dev1.css: tải danh sách/chi tiết, form ghi, chọn chi nhánh, phân trang, ETag, loading/error/empty.
- Thêm tests/dev1-ui-model-entry.ts, tests/dev1-ui-model.test.mjs, tests/dev1-ui.browser.spec.ts và playwright.dev1.config.ts.
- Các controller mới của gói trước giữ nguyên. Không sửa services.ts, api/http.ts, authentication, Inventory controller, backend hoặc SQL.
- Không tạo branch/commit/push/merge. Gói ZIP không chứa backend, node_modules, dist, cấu hình bí mật hoặc .git.

## Màn hình đã nối

| Đường dẫn | Chức năng |
|---|---|
| /branches, /branches/:id | Xem cấu trúc một chi nhánh theo ID |
| /locations, /locations/:id | Chuyển tầng/khu vực, danh sách, chi tiết, thêm/sửa |
| /room-types, /room-types/:id | Danh sách, chi tiết, thêm/sửa/vô hiệu hóa |
| /rooms, /rooms/:id | Danh sách, chi tiết chỉnh sửa, thêm/sửa |
| /departments, /departments/:id | Danh sách từ structure, chi tiết, thêm/sửa |
| /checklists, /checklists/:id | Mở theo ID, tạo mới, sửa tiêu chí, xuất bản/ngừng sử dụng |
| /cleaning/assignment | Mở cleaning task theo ID; xem ứng viên và phân công cleaning/inspection |
| /inspections/queue, /inspections/:id/perform, /inspections/:id/result, /inspections/:id/rework, /inspections/:id/reinspect | Mở inspection thật, bắt đầu, nhập PASS/FAIL/ghi chú, ghi defect, gửi kết quả, yêu cầu rework |
| /cleaning/tasks, /cleaning/tasks/:id | Đọc/lưu tiến độ UC032 và tick các mục thực hiện |
| /dev1/task-progress | Đọc/lưu tiến độ UC109 và kết quả tiêu chí |
| /rooms/turnover, /inspections/history | Danh sách/chi tiết lịch sử, tải evidence có sẵn |

Các route /cleaning/tasks/new, /rework, /rework/:id hiển thị giới hạn backend và cho mở task/inspection theo ID; không bịa API tạo cleaning hoặc list rework.

## Quyền và phiên bản dữ liệu

- SessionGate gốc vẫn kiểm tra đăng nhập và bắt đổi mật khẩu. Chỉ route DEV1 dùng SessionGate không truyền screenId, vì quyền UI kiểu cũ không có trong session DTO DEV1. Không thêm grant giả, không thay thuật toán auth, không bỏ authorization ở server.
- Navigation DEV1 hiển thị cho người đăng nhập. Nhìn thấy link/nút không đồng nghĩa được cấp quyền. Backend kiểm tra quyền/branch; 403 được hiển thị và không dùng dữ liệu giả.
- Các màn hình cũ ngoài phạm vi, gồm Inventory, vẫn đi qua guard cũ.
- ID chi nhánh nhập trực tiếp do chưa có API danh sách chi nhánh. ID mặc định không hardcode. Chuyển chi nhánh xóa kết quả/editor cũ và hủy đọc đang chạy.
- Sửa chỉ từ detail/edit đã tải với ETag thật; gửi đúng If-Match. Thiếu ETag khóa thao tác có điều kiện. Sau lưu tải lại để lấy phiên bản mới.
- Chống double-submit trong cùng form. Không tự retry write. 412/428 và kết quả ghi không rõ buộc đóng form và tải lại. 401/403 không hiển thị thành công hoặc danh sách trống.
- Giữ notification=NOT_INTEGRATED dưới dạng thông báo chưa tích hợp, không giả báo gửi thành công.

## Giới hạn còn lại

- Checklist/planning/progress/inspection không có list API tương ứng đầy đủ; mở bằng ID thật. Không cung cấp lựa chọn mã giả.
- Phải có dữ liệu và schema đúng, bao gồm các bản ghi progress đã được khởi tạo. UI này chưa cung cấp quản trị khởi tạo progress/template cleaning-items.
- Backend không có tất cả create/start/pause/resume/complete cleaning task; không thêm API giả.
- 100% progress không tự hoàn tất task; finish inspection không tự phê duyệt phòng.
- UI ghi inspection/progress hiện hỗ trợ kết quả/ghi chú; upload ảnh mới chưa được nối vào form. API controller vẫn hỗ trợ photos; evidence đã có được tải ở trang lịch sử.
- Một số chi tiết native được trình bày theo tên trường backend; chưa dịch toàn bộ nhãn chi tiết.
- Room type capacity và floor number chỉ nằm trong form create theo contract; department code không sửa qua update.
- Database module của DEV1 có yêu cầu disposable trial riêng; không được xóa guard để áp dụng trực tiếp vào HotelOpsDB cũ.

## Kiểm thử đã chạy tại môi trường làm việc

- PASS: npm run build (có cảnh báo bundle >500 kB).
- PASS: 45 test chọn lọc: 6 test model màn hình + 11 test controller DEV1 + 28 regression auth/http/inventory.
- FAIL: toàn bộ npm test có 54 pass, 11 fail. Cùng 11 workflow test đã fail trong frontend gốc, lỗi ở fixture bootstrap đăng nhập. Không sửa test cũ để che lỗi.
- NOT TESTED: 4 Playwright test có sẵn nhưng runner thất bại trước khi mở trình duyệt (Chromium executable thiếu); tải Chromium tự động trả archive không hợp lệ. Không có chứng cứ UI end-to-end PASS.
- NOT TESTED: backend thật/database thật. Không có dotnet, SQL Server hoặc Docker ở môi trường này. Không có truy cập localhost của máy người dùng.

## Chạy trên Windows

Giải nén gói đầy đủ vào thư mục mới, ví dụ D:\HotelOps-DEV1-UI. Không ghi đè source đang làm.

```bat
cd /d D:\HotelOps-DEV1-UI\frontend
npm ci
if not exist .env.local copy .env.example .env.local
npm run build
node --test tests/dev1-ui-model.test.mjs tests/dev1-controller-contract.test.mjs tests/auth-be1-contract.test.mjs tests/http.test.mjs tests/inventory-contract.test.mjs
```

Để chạy test browser giả lập (dừng Vite 5173 đang chạy trước):

```bat
npx playwright install chromium
npx playwright test --config playwright.dev1.config.ts
```

Đây vẫn là test API giả lập, không thay cho kiểm thử backend thật.

Chạy backend riêng trong source DEV1, với database trial/cấu hình/tài khoản phù hợp đã chuẩn bị:

```bat
dotnet run --project src/HotelOps.Api/HotelOps.Api.csproj --launch-profile https
```

Backend nghe HTTPS7019. Không chạy đồng thời backend cũ ở cổng này. Lệnh trên không tự đảm bảo schema/dữ liệu DEV1 đã đủ; xem log khi lỗi.

Trong cửa sổ CMD frontend:

```bat
npm run dev
```

Mở https://localhost:5173/login. Đăng nhập tài khoản thuộc database DEV1. Từ Profile chọn link nghiệp vụ mới; nhập branchId thật rồi Tải dữ liệu.

## Checklist kiểm thử live cần thực hiện

1. F12 → Network → Fetch/XHR. /locations phải gọi /api/branches/{b}/structure, không gọi /api/ui/screens/.../context.
2. Room type, rooms, departments: đọc 200, thêm bản ghi thử và đọc lại; mở detail, sửa và kiểm tra PUT có If-Match + X-CSRF-TOKEN; dữ liệu sau reload khớp backend.
3. Checklist: tạo với roomTypeIds thật và criteria; mở ID trả về; sửa/publish/retire theo trạng thái cho phép. Sau mỗi write đọc lại detail.
4. Planning: mở cleaning task thật; dùng ID nhân viên hợp lệ; kiểm tra trạng thái trả về, không coi NOT_INTEGRATED là đã gửi thông báo.
5. Inspection: mở đúng inspection của tài khoản; start → lưu PASS/FAIL → ghi defect cho tiêu chí FAIL → gửi kết quả theo nghiệp vụ; không mặc nhiên coi finish là approve.
6. UC032/109: dùng task đã có progress schema/model và được phân công cho tài khoản; lưu %, kết quả/ghi chú; reload xác minh persisted. Nếu 503, đọc log backend thay vì sửa frontend đoán lỗi.
7. Hai cửa sổ cùng sửa một bản ghi: cửa sổ thứ hai dùng ETag cũ phải nhận412 và không tự retry. Đổi branch không giữ draft/data cũ.
8. Tài khoản không quyền phải nhận403 khi gọi API; logout rồi đọc lại phải401/redirect login; kiểm tra lại Inventory cũ với đúng backend hỗ trợ Inventory.

Chỉ đánh dấu live PASS sau khi quan sát các request/response và dữ liệu thật. Gói hiện tại hoàn tất việc nối code giao diện trong phạm vi mô tả, chưa xác nhận live integration.
