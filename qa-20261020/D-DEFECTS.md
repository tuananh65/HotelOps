# D. Defect Report

**12 nhóm phát hiện:** 3 High, 5 Medium, 4 Low; không có Critical được chứng minh trong phạm vi đã chạy. Có 20 testcase FAIL, không đồng nghĩa 20 root cause khác nhau. DEF-11 là supplemental UI gap, không tự gán thành yêu cầu SRS mới. Chưa chỉnh sửa source.

| ID | Severity | Phát hiện | Tests |
| --- | --- | --- | --- |
| DEF-01 | High | Không tính effective permissions/scope và SYS_ADMIN không có navigation | LOGIN-22, ROLE-01, ROLE-03 |
| DEF-02 | High | Logout session expired/revoked trả CSRF 400 thay vì idempotent success | LOGOUT-02, LOGOUT-03 |
| DEF-03 | High | Audit Logout/Session thiếu actor_user_id | LOGOUT-06 |
| DEF-04 | Medium | Inactive + đúng mật khẩu không tăng failed-attempt counter theo UC exception | LOGIN-06 |
| DEF-05 | Medium | MSG12 thiếu thời điểm mở khóa | LOGIN-19 |
| DEF-06 | Low | MSG13 khác nội dung chuẩn SRS | LOGIN-20 |
| DEF-07 | Medium | MSG03 thiếu field reason và danh sách password policy chưa đạt | LOGIN-21, CHANGE-13, CHANGE-14 |
| DEF-08 | Medium | Session revoked chỉ redirect, thiếu MSG01 dialog | SESSION-09 |
| DEF-09 | Low | Đổi mật khẩu thành công dùng thông báo khác MSG08 | CHANGE-16 |
| DEF-10 | Low | Forgot Password sai câu MSG14 và vị trí inline | FORGOT-01, FORGOT-02, FORGOT-03, FORGOT-04 |
| DEF-11 | Low | Profile email rỗng dù SQL có giá trị — supplemental UI gap | PROFILE-04 |
| DEF-12 | Medium | Restricted session có hard cap 15 phút ngoài SYS-PAR-06 30 phút | POLICY-02, LOGIN-15 |

## DEF-01 — Không tính effective permissions/scope và SYS_ADMIN không có navigation

- **Severity:** High
- **UC/GBR/MSG:** UC-001 Normal 7/9; GBR-002; UC-011 chưa triển khai
- **Source:** `src/HotelOps.Api/Security/HotelOpsCookieInfrastructure.cs:51`, `src/api/authAdapter.ts:28`, `src/api/authAdapter.ts:33`, `src/screens/auth/SignInScreen.tsx:26`
- **Cause:** SessionDto chỉ có id/username/name; adapter tạo roles/grants/scopes rỗng; AddAuthorization chưa đăng ký role policy.
- **Reproduce:** Gán SYS_ADMIN hiện có cho QA user bằng user_roles; login → profile; aside nav không có link, response không có grants.
- **Evidence:** LOGIN-22, ROLE-01, ROLE-03 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Thiết kế DTO/session effective grants và truy vấn role/scope theo SRS; enforce server policies cho API nghiệp vụ khi triển khai; không hardcode quyền tại FE.

## DEF-02 — Logout session expired/revoked trả CSRF 400 thay vì idempotent success

- **Severity:** High
- **UC/GBR/MSG:** UC-002 exception Step 2.1; GBR-021
- **Source:** `src/HotelOps.Api/Security/SessionAntiforgeryAdditionalDataProvider.cs:18`, `src/HotelOps.Api/Middleware/ApiCsrfMiddleware.cs:18`, `src/HotelOps.Api/Program.cs:49`
- **Cause:** SQL cookie validation loại principal khi session không hợp lệ; CSRF token trước đó gắn authenticated identity/session nhưng expected binding đổi thành anonymous. CSRF middleware trả 400 trước Logout controller.
- **Reproduce:** Login lấy token/cookie; expire hoặc revoke chỉ session QA trong SQL; POST logout với token đang lưu → 400 CSRF_VALIDATION_FAILED.
- **Evidence:** LOGOUT-02, LOGOUT-03 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Thiết kế lại flow logout idempotent cho session không còn hợp lệ mà vẫn giữ bảo vệ CSRF; có thể bootstrap token anonymous rồi logout trong FE, kèm test expired/revoked. Không bỏ CSRF chung.

