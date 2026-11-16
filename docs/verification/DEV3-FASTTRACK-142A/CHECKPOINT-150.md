# FASTTRACK-142A checkpoint150 — processing-cycle KPI and matching task populations

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=f2eaf649faca3f4fad7889e86148fc4ecd626cc2
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=24/144
SHARED_DB_MUTATION=NO

Đã áp dụng quyết định mới của DEV3 cho KPI-05: tổng elapsed duration từ Start đầu tiên đến Submit được Accept của từng processing cycle, rồi lấy trung bình một lần trên mỗi task hiện Closed trong kỳ UTC. Không cộng thời gian chờ Accept cuối chu kỳ hoặc Closed→Reopen; giữ nguyên ActualStartAt. KPI-06 tiếp tục dùng closed effective work intervals, loại Pause. Quyết định và cách dùng nguồn được ghi tại [UC132_KPI05_DECISION.md](UC132_KPI05_DECISION.md).

TaskCompletionDashboard đọc immutable submission/verification/reopen receipts qua reader kiểm chứng provenance, đối chiếu số event gốc để không bỏ mất chu kỳ lỗi, kiểm tra Start audit và interval cutoff. Thiếu nguồn/chu kỳ hoặc completion candidate không nhất quán trả N/A kèm lý do. Zero denominator trả N/A. Dữ liệu open lifecycle không xác định vẫn được đánh dấu thiếu ở KPI-04; không tự suy trạng thái hoặc làm mất một KPI-05 có đủ bằng chứng riêng. Các read nằm trong transaction/snapshot và authorization hiện có; không thêm storage hay sửa generated EF entities.

Drill-down Open/Overdue/Closed/KPI-05 dùng endpoint task population mới, không dùng số employee participation thay cho số task. Danh sách chứa cả task không có current assignee, giữ branch/department/calendar/period và độc lập kiểm tra TASK_VIEW. Employee workload vẫn giữ historical participation. Frontend hiển thị đúng calendar của response và không cho nhập employee filter vào task population.

Direct evidence trong run này:

- Backend solution build PASS, 0 warnings/errors; regression project **811 PASS, 14 SKIP**. `dotnet test HotelOps.slnx --no-restore` exit0 nhưng không phát hiện test project; không dùng nó làm bằng chứng số test.
- Frontend focused **10 PASS**; Auth HTTP/state/router **158 PASS**; TypeScript và production build PASS. Không rerun npm ci vì dependency/lockfile không đổi. Full frontend suite mở rộng đã dừng, không phải PASS; xem checkpoint150-full-frontend-status.json.
- [Task population trial](checkpoint150-drilldown-runtime.json): **118 records PASS**. Browser mở Open=4, Overdue=2, Closed-in-period=1; SQL/HTTPS và UI khớp kể cả không có current assignment. TASK_VIEW bị thu hồi thì drill-down403, DASHBOARD_VIEW vẫn200. Invalid task set400; UTC effort giữ2400 seconds, hotel-day default3000.
- [Final processing-cycle trial](checkpoint150-cycles-runtime.json): **319 records PASS**. Task được tạo qua public UI, Accept/Start, Pause/Resume, Submit/Verify, Reopen hai lần thành ba chu kỳ; KPI-05 được đối chiếu SQL tại 1/2/3 chu kỳ, mẫu số vẫn một task. Browser đọc và hiển thị KPI-05 qua HTTPS. Reopened và Pending Verification bị loại dù có accepted cycles lịch sử; kỳ không có Closed task trả N/A. Workload/history, CSRF, ETag, rollback và notification regression của workflow hiện có cũng chạy.
- Unit tests mới kiểm tra Return/resubmit trong cùng chu kỳ, Pause gaps trong elapsed duration, nhiều Reopen, thiếu Start/cycle, sai cutoff/completion và thời gian không nhất quán. Không gọi unit test Return là bằng chứng runtime Return mới.
- [Cleanup read-only cuối](checkpoint150-cleanup.json): 7 disposable databases không còn trong sys.databases, 7 backup không còn trên disk, listener7142/7143=0. Không mutation shared/production. Không bypass FK/trigger/security.

Các lần thất bại được giữ rõ trong [cycle trials](checkpoint150-cycle-trials.json): trials1/2 dừng ở assertion cũ của probe linked creation (contract hiện tại thiếu source If-Match trả428); trial3 nhầm đòi KPI-04 count có giá trị khi fixture chứa legacy open task thiếu canonical source. Trial4 PASS317. Trial5 PASS các kiểm tra KPI-05 nhưng browser Workload timeout15s (HTTP499 sau client abort); không đổi timeout/bỏ assertion, trial6 PASS319. Root cause của timeout đơn lẻ chưa được chứng minh; không khẳng định đã sửa một lỗi performance. Cleanup drill-down trial lần đầu còn thấy connection đang đóng; retry sau khi host/probe thoát đã PASS identity guard. Full-suite process đã được dừng và log partial giữ trong artifacts.

Warnings còn: ModelProbe có 3 EF1002 hiện hữu ở SprintCredentialScenario; Vite có use-client và chunk >500kB. Backend production build không có warning. Không nới threshold hoặc suppress warning.

Changed product files: TaskCompletionDashboard.cs, TaskCycleDuration.cs, DashboardService.cs, TaskWorkloadDtos.cs, OperationalTaskWorkloadService.cs, WorkProgressController.cs, dashboardWorkloadLink.ts, TaskWorkloadPanel.tsx, workload.ts. Verification files: TaskCycleDurationTests.cs, TaskWorkloadContractTests.cs, dashboard-workload-link.test.mjs, operational-task-workload.test.mjs, SprintTaskCompletionDashboardScenario.cs, SprintTaskDashboardScenario.cs, SprintTaskPlanningScenario.cs, SprintTaskReopenScenario.cs, Invoke-Dev3LinkedTaskCreationTrial.ps1, sprintTaskCompletionDashboardBrowser.mjs, sprintTaskDashboardBrowser.mjs; decision, UC matrix và evidence đi kèm.

**Newly completed UCs: 0.** UC132 vẫn PARTIAL: còn drill-down cho aggregate nhiều branch/khoảng ngày ngoài giới hạn366 ngày của Workload, kiểm chứng toàn bộ actor/scope và acceptance. Không còn chờ quyết định KPI-05, không gán COMPLETE chỉ vì code/build/E2E slice PASS. Không có runtime/security blocker chặn phần độc lập vừa giao; thiếu historical timing vẫn fail-closed/N/A đúng contract.

Next three coding tasks: hoàn thiện drill-down nhiều branch/khoảng ngày và actor/scope UC132; mở rộng runtime Return/resubmit và nhiều Closed tasks để đối chiếu mẫu số; tiếp tục workflow FE13 gần hoàn tất theo contract đã duyệt, giữ fail-closed các mutation còn chờ quyết định riêng. Sprint/deadline không đổi; đây là checkpoint triển khai, không tuyên bố sprint hoàn tất.
