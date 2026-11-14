# FASTTRACK-142A — checkpoint 14: canonical account activation

Baseline: `6172c523cb18fa1584125a276685eb8fd540e8e8`. Fetch confirmed the same canonical remote SHA before delivery. Sprint remains ACTIVE.

Đã nối `/users/:id/activate` vào host canonical, thêm liên kết từ trang tài khoản và dùng lại ActivateUserFlowBoundary. Boundary mutation giữ cơ chế ẩn dữ liệu khi phiên không xác định, không tự retry và không bị bọc bởi Protected có thể remount khi authority thay đổi. API vẫn tự kiểm tra phiên, quyền USER_MANAGE, CSRF, If-Match, trạng thái và audit; không đổi policy hoặc schema.

SRS UC-006 alternative flow cho phép INACTIVE → ACTIVE và giữ locked_until. API/service đã có trong baseline nhưng thiếu route canonical. Đây là phần được tích hợp trong checkpoint, không phải triển khai mới deactivation.

## Bằng chứng thực thi trực tiếp

- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors. ModelProbe build PASS, 0 warnings/errors.
- TypeScript và Vite production build: PASS. Vite vẫn cảnh báo module use-client và bundle lớn hơn 500 kB; không chỉnh threshold để che cảnh báo.
- `checkpoint14-canonical-router-verified.log.txt`: 260/260 PASS, gồm activation API/client, canonical navigation và session/unknown-mutation regressions. Bộ activation cũ được chuyển từ StandaloneAuthRouteHost sang AuthRouteHost thật.
- `checkpoint14-nosql.log.txt`: 101 PASS; không kết nối SQL, không phải nghiệm thu concurrency SQL.
- `dotnet test HotelOps.slnx`: exit 0, chỉ restore, không discovery test. Không tính lệnh này là xUnit PASS.
- `activation-https-first.json`: 23 checks PASS, cleanup=true. HTTP thật: thiếu CSRF400, thiếu If-Match428, weak ETag400, reason ngắn400, thiếu quyền403, tự kích hoạt403, stale412, success200, replay stale412, đã ACTIVE409. SQL xác nhận rowversion mới, đúng một audit và giữ nguyên lockout, failed-login count, password hash, temporary credential expiry, revoked session.
- `activation-browser-first.json`: 24 harness checks PASS, chứa 6 bước Edge thật: anonymous deep link → login; đăng nhập admin; điều hướng từ tài khoản; nhập lý do/review/confirm; kiểm tra cookie/storage; logout và quay lại protected route. Chạy bundle production qua https://localhost:7142 với TLS validation mặc định, không mock/intercept. SQL sau browser xác nhận trạng thái ACTIVE, audit duy nhất, credential/lockout/phiên cũ không đổi và phiên admin của browser đã revoke. Các con số check không phải số UC hoàn thành.

Các lần regression lỗi được giữ trong `checkpoint14-canonical-router.log.txt` và `checkpoint14-canonical-router-final.log.txt`: harness thiếu CSS loader khi import host canonical, sau đó assertion graph cũ cấm cả UI/i18n dùng bởi module khác. Đã cấu hình CSS/env cho test bundler, giữ assertion không import mocks/user mock screens và kiểm tra host canonical thực. Toàn bộ behavioral tests giữ nguyên và chạy PASS cuối cùng. Đây không phải nới lỏng authorization.

## SQL/HTTPS và giới hạn

Harness kiểm tra instance meita, database `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, marker `cc78c13ffc124efcad67b3d804baa84c` và baseline personnel/session/audit rỗng trước INSERT. Chỉ synthetic data; API commits được cleanup bằng transaction theo fixture identity. Cả hai lần SQL/HTTP đều cleanup=true. Không mutate shared HotelOpsDB. API local đã dừng sau thử nghiệm.

Runtime còn cảnh báo RoomTypeBranchV1 unmapped indexes và Skip/Take thiếu OrderBy đã tồn tại. Khi dừng API có log transaction error không kèm chi tiết; không suy diễn đó là lỗi activation: cả hai harness đã hoàn tất với cleanup=true trước khi dừng. Không có bằng chứng để tuyên bố production readiness.

Deactivation source vẫn có release gates và unresolved task/request/shift/line-manager contracts; không mở fuse chỉ vì owner cũ rời dự án. Chưa xác minh deactivation/revoke thực, toàn bộ writer races, last-admin mutation rollback hay mọi scope authorization trên SQL. UC006 vẫn PARTIAL. Không tăng số UC COMPLETE từ một alternative flow.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=14_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
INITIAL_REMOTE_SHA=6172c523cb18fa1584125a276685eb8fd540e8e8
SOURCE_IMPLEMENTED=CANONICAL_ACCOUNT_ACTIVATION_ROUTE_AND_NAVIGATION;REAL_BROWSER_HTTPS_SQL_VERIFICATION
CHANGED_PATHS=frontend/src/{auth/AuthRouteHost.tsx,shared-host/SharedAuthHost.tsx,users/UserAccessReviewPage.tsx};frontend/tests/{activate-user-router-integration.test.mjs,shared-auth-host.test.mjs};frontend/scripts/sprintActivationBrowser.mjs;scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintAuthProbe.cs,SprintActivationScenario.cs,SprintActivationBrowser.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-14.md,UC_STATUS_MATRIX.md,checkpoint14*,activation-*.json}
TESTS_EXECUTED=260_CANONICAL_ROUTER_ACTIVATION_PASS;101_NOSQL_PASS;23_HTTP_CHECKS_PASS;24_BROWSER_HARNESS_CHECKS_PASS_WITH_6_BROWSER_STAGES
BUILD_STATUS=BACKEND_0_WARNINGS_PASS;PROBE_PASS;TYPECHECK_PASS;FRONTEND_PRODUCTION_PASS
FRONTEND_NPM_CI=NOT_RERUN_THIS_CHECKPOINT;LOCKFILE_UNCHANGED;BASELINE_PASS_HISTORICAL
FE05_STATUS=PARTIAL_UNCHANGED
UC006_UC010_STATUS=ACTIVATION_E2E_VERIFIED;DEACTIVATION_AND_REVOKE_PENDING
FE11_STATUS=PARTIAL
FE13_STATUS=QUERY_COMPONENT_ONLY;AUTHORIZATION_CONTRACT_GAP
FE14_STATUS=INBOX_BROWSER_HTTPS_SQL_VERIFIED_PREVIOUS_CHECKPOINT;ACK_SOURCE_OPEN_PENDING
FE15_STATUS=AUDIT_READ_PARTIAL;REPORTS_PENDING
ISOLATED_SQL_STATUS=SYNTHETIC_FIXTURES_CLEANUP_TRUE
HTTPS_API_STATUS=REAL_EDGE_AND_HTTP_DEFAULT_TLS_VALIDATION
E2E_VERIFIED_WORKFLOWS=CANONICAL_ACCOUNT_ACTIVATION_WITH_LOGIN_CONFIRMATION_AND_LOGOUT
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_LIFECYCLE_COMPOSITION;DEACTIVATION_REVOKE_RESPONSIBILITIES;FE11_13_WRITERS;FE13_READ_CONTRACT;ACKNOWLEDGMENT_STORAGE;REMAINING_ACCEPTANCE
NEXT_IMPLEMENTATION=Resolve target-dependent UC006 responsibilities and actual writer coordination; implement accepted FE05 approval composition; complete independent FE11/FE13 workflows with confirmed authorization

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS reported after delivery to avoid self-reference.
