# Phòng cần tạo yêu cầu dọn — HK Supervisor

## Kết quả

Đã sửa nguyên nhân hàng đợi chỉ đọc công việc CLEANING đã tồn tại. Trang mặc định có nhóm **Phòng cần tạo việc**, hiển thị phòng cần dọn chưa có công việc housekeeping đang mở trong đúng chi nhánh. Có tìm theo số phòng trước phân trang 25 dòng, tầng, hạng phòng và sức chứa.

Chọn **Tạo yêu cầu** mở modal điền sẵn phòng/tiêu đề. Sau khi tạo thành công mở bước phân công staff bằng workflow hiện hữu. Nếu giám sát chưa giao ngay, yêu cầu vẫn ở **Chưa phân công**; không tạo lại. Nút Tạo mới cũng dùng danh sách phòng đủ điều kiện; liên kết từ trang phòng chọn đúng phòng, có thể đổi phòng.

Không đưa phòng có khách, inactive, bị block hiệu lực, Ready hoặc đã có việc dọn/kiểm tra/làm lại đang mở vào danh sách tạo. Không tự chuyển phòng Ready hay tự giao/hoàn thành thay nhân viên.

## Backend và tương thích

- GET `/api/v2/work/cleaning-rooms?branchId=...&page=...&search=...&roomId=...`: xác thực CLEANING_ASSIGN + CLEANING_VIEW + ROOM_VIEW theo branch, read-only, total và pageSize 25. Nguồn là rooms + v_room_readiness + work_items; metadata location/room_type dùng mappings hiện hữu.
- GET cleaning-queue thêm search theo số phòng/tên công việc trước phân trang, giữ các nhóm công việc hiện có.
- CreateWorkRequest thêm RoomRowVersion tùy chọn. Luồng mới gửi version của phòng, backend kiểm tra version và điều kiện phòng lại trong transaction Serializable. Từ chối stale/không còn đủ điều kiện, giữ chặn công việc trùng hiện có. Caller cũ không bị ép đổi contract hay lifecycle.
- Không sửa entities, mappings, schema, permissions/grants hoặc dữ liệu nghiệp vụ primary. Không chạy Staffing/migration.

## Kiểm chứng

- `dotnet build HotelOps.slnx`: PASS, 0 warning/error sau khi dừng đúng API Project 2 đang khóa DLL. Lượt đầu thất bại MSB3027/MSB3021 với 10 retry warnings do DLL đang dùng, không phải lỗi biên dịch. API chính đã khởi động lại trên 5768, worker=false; health ok, schema 2.1.0, 40 tables.
- `dotnet test HotelOps.slnx --no-build`: 28 passed, 3 conditional skipped (worker/email suites cần fixture/env riêng). Không gọi skipped là passed.
- `Test-Backend.ps1 -HousekeepingStaff -Port 5187`: 33/33 nhóm trên fixture mới `HotelOpsCompactApiTestd2f06f5ba0c2`, backup đã xác minh trước test writes. Bao phủ phòng chưa có task, tìm kiếm, 403 sai branch/thiếu quyền, version cũ, block, tạo xong ra khỏi intake, giữ trong queue, READY/occupied không xuất hiện, checkout xuất hiện lại và giao task mới đúng turnover. Đồng thời kiểm tra accept/start/complete/checklist, independent inspection, fail/rework/reinspection, không bypass READY và scope/ownership. Artifact: `artifacts/compact-v2/backend/housekeeping-staff-verification.json`.
- `node tests/compact-cleaning-requests.mjs`: desktop/tablet/mobile 1440/768/375; kiểm tra default tab, search/page, create request body/version, mở assignment, chọn staff, phòng rời intake, linked room, conflict không báo thành công và không tràn ngang. API mock; không dùng kết quả này thay bằng chứng SQL. Artifact: `artifacts/cleaning-requests/verification.json` và ảnh.
- `npm run test:compact:ux`: PASS 48 navbar route/viewport checks và suites settings, Reception rooms, Workday, account/dashboard pagination. APIs mocked.
- `node --test tests/compact-workflow.test.mjs`: 5/5.
- Frontend build/typecheck PASS. `git diff --check` PASS; Git còn cảnh báo chuẩn hóa LF/CRLF theo cấu hình máy.

Lượt browser đầu dùng mock related-work sai kiểu (object thay vì array) và tên submit sai (Phân công thay vì Xác nhận); đã sửa fixture kiểm thử theo contract và chạy lại. Không nới guard backend hay sửa dữ liệu primary để làm test pass.

Kiểm tra ảnh mobile phát hiện quy tắc min-width của bảng chung đẩy nút tạo ra ngoài vùng nhìn. Đã bổ sung bố cục riêng cho bảng intake ở mobile, override min-width đúng selector và thêm assertion vị trí nút trước khi click; không thay bảng của trang khác.

## Files tạo/sửa

- `src/HotelOps.Api/Compact/Services/WorkService.Cleaning.cs` — mới, truy vấn phòng đủ điều kiện.
- `src/HotelOps.Api/Compact/Services/WorkService.cs` — search queue và guard version/eligibility khi tạo từ intake.
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs` — endpoint đọc và search parameter.
- `src/HotelOps.Api/Compact/Contracts/Common.cs` — RoomRowVersion tùy chọn cho CreateWorkRequest.
- `frontend/src/compact/cleaning-rooms.tsx`, `cleaning-rooms.css` — mới, bảng/picker; mobile xếp metadata thành từng ô để nút Tạo yêu cầu luôn thấy, không cần kéo ngang.
- `frontend/src/compact/cleaning-queue.tsx` — tab intake và tìm kiếm.
- `frontend/src/compact/work.tsx` — chọn phòng/modal tạo, prefill và chuyển bước phân công.
- `frontend/src/compact/api.ts` — thông báo phòng không còn đủ điều kiện.
- `frontend/tests/compact-cleaning-requests.mjs` — mới, browser test.
- `scripts/database/compact-v2/test-housekeeping-staff.ps1` — mở rộng kiểm thử API fixture.
- `docs/ui/compact-ux.md`, plan này và journal `plans/journals/2026-11-23-supervisor-cleaning-request-rooms.md`.

Chỉ kiểm tra dữ liệu thật trên fixture độc lập; không login/ghi nghiệp vụ thử lên primary. Không commit/push trong lượt sửa này. Không có blocker còn lại trong phạm vi yêu cầu.
