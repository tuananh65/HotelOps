# Precommit audit — Task07

> Task08 bổ sung source hardening HealthController, PermissionPolicyProvider, Shared/ReadOnlyVerification.cs (linked vào 4 runner projects), standalone integration setup và AuthorizationHardening source/docs. Không còn cần commit deployment-state để chạy setup. Xem [README Task08](../AuthorizationHardening/README.md) để chọn file theo trạng thái mới; private receipts/logs vẫn giữ local. Danh sách bên dưới là audit lịch sử Task07.

## Phạm vi và ownership

Đúng branch feature/BE1-authorization. Git diff tracked có đúng 3 file, tổng 16 dòng thêm từ các task trước:

- `src/HotelOps.Api/Data/HotelOpsDbContext.cs`: 6 dòng mapping DataScope.
- `src/HotelOps.Api/Entities/RolePermission.cs`: 2 dòng property DataScope.
- `src/HotelOps.Api/Program.cs`: 8 dòng namespace/DI authorization.

11 file untracked trong `src/HotelOps.Api/Security/Authorization/` là BE1 core/resolver Tasks01–05. Verification CurrentUserAccessor, DataScope, PermissionService, Authorization, ResourceAuthorization và HttpSqlIntegration là BE1 Tasks01–06. RbacUpgradeTrial và RbacUpgradeDeployment là lịch sử package database BE1 từ các task trước, không phải source authorization mới của Task07. Không khẳng định tác giả chỉ từ trạng thái Git; phân loại dựa trên nội dung và lịch sử task, developer phải duyệt commit selection.

WAV `scripts/database/release/codex-attention-alert.wav` là artifact ngoài phạm vi đã có: giữ nguyên, không đề xuất commit cùng authorization. Không xóa hoặc stage file nào.

## Sensitive artifact audit

Đã duyệt danh sách tracked/untracked, quét text cs/ps1/sql/json/md/txt cho password/API key/private key/connection-password assignments và các JSON fields password_hash/session_key_hash/session credential. Các hit expression được kiểm tra: reference tới secret config, nullable/default bool hoặc mô tả contract; không thấy literal credential sống trong phần được quét. Không in nội dung secrets.

Filesystem scan không thấy file secrets.json, backup .bak/.bacpac/.bcp, .mdf/.ldf hoặc key .pfx/.pem trong workspace. Có 713 files bin/obj tại thời điểm audit; đã xác nhận gitignore loại bỏ binary API, project.assets và build log mẫu. Không coi ignored files là candidates để force-add. Đây là heuristic scan và review hiện trạng, không phải chứng nhận không thể có secret ở mọi định dạng/history. Không đọc hoặc publish User Secrets để làm evidence.

Các JSON receipts thường chỉ có counts/hashes/IDs nhưng vẫn có server/machine/database identity, create timestamps, absolute paths; deployment/recovery artifacts còn chứa backup locations, recovery plan và user/business-reference metadata. Giữ local ngay cả khi không tìm thấy password. Các log .txt có thể không được gitignore; không gom stage cả thư mục verification.

## Recommended commit selection

| Nhóm | Đề xuất |
|---|---|
| 3 tracked changes nêu trên | Commit review cùng core; mapping/DI là shared changes đã thuộc Tasks02–05, DEV2 cần biết DB phải có data_scope |
| 11 authorization .cs files | Commit cùng nhau; không chỉ stage Program rồi bỏ interfaces/services untracked |
| CurrentUserAccessor/ | .csproj, Program.cs, README.md |
| DataScope/, PermissionService/, Authorization/, ResourceAuthorization/ | .csproj, Program.cs, README.md; RESULTS.md sau duyệt/sanitize máy, IDs, historical claims |
| AuthorizationHandoff/ | 4 Markdown files của Task07 |
| HttpSqlIntegration/ | Source cs/csproj/ps1 và docs chỉ là conditional candidate; xử lý F03 dependency/portability trước khi coi package clean-checkout-ready |
| scripts/database/release/ | SQL release chính đã tracked; không có SQL release change mới cần stage trong Task07 |

Tên 11 source files: CurrentUserAccessor.cs, ICurrentUserAccessor.cs, IPermissionService.cs, PermissionAuthorizationHandler.cs, PermissionAuthorizationRegistration.cs, PermissionDataSource.cs, PermissionPolicyProvider.cs, PermissionRequirement.cs, PermissionService.cs, Resources/CleaningTaskAccessReader.cs, Resources/CleaningTaskResourceResolver.cs.

Không stage bằng `git add .` hoặc cả scripts/verification. Đây là đề xuất để developer review, không phải lệnh đã thực thi.

## Giữ local / review riêng

- HttpSqlIntegration: environment.json, fixtures.json, main-before.json, main-after.json, preexisting-files.json, preservation.json, provisioning-checkpoint.json, regressions.json, results.json, test-final-state.json và các logs.
- ResourceAuthorization/preexisting-file-hashes.json: local baseline.
- Toàn bộ RbacUpgradeTrial/** và RbacUpgradeDeployment/**: giữ ngoài commit authorization. Nếu cần lưu procedural source/docs lâu dài, review/sanitize trong change riêng; không tự di chuyển/xóa.
- Mọi backup/export, generated binaries, bin/obj, temporary lock/certificate, cookies/session credentials, User Secrets/env exports.
- WAV đã ghi nhận: giữ nguyên local.

Các HTTP SQL preparation scripts hiện hard-code môi trường test đã được cấp phép. Không phải secret nhưng chưa portable; không áp vào appsettings/API Development connection string. Không commit deployment-state chỉ để thỏa dependency của helper.

## Thay đổi do Task07

Tạo đúng README.md, PRECOMMIT_AUDIT.md, DEV2_INTEGRATION_GUIDE.md và OUTSTANDING_WORK.md trong AuthorizationHandoff. Chạy validation refresh `HttpSqlIntegration/regressions.json`, main-after.json/preservation.json/test-final-state.json và ignored logs theo runner hiện có. Compile probe từ ví dụ guide nằm trong ignored obj, không phải source giao hàng. Không chỉnh C# ứng dụng hoặc sửa artifact deployment, không reset test DB, không tự sửa SQL privileges. Không Commit/Push/Merge/PR, không đổi branch hoặc Git index.

Trước commit: review exact staged diff, explicit allowlist, secret scan trên staged content và clean-checkout dependency check. Các local evidence không tự trở thành file nên commit chỉ vì tests Passed.
