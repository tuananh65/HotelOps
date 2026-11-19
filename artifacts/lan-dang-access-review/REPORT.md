# Kiểm tra tài khoản lan.dang — 19/11/2026

> Cập nhật tiếp theo: đã thêm màn tra cứu hồ sơ cũ và hai công việc DEMO trên database làm việc theo yêu cầu mới. Xem [báo cáo sửa lỗi màn đỏ](../lan-workflow-demo/REPORT.md). Các kết luận dưới đây là kết quả của đợt kiểm tra trước khi bổ sung dữ liệu đó.

## Kết luận

Lan (Đặng Thị Lan, user 6) là Nhân viên Buồng phòng, đang hoạt động tại Bùi Viện, bộ phận Buồng phòng. SQL hiện có 15 quyền hiệu lực và API navigation trả 18 đường dẫn. Các nhóm chức năng tương ứng đã có controller/service và màn hình web; phát hiện chính là liên kết sai phạm vi, điều hướng sai và hồ sơ thử nghiệm cũ thiếu điều kiện workflow. Đã sửa các lỗi giao diện/điều hướng trong phạm vi này, không thêm quyền hoặc sửa schema.

Theo lựa chọn của người dùng, giữ nguyên công việc vệ sinh phòng 201/202 và phiếu làm lại #8/#9 để tra cứu. Đã tạo và chạy công việc làm lại mới trong database clone, bằng quyền hiện có của Lan. Đây là kiểm thử tích hợp tách biệt, không phải công việc mới trên website/database làm việc.

## Quyền, API và web

OWN nghĩa là API còn kiểm tra quan hệ sở hữu/phân công của từng hồ sơ; có mã quyền không đồng nghĩa được xem tất cả dữ liệu cùng chi nhánh.

| Chức năng | Quyền hiệu lực | API/source hiện có | Web và kết quả |
|---|---|---|---|
| Tra cứu phòng/trạng thái | ROOM_VIEW — BRANCH | API cấu trúc/phòng, room workspace | `/room-directory`, `/rooms/status`, `/hotel`; không có thao tác quản trị cấu trúc hoặc trạng thái theo vai trò khác |
| Công việc vệ sinh được giao | CLEANING_VIEW, CLEANING_EXECUTE — OWN | CleaningTaskLifecycleController: my-cleaning-assignments; CleaningExecutionController: execution/start/blockers/complete | `/cleaning/tasks`; không mở danh sách giám sát/phân công; liên kết thực hiện kiểm tra quyền execute |
| Làm lại được giao | DEFECT_RESOLVE cùng quyền vệ sinh — OWN | my-rework-assignments; rework execution/accept/start/complete | `/rework`; tự tải danh sách của Lan, quay lại đúng danh sách làm lại |
| Công việc vận hành | TASK_VIEW, TASK_EXECUTE — OWN | OperationalTasksController và dịch vụ tiến độ | `/operational-tasks`; không tạo, quản lý chuỗi hoặc xem workload quản lý |
| Yêu cầu nội bộ | IR_CREATE, IR_VIEW, IR_PROCESS — OWN | InternalRequestIntake/History/Progress và các controller workflow | `/internal-requests`, `/internal-requests/new`; thao tác theo capability của từng hồ sơ, không có quyền điều phối chi nhánh |
| Yêu cầu vật tư | MR_CREATE, MR_VIEW — OWN | MaterialRequestsController; dịch vụ đọc/tạo/gửi và lịch sử | `/operations/material-requests`, `/material-requests/new`; không duyệt/cấp phát hoặc mở kho |
| Báo và theo dõi nguồn sự cố của mình | INCIDENT_CREATE — BRANCH; INCIDENT_VIEW — OWN | POST `/api/incidents`; GET chi tiết theo source-record và quyền sở hữu | `/incidents/new`; đã cho mở chi tiết nguồn được API cho phép, không mở danh sách quản lý sự cố |
| Ca, chấm công, nhật ký và đề nghị sửa giờ | SHIFT_VIEW, ATTENDANCE_RECORD — OWN | WorkdayReadsController, WorkdaysController, TimeCorrectionsController | `/workday`, `/workday/scheduled`, `/my-time-log`, `/time-corrections`; không duyệt giờ của người khác |
| Cá nhân và thông báo | Phiên đăng nhập/người nhận | Auth/profile và NotificationsController | `/profile`, `/change-password`, `/notifications` |

