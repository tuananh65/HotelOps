# Hàng đợi vệ sinh và workspace bảo trì — 24/11/2026

## Kết quả

IMPLEMENTED và VERIFIED trên API TEST cùng giao diện desktop/mobile:

- Tất cả gồm phòng cần dọn chưa có việc và công việc hiện tại. Phòng cần dọn là tab thứ hai; bỏ tab Chưa phân công nhưng giữ việc chưa giao trong Tất cả. Tổng, tìm kiếm, sắp xếp và phân trang chạy trên danh sách gộp. Không lặp phòng đã có việc mở.
- Sidebar nhân viên buồng phòng ẩn Công việc khác, Thông báo, Vật tư của tôi, Sự cố phòng và Lịch sử công việc. Giữ quyền và link báo sự cố từ công việc đang làm; lịch sử vệ sinh vẫn có trong trang dọn phòng.
- Chờ kiểm tra và Kiểm tra phòng gặp sự cố có badge vàng.
- Giám sát bảo trì có một workspace Công việc bảo trì, gồm sự cố đã chuyển kỹ thuật và công việc bảo trì độc lập. Sự cố gốc chỉ xuất hiện một dòng, không lặp các lần sửa chữa con. Có nhóm chờ tiếp nhận, đang xử lý, chờ kiểm tra, lịch sử, tìm kiếm và phân trang. Các URL cũ room-issues/in-progress vẫn mở workspace chung.

## Nguyên nhân không tiếp nhận được

Audit dữ liệu Production: sự cố 660 và 662 đã được chuyển đúng sang PENDING_MAINTENANCE_SUPERVISOR, chưa có người nhận. Nhân viên 437 có phân công ca 37 nhưng trạng thái PLANNED, chưa có StartedAt. Đây chưa phải ca WORKING hợp lệ theo WorkdayAuthority.

Backend đúng khi từ chối thao tác ngoài ca. Lỗi trải nghiệm là nút bị khóa nhưng không giải thích. Request detail nay trả `actionBlockedReason`; drawer hiển thị nguyên nhân, link Lịch làm việc và nút Kiểm tra lại. Nhân viên cần tự bắt đầu ca rồi tiếp nhận. Không tự chấm công, không nới quyền hoặc bỏ kiểm tra ca.

