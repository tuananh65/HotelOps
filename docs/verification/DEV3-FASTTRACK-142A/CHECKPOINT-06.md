# FASTTRACK-142A — checkpoint 06

Baseline `1356ba5228bbbd9a21313e46c85989c7cb5db648`, branch `integration/dev1-dev2-dev3-unblock-20261114`. Đây là checkpoint đang thực hiện, không phải nghiệm thu toàn bộ sprint hoặc UC010.

## Chức năng đã tích hợp

Router chính mở `/users/:id/roles/assign` qua `AssignUserRoleFlowBoundary` hiện có; trang tài khoản có liên kết Assign role. Shared host mặc định tắt khả năng này, canonical host bật rõ ràng. Boundary giữ trạng thái kết quả ghi không xác định qua phục hồi phiên; không bọc bằng lớp remount làm mất trạng thái đó. API tiếp tục quyết định quyền, phạm vi, ETag và danh mục vai trò; không thêm quyền hoặc nới điều kiện nghiệp vụ. Thu hồi vai trò và vô hiệu hóa tài khoản vẫn giữ các release gate chưa được chứng minh.

Chuyển bộ regression gán vai trò từ standalone host sang canonical host; bổ sung kiểm tra điều hướng và bốn trạng thái phiên không được đọc nghiệp vụ. Cấu hình test nạp CSS riêng cho bundle kiểm thử. Kiểm tra graph giữ cấm prototype/mock/storage; `ui.tsx` hiện chỉ chứa PageHeader dùng metadata FE04, nên không còn áp dụng lệnh cấm tên file từ standalone host. Build production vẫn xử lý CSS thực.

## Bằng chứng trực tiếp

| Lệnh / phạm vi | Kết quả | Evidence |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS, 0 warnings/errors | `checkpoint06-build-final.log.txt` |
| `dotnet test HotelOps.slnx` | Exit 0; solution không discover test, không tính là suite PASS | `checkpoint06-solution-test.log.txt` |
| `dotnet run --project tests/HotelOps.Dev3Unblock.NoSql` | 101 PASS; không dùng SQL | `checkpoint06-nosql.log.txt` |
| `npm.cmd run typecheck` | PASS | `checkpoint06-typecheck-final.log.txt` |
| `npm.cmd run build` | PASS | `checkpoint06-frontend-build-final.log.txt` |
| `node --test --test-concurrency=2 tests/assign-user-role-router-integration.test.mjs tests/shared-auth-host.test.mjs` | 179 PASS, 0 FAIL/SKIP | `checkpoint06-router-final.log.txt` |
| Navigation test riêng | 1 PASS | `checkpoint06-navigation-verified.log.txt` |
| ModelProbe `--sprint-user-coordination` | 3 physical SQL cases PASS, cleanup true | `user-coordination-verified.json` |
| ModelProbe `--sprint-role-assignment` | 22 HTTPS/SQL checks PASS, cleanup true | `role-assignment-https-final.json` |

ModelProbe invocation: `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe -p:BuildProjectReferences=false -- <mode> <new-evidence-path>`. Harness từ chối ghi đè evidence. npm ci không chạy lại: lockfile và dependency không đổi; kết quả npm ci trước đây chỉ là bằng chứng lịch sử.

SQL coordination gọi thật `GlobalAdminSerialization` và `UserEligibilityLock`, quan sát DMV xác nhận waiter/blocker với lock wait: global-first đọc trạng thái INACTIVE mới thay vì entity ACTIVE đang tracked; receiver-first giữ global snapshot chờ đến rollback; cancellation khi chờ không trở thành cấp phép. Đây là primitive coordination, không thay thế nghiệm thu tất cả writer hoặc API deactivation/revoke.

HTTPS dùng cookie, CSRF và kiểm chứng chứng chỉ localhost bình thường. Fixture dùng SYS_ADMIN hiện có, target HK_STAFF trong branch/department tổng hợp. Xác minh 400 thiếu CSRF, 428 thiếu If-Match, 403 thiếu quyền/vai trò không được phép, GET management profile đúng target và ETag, 412 revision cũ, 201 assignment persisted với audit và revision mới, 409 gán trùng; thêm login/session/logout/revocation checks. Browser regression dùng mocked HTTP; chưa phải browser-to-real-SQL E2E.

## Lỗi gặp và xử lý