Không có quyền phân công/kiểm tra phòng, quản lý sự cố/bảo trì, tài sản, tồn kho, duyệt vật tư, báo cáo hoặc quản trị. Không thêm các chức năng này vào menu của Lan. Menu và quyền thực tế được lưu trong `results.txt` (không chứa mật khẩu/token).

## Các lỗi đã sửa

1. Chi tiết sự cố OWN bị frontend chặn vì Lan không có menu danh sách quản lý: cho phép đúng action đọc chi tiết; API tiếp tục kiểm tra sở hữu và chi nhánh, không mở quyền danh sách.
2. Đóng popup báo sự cố dẫn tới danh sách không được phép: trở về trang chủ khi không có màn hình danh sách/chi tiết phù hợp.
3. Tab, nút quay lại và liên kết liên quan dẫn tới bảo trì, quản lý sự cố, tài sản/kho không được phép: kiểm tra action trước khi hiển thị, kể cả liên kết nằm trong dữ liệu lồng nhau.
4. Liên kết tạo vật tư từ sự cố không phù hợp với quyền đọc nguồn sự cố của Lan: ẩn khi không đáp ứng quyền nguồn; giữ chức năng tạo yêu cầu vật tư thông thường.
5. Tiêu đề lịch sử chuyển cấp gây hiểu nhầm là quyền chuyển cấp: hiển thị tiêu đề lịch sử khi chỉ được đọc, vẫn giữ guard `canCreate` cho hành động ghi.
6. Liên kết vệ sinh ghi “Start / Reassign” dù Lan không được phân công lại: đổi nhãn và kiểm tra CLEANING_EXECUTE trước khi hiển thị thực hiện/tiến độ; liên kết báo sự cố/vật tư trong blocker kiểm tra quyền tương ứng.
7. Quay lại từ làm lại dẫn nhầm sang danh sách vệ sinh: về `/rework` và giữ chi nhánh.
8. Thông báo lỗi dữ liệu cũ thiếu checklist/workflow hoặc trùng phiếu nguồn quá chung chung: giải thích rõ nguyên nhân, không khuyến khích thử thao tác ghi trên hồ sơ không hợp lệ.
9. Đường về từ màn workload không phù hợp dẫn tới dashboard: đổi về trang chủ.

## Hồ sơ cũ được giữ nguyên

- Công việc phòng 201/202: thiếu `workflow_status`, checklist vệ sinh theo loại phòng, progress version và task items. API thực hiện trả SchemaRequired; đây không phải thiếu quyền của Lan.
- Phiếu làm lại #8/#9: cùng inspection 13/task 15 có hai phiếu chưa kết thúc, workflow chưa hợp lệ. API trả DUPLICATE_REWORK_SOURCE. Không gộp, xóa hoặc tự kết luận trạng thái.
- Các hồ sơ này vẫn chỉ tra cứu cho đến khi có quyết định xử lý dữ liệu riêng. Không thể báo rằng hồ sơ cũ đã thực hiện được.

## Kiểm chứng

| Kiểm tra | Kết quả | Bằng chứng/giới hạn |
|---|---|---|
| `dotnet build HotelOps.slnx --no-restore` | Passed | `build-backend.log`: 0 lỗi, 0 cảnh báo |
| `dotnet test HotelOps.slnx --no-restore` | Passed (exit 0) | Solution không chạy bộ regression nằm ngoài solution; không tính lệnh này là các test dưới đây |
| Regression backend theo nhóm sự cố/yêu cầu/vệ sinh/làm lại | Passed 54/54 | `backend-tests.log` |
| SQL đọc thật, quyền và menu của Lan | Passed 1/1 | `sql-read.log`, `tests/lan-read.trx`; xác nhận từ chối công việc người khác/chi nhánh khác, không ghi dữ liệu |
| Workflow mới trên clone với quyền Lan hiện có | Passed 1/1 | `sql-workflow.log`, `tests/lan-workflow.trx`; tạo nguồn kiểm thử, yêu cầu làm lại → nhận → bắt đầu → gửi → kiểm tra lại; kiểm tra quyền và hồ sơ cũ không đổi |
| Frontend build | Passed | `build-frontend.log`; còn cảnh báo React Router `use client` bị bỏ qua và bundle lớn hơn 500 kB |
| Frontend client + trình duyệt Edge headless | Passed 56/56 | `client-tests-final.log`; gồm 9 ca riêng cho Lan và hồi quy Mai/yêu cầu/bảo trì/vệ sinh/làm lại/tiếp nhận sự cố |
| Kiểm tra diff whitespace | Passed | Git có cảnh báo chuyển LF→CRLF theo cấu hình, không có lỗi whitespace |