## Bằng chứng kiểm thử

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx --no-restore` | Passed, 0 lỗi, 0 cảnh báo |
| `dotnet test HotelOps.slnx --no-build --no-restore` | Passed 101, Failed 0, Skipped 3 |
| `npm run build` | Passed |
| `npm run test:compact:ci` | Passed 9/9 |
| API housekeeping regression | Passed cả DVBV/DVCT trên TEST |
| API maintenance workflow | Passed báo hỏng → HK xác nhận → ngoài ca 403 → Start Shift → tiếp nhận → sửa chữa → kiểm tra lại → lịch sử |
| Retry tiếp nhận | Một sự cố 780 chỉ tạo một việc sửa chữa 781 |
| Phân quyền | Cross-branch và HK Staff gọi maintenance-inbox bị 403; queue không hợp lệ 400 |
| Giao diện | HK supervisor, HK Staff, maintenance supervisor ở 1440px và 390px; không lỗi JavaScript; drawer công việc độc lập mở/đóng được |
| Local release | HTTPS5080 phục vụ bundle mới; API5769/5774 và proxy5080 trả 401 khi không đăng nhập |

Ba bài bỏ qua có sẵn: `MaintenanceWorkerTests.LeadPlanCreatesOneMaintenanceOccurrenceAcrossRepeatedTicks`, `WorkerIntegrationTests.RecurrenceAndLowStockAreCommittedOnceAcrossRepeatedTicks`, `AccountManagementTests.ManagementAndResetPreserveSecurityAndRollbackOnEmailFailure`. Không tính là đã chạy đạt.

Release publish thành công nhưng có một cảnh báo MSB3026 khi chép Resources/Fonts/OFL.txt; retry thành công. Hash file nguồn và file publish đều `3C05A56499A20EE045A6D36834B6A9E1108F359EEDE10D7C1613BC4524D01EEF`.

Bằng chứng local:

- `artifacts/maintenance-workspace/runtime.json`, `runtime-checkpoint.json`: API thật, sự cố 780/việc sửa chữa 781.
- `artifacts/maintenance-workspace/browser.json`, ảnh inbox/off-shift: browser với fixture API.
- `artifacts/housekeeping-workspace/runtime.json`, `browser.json`: hàng đợi, phân quyền và giao diện.
- `artifacts/maintenance-workspace/local-release.json`: asset mới và HTTP sau cập nhật.

## Database và phiên bản local

- Không thay schema, grants, mật khẩu hay lịch làm việc Production/Develop.
- Test nghiệp vụ có ghi dữ liệu chỉ trên `HotelOpsCompactWorkdayTestWarehouse261124`. Backup COPY_ONLY/CHECKSUM và VERIFYONLY đã đạt trước khi tạo tình huống ca PLANNED. File backup: `HotelOpsCompactWorkdayTestWarehouse261124_before_workday_20261124_203753248.bak` trong thư mục backup SQL Server.
- TEST đã bắt đầu lại ca qua API, hoàn tất sửa chữa và kiểm tra sự cố. Checkpoint tránh reset ca lại khi chạy lại fixture đã hoàn thành.
- Production: API PID30152, port5769, DB HotelOpsCompactDB. Frontend HTTPS5080, PID7680.
- Develop: API PID8072, port5774, DB HotelOpsCompactWorkdayTestP0261124. Vite5174, PID48136.
- DLL chung: `artifacts/production-261124-maintenance-workspace/api-verified/HotelOps.Api.dll`, SHA256 `7ECF423D33F8DAFCE907F57AB6C719F334AF0C99EAE8C36A72795AB5DD1A6636`.
- Frontend: `index-DIvRyoep.js`, `index-B9cduFHY.css`.
- Không commit/push. Không dùng sub-agent.

## Tệp thay đổi trong yêu cầu này

Frontend:

- `frontend/src/compact/app.tsx`
- `frontend/src/compact/cleaning-queue.tsx`
- `frontend/src/compact/ui.tsx`
- `frontend/src/compact/compact.css`
- `frontend/src/compact/room-issues.tsx`
- `frontend/src/compact/work.tsx`
- `frontend/src/compact/maintenance-workspace.tsx` (mới)

Backend:

- `src/HotelOps.Api/Compact/Services/WorkService.cs`
- `src/HotelOps.Api/Compact/Services/WorkService.MaintenanceInbox.cs` (mới)
- `src/HotelOps.Api/Compact/Services/RequestWorkflowService.cs`
- `src/HotelOps.Api/Compact/Contracts/RequestWorkflowContracts.cs`
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`

Kiểm thử/tài liệu:

- `frontend/tests/housekeeping-workspace-browser.mjs` (bổ sung test vào tệp đang chưa commit từ yêu cầu trước)
- `frontend/tests/maintenance-workspace-browser.mjs` (mới)
- `scripts/verification/maintenance-workspace-fixture.ps1` (mới)
- `scripts/verification/maintenance-workspace-runtime.mjs` (mới)
- `scripts/verification/maintenance-workspace-local.mjs` (mới)
- `docs/ui/compact-ux.md`
- `plans/261124-2017-housekeeping-maintenance-workspace/plan.md` và báo cáo này.

Các thay đổi có sẵn từ yêu cầu trước được giữ nguyên. Launcher và bản publish nằm trong artifacts local.

## Khôi phục và giới hạn

- Bản API trước còn tại `artifacts/production-261124-incident-configuration/api-verified`. Frontend trước cập nhật được lưu tại `artifacts/production-261124-maintenance-workspace/frontend-before`.
- Khi cần rollback, xác minh PID/port, dừng đúng API mới, khởi động DLL cũ với đúng database và cấu hình môi trường, phục hồi index/assets frontend từ bản lưu. Không cần rollback schema.
- API thao tác nghiệp vụ được kiểm thử trên TEST; không thực hiện tiếp nhận sự cố hoặc bắt đầu ca thay nhân viên thật trên Production. Browser kiểm tra giao diện dùng fixture được nêu rõ, không coi đó là kiểm chứng dữ liệu nghiệp vụ Production.
- Các ca chưa bắt đầu hoặc đã hết giờ vẫn không được tiếp nhận; nếu không có ca hợp lệ cần OM phân ca đúng nghiệp vụ.

## Nhật ký thực hiện

Audit xác định dữ liệu chuyển đúng queue nhưng ca chưa bắt đầu; giữ điều kiện backend và sửa phản hồi UI. Gộp hai kiểu dữ liệu phòng/công việc trước khi phân trang; gộp bảo trì theo sự cố gốc. Chạy unit, API TEST và browser; publish, thay bản local và kiểm tra HTTP/assets. Cảnh báo publish chép font được xử lý bằng retry và đối chiếu hash.
