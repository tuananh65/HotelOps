# BE1 Authorization — final review và handoff

> Cập nhật Task08: [AuthorizationHardening](../AuthorizationHardening/README.md) là trạng thái remediation mới nhất. F02 health và F04 cancellation đã sửa; F03 dependency local đã loại bỏ khỏi setup. F01 chưa áp dụng SQL least privilege: live runners mới từ chối credential quyền rộng và không fallback main config. Phần còn lại giữ bằng chứng review Task07.

Ngày review: 22/10/2026 (Asia/Saigon). Branch: `feature/BE1-authorization`.

**Verdict: READY WITH NOTES cho commit review và tích hợp core; chưa phải production rollout toàn bộ authorization.** Không phát hiện authorization bypass tái hiện được trong core đã review. Các finding và giới hạn bên dưới vẫn phải được xử lý theo phạm vi triển khai.

## Trạng thái và bằng chứng

| Thành phần | Đánh giá |
|---|---|
| CurrentUserAccessor | Đọc identity/snapshot request-local đã xác thực; không cấp permission |
| DataScope | Mapping varchar(20), required, default BRANCH khớp SQL hiện tại |
| PermissionService | Kiểm tra user, role, assignment, branch access và scope theo từng grant path; không dùng role-name bypass |
| Policy/handler | Dynamic catalog, không cache policy; giữ default/fallback/external policies và requirements khác |
| Cleaning resolver | Chỉ CLEANING_EXECUTE + cleaning_task IN_PROGRESS + room active + đúng một assignee ACTIVE, chưa ended, không ở tương lai |
| API production | Chưa có Cleaning endpoint đủ điều kiện; chưa gắn policy nghiệp vụ |
| SQL integration | Task06 có executable, source và receipt 102 assertions; database test riêng |

Task07 đã chạy lại build solution (0 warning/error), dotnet test (exit 0 nhưng không discover test project), và các executable tuần tự: CurrentUserAccessor **14**, DataScope **15**, PermissionService --live **50**, Authorization --live **65**, ResourceAuthorization --live **59**, tất cả Passed. DI được kiểm tra trong source và verification hiện có. `git diff --check` Passed.

Hai helper C# hoàn chỉnh trong DEV2 guide đã được trích ra compile probe tham chiếu API hiện tại: build Passed, 0 warning/error. Probe ở ignored obj, không thêm project vào solution. 4 tài liệu handoff được kiểm tra sensitive literal và whitespace. Đối chiếu preservation tiếp tục khớp 250 file baseline và 72 bảng ứng dụng chính; SELECT test-state vẫn có 0 session chưa revoke.

**102/102 HTTPS + SQL assertions là bằng chứng Task06 đã đối chiếu lại với source/results.json, không phải lượt HTTP SQL chạy mới trong Task07.** Task07 không chạy login, tạo account/session, provisioning, reset hoặc deployment. SQL trực tiếp và live regression trong Task07 chỉ đọc database chính. Run-Regressions.ps1 refresh `HttpSqlIntegration/regressions.json` và các log local; không sửa source cũ.

65 so với 61: `Authorization/Program.cs` mặc định có 61 checks; --live thêm 4 checks về database đích, code có/không tồn tại và ordinal case matching. Không có assertion bị xóa.

## Security findings

### F01 — Medium: credential verification có quyền ghi database chính

Vị trí: các live runners lấy `ConnectionStrings:HotelOpsDb` từ User Secrets local; `HttpSqlIntegration/Prepare-Secrets.ps1` cũng dẫn xuất test connection từ kết nối SQL local. SELECT `HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE',...)` trên HotelOpsDB trả **1** cho INSERT, UPDATE, DELETE, ALTER. Không đưa identity SQL hay connection string vào báo cáo.

Điều kiện/rủi ro: runner hiện tại chỉ SELECT theo code, nhưng một lỗi hoặc thay đổi script sau này có thể ghi bằng credential này. TestDatabase interceptor của HTTP SQL runner kiểm tra đích trước từng command, song đó là guard bằng code, không phải phân quyền SQL account. Không phát hiện write ngoài ý muốn trong lượt review; không được gọi account hiện tại là read-only.

Khắc phục đề xuất: credential SELECT-only cho live read verification; credential test riêng chỉ có quyền trên integration DB; quyền tạo database/provisioning tách riêng khỏi runtime test. Cần người quản trị phê duyệt, không sửa SQL permissions trong Task07.

### F02 — Medium khi endpoint được public: health diagnostic có thể lộ thông tin

Vị trí `src/HotelOps.Api/Controllers/HealthController.cs:18` và `:48`: GET /api/health/database không có Authorize, trả tên database khi thành công và `ex.Message` trong catch. Program không cấu hình fallback yêu cầu authentication.

Tái hiện: gọi route không có cookie; khi CanConnectAsync trả true sẽ thấy tên DB. Nếu provider/configuration phát sinh exception đi vào catch, message provider được trả ra client. Nhiều lỗi kết nối được CanConnectAsync chuyển thành false; không khẳng định mọi lỗi đều vào catch. Nhánh exception này được review tĩnh, chưa fault-inject trên database chính.