## DEF-03 — Audit Logout/Session thiếu actor_user_id

- **Severity:** High
- **UC/GBR/MSG:** GBR-008; UC-002 Normal 3
- **Source:** `src/HotelOps.Api/Services/Authentication/SqlSessionService.cs:117`, `src/HotelOps.Api/Services/Authentication/SqlSessionService.cs:123`
- **Cause:** Helper Audit tạo row nhưng không set ActorUserId; cột nullable nên commit vẫn thành công.
- **Reproduce:** Login → logout bình thường; SELECT audit event SESSION_REVOKE: action LOGOUT, actor_user_id NULL.
- **Evidence:** LOGOUT-06 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Gán actor từ danh tính/session đã xác minh trong helper/caller; kiểm tra cả SESSION_CREATE/REVOKE_ALL, không ghi credential values.

## DEF-04 — Inactive + đúng mật khẩu không tăng failed-attempt counter theo UC exception

- **Severity:** Medium
- **UC/GBR/MSG:** UC-001 Step 5.1; GBR-019/020
- **Source:** `src/HotelOps.Api/Services/Authentication/CredentialLockoutPolicy.cs:26`
- **Cause:** passwordVerified=true nhưng eligible=false trả InvalidCredentials với failedCount giữ nguyên.
- **Reproduce:** QA account INACTIVE, counter=0; gửi đúng mật khẩu hai lần; 401 generic nhưng counter vẫn 0.
- **Evidence:** LOGIN-06 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Thống nhất chuyển trạng thái thất bại cho account không eligible theo đúng SRS; giữ generic response và không gia hạn lock sai.

## DEF-05 — MSG12 thiếu thời điểm mở khóa

- **Severity:** Medium
- **UC/GBR/MSG:** MSG12; UC-001 Step 6.1; GBR-020
- **Source:** `src/api/http.ts:156`, `src/HotelOps.Api/Controllers/AuthController.cs:27`
- **Cause:** FE ưu tiên authMessages hardcoded, không sử dụng parameters.lockedUntil cho MSG12.
- **Reproduce:** Khóa QA user rồi nhập đúng password; UI hiển thị This account is temporarily locked. Try again later.
- **Evidence:** LOGIN-19 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Map code ACCOUNT_LOCKED sang MSG12, format lockedUntil theo timezone ứng dụng và giữ dạng inline.

## DEF-06 — MSG13 khác nội dung chuẩn SRS

- **Severity:** Low
- **UC/GBR/MSG:** MSG13; UC-001 Step 7.2
- **Source:** `src/api/http.ts:157`
- **Cause:** Hardcoded auth message khác catalog SRS.
- **Reproduce:** Login bằng temporary credential đã hết hạn; UI không hiển thị câu Your temporary password has expired. Please contact the Administrator.
- **Evidence:** LOGIN-20 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Dùng catalog message chuẩn; kiểm tra i18n tương đương sau khi bản tiếng Anh đúng.

## DEF-07 — MSG03 thiếu field reason và danh sách password policy chưa đạt

- **Severity:** Medium
- **UC/GBR/MSG:** MSG03; UC-001 Step 4.1; UC-003 Step 4.1/5.1; GBR-003/022
- **Source:** `src/HotelOps.Api/Security/AuthenticationApiBehavior.cs:22`, `src/HotelOps.Api/Controllers/AuthController.cs:74`, `src/HotelOps.Api/Controllers/AuthController.cs:76`, `src/ui.tsx:776`
- **Cause:** Backend trả generic ProblemDetails không có errors theo field; policy failure chỉ có code; frontend required message không theo template MSG03.
- **Reproduce:** Submit blank login, change với sai current hoặc password short; thấy generic form error và không có policy reasons gắn field.
- **Evidence:** LOGIN-21, CHANGE-13, CHANGE-14 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Trả errors DTO có tên field/reason an toàn, không echo secret; FE render MSG03 dưới trường; báo từng unmet complexity item.

## DEF-08 — Session revoked chỉ redirect, thiếu MSG01 dialog

