# Sửa lỗi màn công việc của Lan — 19/11/2026

## Kết quả trên database và web đang dùng

- Công việc cũ `OT-2026-0009` (và `OT-2026-0006`) có phân công ACTIVE nhưng không có `task_progress_versions`. Giữ hồ sơ cũ để tra cứu theo lựa chọn trước; giao diện hiển thị **Hồ sơ công việc cũ · Chỉ xem**, không còn báo lỗi đỏ chung chung. Chỉ chuyển sang chế độ này khi API trả đúng 503/SCHEMAREQUIRED; lỗi quyền, phiên, kết nối và lỗi ghi vẫn được báo.
- Phiếu làm lại cũ #8/#9 trùng nguồn kiểm tra: thêm API đọc hồ sơ đã ghi nhận, giới hạn đúng người được giao/chi nhánh/quyền CLEANING_VIEW. Màn chi tiết hiển thị phòng, công việc nguồn, lý do, trạng thái đã ghi nhận và ngày yêu cầu; không cho thực hiện trên nguồn trùng.
- Đã thêm công việc và phiếu làm lại **mới trên HotelOpsDB**, không chỉ trên clone. Các bản ghi có nhãn `[DEMO]`; không sửa dữ liệu cũ, quyền hoặc schema.

Mở bằng tài khoản `lan.dang`:

1. [Công việc thực hành cập nhật tiến độ](https://localhost:5173/operational-tasks?branchId=1&taskId=16): `[DEMO] Thực hành cập nhật công việc của Lan`, ID 16, mã `TASK-48AB741E819B47D798EF347E0DF6D0C2`. Trạng thái workflow ASSIGNED; service tạo và phân công thật đã tạo đủ phiên bản tiến độ/tiêu chí. Lan có thể mở phần nhận công việc.
2. [Phiếu làm lại thực hành #11](https://localhost:5173/rework/11?branchId=1): phòng `DEMO-LAN`, nguồn vệ sinh `DEMO-LAN-20261119`, trạng thái PENDING_ACCEPTANCE. Có tiêu chí, lỗi nguồn, bằng chứng mẫu và hạn xử lý; Lan có thể mở phần tiếp nhận.
3. [Hồ sơ làm lại cũ #8](https://localhost:5173/rework/8?branchId=1): chỉ xem thông tin thật đã ghi nhận, không còn trang chỉ có thông báo đỏ.

Điều kiện ca làm/chấm công và quyền thực hiện tiếp tục được API kiểm tra khi nhận/bắt đầu/gửi công việc. Không chấm công hộ Lan hoặc tự hoàn tất dữ liệu mẫu. Hạn DEMO là một ngày sau thời điểm tạo; đây là dữ liệu thực hành, không phải kết quả xử lý phòng thật.

## Bổ sung dữ liệu có kiểm soát

- Chạy thử cùng gói trên database clone: thành công; kiểm tra đọc lại bằng quyền Lan hiện có, hồ sơ cũ không đổi.
- Trước khi ghi HotelOpsDB: COPY_ONLY backup + CHECKSUM + RESTORE VERIFYONLY; xem `backup-receipt.json`.
- Tạo riêng phòng/loại phòng/checklist DEMO. Nguồn kiểm tra không đạt và tệp ảnh placeholder được ghi rõ là dữ liệu thực hành; không đưa kết quả giả vào phòng thật.
- Phân công kiểm tra/yêu cầu làm lại qua writer hạn quyền bằng quyền Giám sát Buồng phòng; tạo/phân công công việc vận hành qua service bằng quyền Trưởng bộ phận. Không cấp thêm quyền để seed thành công.
- Phiên kiểm chứng tạm được xóa sau khi chạy. Clone và SQL login tạm đã được dọn (`clone-cleanup.log`); hồ sơ DEMO trên HotelOpsDB được giữ để người dùng thử.
- `Run.ps1` mặc định dùng clone; ghi database làm việc cần `-Mode Working -Apply`. Marker chống tạo trùng và không ghi đè hồ sơ đã tồn tại. Script dùng bảng/cột hiện có; không tạo migration/schema.

## Kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx --no-restore` | Passed, 0 cảnh báo/0 lỗi ở lần cuối |
| `dotnet test HotelOps.slnx --no-restore` | Exit 0; bộ regression chạy riêng bên dưới |
| Regression backend + SQL quyền Lan/đọc hồ sơ cũ | 41/41 Passed (`backend-tests.log`); kiểm tra từ chối người khác/chi nhánh khác và định dạng UTC |
| Frontend build | Passed (`frontend-build.log`) |
| Frontend browser/client | 37/37 Passed (`ui-tests.log`); bao gồm hồ sơ cũ hiển thị dữ liệu không báo đỏ, lỗi 403 vẫn báo và không lộ dữ liệu |
| Dữ liệu DEMO trên clone và HotelOpsDB | Đọc tiến độ Success; rework PENDING_ACCEPTANCE; oldRecordsUnchanged=true (`clone.log`, `working.log`) |
| API/web sau khởi động lại | Route mới trả 401 khi chưa đăng nhập; web trả 200; API localhost:7019 đã nạp bản sửa |

Build đầu tiên bị khóa DLL bởi tiến trình API debug đang chạy. Đã dừng đúng tiến trình đó, build lại thành công và khởi động lại API. Frontend còn cảnh báo có sẵn về React Router `use client` và bundle lớn hơn 500 kB. Không sửa module khác để loại cảnh báo.

Kiểm thử browser dùng HTTP mock. Kiểm chứng SQL dùng service thật và quyền thật của tài khoản; chưa đăng nhập trình duyệt trực tiếp bằng mật khẩu Lan để thao tác ghi toàn bộ luồng trên HotelOpsDB. Bản ghi DEMO được giữ ở bước đầu để người dùng tự kiểm tra. Ảnh kiểm thử: `archived-rework.png`, `archived-task.png`.

## File tạo/sửa

- `src/HotelOps.Api/Services/Housekeeping/AssignedReworkRecordService.cs` (mới): DTO đọc lịch sử và query có scope, AsNoTracking/async/CancellationToken; không cấp capability ghi.
- `src/HotelOps.Api/Controllers/CleaningExecutionController.cs`: GET `/api/branches/{branchId}/reworks/{id}/record`.
- `frontend/src/api/dev1/reworkRecord.ts` (mới): đọc và kiểm tra contract/định danh hồ sơ cũ.
- `frontend/src/screens/housekeeping/ReworkExecutionPanel.tsx`: hiển thị hồ sơ lưu trữ khi nguồn trùng.
- `frontend/src/operationalTasks/progress.ts`: bảo toàn mã lỗi máy chủ.
- `frontend/src/operationalTasks/TaskProgressPanel.tsx`: tách hồ sơ thiếu workflow khỏi lỗi quyền/kết nối.
- `frontend/tests/lan-access-ui.test.mjs`, `tests/HotelOps.Dev1.RegressionTests/LanAccessReadSqlTests.cs`: thêm kiểm thử tương ứng.
- `artifacts/lan-workflow-demo/`: Seed.cs, Run.ps1, Backup.ps1, báo cáo, log và bằng chứng.
- `artifacts/lan-dang-access-review/REPORT.md`: liên kết cập nhật sang báo cáo này.

Không commit, push hoặc deploy. Khởi động lại localhost phục vụ kiểm tra trong workspace.
