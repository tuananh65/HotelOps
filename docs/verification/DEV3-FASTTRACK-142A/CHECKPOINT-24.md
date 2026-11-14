# FASTTRACK-142A — checkpoint 24

Đã nối API tiến độ FE11 hiện hữu vào canonical `/operational-tasks`: phần trăm, kết quả tiêu chí PASS/FAIL, ghi chú và upload evidence theo validator hiện hữu. Editor gắn với authority generation/task, abort khi unmount, lấy CSRF mới mỗi lần lưu, gửi strong ETag và khóa ghi sau lỗi cho tới khi reload; không tự retry request ghi. Backend kiểm tra thêm department/room/area cùng chi nhánh trong ownership của task progress. Không đổi schema, generated entities, permission hay lifecycle.

Đính chính giới hạn checkpoint23: completion criteria không nằm trong generated OperationalTask entity nhưng **đã có** trong additive progress schema và API hiện hữu. Schema đó hỗ trợ IN_PROGRESS/PAUSED, chưa chứng minh Create/PendingAssignment/expected outcome/recurrence hoàn chỉnh. UC109 cho phép executor cập nhật tiến độ/criterion/note/evidence khi In Progress hoặc Paused; 100% không tự complete. Không suy diễn mapping mới cho trạng thái.

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint24-backend-build.txt). Probe [build PASS, 0 warnings/errors](checkpoint24-probe-build-final.txt).
- `npm --prefix frontend run typecheck`: [PASS](checkpoint24-typecheck.txt); `npm --prefix frontend run build`: [PASS](checkpoint24-frontend-build.txt), giữ Vite use-client/chunk >500 kB warnings.
- `node --test frontend/tests/operational-task-progress.test.mjs frontend/tests/operational-task-read.test.mjs`: [14 PASS](checkpoint24-task-tests.txt), gồm 8 progress contract tests: exact scope/version, Paused, fresh CSRF/ETag, criteria validation, invalid CSRF, authority abort, no retry stale/unknown outcome, invalid IDs. Đây là offline transport tests.
- Từ `frontend`, `node --test tests/shared-auth-host.test.mjs tests/auth-router-integration.test.mjs`: [163 PASS](checkpoint24-auth-router-final.txt). Lần đầu chạy sai cwd ở repo root làm esbuild không resolve React; [log lỗi được giữ](checkpoint24-auth-router-tests.txt), không sửa dependencies để che lỗi.
- `dotnet test tests/HotelOps.Dev1.RegressionTests --filter FullyQualifiedName~EvidenceFileLifetimeTests`: [5 PASS](checkpoint24-evidence-tests.txt). Đây là filesystem regression, không chứng minh upload SQL E2E.
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-read docs/verification/DEV3-FASTTRACK-142A/checkpoint24-runtime-final.json`: [44 top-level checks PASS, cleanup=true](checkpoint24-runtime-final.json), trong đó browser 7 bước thực thi Edge với TLS mặc định, production bundle và API/SQL thật, không mock/interception.

Runtime chứng minh percent/note được lưu; 100% giữ task IN_PROGRESS, CompletedAt null; thiếu CSRF400, thiếu If-Match428, invalid percent/foreign criterion400, stale412; malformed department và ended assignment403. Hai PUT đồng thời cùng ETag trả đúng 200/412, một log và một audit. Sau browser lưu 75% trên task có workflow_status PAUSED rồi reload, SQL giữ percent/note và tổng đúng 3 audit của 3 lần ghi thành công; request bị từ chối không tạo audit thừa. Các read/scope/logout checks checkpoint23 tiếp tục chạy.

Trial dùng đúng identity/GUID/ownership của `HotelOps_DEV3_142A_20261114_cc78c13f`, synthetic data, rehearsal transaction rollback rồi compensation theo owned IDs. API đã dừng. Không chạm shared HotelOpsDB. Runtime còn warnings RoomTypeBranchV1 unmapped indexes và infrastructure Skip/Take không OrderBy; không che warnings. npm ci không chạy lại vì lockfile/dependencies không đổi.

Giới hạn: fixture runtime này có progress version nhưng không có completion criteria; không xóa/disable trigger immutable criteria để tiện cleanup. Criterion PASS/FAIL và file upload đã nối source nhưng **chưa runtime accepted** ở FE11. Chưa chứng minh failure tại COMMIT, task Create/Assign/Complete/Cancel/Reopen, hoặc permission changes đồng thời. Summary/history ngoài editor cần Refresh để nhận bản mới sau save. Toàn bộ UC109 vẫn PARTIAL.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=24_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=db3a696e2316eb584e2217698edbf5492a097209
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=CANONICAL_FE11_PROGRESS_EDITOR_AND_CROSS_BRANCH_RESOURCE_INTEGRITY
CHANGED_PATHS=frontend/src/operationalTasks/{TaskReadPage.tsx,TaskProgressPanel.tsx,progress.ts};frontend/tests/operational-task-progress.test.mjs;frontend/scripts/sprintTaskReadBrowser.mjs;src/HotelOps.Api/Services/Housekeeping/WorkProgressService.cs;scripts/verification/CanonicalDatabase/ModelProbe/{SprintAuthProbe.cs,SprintTaskReadScenario.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-24.md,UC_STATUS_MATRIX.md,checkpoint24-*}
TESTS_EXECUTED=14_TASK_CONTRACT_PASS;5_EVIDENCE_LIFETIME_PASS;163_AUTH_ROUTER_PASS;44_RUNTIME_CHECKS_INCLUDING_7_BROWSER_STAGES_PASS
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;TYPECHECK_PASS;FRONTEND_PASS_EXISTING_WARNINGS
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_COMPOSITION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=READ_AND_PERCENT_NOTE_WRITE_RUNTIME_ACCEPTED;CRITERIA_EVIDENCE_AND_FULL_LIFECYCLE_PENDING
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_INTEGRATION_REMAIN
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=IDENTITY_VERIFIED;REHEARSAL_ROLLBACK_AND_FIXTURE_CLEANUP_PASS
HTTPS_API_STATUS=REAL_API_BROWSER_PASS;TRIAL_STOPPED
E2E_VERIFIED_WORKFLOWS=TASK_READ_AND_PERCENT_NOTE_PROGRESS_SAVE_RELOAD_WITH_AUDIT_AND_CONCURRENT_STALE_PROTECTION
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_ROOM_MAPPING;FE11_CREATE_LIFECYCLE_CONTRACTS;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=FE11 criteria/evidence full runtime fixture; task creation and lifecycle prerequisites; FE05 room projection after mapping decision
```
