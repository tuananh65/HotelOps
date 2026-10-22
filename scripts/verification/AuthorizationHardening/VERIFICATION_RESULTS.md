# Task08 — kết quả thực chạy

Ngày 22/10/2026 (Asia/Saigon). Phân biệt với kết quả Task06/07; không dùng kết quả cũ để điền Passed cho checks bị chặn hiện tại.

| Kiểm chứng | Task08 |
|---|---|
| dotnet build HotelOps.slnx | Passed, 0 warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit 0, solution chưa có test project để discover |
| CurrentUserAccessor | 14/14 Passed |
| DataScope --model-only | 7 model checks; không SQL |
| PermissionService default | 43/43 doubles/core/DI Passed |
| Authorization default | 61/61 policy/loopback với doubles Passed |
| ResourceAuthorization default | 49/49 resolver/core/HTTP với doubles Passed |
| Health + SELECT guards + policy cancellation | 21/21 Passed; database facade doubles, abort và timeout 5s thực tế |
| HTTP + SQL real session integration | **102/102 chạy lại Passed** trên HotelOps_Authorization_Test |
| DataScope live15 | BLOCKED, thiếu approved read-only config/principal |
| PermissionService --live50 | BLOCKED phần SQL, không tuyên bố 50 Passed |
| Authorization --live65 | BLOCKED phần SQL, không tuyên bố 65 Passed |
| ResourceAuthorization --live59 | BLOCKED phần SQL, không tuyên bố 59 Passed |
| Credential overprivilege rejection | Passed: preflight SELECT từ chối credential cũ, không chạy live assertions |
| SQL test setup reuse | Passed: receipt/marker/hash/version check, không schema/data writes |
| Source-only setup không deployment-state | Passed: đến SQL bằng config riêng, từ chối existing DB thiếu receipt, không writes |
| Fresh create từ clean checkout | Not Run trên DB mới; review/parse code và release source; không reset DB đã có |
| Main data preservation | One-time SELECT audit: fingerprint 72 bảng ứng dụng khớp baseline Task06 |
| Test cleanup | SELECT sau integration: 0 session chưa revoke, catalog 12/62/140 giữ nguyên |
| git diff --check / script syntax | Passed |

102 assertions chạy pipeline HTTPS có login cookie thật, SqlSessionService thật, session validation/touch/revocation, inactive/restricted accounts, OWN/branch/multi-role, CSRF, additional requirements, controlled SQL read error và cleanup. Không biến 21 health/doubles tests thành SQL-health outage certification. Không tạo lỗi trên database chính; exception response health kiểm bằng controller thật + facade double chứa sentinel metadata.

Current SQL engine: 16.0.1000.6 Express Edition 64-bit, test compatibility 160. Main credential INSERT/UPDATE/DELETE/ALTER vẫn 1; SQL least privilege **chưa applied**. Missing config và overprivilege đều được guard reject; account SELECT-only thật chưa có để chứng minh acceptance/live queries dưới quyền tối thiểu.

Không xóa assertion để giảm số lượng. Modes default/model-only và live được tách rõ; sau DBA setup cần chạy lại đầy đủ 15/50/65/59. DataScope thêm model-only chỉ tách 7 checks hiện có khỏi 8 SQL checks, không sửa expectations RBAC.

Lỗi compile AddUserSecrets overload và analyzer ASP0000 trong infrastructure mới đã sửa; restore project mới gặp NU1900 trong sandbox đã retry ngoài sandbox thành công, auditing vẫn bật. Lượt preflight ban đầu thiếu setup báo exception exit âm; nay caller báo BLOCKED/exit2 gọn, không stack trace cho expected setup rejection. Xem ignored obj/validation/results.json và logs của lượt cuối.

Local evidence không đưa vào Git: HttpSqlIntegration/results.json/test-final-state.json, AuthorizationHardening/obj/validation và bản sao source-only trong obj. Không chứa cookie/password/session credential trong kết quả chia sẻ. Không Commit/Push/Merge/PR, không đổi Login/User/Permission/schema trên HotelOpsDB.