Ảnh hưởng: tiết lộ metadata/provider diagnostics tùy exception, không phải bằng chứng lộ password thực tế. Đây là endpoint baseline có sẵn, không tự gán permission nghiệp vụ hoặc sửa shared controller trong task handoff. Trước public deployment cần quyết định tách liveness công khai khỏi readiness/diagnostics nội bộ và chuẩn hóa error response.

### F03 — Medium cho khả năng tái lập handoff: test preparation phụ thuộc artifact local

Vị trí `HttpSqlIntegration/Prepare-Database.ps1:2`, `Verify-Preservation.ps1:2`, `RbacUpgradeDeployment/Task03B/Inspect-Task03B.ps1:4`. Helper DefinitionsOnly vẫn đọc deployment-state.json trước khi return. Source setup HTTP SQL phụ thuộc cả package Task03A/03B và deployment-state local; receipt, fixture IDs và server cũng gắn môi trường hiện tại.

Tái hiện ở clean checkout chỉ có source core + tests: thiếu deployment-state/receipt sẽ fail preparation, không thể hứa DEV2 chạy full SQL suite ngay. Không nên commit deployment-state chứa thông tin máy/backup để chữa lỗi dependency.

Đề xuất tách helper dùng chung không đọc deployment state, cấu hình integration riêng và luồng receipt an toàn/portable ở task chuẩn hóa sau. Không refactor package đã dùng triển khai trong Task07.

### F04 — Low: policy catalog không có request cancellation

Vị trí `PermissionPolicyProvider.cs:41`: catalog lookup dùng CancellationToken.None vì GetPolicyAsync không nhận request token. Failures không grant; query có thể tiếp tục sau client disconnect. Cân nhắc timeout/budget và instrumentation khi chuẩn hóa; chưa thay contract.

### Ghi chú guard test

`HttpSqlIntegration/TestDatabase.cs:30-31` đưa createStamp từ receipt local vào SQL literal. Receipt hiện là artifact tin cậy của package, không phải input HTTP; không xem đây là production injection. Nên parse/format nghiêm ngặt hoặc parameterize metadata trước khi hỗ trợ receipt từ môi trường khác. Không chỉnh sửa hoặc sử dụng receipt không tin cậy với credential có quyền rộng.

## Các kiểm tra bypass

| Kiểm tra | Kết luận review |
|---|---|
| Role claim giả | Cookie events dựng lại principal chỉ với fields hiểu được; core dùng SQL grants |
| Resource chưa xác minh | Resource check cần resolver; null/unknown type/missing resource không Granted, kể cả ALL |
| Ghép role A/B | Join assignment.RoleId = grant.RoleId; đánh giá từng path; không union grant với branch của role khác |
| ALL | Null-branch assignment có thể global; branch-bound ALL vẫn cùng branch, active access; resource rule vẫn bắt buộc |
| Inactive/revoked | SQL session kiểm tra revoked/expiry/user active; permission query kiểm tra lại user/role/assignment |
| Failure | DB/timeout thành Unavailable, handler chỉ Succeed nếu Granted; exception khác lên generic error, không fallback allow |
| Requirements khác | Succeed chỉ requirement đang xét; không thay authorization result để bỏ requirements khác |
| Cache | Không cache user grants; provider AllowsCachingPolicies=false; không bảo đảm atomic giữa nhiều lần SELECT |
| IN_PROGRESS | Resolver từ chối trạng thái khác, former/ended/future/ambiguous assignments |

Health và WeatherForecast là diagnostics/template công khai, không phải Cleaning workflows. Auth login/csrf/logout có AllowAnonymous có chủ đích; logout vẫn qua CSRF. Session/change-password có cookie authorization. Không thấy endpoint nghiệp vụ Cleaning bị bỏ sót, vì chưa có endpoint đó.

Production cookie infrastructure hiện chỉ cho Windows Development; persistent encrypted key ring cho production chưa được cấu hình. Restricted middleware, handler và service đều giữ giới hạn; CSRF chạy trước authorization; session touch revalidate trước endpoint nhưng không giải quyết race giữa authorization và business write.

## Tài liệu bàn giao

- [PRECOMMIT_AUDIT.md](PRECOMMIT_AUDIT.md): phân loại toàn bộ thay đổi, commit allowlist và local-only artifacts.
- [DEV2_INTEGRATION_GUIDE.md](DEV2_INTEGRATION_GUIDE.md): contract, ví dụ tương thích source và test checklist.
- [OUTSTANDING_WORK.md](OUTSTANDING_WORK.md): owner/phụ thuộc và tiêu chí hoàn tất.

Verify-Preservation.ps1 đã chạy SELECT để đối chiếu dữ liệu chính và session test, refresh main-after.json/preservation.json/test-final-state.json local. Không đổi source, schema, matrix, SQL permissions, branch hoặc Git index. Không Commit/Push/Merge/PR. WAV untracked được giữ nguyên.