- Lần coordination đầu cast `@@SPID` Int16 thành Int32 lỗi; sửa SQL `CONVERT(int,@@SPID)`, chạy lại đạt 3 cases. Giữ `user-coordination-first.json`.
- Lần role trial bị gián đoạn để lại actor 41, target 42, branch 37: cleanup xóa branch trước audit có FK. Đã xác minh exact database GUID/run marker và username/branch code, cleanup đúng các row tổng hợp trong transaction; sửa helper xóa audit trước branch. Một lần sqlcmd cleanup bị từ chối vì thiếu QUOTED_IDENTIFIER; transaction rollback, chạy lại đủ SET options đạt. Không dùng DELETE toàn bảng. Hai lần trial sau lần lượt 21 và 22 checks PASS với cleanup true.
- Thử khởi động API trùng listener gặp address-in-use; không sửa cổng hoặc dừng process không xác minh. API đã được dừng sau trial.
- Lần browser đầu: 122 PASS, 1 file lỗi bundle do CSS; sửa harness. Lần tiếp theo: 177 PASS, 1 FAIL tại assertion tên `ui.tsx` cũ; kiểm tra nội dung và đổi graph assertion theo canonical composition, giữ cấm prototype/storage. Giữ các log thất bại; xem lần chạy final để đánh giá kết quả sửa.
- Filter xUnit `FullyQualifiedName~RoleAssignment` không tìm thấy test, không tính PASS. Navigation wrapper đầu lỗi quoting; lần chạy đúng đạt 1 PASS.
- Vite vẫn cảnh báo React Router `use client` và bundle 554.24 kB; không tăng warning threshold. Runtime có ba cảnh báo mapping index RoomTypeBranchV1 và một cảnh báo query Take/Skip thiếu OrderBy. Không sửa generated mappings để che cảnh báo.

## SQL / HTTPS safety

Target `meita` / `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, marker `cc78c13ffc124efcad67b3d804baa84c`. Harness kiểm tra identity trước mutation, dùng synthetic data. HTTP cần commit thật nên dùng compensating cleanup có điều kiện nhận diện fixture; các trial lock có rollback/cleanup riêng. `checkpoint06-sql-cleanup.log.txt` ghi 0 users/sessions/audits/grants/branches/departments. Trial cuối cũng xác nhận cleanup true. Không mutation shared HotelOpsDB/ImportReview; không đổi User Secrets, certificate policy, schema hoặc permission catalog.

## Receipt

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=06_ACTIVE`; `ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114`; `INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`. CURRENT_SHA và COMMIT_SHAS được ghi trong receipt sau commit/push để không tạo self-referential hash.

`SOURCE_IMPLEMENTED=CANONICAL_ROLE_ASSIGNMENT_ROUTE_AND_NAVIGATION; REAL_SQL_COORDINATION_AND_HTTPS_PROBES`.

`CHANGED_PATHS=frontend/src/auth/AuthRouteHost.tsx; frontend/src/shared-host/SharedAuthHost.tsx; frontend/src/users/UserAccessReviewPage.tsx; frontend/tests/{assign-user-role-router-integration,shared-auth-host}.test.mjs; scripts/verification/CanonicalDatabase/ModelProbe/{Program,SprintAuthProbe,SprintRoleAssignmentScenario,SprintUserCoordinationProbe}.cs; docs/verification/DEV3-FASTTRACK-142A/*checkpoint06* and new trial JSON, CHECKPOINT-06.md, UC_STATUS_MATRIX.md`.

`BUILD_STATUS=BE/TYPECHECK/FE_PRODUCTION_PASS`; `FE05_STATUS=PARTIAL; APPROVAL_COMPOSITION_PENDING`; `UC006_UC010_STATUS=PRIMITIVE_SQL_COORDINATION_AND_SUPPORTED_GRANT_VERIFIED; DEACTIVATION_REVOKE_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=PARTIAL`; `FE15_STATUS=AUDIT_READ_API_UI_IMPLEMENTED; FULL_ACCEPTANCE_PENDING`.

`ISOLATED_SQL_STATUS=EXACT_IDENTITY_VERIFIED; SYNTHETIC_CLEANUP_PASS`; `HTTPS_API_STATUS=22_CHECKS_PASS`; `E2E_VERIFIED_WORKFLOWS=HTTPS_TO_SQL_SUPPORTED_ROLE_ASSIGNMENT_SLICE; NO_BROWSER_TO_SQL_ACCEPTANCE`.

`UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`. Không nâng whole-UC status theo số test của một phần chức năng.

`CRITICAL_BLOCKERS=FE05_PASSED_ISSUER_NOTIFICATION_PROJECTION_REPLAY; ACTUAL_DEACTIVATION_REVOKE_WRITERS; FE11_FE13_WORKFLOWS; FE14_REPORT_INTEGRATION; BROWSER_SQL_E2E`.

`NEXT_IMPLEMENTATION=Complete FE05 accepted Passed composition and SQL cases; prove actual competing UC006/010 writer transactions; integrate confirmed FE11/13 and FE14/report workflows`. SRS UC044 xác định thông báo cho housekeeper đã dọn phòng; candidate binding vẫn cố ý đóng vì trusted issuer/staging chưa triển khai. Đây là việc triển khai tiếp, không phải chờ DEV1/DEV2.
