# FASTTRACK-142A — checkpoint 27

Đã nối khởi tạo completion criteria vào canonical task detail bằng API `POST /api/branches/{branch}/operational-tasks/{task}/progress-initialization` hiện hữu. Form nhận 1–100 tiêu chí, required flag, lý do; CSRF mới mỗi lần ghi, validate response identity, chặn submit lặp và không tự retry. Sau thành công editor progress được tải lại. Thay đổi authority/task unmount form và abort request cũ. Permission do server quyết định bằng TASK_MANAGE hiện hữu.

Sửa lỗi phạm vi trong initialization: ngoài task.BranchId, service kiểm tra department/room/area cũng thuộc branch. Trước đó read/progress đã kiểm tra quan hệ này nhưng initializer chưa kiểm tra. Không đổi generated entities, schema, permissions hoặc lifecycle. API chỉ cho task IN_PROGRESS có ActualStartAt và chưa có progress version. Đây là setup cho task đã tồn tại/đã bắt đầu, **không phải** UC102 Create Task hoàn chỉnh; SRS yêu cầu criteria ở lúc tạo vẫn còn gap.

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint27-backend-build.txt); probe [PASS](checkpoint27-probe-build.txt).
- `npm --prefix frontend run typecheck`: [PASS](checkpoint27-typecheck.txt); `npm --prefix frontend run build`: [PASS](checkpoint27-frontend-build.txt), giữ warnings Vite use-client/chunk >500 kB.
- `node --test frontend/tests/operational-task-progress.test.mjs frontend/tests/operational-task-read.test.mjs`: [18 PASS](checkpoint27-task-tests.txt), gồm definitions validation, fresh CSRF/created identity và duplicate không retry.
- Từ `frontend`, `node --test tests/shared-auth-host.test.mjs tests/auth-router-integration.test.mjs`: [163 PASS](checkpoint27-auth-router.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-criteria docs/verification/DEV3-FASTTRACK-142A/checkpoint27-runtime.json`: [57 top-level checks PASS](checkpoint27-runtime.json), gồm 4 bước browser setup và 9 bước browser progress/evidence.

Trial không seed criteria/progress version cho task đích bằng SQL nữa. Browser đăng nhập bằng synthetic actor có existing DEPT_MANAGER branch grant, mở form, tạo required/optional criteria qua HTTPS. Sau đó bỏ grant quản lý, executor dùng quyền hiện hữu lưu PASS/FAIL/note/evidence và reload; SQL/audit được kiểm tra. Tổng task audit là 1 initialization + 3 progress writes. API từ chối no TASK_MANAGE403, no CSRF400, foreign department404, task chưa bắt đầu409, duplicate initialization409; criteria không bị overwrite. Các stale/concurrent progress, invalid evidence rollback và download denial checks vẫn chạy. Chưa kiểm chứng concurrent initialization hoặc mọi room/area mismatch bằng runtime.

Runtime clone được tạo COPY_ONLY từ database sprint rỗng và xác minh exact instance/name/GUID/run. Sau khi API dừng, [clone/backup bị xóa](checkpoint27-clone.json.cleanup.json); [đọc kiểm chứng độc lập](checkpoint27-cleanup.json) xác nhận clone vắng mặt, source synthetic tables và thư mục evidence trống. Runtime report `cleanup=false` thể hiện cleanup từng dòng không áp dụng với immutable criteria; cleanup whole-clone được chứng minh riêng. Không disable trigger, không shared HotelOpsDB mutation. Probe pin clone của lượt này đã drop; replay cần provision và xác minh identity mới.

Giữ runtime warnings RoomTypeBranchV1 unmapped indexes và infrastructure Skip/Take. npm ci không rerun vì dependencies không đổi. FE11 Create/Assign/Accept/Start/Complete/Cancel/Reopen chưa hoàn chỉnh; không claim whole-UC completion.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=27_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=6340614195e092652af87d3bb7d5e3ed81d25a2c
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=CANONICAL_TASK_CRITERIA_INITIALIZATION_FORM_AND_SCOPED_INITIALIZATION_GUARD
CHANGED_PATHS=frontend/src/operationalTasks/{TaskCriteriaSetup.tsx,TaskReadPage.tsx,progress.ts};frontend/tests/operational-task-progress.test.mjs;frontend/scripts/sprintTaskInitializeBrowser.mjs;src/HotelOps.Api/Services/Housekeeping/WorkProgressService.cs;scripts/verification/CanonicalDatabase/ModelProbe/{SprintAuthProbe.cs,SprintTaskReadScenario.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-27.md,UC_STATUS_MATRIX.md,checkpoint27-*}
TESTS_EXECUTED=18_TASK_CONTRACT_PASS;163_AUTH_ROUTER_PASS;57_RUNTIME_CHECKS_INCLUDING_13_BROWSER_STAGES_PASS
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;TYPECHECK_PASS;FRONTEND_PASS_EXISTING_WARNINGS
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_COMPOSITION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=EXISTING_STARTED_TASK_CRITERIA_SETUP_AND_EXECUTOR_PROGRESS_RUNTIME_ACCEPTED;CREATE_AND_LIFECYCLE_PENDING
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_REMAIN
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=OWNED_CLONE_DROPPED;BACKUP_REMOVED;SOURCE_SYNTHETIC_ROWS_EMPTY
HTTPS_API_STATUS=REAL_API_BROWSER_PASS;TRIAL_STOPPED
E2E_VERIFIED_WORKFLOWS=MANAGER_CRITERIA_INITIALIZATION_THEN_EXECUTOR_RESULTS_NOTES_PROGRESS_EVIDENCE
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_ROOM_MAPPING;FE11_CREATE_LIFECYCLE_CONTRACTS;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=FE11 create/lifecycle contract reconciliation; FE13 supported workflow integration; FE05 room projection after mapping decision
```
