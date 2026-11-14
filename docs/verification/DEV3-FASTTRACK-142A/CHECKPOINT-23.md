# FASTTRACK-142A — checkpoint 23

Tiến triển: thêm GET `/api/branches/{branchId}/operational-tasks` và `/{id}`, DTO riêng, trang canonical `/operational-tasks`, link trên home và return-to-login. List hỗ trợ search/status/department/page; detail chứa progress history phân trang có thứ tự. Dates trả UTC; không trả EF entities. UI hiện nhập Branch ID, chưa có branch selector theo tên.

Authority: dùng session/transaction/final gate hiện có; TASK_VIEW BRANCH/GLOBAL hoặc OWN theo đúng một ACTIVE assignment chưa kết thúc và đã bắt đầu. OWN không tự có quyền đọc sau khi assignment kết thúc; chưa tuyên bố historical assignee policy. Permission/branch access thực tế được kiểm tra, không hard-code role IDs. Department Head có branch scope, không tự giới hạn vào một department. Query loại task có department/room/area sai branch. Kết quả đã đọc với scope rộng không được trả khi final gate chuyển sang OWN. UI abort request cũ và ràng buộc kết quả theo authority generation.

UC102 yêu cầu expected outcome, completion criteria và recurring series chưa có trong OperationalTask entity; không tạo API Create giả hoàn chỉnh bằng cách bỏ các điều kiện đó. Status hiển thị nguyên trạng persisted SQL (`TODO`, v.v.), không tự đổi thành trạng thái SRS. Không đổi schema/generated entities. FE05 room Blocked mapping vẫn đang chờ câu trả lời; lượt này triển khai FE11 độc lập.

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint23-build-final.log.txt).
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS](checkpoint23-probe-build-anonymous.log.txt).
- `npm run typecheck`: [PASS](checkpoint23-typecheck.log.txt); `npm run build`: [PASS](checkpoint23-frontend-build.log.txt), giữ cảnh báo Vite use-client và chunk >500 kB.
- `node --test tests/operational-task-read.test.mjs tests/shared-auth-host.test.mjs tests/auth-router-integration.test.mjs`: [169 PASS](checkpoint23-router.log.txt), gồm 6 API contract tests mới. Đây là offline/mock tests, không chứng minh SQL persistence.
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-task-read docs/verification/DEV3-FASTTRACK-142A/checkpoint23-runtime-final.json`: [PASS, 31 top-level probe checks, cleanup=true](checkpoint23-runtime-final.json). Trong đó browser result có 6 bước qua production bundle, Edge TLS mặc định và API/SQL thật, không interception/mock: deep-link login; list; detail/history; empty search; wrong branch denial; logout/deep-link protection.
- HTTP cases gồm anonymous401, no-grant403, OWN list/detail/history, unassigned/foreign task404, foreign branch403, invalid paging400, ended assignment mất OWN access, expired grant403, branch-wide list loại foreign department, search, detail và revoked branch access403. Fixture rehearsal rollback trước setup commit; cleanup chỉ fixture IDs, không shared HotelOpsDB. API trial đã dừng.
- `dotnet test HotelOps.slnx`: [restore-only exit0](checkpoint23-solution-test.log.txt), không tính discovery/PASS. Không có xUnit case mới; behavior backend mới kiểm chứng bằng runtime HTTP/SQL.

Runtime giữ cảnh báo RoomTypeBranchV1 indexes và Skip/Take thiếu OrderBy trong infrastructure hiện hữu; query task list/history mới có thứ tự tường minh. Lệnh npx.ps1 bị execution policy chặn, đã dùng `cmd /c npx` có sẵn; không đổi execution policy. npm ci không chạy lại do lockfile/dependencies không đổi. Không claim create/assignment/lifecycle E2E từ read workflow.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=23_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=5c48f1b8b22eceeeefc2e20dbe1385944db62763
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=FE11_SCOPED_TASK_LIST_DETAIL_PROGRESS_HISTORY_API_AND_CANONICAL_REACT_ROUTE
CHANGED_PATHS=src/HotelOps.Api/{Controllers/OperationalTasksController.cs,DTOs/OperationalTasks/OperationalTaskReadContracts.cs,Services/OperationalTasks/OperationalTaskReadService.cs};frontend/{src/operationalTasks,src/auth/AuthRouteHost.tsx,src/shared-host/SharedAuthHost.tsx,src/shared-host/returnUrl.ts,tests/operational-task-read.test.mjs,scripts/sprintTaskReadBrowser.mjs};scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintAuthProbe.cs,SprintTaskReadScenario.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-23.md,UC_STATUS_MATRIX.md,checkpoint23-*}
TESTS_EXECUTED=169_OFFLINE_PASS;31_RUNTIME_PROBE_CHECKS_INCLUDING_6_BROWSER_STAGES_PASS
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;TYPECHECK_PASS;FRONTEND_BUILD_PASS_WITH_EXISTING_WARNINGS
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_COMPOSITION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=READ_API_AND_CANONICAL_BROWSER_RUNTIME_VERIFIED;CREATE_ASSIGN_VERIFY_REOPEN_INCOMPLETE
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_INTEGRATION_REMAIN
FE14_STATUS=PARTIAL;PASSED_DELIVERY_COMPONENT_PREVIOUSLY_TESTED;WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=OWNED_FIXTURE_REHEARSAL_AND_CLEANUP_PASS;SHARED_DB_UNTOUCHED
HTTPS_API_STATUS=REAL_API_AND_BROWSER_PASS;TRIAL_STOPPED
E2E_VERIFIED_WORKFLOWS=CANONICAL_TASK_READ_LIST_DETAIL_HISTORY_SEARCH_SCOPE_DENIAL_LOGOUT
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_ROOM_MAPPING;FE11_REQUIRED_CREATE_FIELDS_AND_LIFECYCLE_CONTRACTS;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=Task branch selector and supported task update contract; FE05 room projection after mapping decision; missing FE11 creation criteria/series source recovery
