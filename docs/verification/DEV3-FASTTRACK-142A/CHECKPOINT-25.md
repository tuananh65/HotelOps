# FASTTRACK-142A — checkpoint 25

Đã hoàn thiện phần xem/tải evidence đã lưu trong editor FE11 canonical. DTO frontend kiểm tra entity TASK, task ID, attachment ID, MIME, size/date và đường dẫn chính xác theo branch/task; chỉ dựng link tới endpoint private hiện hữu. Không cho response tùy ý chuyển người dùng sang URL ngoài. Server tiếp tục kiểm tra session, quyền và assignment ở mỗi lần download. Không đổi backend permission/schema/generated entities.

## Bằng chứng mới

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint25-backend-build.txt). `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS](checkpoint25-probe-build-final.txt).
- `npm --prefix frontend run typecheck`: [PASS](checkpoint25-typecheck.txt); `npm --prefix frontend run build`: [PASS](checkpoint25-frontend-build.txt), giữ cảnh báo use-client/chunk >500 kB hiện hữu.
- `node --test frontend/tests/operational-task-progress.test.mjs frontend/tests/operational-task-read.test.mjs`: [15 PASS](checkpoint25-task-tests.txt). Test mới từ chối evidence sai entity/task/attachment/branch, URL ngoài, MIME và size không hợp lệ. Đây là offline contract tests.
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-read docs/verification/DEV3-FASTTRACK-142A/checkpoint25-runtime-final.json`: [49 top-level checks PASS, cleanup=true](checkpoint25-runtime-final.json), gồm 8 bước browser thật qua production bundle/Edge HTTPS/API/SQL.

Browser upload PNG synthetic, lưu progress Paused, reload, bấm link tải, kiểm tra filename và byte khớp hoàn toàn. Probe xác minh attachment metadata, file private và progress_evidence_links trỏ đúng progress đã commit; download MIME image/png và Content-Disposition attachment. Attachment không tồn tại trả404; thu hồi branch access khiến tải lại trả403. Các trường hợp CSRF, stale/concurrent write, assignment/scope và audit của checkpoint24 vẫn chạy.

Thử lỗi giữa chừng: PUT chứa tệp đầu hợp lệ, tệp thứ hai sai nội dung. API trả400; progress/attachment SQL rollback, file private đầu tiên được dọn; lần lưu tiếp theo bằng ETag cũ vẫn thành công. Đây là rollback trước COMMIT, không phải kiểm chứng COMMIT disconnect/unknown outcome.

Runtime target giữ exact instance/database GUID/ownership guard. API dùng `TestResults/sprint25-evidence`; probe bắt buộc thư mục trống và không reparse point, chỉ dọn storage key GUID của attachment thuộc task/user synthetic. Rehearsal SQL rollback trước commit fixture; cuối trial cleanup link/attachment/progress/task/audit rồi file theo owned paths. Không disable trigger, không thay đổi shared HotelOpsDB. API đã dừng. Giữ warnings RoomTypeBranchV1 unmapped indexes và Skip/Take trong infrastructure.

Không chạy lại npm ci hoặc toàn bộ Auth/Router khi dependencies/routing không đổi; 163 Auth/Router PASS ở checkpoint24 là bằng chứng lịch sử, không phải test mới của lượt này. Criterion mutation vẫn chưa runtime accepted; task Create/Assign/Complete/Cancel/Reopen còn thiếu. UC109 vẫn PARTIAL.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=25_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=9ab3a9e2ebcce53b532012b03f373ca4ccdfa590
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=CANONICAL_TASK_SAVED_EVIDENCE_LIST_AND_PRIVATE_DOWNLOAD_WITH_SCOPED_DTO_VALIDATION
CHANGED_PATHS=frontend/src/operationalTasks/{progress.ts,TaskProgressPanel.tsx};frontend/tests/operational-task-progress.test.mjs;frontend/scripts/sprintTaskReadBrowser.mjs;scripts/verification/CanonicalDatabase/ModelProbe/{SprintTaskReadScenario.cs,SprintAuthProbe.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-25.md,UC_STATUS_MATRIX.md,checkpoint25-*}
TESTS_EXECUTED=15_OFFLINE_PASS;49_RUNTIME_CHECKS_INCLUDING_8_BROWSER_STAGES_PASS
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;TYPECHECK_PASS;FRONTEND_PASS_EXISTING_WARNINGS
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_COMPOSITION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=PROGRESS_NOTE_EVIDENCE_UPLOAD_DOWNLOAD_RUNTIME_ACCEPTED;CRITERIA_AND_FULL_LIFECYCLE_PENDING
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_REMAIN
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=IDENTITY_VERIFIED;SQL_FILE_ROLLBACK_AND_FIXTURE_CLEANUP_PASS
HTTPS_API_STATUS=REAL_API_BROWSER_PASS;TRIAL_STOPPED
E2E_VERIFIED_WORKFLOWS=TASK_PROGRESS_WITH_EVIDENCE_UPLOAD_RELOAD_PRIVATE_DOWNLOAD_AND_REVOKED_ACCESS_DENIAL
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_ROOM_MAPPING;FE11_CREATE_LIFECYCLE_CONTRACTS;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=Dedicated disposable criteria fixture preserving immutable criteria; task creation/lifecycle prerequisites; FE05 room projection after mapping decision
```
