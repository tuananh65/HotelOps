# Task09A — pre-commit packaging và selective staging

Ngày audit: 22/10/2026 (Asia/Saigon). Branch: feature/BE1-authorization. **READY WITH NOTES** để DEV1 review commit; Task09A chỉ stage, không Commit/Push/Merge/PR hoặc thay đổi database.

## Package và allowlist

[STAGING_ALLOWLIST.txt](STAGING_ALLOWLIST.txt) chứa chính xác 55 đường dẫn được chọn, mỗi dòng một path repository-relative, không dùng wildcard khi git add. Index ban đầu trống. Phân loại:

| Nhóm | Số file | Nội dung |
|---|---:|---|
| Application source | 15 | 4 tracked changes Health/DbContext/RolePermission/Program và 11 authorization core/resolver files |
| Verification source/config/scripts | 25 | 5 core packages, shared read-only guard, SQL integration source/portable setup/scoped ignore, hardening test source và provisioning draft |
| Documentation | 15 | 5 verification README, 4 handoff docs, 4 hardening docs, 2 packaging files này |
| Tổng | **55** | Chỉ các file đã audit; không stage deployment history hoặc database release đã commit |

Initial inspection: 4 tracked modifications + 152 untracked files (156 paths), chưa có staged changes. Sau khi thêm 2 tài liệu packaging, dự kiến index có 55 files (4 modified + 51 added), 103 untracked paths có sẵn được giữ ngoài index, không có tracked change ngoài scope cần stage. Các ignored local evidence/build outputs không tính vào số untracked trên.

Commit Database Release đã có trong history; 4 release SQL scripts và README đều tracked/không có diff, không stage lại. Không đổi source, .gitignore có sẵn, cấu hình hay artifact hiện hữu trong Task09A. Build chỉ cập nhật generated outputs; validation/audit mới ở obj/task09a (ignored), không overwrite evidence Task08.

## Files giữ local

- 95 files trong RbacUpgradeTrial và RbacUpgradeDeployment: procedural scripts/history, deployment/recovery evidence, metadata/backup paths và snapshots; không thuộc commit Authorization Foundation này.
- 5 RESULTS.md lịch sử của DataScope, PermissionService, Authorization, ResourceAuthorization và HttpSqlIntegration: thiếu trạng thái hardening mới nhất hoặc gắn evidence local; báo cáo Task08 hiện tại đã được chọn thay thế.
- ResourceAuthorization/preexisting-file-hashes.json: fingerprint runtime local.
- HttpSqlIntegration/README.md: giữ local vì có link báo cáo Task06 đã loại; hướng dẫn reproducible đầy đủ ở [TEST_ENVIRONMENT_SETUP.md](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md). Không sửa file gốc chỉ để đóng gói.
- WAV untracked ngoài scope: giữ nguyên.
- Receipts, fixture IDs, session/history counts JSON, snapshots main-before/after, logs, bin/obj, source-only scratch checkout và User Secrets: không stage. Scoped ignore integration đã có từ Task08; không sửa gitignore diện rộng.

Không xóa/move/rewrite các files bị loại. Không in credential hoặc private database rows vào báo cáo.

## Audit nội dung

Đã review tracked diff, source core/verification, setup scripts và DEV2 guide. Scan candidates cho plaintext password/API key/private key/session material, credential-bearing connection string, absolute machine paths và runtime metadata. Match path-pattern là phần http/https URL, không phải absolute Windows path; password assignment trong HTTP runner đọc từ secret configuration, không có literal password. Credentials/session tickets test được sinh runtime; không stage cookie/token/journal values.

Provisioning SQL được chọn chỉ là **REVIEW ONLY**, chứa placeholder existing login, không password. Không được import/thực thi bởi build, clone hoặc test runner. Script có chủ đích thay DB permissions khi DBA duyệt/chạy thủ công; việc stage file không phải phê duyệt execute. SQL Least Privilege vẫn **OPEN**; Task09A không tạo principal/grant quyền hay kiểm thử bằng credential rộng.

Integration runtime chỉ cho HotelOps_Authorization_Test, kiểm tra connection target và receipt/server/DB ID/create date/provenance trước EF commands. Setup không đọc main DB configuration/deployment-state; missing DB cần explicit -CreateIfMissing; existing unknown DB dừng, không reset/reseed. Master-context CREATE chỉ dành tên test DB được kiểm tra; không chạy setup hoặc HTTP SQL runner trong Task09A.

Live read runners dùng cấu hình read-only riêng, preflight quyền và SELECT guards; không fallback main application credential để ép Passed. Scripts deployment có stage ghi main DB bị loại hoàn toàn.

DEV2 guide dùng types/signatures hiện có, không role-name bypass cho SYS_ADMIN/OWNER. Có grant không đồng nghĩa quyền mọi resource; ALL không bỏ qua resolver, BRANCH không chứng minh OWN. Typed resource binding phải trước authorization; không override resolver Cleaning bằng last-registration-wins. Cleaning adapter chỉ hỗ trợ execution IN_PROGRESS, không Start/Resume. Production controller rollout, SoD/concurrency và Legacy Department Retirement còn outstanding.

## Validation Task09A — không SQL

| Kiểm tra | Kết quả |
|---|---|
| dotnet build HotelOps.slnx | Passed, 0 warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; chưa discover framework test projects |
| CurrentUserAccessor | 14/14 |
| DataScope --model-only | 7/7, không mở connection |
| PermissionService default | 43/43 doubles/core/DI |
| Authorization default | 61/61 loopback/doubles |
| ResourceAuthorization default | 49/49 loopback/doubles |
| Health/guards/cancellation | 21/21, health facade doubles, timeout/abort |
| Live SELECT 15/50/65/59 | Không chạy; vẫn blocked theo preflight Task08 do thiếu read-only principal |
| HTTPS + real SQL 102/102 | Đối chiếu source và báo cáo Task08; **không chạy lại Task09A** vì không được thay DB |

Chênh lệch số checks là tách modes, không xóa assertions: DataScope 7 model + 8 SQL; PermissionService 43 + 7 SQL; Authorization 61 + 4 SQL; ResourceAuthorization 49 + 10 SQL. Build/test/console chạy tuần tự, không provisioning, no --live, no --readonly-probe, không sử dụng SQL credentials. Không tắt NuGet auditing.

Audit cuối phải so sánh staged paths với allowlist, hash candidate với bản đã review, scan staged content, chạy git diff --cached --check và xem --name-status/--stat/--numstat. Con số additions/deletions cuối lấy trực tiếp từ index trong báo cáo kết thúc; không suy ra từ working tree hoặc báo cáo cũ.

## Review entry points và rủi ro

- [Authorization handoff](../AuthorizationHandoff/README.md), [DEV2 guide](../AuthorizationHandoff/DEV2_INTEGRATION_GUIDE.md), [Outstanding work](../AuthorizationHandoff/OUTSTANDING_WORK.md).
- [Hardening findings](../AuthorizationHardening/SECURITY_FINDINGS.md), [Task08 verification](../AuthorizationHardening/VERIFICATION_RESULTS.md), [Test setup](../AuthorizationHardening/TEST_ENVIRONMENT_SETUP.md).

SQL Least Privilege vẫn OPEN/MEDIUM, chưa có principal SELECT-only thật được cấp và chạy positive/live assertions. External health consumers và fresh provisioning trên máy DEV2 chưa xác minh. Không coi staged foundation là production authorization rollout hoàn chỉnh hoặc phê duyệt DBA script.
