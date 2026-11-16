# FASTTRACK-142A checkpoint167 — RPT06/07 attribution and processing cycles

TASK_ID=DEV3-FASTTRACK-142A
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=4d8df59e66fcd514ed110de4c4318d7fb737a3fb
NEW_COMPLETE_VERIFIED=0
TOTAL_COMPLETE_VERIFIED=27/144

Đã áp dụng quyết định RPT-06/07 của DEV3 trong lượt này:

- RPT06 employee filter: task detail/status theo duy nhất current active assignment; effort theo work interval performer; workday time theo employee của workday. Reassign không chuyển thời gian lịch sử sang assignee mới. Dashboard mặc định không đổi vì tham số employee là tùy chọn của query component.
- RPT07 department filter theo current receiving department; type filter có trên UI. Giữ nguyên period population là request được tạo trong kỳ UTC và branch scope đã được cấp.
- Response: SubmittedAt tới event ACCEPT/REJECT, có event ID/timestamp và evidence reason. Thiếu/không nhất quán/đa quyết định/Reopen chưa chứng minh được submission origin trả N/A, không lấy UpdatedAt hoặc thời gian chạy báo cáo thay thế.
- Completion: ASSIGN từ ACCEPTED sang IN_PROGRESS có assignment record khớp actor/time/state; REASSIGN không reset mốc; REOPEN từ CLOSED bắt đầu chu kỳ sau; COMPLETE kết thúc chu kỳ. Kiểm tra lịch sử tuần tự, timestamp và current closure. Cộng elapsed duration mỗi request, bỏ khoảng Closed→Reopen. Open cycle hoặc thiếu bằng chứng trả N/A.
- Summary theo request type/current receiving department: mỗi Closed request là một mẫu, không lấy cycle count làm mẫu số. Nếu có Closed request thiếu dữ liệu, average là N/A và có missing count. UI, frozen CSV/XLSX cùng dùng snapshot server.
- Không đổi permission catalog, schema, generated EF entities, CSRF, Serializable snapshot, export audit, admission Operations Manager/Owner + REPORT_VIEW hoặc branch scope.

Bằng chứng trực tiếp:

- `dotnet build HotelOps.slnx`: PASS, 0 warning/error. ModelProbe build PASS với 3 EF1002 cũ tại SprintCredentialScenario.cs:62/72/83.
- `dotnet test HotelOps.slnx`: exit0; suite regression riêng: 853 PASS,15 runtime SKIP. Trong đó24 test mới kiểm tra response/cycle gồm missing/future/duplicate, assignment provenance, Reassign, Reopen, open cycle và closure mismatch.
- Frontend typecheck và production build PASS.26 report API/browser regression PASS. Vite vẫn cảnh báo react-router use-client bị bỏ qua và bundle >500kB. Không chạy lại npm ci vì không đổi dependency/lockfile.
- SQL report probe cuối86 checks PASS, rollback=true. Exact server/database/GUID/disposable-run + empty fixture guard chạy trước mutation. Reassign chuyển task population, giữ300 giây effort/workday cho nhân viên cũ; Route chuyển current department, snapshot cũ bất biến; hai cycle tổng780 giây, mẫu số request=1.
- Owned clone browser→HTTPS→SQL29 records PASS. RPT07 lọc receiving department/type; single cycle600s, two cycles900s, average750s/2requests; response30s và N/A có lý do cho reopened origin; CSV/XLSX download và2 export audit rows. XLSX chỉ kiểm tra ZIP container, không tuyên bố desktop Office rendering.
- Clone `HotelOps_DEV3_142A_CriteriaTrial_0baf3142` dropped, backup removed. Kiểm tra độc lập master:0 matching databases; backup:false; listener7142/7143:0. Runtime receipt cleanup=false là trạng thái trước whole-clone cleanup; cleanup receipt riêng xác nhận kết quả cuối.
- Không mutation shared/production DB. Trial báo cáo dùng synthetic lifecycle fixtures; không chứng minh riêng UC121 Reopen mutation đã nghiệm thu. RPT06 Reassign attribution được kiểm tra SQL; UI filter dùng focused browser/mock HTTP; không tuyên bố một full Reassign browser workflow mới.

Các lần lỗi được giữ trong artifacts: probe contract đầu thiếu nhân viên thứ hai trong fixture (`Sequence contains no elements`, rollback=true); browser mock chưa có department option (25PASS/1FAIL). Đã sửa fixture và rerun PASS. Build sandbox ban đầu bị chặn đọc NuGet.Config; build có quyền đọc cấu hình đã PASS. Đây không phải lỗi product bị bỏ qua.

Evidence: checkpoint167-reports.json, checkpoint167-runtime.json, checkpoint167-cleanup.json.

UC135 vẫn PARTIAL: các indicator/filter thuộc report families khác và PDF còn thiếu. Không nâng COMPLETE_VERIFIED dựa riêng test tổng hoặc báo cáo con. UC006/010 release gates và các FE13 mutation contract còn thiếu giữ nguyên.

Next highest impact: hoàn thiện các indicator còn thiếu trong RPT02/03; hoàn thiện các filter/report families còn lại của UC135; triển khai PDF với cùng snapshot/authorization/audit trước khi xét toàn bộ UC135.

Changed files: ReportSnapshotQueries.cs; ReportRequestResponse.cs(new); ReportRequestProcessing.cs(new); TaskEffortRead.cs; ReportWorkdayTime.cs; ReportsPage.tsx; reportApi.ts; reports-api.test.mjs; reports-browser.test.mjs; sprintReportsBrowser.mjs; ReportRequestResponseTests.cs(new); ReportRequestProcessingTests.cs(new); SprintReportProbe.cs; SprintReportLifecycleProbe.cs(new); SprintReportCyclesScenario.cs(new); SprintReportActorsScenario.cs; SprintReportsBrowser.cs; receipt, matrix và3 evidence JSON.
