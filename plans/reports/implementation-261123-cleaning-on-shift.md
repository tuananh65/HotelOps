# Tạo yêu cầu dọn phòng và phân công trong ca

## Đã triển khai

- Form dọn phòng thay **Bộ phận xử lý** bằng **Nhân viên dọn phòng**, bắt buộc chọn trước khi lưu. Chỉ trả nhân viên HK_STAFF có quyền CLEANING_EXECUTE, tài khoản/việc làm hoạt động và quyền truy cập chi nhánh hợp lệ.
- Người tạo vẫn cần CLEANING_ASSIGN. Cả người tạo và người nhận phải có Workday WORKING đã bắt đầu, chưa kết thúc, đúng ngày và nằm trong giờ ca theo timezone chi nhánh. PLANNED, ENDED, ca hôm qua/tương lai hoặc giờ ca chưa đến/đã qua không hợp lệ.
- Backend kiểm tra lại khi POST. Không thể vượt điều kiện bằng request trực tiếp hoặc bằng danh sách đã tải trước khi kết thúc ca.
- Tạo công việc và phân công trong transaction Serializable hiện có; tái sử dụng action assign để lưu lịch sử và thông báo. Không còn bước phân công riêng sau khi tạo Cleaning.
- Bộ phận suy ra từ ca người nhận; không tin departmentId do client gửi. Giữ kiểm tra phòng cần dọn, không bị chặn/có khách, trùng việc và RowVersion.
- Phân công lại và bàn giao Cleaning cũng kiểm tra ca người nhận. Lỗi tiếng Việt, chặn submit khi pending, người tạo ngoài ca hoặc không có ứng viên.

## Contract

- `GET /api/v2/work/cleaning-creation-options?branchId=...`: quyền CLEANING_ASSIGN; trả `canCreate` và `candidates`.
- `POST /api/v2/work` với `kind=CLEANING` bắt buộc thêm `assigneeUserId`; thành công trả trạng thái ASSIGNED và assignment đang ACTIVE. Không cho tạo việc Cleaning chưa chọn nhân viên.
- `GET /api/v2/work/candidates?kind=CLEANING`: chỉ nhân viên hợp lệ đang trong ca.
- 403: người tạo ngoài ca hoặc thiếu quyền. 400: thiếu nhân viên. 409: nhân viên ngoài ca/không hợp lệ, room version cũ, phòng không còn cần dọn, tạo trùng.

## Kiểm chứng

- Backend: `dotnet build HotelOps.slnx` PASS, 0 cảnh báo, 0 lỗi. Lần đầu bị API cũ khóa DLL; đã dừng đúng API Project 2 và build lại thành công.
- Frontend: `npm run build` PASS, gồm TypeScript và Vite.
- .NET: `dotnet test HotelOps.slnx` / lần cuối `--no-build`: 43 PASS, 3 SKIP (các integration test cần cấu hình riêng).
- API/SQL Server: `Test-Backend.ps1 -CleaningOnShift -Port 5192`, 27 kiểm tra PASS trên database fixture mới. Bao gồm hai chi nhánh, ca chưa bắt đầu/kết thúc/sai ngày/sai giờ/inactive, thiếu assignee, thiếu quyền, stale room version, không tạo bản ghi khi thất bại, tạo + phân công + lịch sử + thông báo, chống tạo trùng; staff nhận/bắt đầu/hoàn thành về PENDING_INSPECTION, chưa thành READY.
- Browser: `node tests/compact-cleaning-requests.mjs` PASS tại 1440/768/375px với API mock: chọn nhân viên, không còn chọn bộ phận, một POST, thông báo ngoài ca/rỗng, giữ form khi conflict, không tràn ngang.
- `git diff --check` PASS.

Bằng chứng: `artifacts/cleaning-shift-build.log`, `cleaning-shift-frontend-build.log`, `cleaning-shift-dotnet-test.log`, `cleaning-shift-api.log`, `cleaning-shift-browser.log`, `artifacts/compact-v2/backend/cleaning-on-shift-verification.json`, `artifacts/cleaning-requests/verification.json` và ảnh form theo ba viewport.

## File tạo/sửa trong lượt này

- `frontend/src/compact/work.tsx`
- `frontend/src/compact/api.ts`
- `src/HotelOps.Api/Compact/Contracts/Common.cs`
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.Cleaning.cs`
- `frontend/tests/compact-cleaning-requests.mjs`
- `scripts/database/compact-v2/Test-Backend.ps1`
- `scripts/database/compact-v2/test-cleaning-on-shift.ps1`
- `docs/ui/compact-ux.md`
- Plan, báo cáo và nhật ký của thay đổi này.

## Giới hạn và vận hành

- Không sửa schema, grants, dữ liệu nghiệp vụ primary hoặc Project 1. Các thay đổi có sẵn trong workspace được giữ nguyên. Không commit/push, không dùng sub-agent.
- Browser dùng mock; API được kiểm chứng riêng với SQL Server thật trên fixture. Chưa chạy toàn bộ regression của tất cả role; các client/script cũ tạo Cleaning không có assignee và ca thực tế phải chuyển sang contract mới.
- Đã khởi động lại API phát triển Project 2 tại cổng 5768, frontend hiện tại tại 5186. Reload trang để lấy form mới; phải bắt đầu ca hợp lệ trước khi tạo yêu cầu.
