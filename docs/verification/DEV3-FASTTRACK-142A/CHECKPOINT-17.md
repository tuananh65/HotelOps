# FASTTRACK-142A — checkpoint 17: canonical inspection execution

Sprint ACTIVE. Baseline `9631d39eaa47117db7d5fc0be8df01346b6b4ada`.

Đã bật route `/inspections/:branch/:id/execution` trong AuthRouteHost canonical và thêm InspectionExecutionAuthRoute. Trước khi mount form, boundary gọi execution GET thật qua client hiện có, kiểm tra DTO/ETag/scope; server vẫn quyết định INSPECTION_PERFORM và assigned inspector. Workspace đọc lại dữ liệu trước thao tác. Không suy quyền từ role name, URL hoặc session; không mở Approve/Reject/Reinspection.

Anonymous được chuyển login với return URL đã kiểm tra; restricted chuyển change-password; unknown/unavailable không gửi business read. ID phải là positive safe integer. Sau thay đổi authority, boundary hủy scope cũ, ẩn dữ liệu và yêu cầu explicit Verify inspection access. Việc xác minh chỉ đọc mới, không tự retry mutation. Các lỗi 403/503 ở admission không mount form. Default flag của SharedAuthHost vẫn false, canonical bật rõ ràng.

## Kiểm chứng trực tiếp

- Frontend production build và typecheck PASS. Cảnh báo Vite use-client và bundle >500 kB còn tồn tại.
- `checkpoint17-router.log.txt`: 178 PASS trên shared canonical host, activation integration và inspection contract tests.
- Sau khi thêm hai tình huống authority đổi trong CSRF / sau mutation dispatch: `checkpoint17-inspection-router-verified.log.txt`, 7 PASS (5 trường hợp trùng suite trước, 2 mới). Xác nhận không gửi mutation sau CSRF bị hủy; mutation đã gửi chỉ được gửi một lần; không tự đọc lại trước explicit recovery; form cũ không còn hiển thị.
- `checkpoint17-execution-browser.log.txt`: 11 Edge tests PASS cho execution/defect/evidence flow và conflict/unknown-write handling.
- Browser tests dùng HTTP giả lập. Chưa chạy end-to-end canonical HTTPS/SQL cho route này. SQL component PASS ở checkpoint16 là lịch sử, không chuyển thành E2E PASS.
- Lần gọi focused tests đầu sai shell quoting và không chạy test; giữ `checkpoint17-inspection-router.log.txt`. Lần chạy đúng filter ở file verified đã PASS.
- Không đổi C#, dependencies hoặc lockfile; backend build/test và npm ci không chạy lại. Không SQL mutation, không API runtime được khởi động checkpoint này.

UC042/043 đã có canonical route nhưng vẫn PARTIAL. Các khoảng trống: execution HTTPS/SQL acceptance, approval/rejection/reinspection composition, room projection, notification/receipt, evidence retrieval. Không coi route mount là nghiệm thu toàn UC.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=17_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=9631d39eaa47117db7d5fc0be8df01346b6b4ada
SOURCE_CONSOLIDATION_STATUS=CANONICAL_PRESERVED;NO_DUPLICATE_MERGE
BACKEND_BUILD=NOT_RERUN;NO_CSHARP_CHANGE
FRONTEND_NPM_CI=NOT_RERUN;LOCKFILE_UNCHANGED
FRONTEND_TYPECHECK=PASS
FRONTEND_BUILD=PASS_WITH_EXISTING_WARNINGS
AUTH_ROUTER_TESTS=178_SELECTED_PASS;7_FOCUSED_PASS_OVERLAPPING_5;11_EXECUTION_BROWSER_PASS
IMPLEMENTED_FUNCTIONALITY=CANONICAL_INSPECTION_EXECUTION_ROUTE;API_ADMISSION;AUTHORITY_RECOVERY_WITHOUT_WRITE_REPLAY;LOGIN_RETURN_URL
CHANGED_FILES=frontend/src/auth/AuthRouteHost.tsx;frontend/src/shared-host/{SharedAuthHost.tsx,InspectionExecutionAuthRoute.tsx,returnUrl.ts};frontend/tests/shared-auth-host.test.mjs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-17.md,UC_STATUS_MATRIX.md,checkpoint17-*.log.txt}
UC_REMAINING_GAPS=144_PARTIAL;CANONICAL_INSPECTION_HTTPS_SQL;FE05_COMPOSITION;UC006_UC010_RESPONSIBILITIES;FE11_FE13_LIFECYCLES
SQL_HTTPS_STATUS=NOT_RUN;NO_SHARED_MUTATION
NEXT_THREE_CODING_TASKS=Real canonical inspection HTTPS/SQL start-results-defect-finish trial; accepted FE05 approval projection/notification/receipt composition; confirmed FE11_FE13 workflows

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS are reported after delivery to avoid self-reference.
