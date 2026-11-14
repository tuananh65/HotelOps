# FASTTRACK-142A — checkpoint 19: execution rejection and concurrent Finish

Sprint ACTIVE. Baseline `fa60d6590d345cb2c4b86d3e5ff37589f263af66`.

Mở rộng harness browser canonical hiện có để gọi API qua fetch trong browser, cùng cookie jar và TLS validation mặc định. Không mock/intercept, không đổi application source, schema hoặc policy để làm test PASS.

## Bằng chứng trực tiếp

`checkpoint19-concurrent.json`: success=true, cleanup=true. 21 checks/stages trong browser gồm happy path trước đây và:

- Anonymous execution GET401.
- Thiếu CSRF400; thiếu If-Match428; weak ETag400; stale ETag412; sai branch403.
- Finish trước Start409; thiếu mandatory result400; FAIL chưa có defect400.
- Criterion không tồn tại400; severity không tồn tại400.
- Hai attachments: file đầu hợp lệ, file thứ hai invalid400. Response execution và ETag giữ nguyên sau mọi request bị từ chối. Cuối trial chỉ có một defect/attachment hợp lệ; cleanup không để file dư. Đây là bằng chứng rollback SQL và precommit file cleanup cho trường hợp lỗi validation, không phải lost commit acknowledgment.
- Hai request Finish đồng thời dùng cùng ETag trả [200,412]. Nút Finish trên UI còn ETag cũ bị chặn, yêu cầu explicit GET; sau đọc lại hiển thị PENDING_CONCLUSION. Không tuyên bố đã đo SQL lock wait hay kiểm chứng mọi lịch race.
- SQL xác nhận PENDING_CONCLUSION, một FAIL result, một defect, đúng bốn audit inspection và bytes evidence đúng; reload/logout thành công.

`checkpoint19-browser-transport.json` là lần chạy trước khi thêm concurrent Finish, success=true/cleanup=true. Lần đầu dùng Playwright request context thất bại ở anonymous execution check, cleanup=true (`checkpoint19-runtime.json`); report chỉ giữ stage, không đủ thông tin kết luận nguyên nhân. Đã chuyển API checks sang fetch trong browser thực, không tắt TLS. Không tính lần thất bại là PASS.

Harness .NET/fixture không đổi: exact server/database/GUID/marker guard, rehearsal rollback và transactional cleanup theo ID fixture như checkpoint18. Không mutate shared HotelOpsDB. API đã dừng sau kiểm chứng. Cảnh báo RoomTypeBranchV1 indexes và Skip/Take thiếu OrderBy vẫn tồn tại.

Chỉ sửa script kiểm chứng; backend build/test, frontend typecheck/build và npm ci không chạy lại. Runtime dùng backend đã build checkpoint18 và production frontend checkpoint17. UC042/043 vẫn PARTIAL: chưa kiểm chứng wrong assigned inspector, role/session revoke race, defect editing, evidence download, approval/rejection/reinspection và room projection.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=19_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=fa60d6590d345cb2c4b86d3e5ff37589f263af66
SOURCE_CONSOLIDATION_STATUS=CANONICAL_PRESERVED;NO_DUPLICATE_MERGE
BACKEND_BUILD=NOT_RERUN;CHECKPOINT18_BUILD_USED
FRONTEND_NPM_CI=NOT_RERUN
FRONTEND_TYPECHECK=NOT_RERUN
FRONTEND_BUILD=NOT_RERUN;CHECKPOINT17_BUNDLE_USED
AUTH_ROUTER_TESTS=21_REAL_BROWSER_HTTP_SQL_CHECKS_STAGES_PASS
IMPLEMENTED_FUNCTIONALITY=EXECUTION_NEGATIVE_AND_CONCURRENT_FINISH_RUNTIME_REGRESSION_HARNESS
CHANGED_FILES=frontend/scripts/sprintInspectionBrowser.mjs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-19.md,UC_STATUS_MATRIX.md,checkpoint19-*}
SQL_HTTPS_STATUS=REAL_BROWSER_TLS_AND_OWNED_SQL_PASS;CLEANUP_TRUE;API_STOPPED
UC_REMAINING_GAPS=144_PARTIAL;FE05_COMPOSITION;UC006_UC010_RESPONSIBILITIES;FE11_FE13_WORKFLOWS;REMAINING_ACCEPTANCE
NEXT_THREE_CODING_TASKS=Accepted FE05 approval notification/projection/receipt composition; rejection/reinspection lifecycle integration; confirmed FE11_FE13 workflows

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS reported after delivery to avoid self-reference.
