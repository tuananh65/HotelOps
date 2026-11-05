# Task 06 — báo cáo

## A. Context Verification

Đã kiểm tra FE-01 cookie/events, SQL validator/session/login/credential workflow, CurrentUserAccessor, permission data source/service, dynamic policies/handler, CleaningTask resolver và pipeline. Controller hiện có: Auth, Csrf, Health và WeatherForecast. Chưa có endpoint Cleaning nghiệp vụ tương ứng `CLEANING_EXECUTE` trên `cleaning_task` trạng thái `IN_PROGRESS`.

Không sửa controller, Program.cs, entities, DbContext hoặc authentication/authorization core trong Task06. Mọi file thêm mới thuộc `scripts/verification/HttpSqlIntegration/`.

## B. Test Coverage Audit

Không có assertion bị xóa. Authorization mặc định chạy 61 checks; `--live` thêm đúng 4 SQL checks: database đích HotelOpsDB, permission ROOM_VIEW tồn tại, permission không tồn tại bị từ chối, và mã lowercase bị từ chối theo so sánh ordinal dù SQL collation không phân biệt hoa thường. Task04 dùng `--live`, Task05 dùng mặc định. Task06 đã chạy lại đầy đủ **65/65**.

## C. HTTP Authentication Integration

Database riêng `HotelOps_Authorization_Test`, DB_ID 29, được tạo bằng Full Schema và RBAC Seed chuẩn. Credentials riêng nằm trong Integration User Secrets. Có 8 tài khoản `it06.*`, 2 branch và 4 cleaning tasks; không sao chép credentials từ database chính.

SELECT cuối xác nhận 12 roles (10 active), 62 permissions, 140 mappings; ALL=8, BRANCH=81, OWN=51. Lịch sử có 32 sessions qua các lần chạy, **0 session chưa thu hồi**. Evidence: `test-final-state.json`.

Lần chạy cuối **102/102 assertions** dùng HTTPS, authentication cookie và SQL-backed session thật:

- Anonymous, invalid/expired cookie: 401.
- Login bằng credential thật, SQL commit và cookie Secure/HttpOnly.
- Validated identity: 200; SQL last_used_at và idle expiry tăng sau session touch.
- Logout thật thu hồi session; replay cookie cũ: 401.
- Chuyển fixture account sang INACTIVE làm cookie/session thật bị từ chối: 401.
- Restricted marker đúng; self-session: 200; protected identity/business probes: 403, mã PASSWORD_CHANGE_REQUIRED.
- OWN current assignee: 200; former reassigned assignee: 403.
- Resource owner thiếu permission: 403; role/branch grant mixing: 403.
- OWN hợp lệ ở branch B: 200; caller branch A truy cập task B kèm thông tin owner/branch giả: 403.
- Missing resource và additional authorization requirement: 403.
- CSRF thiếu/sai: 400; CSRF hợp lệ trên protected POST probe: 204.
- SQL resource read phát sinh lỗi có kiểm soát: 403, không lộ SQL exception/stack/connection string.
- Các session do lần chạy tạo đều đã được revoke qua FE-01; inactive fixture trở về INACTIVE.

Expired cookie được tạo bằng cách thay expiry rồi protect lại ticket đã lấy từ cookie login thật trong host test. Không thay validator hoặc tạo SQL session giả.

## D. API Protection

**BLOCKED cho endpoint nghiệp vụ production** do chưa có Cleaning API phù hợp. Không tạo API Cleaning mới, không áp adapter IN_PROGRESS cho Start/Resume.

Trong host test: policy `CLEANING_EXECUTE` nhận PermissionResource từ route ID trước authorization. ID chỉ dùng để tìm task; EF resolver lấy branch từ room và OWN từ current assignment thật. Client query không được dùng làm bằng chứng branch/ownership. Không có route probe nào được thêm vào production Program.

## E. Security Verification

Đã kiểm chứng 401/403/200, branch/OWN, role mixing, Restricted Session, antiforgery, session validation/touch/revoke và additional requirements. SQL exception injection chạy ở SQL engine thật; không thử outage, deadlock hay failover toàn server.

Chưa có workflow ghi Cleaning để kiểm chứng TOCTOU. Authorization read không bảo đảm assignment/resource vẫn giữ nguyên khi một workflow ghi thực thi sau đó; endpoint tương lai phải revalidate trong transaction hoặc có concurrency control phù hợp.

Guard ngăn ghi nhầm database; file lock cô lập runner trong package; cleanup chỉ tác động session của chính lần chạy. File lock không khóa ứng dụng độc lập khác dùng cùng database test. Không reset/restore đè database có sẵn.

## F. Regression Results

| Kiểm tra | Kết quả |
|---|---|
| dotnet build HotelOps.slnx | Passed, 0 warnings / 0 errors |
| Build HttpSqlIntegration.csproj | Passed, 0 warnings / 0 errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0; solution chưa có test project |
| CurrentUserAccessor | 14/14 |
| DataScope, SQL read-only | 15/15 |
| PermissionService --live | 50/50 |
| Authorization --live | 65/65 |
| ResourceAuthorization --live | 59/59 |
| HTTPS + real SQL sessions | 102/102 |
| git diff --check | Passed |

Các regression SQL chạy tuần tự, không timeout. Các bộ cũ trộn unit/doubles/loopback và SELECT live như mô tả trong runner; không gộp chúng thành kiểm thử session SQL thật. Bộ mới độc lập chứng minh integration session SQL thật.

Lỗi preparation/build ban đầu của project mới thiếu assets được xử lý bằng restore. Đã sửa đường dẫn receipt và cách nạp certificate cho Schannel trong infrastructure test. NU1900 trong sandbox/cache đã được kiểm tra bằng restore --force ngoài sandbox cho các verification projects; không tắt security auditing. Không cần sửa FE-01/core.

## G. Outstanding Work

Chờ endpoint/workflow Cleaning phù hợp và các adapter còn thiếu được xác nhận theo business rules. Chưa xác minh workflow ghi hoặc concurrency nghiệp vụ, deployment production, browser end-to-end hay outage SQL toàn server. Không có xUnit/MSTest project mới; verification console được chạy trực tiếp, không được tuyên bố là test discovery của dotnet test.

## H. Git Status và bảo toàn

File thêm mới: project test; Program.cs; TestDatabase.cs; Fixtures.cs; Prepare-Database.ps1; Prepare-Secrets.ps1; Run-Regressions.ps1; Verify-Preservation.ps1; README.md; RESULTS.md; các receipt/evidence JSON trong cùng folder.

250 file có sẵn đã được đối chiếu SHA-256 và giữ nguyên, gồm các thay đổi chưa commit từ các Task trước. Count/hash dữ liệu của cả 72 bảng ứng dụng HotelOpsDB trước và sau không đổi. Connection string Development chính được giữ nguyên. Không Commit, Push, Merge, tạo PR hoặc chuyển branch.

## I. Final Verdict

**PASS WITH NOTES** cho phạm vi có thể triển khai và integration test. API protection production vẫn **BLOCKED do chưa có endpoint Cleaning phù hợp**; không coi kết quả probe là xác nhận workflow nghiệp vụ hoàn chỉnh.
