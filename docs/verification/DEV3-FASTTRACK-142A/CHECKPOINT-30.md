# FASTTRACK-142A — checkpoint 30

Đã triển khai Resume task tại API và màn hình canonical `/operational-tasks`. Chỉ current executor có quyền TASK_EXECUTE được chuyển progress state PAUSED sang IN_PROGRESS. Strong ETag, CSRF, Working workday và không có interval mở là điều kiện bắt buộc. Interval, progress revision và audit cùng transaction; giữ nguyên phần trăm tiến độ và actual start. Frontend lấy CSRF mới, không tự retry write, giữ dữ liệu biểu mẫu chưa lưu.

UC110 vẫn PARTIAL. Fixture tự tạo PAUSED task và Working workday; không chứng minh Create/Accept/Start/Pause hoặc Start Workday. Chưa triển khai Pause vì cần xác nhận reason catalog; không tự tạo status hoặc permission. Không thay đổi generated entities/schema.

Bằng chứng thực thi:

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint30-backend-build-final.txt). [Lượt đầu lỗi thiếu using Data](checkpoint30-backend-build.txt) được giữ; đã sửa.
- `npm run typecheck`: [PASS](checkpoint30-typecheck.txt); `npm run build`: [PASS](checkpoint30-frontend-build.txt), còn cảnh báo bundle 614.47 kB vượt 500 kB. npm ci không chạy lại; không đổi lockfile/dependency.
- `node --test tests/operational-task-progress.test.mjs tests/operational-task-read.test.mjs tests/auth-router-integration.test.mjs tests/shared-auth-host.test.mjs`: [183 PASS, 0 skip/fail](checkpoint30-regression.txt).
- `dotnet test tests/HotelOps.Dev1.RegressionTests --no-restore`: [kết quả regression backend](checkpoint30-backend-regression.txt).
- `dotnet test HotelOps.slnx`: [không phát hiện test project](checkpoint30-solution-test.txt); không coi là regression PASS.
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS](checkpoint30-probe-build-final.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-resume docs/verification/DEV3-FASTTRACK-142A/checkpoint30-runtime-final.json`: [30 check records, success/cleanup=true](checkpoint30-runtime-final.json), gồm browser 5 bước. Thiếu ETag 428, thiếu CSRF 400, không được giao task 403, thiếu workday/open interval 409, stale 412; cạnh tranh cùng version chỉ một 200 và một 412. Ba resume commit tạo ba interval và đủ task/interval audits.
- [Lượt runtime đầu](checkpoint30-runtime.json) thất bại kiểm tra logout vì browser chưa logout; đã sửa harness logout, lượt cuối thành công. Không giấu failure.
- SQL exact identity guard trước mutation; chỉ database owned `HotelOps_DEV3_142A_20261114_cc78c13f`, dữ liệu synthetic, rehearsal rollback và cleanup các HTTP commit. [Đọc độc lập sau cleanup](checkpoint30-cleanup.json): users/branches/tasks/workdays/intervals/audits đều 0. API thử nghiệm đã dừng. Không mutation shared DB.
- Chưa fault-inject sau interval/audit, chưa kiểm chứng toàn bộ lifecycle hoặc wrong-branch/department riêng cho Resume. Không suy rộng các kết quả này thành full UC acceptance.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=30_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=225a1691d19bf011df6f44fc770c7ea871897bc8
CURRENT_SHA=see delivery commit
SOURCE_IMPLEMENTED=FE11_UC110_RESUME_API_CANONICAL_UI_ATOMIC_WORK_INTERVAL_AND_AUDIT
CHANGED_PATHS=OperationalTaskResumeService.cs;WorkProgressController.cs;frontend/src/operationalTasks/{progress.ts,TaskProgressPanel.tsx};frontend/tests/operational-task-progress.test.mjs;frontend/scripts/sprintTaskResumeBrowser.mjs;ModelProbe/{Program,SprintAuthProbe,SprintTaskReadScenario,SprintTaskResumeScenario}.cs;checkpoint30 evidence;UC_STATUS_MATRIX.md
TESTS_EXECUTED=183_FE_PASS;486_BACKEND_PASS_11_SKIP;30_REAL_RUNTIME_CHECK_RECORDS_WITH_5_BROWSER_STAGES
BUILD_STATUS=BACKEND_PROBE_TYPECHECK_FRONTEND_PASS;VITE_CHUNK_WARNING
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=RESUME_RUNTIME_ACCEPTED_WITH_SEEDED_PAUSED_TASK;FULL_LIFECYCLE_PENDING
FE13_STATUS=HISTORY_QUERY_SQL_VERIFIED_PREVIOUS_CHECKPOINT;READ_PERMISSION_PENDING
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=QUERY_CACHE_CSV_COMPONENTS_PREVIOUSLY_VERIFIED;ROLE_CONFLICT_EXPORT_ADMISSION_AUDIT_UI_PENDING
ISOLATED_SQL_STATUS=OWNED_TARGET_ONLY;CLEANUP_VERIFIED
HTTPS_API_STATUS=RESUME_POSITIVE_NEGATIVE_CONCURRENCY_VERIFIED
E2E_VERIFIED_WORKFLOWS=LOGIN_OPEN_PAUSED_TASK_RESUME_RELOAD_LOGOUT;SEEDED_WORKDAY
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=PAUSE_REASON_CONTRACT;FE15_ROLE_CONFLICT;FE13_READ_PERMISSION;FE05_ROOM_MAPPING;UC006_UC010_COORDINATION
COMMIT_SHAS=see delivery commit
PUSH_STATUS=verified after commit
NEXT_IMPLEMENTATION=FE11 atomic Pause after reason contract; UC006/010 writer coordination; report API/UI after role resolution
```
