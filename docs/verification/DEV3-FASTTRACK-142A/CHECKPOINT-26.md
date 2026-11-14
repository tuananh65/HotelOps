# FASTTRACK-142A — checkpoint 26

Đã chạy workflow tiêu chí FE11 thật: executor cập nhật required criterion PASS, optional criterion FAIL, ghi chú riêng từng tiêu chí, phần trăm và evidence; reload browser giữ đúng dữ liệu, SQL và audit chứa kết quả/ghi chú. Sửa editor bằng `aria-label` riêng cho mỗi ô kết quả để control có tên truy cập rõ ràng và được chọn ổn định. Không thay đổi business status/permission/schema.

## Thực thi và giới hạn

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint26-backend-build.txt). Probe: [PASS](checkpoint26-probe-final-build.txt).
- `npm --prefix frontend run typecheck` và `npm --prefix frontend run build`: [exit codes 0](checkpoint26-frontend-exits.json), [typecheck](checkpoint26-typecheck-final.txt), [production build](checkpoint26-frontend-build-final.txt). Giữ warnings use-client/chunk >500 kB. Một shell gộp trước đó báo exit1 dù log build thành công; đã chạy lại hai lệnh và ghi exit code riêng, không suy đoán PASS từ shell đó.
- `node --test frontend/tests/operational-task-progress.test.mjs frontend/tests/operational-task-read.test.mjs`: [15 PASS](checkpoint26-task-tests.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-criteria docs/verification/DEV3-FASTTRACK-142A/checkpoint26-runtime-final.json`: [50 top-level checks PASS](checkpoint26-runtime-final.json), gồm 9 bước browser thật; SQL kiểm tra 2 kết quả PASS/FAIL và ghi chú, audit và progress/evidence. Các checks CSRF, scope, stale/concurrent write, rollback invalid evidence và revoked access vẫn chạy.

Trial đầu [FAIL tại select criterion](checkpoint26-runtime.json), không tính PASS; đã xác nhận 2 tiêu chí tồn tại trong SQL, bổ sung nhãn control rồi chạy trên clone mới. Lần đầu tạo clone nhận GUID chưa ổn định; [receipt đọc kết nối mới](checkpoint26-clone-verified.json) được xác minh trước fixture. Runner nay reconnect/CHECKPOINT và từ chối Guid.Empty trước CLONE_READY.

`Invoke-Dev3CriteriaClone.ps1 -Mode Create -ReceiptPath <new receipt>` kiểm tra exact server/source GUID/run, yêu cầu nguồn không có synthetic users/branches/tasks/attachments/audits, COPY_ONLY backup + CHECKSUM/VERIFYONLY, restore vào tên clone ngẫu nhiên với file riêng và run marker mới. Giữ nguyên schema và trigger immutable criteria. Chỉ clone được ghi fixture; database sprint gốc được đọc để backup. Không dùng shared HotelOpsDB.

Runtime report có `cleanup=false, cleanupRequiresDatabaseDrop=true` vì immutable criteria không được xóa từng dòng. Sau khi dừng API, runner Cleanup xác minh clone GUID/run, từ chối nếu còn session khác, DROP đúng clone và xóa đúng backup path đã cấp. [Cleanup trial đầu](checkpoint26-clone-verified.json.cleanup.json), [cleanup trial đạt](checkpoint26-clone-final.json.cleanup.json), và [kiểm chứng cuối độc lập](checkpoint26-cleanup-verification.json) chứng minh cả hai clone đã vắng mặt, dữ liệu synthetic nguồn và thư mục evidence đều trống. Không dùng force disconnect hoặc disable trigger. API đã dừng.

Probe criteria pin clone đã xác minh của lượt này; clone đã bị drop nên không thể replay trực tiếp. Muốn chạy lại phải tạo clone mới và đối chiếu/cập nhật target identity trong probe trước runtime. Đây là harness verification, không cấu hình ứng dụng production. Không commit backup/database/credentials.

UC109 vẫn PARTIAL: trial bắt đầu từ synthetic task đã In Progress/Paused và criteria được seed bằng SQL, không chứng minh task creation/initialization/assignment hoặc các lifecycle còn thiếu. Chưa có acceptance tất cả file limits/formats, commit disconnect hay mọi permission race. Không suy diễn hoàn tất toàn UC từ workflow dương tính này.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=26_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=7972e754000041b461a829436e4d596726a67dc3
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=ACCESSIBLE_CRITERION_RESULT_CONTROLS_AND_GUARDED_DISPOSABLE_CLONE_RUNTIME_HARNESS
CHANGED_PATHS=frontend/src/operationalTasks/TaskProgressPanel.tsx;frontend/scripts/sprintTaskReadBrowser.mjs;scripts/verification/Invoke-Dev3CriteriaClone.ps1;scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintAuthProbe.cs,SprintTaskReadScenario.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-26.md,UC_STATUS_MATRIX.md,checkpoint26-*}
TESTS_EXECUTED=15_OFFLINE_PASS;50_RUNTIME_CHECKS_INCLUDING_9_BROWSER_STAGES_PASS;INITIAL_BROWSER_TRIAL_FAIL_RETAINED
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;TYPECHECK_PASS;FRONTEND_PASS_EXISTING_WARNINGS
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_COMPOSITION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=CRITERIA_PASS_FAIL_NOTES_PROGRESS_EVIDENCE_WORKFLOW_RUNTIME_ACCEPTED;INITIALIZATION_AND_LIFECYCLE_PENDING
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_REMAIN
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=OWNED_CLONES_VERIFIED_AND_DROPPED;BACKUPS_REMOVED;SOURCE_SYNTHETIC_ROWS_EMPTY
HTTPS_API_STATUS=REAL_API_BROWSER_PASS;TRIAL_STOPPED
E2E_VERIFIED_WORKFLOWS=TASK_CRITERIA_RESULTS_NOTES_PROGRESS_EVIDENCE_SAVE_RELOAD_SQL_AUDIT
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_ROOM_MAPPING;FE11_CREATE_LIFECYCLE_CONTRACTS;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=FE11 supported initialization and lifecycle integration; FE13 authority and workflow gaps; FE05 room projection after mapping decision
```