- **Severity:** Medium
- **UC/GBR/MSG:** MSG01; GBR-001/021
- **Source:** `src/services.ts:187`, `src/components/ApiGate.tsx:94`
- **Cause:** 401 gọi clearSession, SessionGate Navigate về /login, không phát thông báo session expired.
- **Reproduce:** Login UI, revoke session QA ở SQL, reload profile; chuyển login nhưng không có MSG01.
- **Evidence:** SESSION-09 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Giữ lý do expire/revoke và hiển thị dialog MSG01 khi redirect do mất phiên; phân biệt truy cập anonymous lần đầu.

## DEF-09 — Đổi mật khẩu thành công dùng thông báo khác MSG08

- **Severity:** Low
- **UC/GBR/MSG:** MSG08; UC-003 Normal 8
- **Source:** `src/screens/auth/ChangePasswordScreen.tsx:40`
- **Cause:** Toast hardcoded Password changed.
- **Reproduce:** Đổi mật khẩu thành công qua form; toast Password changed. thay vì {Object} has been saved successfully.
- **Evidence:** CHANGE-16 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Dùng MSG08 với object phù hợp đã thống nhất SRS.

## DEF-10 — Forgot Password sai câu MSG14 và vị trí inline

- **Severity:** Low
- **UC/GBR/MSG:** MSG14; GBR-024; UC-001 Step 2.1
- **Source:** `src/screens/auth/ForgotPasswordScreen.tsx:20`
- **Cause:** Static paragraph/toast khác exact MSG14, submit không tạo inline result theo SRS.
- **Reproduce:** Gửi username/email tồn tại/không tồn tại/inactive; tất cả nhận cùng toast an toàn nhưng sai nội dung/vị trí.
- **Evidence:** FORGOT-01, FORGOT-02, FORGOT-03, FORGOT-04 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Hiển thị inline đúng MSG14 và giữ độc lập với account existence; không thêm reset token/email API ngoài SRS.

## DEF-11 — Profile email rỗng dù SQL có giá trị — supplemental UI gap

- **Severity:** Low
- **UC/GBR/MSG:** Không khẳng định vi phạm UC-011; profile hỗ trợ UC-001, email là field UI hiện có
- **Source:** `src/api/authAdapter.ts:27`
- **Cause:** Backend user DTO thiếu email; adapter điền chuỗi rỗng.
- **Reproduce:** QA user có email trong SQL; login vào profile, nhãn Email rỗng.
- **Evidence:** PROFILE-04 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Nếu chấp thuận bổ sung profile contract, trả field thật an toàn; không tự gán dữ liệu giả hoặc coi profile là access review UC-011.

## DEF-12 — Restricted session có hard cap 15 phút ngoài SYS-PAR-06 30 phút

- **Severity:** Medium
- **UC/GBR/MSG:** GBR-021/023; SYS-PAR-06
- **Source:** `src/HotelOps.Api/Services/Authentication/SqlSessionPolicy.cs:9`, `src/HotelOps.Api/Services/Authentication/SqlSessionPolicy.cs:15`
- **Cause:** RestrictedLifetime=15m được dùng cả idle/absolute cap; DOCX không nêu ngoại lệ 15m cho restricted session.
- **Reproduce:** Temporary login tạo SQL session expiry ~created+15m; service probe xác nhận constant15 khác tham số30. Chưa chờ đủ15m thời gian thực.
- **Evidence:** POLICY-02, LOGIN-15 trong all-test-cases.json; ảnh UI theo ID nếu có.
- **Đề xuất, chưa áp dụng:** Thống nhất timeout với SRS hoặc xin phê duyệt ngoại lệ bảo mật trước khi thay policy; normal absolute cap12h cũng cần ghi rõ yêu cầu.

## Giới hạn bảo mật

Không phát hiện secret values trong response auth/audit/runtime log của ca đã chạy. CSRF request token được trả về có chủ đích để chống CSRF, không phải raw authentication/session credential; không coi việc trả CSRF token là rò rỉ credential. Chưa thực hiện timing attack, pentest, concurrency stress hoặc commit-failure injection. Endpoint quản trị trả 404 không phải bằng chứng RBAC an toàn; service reset không có public endpoint nên cũng không phải remote privilege-escalation đã chứng minh.