Hai kiểm thử UI tiếp nhận sự cố ban đầu thất bại do fixture chưa mở popup/chưa gắn host thông báo của giao diện hiện tại. Đã cập nhật fixture để mở popup, nhận toast thành công và xác nhận popup đóng; lần cuối 56/56 đều qua. Fixture vệ sinh cũ cũng được bổ sung trường bắt buộc theo contract hiện tại, không nới lỏng validator.

Kiểm thử trình duyệt sử dụng phản hồi HTTP giả lập dựa trên quyền/menu đọc từ SQL, không phải đăng nhập HTTP trực tiếp bằng mật khẩu Lan. Kiểm thử SQL chạy các service thật với session test; luồng clone có session và writer SQL hạn quyền. Chưa chạy thao tác ghi vật tư/nội bộ/chấm công qua browser trên database làm việc. Không tuyên bố đã kiểm thử toàn bộ mọi nhánh nghiệp vụ của 18 trang.

Clone và SQL login thử nghiệm đã được dọn: `clone-cleanup.log`. Bản ghi backup/clone nằm trong `clone-receipt.json`. Không sửa role/grant/schema hay hồ sơ nghiệp vụ trong HotelOpsDB. Có thể chạy lại kiểm thử tách biệt bằng `Test-NewWork.ps1` trên máy đã cấu hình User Secrets và SQL writer hiện có; script không tạo công việc trên database làm việc.

## File thay đổi trong đợt kiểm tra này

Frontend:

- `frontend/src/dev2/AuthorizedOperations.tsx`
- `frontend/src/dev2/Workspace.tsx`
- `frontend/src/dev2/DataView.tsx`
- `frontend/src/dev2/IncidentEscalationPanel.tsx`
- `frontend/src/screens/housekeeping/AssignedCleaningHeader.tsx`
- `frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx`
- `frontend/src/screens/housekeeping/ReworkExecutionPanel.tsx`
- `frontend/src/api/dev1/cleaningProgress.ts`
- `frontend/src/operationalTasks/TaskReadPage.tsx`

Kiểm thử:

- `frontend/tests/lan-access-ui.test.mjs` (mới)
- `frontend/tests/cleaning-execution.test.mjs`
- `frontend/tests/incident-triage-browser.test.mjs`
- `tests/HotelOps.Dev1.RegressionTests/LanAccessReadSqlTests.cs` (mới)
- `tests/HotelOps.Dev1.RegressionTests/IncidentReadAccessTests.cs`
- `tests/HotelOps.Dev1.RegressionTests/ReworkWorkflowWriterSqlTests.cs` (bổ sung ca Lan vào file đã có từ công việc trước)

Bằng chứng và script kiểm tra nằm trong `artifacts/lan-dang-access-review/`: Probe.cs, Extra.cs, run.ps1, Test-NewWork.ps1, REPORT.md, kết quả/log/TRX/ảnh chụp và biên nhận clone. Không thay đổi production C# trong đợt này. Workspace có nhiều thay đổi từ các yêu cầu trước; danh sách trên chỉ mô tả phần công việc lần này. Không commit/push/deploy.

## Cập nhật kiểm tra sidebar và popup (19/11/2026)

Đã tiếp tục quét các trang và liên kết của Lan, sửa hồ sơ vệ sinh lưu trữ, liên kết vượt quyền và popup dùng chung. Báo cáo, kết quả build/test và bằng chứng mới: [lan-sidebar-sweep/REPORT.md](../lan-sidebar-sweep/REPORT.md).

Cập nhật 20/11/2026: đã sửa constraint/tạm giữ hồ sơ theo duyệt, bổ sung dữ liệu so sánh và form điều chỉnh giờ. Xem [báo cáo worktime](../worktime-repair/REPORT.md).
