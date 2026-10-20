# B. Test Case Report

Lượt kết luận: **114 ca mới đã chạy** (93 HTTP/UI/SQL, 21 service/helper), 94 PASS và 20 FAIL. 10 ca bổ sung chưa chạy: 3 BLOCKED, 7 NOT TESTED. 37 test Node cũ báo riêng, không trộn hoặc đếm hai lần.

Nguồn chi tiết máy đọc: [all-test-cases.json](all-test-cases.json), gồm toàn bộ before/after SQL, request status/response đã redacted và quan sát UI. SQL snapshot chỉ chứa fingerprint của credential để so sánh, không chứa password hash thực hoặc token. Với API-only/service-only, lớp UI được ghi NOT TESTED; không suy diễn UI PASS. Các service/helper không có endpoint không được tính là một UC admin đã hoàn thành.

| ID | Feature | UC | Status | Mục tiêu |
| --- | --- | --- | --- | --- |
| LOGIN-01 | Login | UC-001 | PASS | Username UI login with SQL user |
| LOGIN-02 | Login | UC-001 | PASS | Email UI login |
| LOGIN-03 | Login | UC-001 | PASS | Wrong password |
| LOGIN-04 | Login | UC-001 | PASS | Unknown account matches invalid credentials response |
| LOGIN-05 | Login | UC-001 | PASS | Inactive user correct password |
| LOGIN-06 | Login | UC-001 | FAIL | Inactive attempted login increments counter per UC exception |
| LOGIN-07 | Login | UC-001 | PASS | Missing login field 07 |
| LOGIN-08 | Login | UC-001 | PASS | Missing login field 08 |
| LOGIN-09 | Login | UC-001 | PASS | Missing login field 09 |
| LOGIN-10 | Login | UC-001 | PASS | Five direct API failures lock stored account |
| LOGIN-11 | Login | UC-001 | PASS | Correct password while locked |
| LOGIN-12 | Login | UC-001 | PASS | Wrong password while locked |
| LOGIN-13 | Login | UC-001 | PASS | Correct password after lock deadline |
| LOGIN-14 | Login | UC-001 | PASS | Success resets ordinary failure counter |
| LOGIN-15 | Login | UC-001 | PASS | Temporary password login redirects to forced change |
| LOGIN-16 | Login | UC-001 | PASS | Expired temporary password |
| LOGIN-17 | Login | UC-001 | PASS | Successful login audit actor/time |
| LOGIN-18 | Login | UC-001 | PASS | Wrong-password UI message exact MSG11 |
| LOGIN-19 | Login | UC-001 | FAIL | Locked UI message includes unlock time and exact MSG12 |
| LOGIN-20 | Login | UC-001 | FAIL | Expired temporary UI exact MSG13 |
| LOGIN-21 | Login | UC-001 | FAIL | Blank form identifies required fields using MSG03 |
| LOGIN-22 | Login | UC-001 | FAIL | Login calculates role/scoped permissions |
| LOGIN-23 | Login | UC-001 | PASS | Whitespace around identifier |
| SESSION-01 | Session | UC-001 | PASS | Anonymous protected API |
| SESSION-02 | Session | UC-001 | PASS | Normal session idle deadline |
| SESSION-03 | Session | UC-001 | PASS | Valid request advances last-used and expiry |
| SESSION-04 | Session | UC-001 | PASS | Expired SQL session rejected |
| SESSION-05 | Session | UC-002 | PASS | Revoked SQL session rejected |
| SESSION-06 | Session | UC-001 | PASS | Tampered authentication cookie |
| SESSION-07 | Session | UC-001 | PASS | Cookie security attributes |
| SESSION-08 | Session | UC-001 | PASS | Restricted session blocks other API |
| SESSION-09 | Session | UC-001 | FAIL | Revoked session UI returns login with exact MSG01 |
| SESSION-10 | Session | UC-001 | PASS | Account deactivation invalidates existing cookie |
| CSRF-01 | CSRF | UC-001 | PASS | Anonymous CSRF bootstrap |
| CSRF-02 | CSRF | UC-001 | PASS | Missing CSRF login |
| CSRF-03 | CSRF | UC-001 | PASS | Forged CSRF header |
| CSRF-04 | CSRF | UC-002 | PASS | Missing CSRF logout does not revoke session |
| CSRF-05 | CSRF | UC-003 | PASS | Missing CSRF password change |
| LOGOUT-01 | Logout | UC-002 | PASS | Logout current session |
| LOGOUT-02 | Logout | UC-002 | FAIL | Logout already expired session |
| LOGOUT-03 | Logout | UC-002 | FAIL | Logout already revoked session |
| LOGOUT-04 | Logout | UC-002 | PASS | Logout without authenticated cookie |
| LOGOUT-05 | Logout | UC-002 | PASS | Logout one session leaves another valid |
| LOGOUT-06 | Logout | UC-002 | FAIL | Logout audit contains actor |
| LOGOUT-07 | Logout | UC-002 | PASS | UI logout redirects to login |
| CHANGE-01 | Change Password | UC-003 | PASS | Wrong current password |
| CHANGE-02 | Change Password | UC-003 | PASS | Reject password missing minimum 8 |
| CHANGE-03 | Change Password | UC-003 | PASS | Reject password missing uppercase |
| CHANGE-04 | Change Password | UC-003 | PASS | Reject password missing lowercase |
| CHANGE-05 | Change Password | UC-003 | PASS | Reject password missing digit |
| CHANGE-06 | Change Password | UC-003 | PASS | Reject password missing special character |
| CHANGE-07 | Change Password | UC-003 | PASS | Confirmation mismatch API |
| CHANGE-08 | Change Password | UC-003 | PASS | New password equals current |
| CHANGE-09 | Change Password | UC-003 | PASS | Missing current/new/confirm |
| CHANGE-10 | Change Password | UC-003 | PASS | Unauthenticated change password |
| CHANGE-11 | Change Password | UC-003 | PASS | Successful change updates hash, revokes other sessions and rotates current |
| CHANGE-12 | Change Password | UC-003 | PASS | Temporary password promoted to normal |
| CHANGE-13 | Change Password | UC-003 | FAIL | UI wrong current password has field-level MSG03 |
| CHANGE-14 | Change Password | UC-003 | FAIL | UI lists unmet password rules |
| CHANGE-15 | Change Password | UC-003 | PASS | UI confirmation mismatch attached to confirm field |
| CHANGE-16 | Change Password | UC-003 | FAIL | UI successful change exact MSG08 |
| FORGOT-01 | Forgot Password | UC-001 Step 2.1 | FAIL | Forgot instruction for 01 |
| FORGOT-02 | Forgot Password | UC-001 Step 2.1 | FAIL | Forgot instruction for 02 |
| FORGOT-03 | Forgot Password | UC-001 Step 2.1 | FAIL | Forgot instruction for 03 |
| FORGOT-04 | Forgot Password | UC-001 Step 2.1 | FAIL | Forgot instruction for 04 |
| FORGOT-05 | Forgot Password | UC-001 Step 2.1 | PASS | Instruction independent of account existence |
| FORGOT-06 | Forgot Password | UC-001 Step 2.1 | PASS | Forgot empty input required validation |
| PROFILE-01 | Profile | UC-001 | PASS | Profile renders SQL name and username |
| PROFILE-02 | Profile | UC-001 | PASS | Profile reload restores identity from API |
| PROFILE-03 | Profile | UC-001 | PASS | Anonymous profile redirected |
| PROFILE-04 | Profile | UC-001 supporting profile UI; not UC-011 | FAIL | Profile displays SQL email |
| PROFILE-05 | Profile | UC-001 | PASS | Restricted user cannot render profile |
| HEALTH-01 | Health | N/A | PASS | Direct Backend SQL health |
| HEALTH-02 | Health | N/A | PASS | Frontend proxy SQL health |
| HEALTH-03 | Health | N/A | PASS | Health read leaves users and sessions unchanged |
| HEALTH-04 | Health | N/A | PASS | Health JSON returns no credential data |
| HEALTH-05 | Health | N/A | PASS | Health authenticated user still reads old SQL |
| SEC-01 | Security | UC-001 | PASS | Auth response excludes password/hash/session credentials |
| SEC-02 | Security | UC-001..003 | PASS | Audit excludes secret values |
| SEC-03 | Security | UC-008..011 | PASS | User/role APIs actually present |
| SEC-04 | Security | UC-001 | PASS | Login body malformed does not echo supplied secret |
| SEC-05 | Security | UC-001 | PASS | Direct email requests share username lock counter |
| SEC-06 | Security | UC-001..003 | PASS | Runtime logs exclude plaintext credential values |
| ROLE-01 | Role/permissions partial | UC-001 | FAIL | Assigned SYS_ADMIN receives role-aware navigation |
| ROLE-02 | Role/permissions partial | UC-011 | PASS | SQL user_roles preserved and readable |
| ROLE-03 | Role/permissions partial | UC-001 | FAIL | Login includes calculated effective permissions for assigned role |
| ROLE-04 | Role/permissions partial | UC-008..010 | PASS | Role management API availability |
| ROLE-05 | Role/permissions partial | UC-001 | PASS | User without any assigned role rejected for business route |
| WEATHER-01 | Weather template | N/A | PASS | Weather template GET |
| WEATHER-02 | Weather template | N/A | PASS | Weather fields |
| WEATHER-03 | Weather template | N/A | PASS | Weather read does not mutate SQL |
| WEATHER-04 | Weather template | N/A | PASS | Weather unsupported write |
| WEATHER-05 | Weather template | N/A | PASS | Weather projection C to F |
| TEMP-01 | Temporary Credential | UC-004/UC-005 service only | PASS | Regular credential state |
| TEMP-02 | Temporary Credential | UC-004/UC-005 service only | PASS | Valid temporary state |
| TEMP-03 | Temporary Credential | UC-004/UC-005 service only | PASS | Expired boundary state |
| TEMP-04 | Temporary Credential | UC-004/UC-005 service only | PASS | Initial provision stored hash and 24-hour expiry |
| TEMP-05 | Temporary Credential | UC-004/UC-005 service only | PASS | One-time delivery cannot be read twice |
| TEMP-06 | Temporary Credential | UC-004/UC-005 service only | PASS | Preparation cannot be applied twice |
| TEMP-07 | Temporary Credential | UC-004/UC-005 service only | PASS | Initial provisioning refuses existing credential |
| TEMP-08 | Temporary Credential | UC-004/UC-005 service only | PASS | Admin reset rotates credential and revokes all sessions |
| TEMP-09 | Temporary Credential | UC-004/UC-005 service only | PASS | Disposed preparation unusable |
| TEMP-10 | Temporary Credential | UC-004/UC-005 service only | PASS | Provision/reset audit omits secrets |
| HASH-01 | Credential Hashing | UC-001/UC-003 internal | PASS | Valid hash verifies |
| HASH-02 | Credential Hashing | UC-001/UC-003 internal | PASS | Wrong password fails |
| HASH-03 | Credential Hashing | UC-001/UC-003 internal | PASS | Legacy sample hash rejected |
| HASH-04 | Credential Hashing | UC-001/UC-003 internal | PASS | Random salt generates unique hashes |
| HASH-05 | Credential Hashing | UC-001/UC-003 internal | PASS | Missing stored hash fails safely |
| HASH-06 | Credential Hashing | UC-001/UC-003 internal | PASS | DTO diagnostic strings redact secrets |
| POLICY-01 | Session Policy | UC-001/UC-003 internal | PASS | Normal idle timeout matches SRS |
| POLICY-02 | Session Policy | UC-001/UC-003 internal | FAIL | Restricted session follows 30-min idle parameter |
| POLICY-03 | Session Policy | UC-001/UC-003 internal | PASS | Hash format canonical and stable |
| POLICY-04 | Session Policy | UC-001/UC-003 internal | PASS | Malformed session credential rejected |
| POLICY-05 | Session Policy | UC-001/UC-003 internal | PASS | Expiry boundary rejects session |
| PLAN-01 | Transaction failure | UC-001/003 | BLOCKED | Inject SQL outage/commit-unknown safely |
| PLAN-02 | Concurrency | UC-001 | NOT TESTED | Concurrent wrong-password requests |
| PLAN-03 | Concurrency | UC-003 | NOT TESTED | Concurrent password change/reset/revoke |
| PLAN-04 | Session | UC-001 | NOT TESTED | Real-time 30-minute idle expiry |
| PLAN-05 | Actors | UC-001/002/003 | NOT TESTED | All ten Permission Matrix actor columns |
| PLAN-06 | Authorization | UC-011 and business UCs | BLOCKED | Cross-branch/department backend access isolation |
| PLAN-07 | Session | UC-001 | NOT TESTED | 12-hour continuously active session |
| PLAN-08 | Password boundary | UC-003 | NOT TESTED | Exact 8-character and 256/257-character boundary |
| PLAN-09 | User provisioning UI | UC-004/005 | BLOCKED | Administrator provisions/resets via UI |
| PLAN-10 | Production transport | N/A | NOT TESTED | Production TLS/CORS/deployment |

## LOGIN-01 — Username UI login with SQL user

- **UC:** UC-001. **GBR:** GBR-019, GBR-021. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Username UI login with SQL user. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; real user profile
- **Actual:** {"url":"https://localhost:5185/profile","user":{"id":"43","username":"qa_1792482958149_active","name":"QA active"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-01; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-02 — Email UI login

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Email UI login. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; same SQL user
- **Actual:** {"id":"43","username":"qa_1792482958149_active","name":"QA active"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-02; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-03 — Wrong password

- **UC:** UC-001. **GBR:** GBR-019, GBR-020. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Wrong password. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401 generic; counter increments; no new session
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"INVALID_CREDENTIALS","status":401,"code":"INVALID_CREDENTIALS","traceId":"0HNPEFS5VE5PF:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-03; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-04 — Unknown account matches invalid credentials response

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Unknown account matches invalid credentials response. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401 same code/status/title
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"INVALID_CREDENTIALS","status":401,"code":"INVALID_CREDENTIALS","traceId":"0HNPEFS5VE5PH:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-04; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-05 — Inactive user correct password

- **UC:** UC-001. **GBR:** GBR-019, GBR-020. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Inactive user correct password. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401 generic; no session
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"INVALID_CREDENTIALS","status":401,"code":"INVALID_CREDENTIALS","traceId":"0HNPEFS5VE5PJ:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-05; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-06 — Inactive attempted login increments counter per UC exception

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Inactive attempted login increments counter per UC exception. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Existing inactive account failure counter increases
- **Actual:** {"error":"Expected values to be strictly equal:\n\n0 !== 1\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-06; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-07 — Missing login field 07

- **UC:** UC-001. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing login field 07. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; no session/counter mutation
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"VALIDATION_FAILED","traceId":"0HNPEFS5VE5PN:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-07; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-08 — Missing login field 08

- **UC:** UC-001. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing login field 08. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; no session/counter mutation
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"VALIDATION_FAILED","traceId":"0HNPEFS5VE5PP:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-08; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-09 — Missing login field 09

- **UC:** UC-001. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing login field 09. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; no session/counter mutation
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"VALIDATION_FAILED","traceId":"0HNPEFS5VE5PR:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-09; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-10 — Five direct API failures lock stored account

- **UC:** UC-001. **GBR:** GBR-020, GBR-008. **MSG:** MSG11. **SYS-PAR:** SYS-PAR-01, SYS-PAR-02.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Five direct API failures lock stored account. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** counter 1..5; lock ~30 min; one lock audit; no sessions
- **Actual:** {"attempts":[{"attempt":1,"status":401},{"attempt":2,"status":401},{"attempt":3,"status":401},{"attempt":4,"status":401},{"attempt":5,"status":401}],"stored":{"user_id":45,"username":"qa_1792482958149_lock","email":"qa_1792482958149_lock@example.invalid","account_status":"ACTIVE","failed_login_count":5,"locked_until":"2026-10-20T08:26:03.2205498","temporary_password_expires_at":null,"last_login_at":null}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-10; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-11 — Correct password while locked

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** MSG12. **SYS-PAR:** SYS-PAR-01, SYS-PAR-02.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Correct password while locked. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 423. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 423 with stored unlock time; no session
- **Actual:** {"status":423,"body":{"type":"about:blank","title":"ACCOUNT_LOCKED","status":423,"code":"ACCOUNT_LOCKED","traceId":"0HNPEFS5VE5Q7:00000001","parameters":{"lockedUntil":"2026-10-20T08:26:03.2205498Z"}}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-11; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-12 — Wrong password while locked

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** MSG11. **SYS-PAR:** SYS-PAR-01, SYS-PAR-02.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Wrong password while locked. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401 generic; lock deadline not extended
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"INVALID_CREDENTIALS","status":401,"code":"INVALID_CREDENTIALS","traceId":"0HNPEFS5VE5Q9:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-12; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-13 — Correct password after lock deadline

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** . **SYS-PAR:** SYS-PAR-01, SYS-PAR-02.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** QA-only deadline backdated to simulate elapsed 30 min; no 30-min real-time wait.
- **Steps:** Correct password after lock deadline. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; count=0, lock=NULL
- **Actual:** {"status":200,"body":{"user":{"id":"45","username":"qa_1792482958149_lock","name":"QA lock"},"expiresAt":"2026-10-20T08:26:04.2413404Z","absoluteExpiresAt":"2026-10-20T19:56:04.2413404Z","mustChangePassword":false,"csrfToken":"[REDACTED]"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-13; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-14 — Success resets ordinary failure counter

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** . **SYS-PAR:** SYS-PAR-01, SYS-PAR-02.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Success resets ordinary failure counter. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; prior failure count resets
- **Actual:** {"user_id":45,"username":"qa_1792482958149_lock","email":"qa_1792482958149_lock@example.invalid","account_status":"ACTIVE","failed_login_count":0,"locked_until":null,"temporary_password_expires_at":null,"last_login_at":"2026-10-20T07:56:04.8233765"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-14; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-15 — Temporary password login redirects to forced change

- **UC:** UC-001. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Temporary password login redirects to forced change. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200 restricted; UI /change-password
- **Actual:** {"url":"https://localhost:5185/change-password","restricted":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-15; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-16 — Expired temporary password

- **UC:** UC-001. **GBR:** GBR-023. **MSG:** MSG13. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Expired temporary password. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 403. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 403; no session
- **Actual:** {"status":403,"body":{"type":"about:blank","title":"TEMPORARY_PASSWORD_EXPIRED","status":403,"code":"TEMPORARY_PASSWORD_EXPIRED","traceId":"0HNPEFS5VE5QL:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-16; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-17 — Successful login audit actor/time

- **UC:** UC-001. **GBR:** GBR-008. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Successful login audit actor/time. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** LOGIN_SUCCESS has user actor and UTC timestamp
- **Actual:** {"audit_id":196,"actor_user_id":43,"action":"LOGIN","entity_type":"users","entity_id":"43","occurred_at":"2026-10-20T07:56:00.5670946","old_values":null,"new_values":"{\"authenticationEvent\":\"LOGIN_SUCCESS\"}","changed_fields":null}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-17; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-18 — Wrong-password UI message exact MSG11

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** MSG11. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Wrong-password UI message exact MSG11. Request thực: POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Invalid username/email or password.
- **Actual:** {"text":"Invalid username/email or password."}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-18; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-19 — Locked UI message includes unlock time and exact MSG12

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** MSG12. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Locked UI message includes unlock time and exact MSG12. Request thực: POST /api/auth/login → 423. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Your account is temporarily locked. Please try again after {time}.
- **Actual:** {"error":"The input did not match the regular expression /Your account is temporarily locked\\. Please try again after .+\\./. Input:\n\n'This account is temporarily locked. Try again later.'\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-19; [ảnh UI](screenshots/LOGIN-19.png); before/after SQL và HTTP nằm trong cùng record.

## LOGIN-20 — Expired temporary UI exact MSG13

- **UC:** UC-001. **GBR:** GBR-023. **MSG:** MSG13. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Expired temporary UI exact MSG13. Request thực: POST /api/auth/login → 403. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Your temporary password has expired. Please contact the Administrator.
- **Actual:** {"error":"Expected values to be strictly equal:\n+ actual - expected\n\n+ 'Temporary password has expired. Contact your administrator.'\n- 'Your temporary password has expired. Please contact the Administrator.'\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-20; [ảnh UI](screenshots/LOGIN-20.png); before/after SQL và HTTP nằm trong cùng record.

## LOGIN-21 — Blank form identifies required fields using MSG03

- **UC:** UC-001. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Blank form identifies required fields using MSG03. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Field: reason. Please check and try again.
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(text.every(t=>t.includes('Please check and try again.')))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-21; [ảnh UI](screenshots/LOGIN-21.png); before/after SQL và HTTP nằm trong cùng record.

## LOGIN-22 — Login calculates role/scoped permissions

- **UC:** UC-001. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Login calculates role/scoped permissions. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Effective roles/grants/scopes are available after login
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(s.body.permissions\|\|s.body.grants\|\|s.body.principal?.grants?.length)\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-22; before/after SQL và HTTP nằm trong cùng record.

## LOGIN-23 — Whitespace around identifier

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Whitespace around identifier. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Username trimmed; password unchanged; 200
- **Actual:** {"id":"43","username":"qa_1792482958149_active","name":"QA active"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGIN-23; before/after SQL và HTTP nằm trong cùng record.

## SESSION-01 — Anonymous protected API

- **UC:** UC-001. **GBR:** GBR-001. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Anonymous protected API. Request thực: GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** GET session 401
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"AUTHENTICATION_REQUIRED","traceId":"0HNPEFS5VE5R0:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-01; before/after SQL và HTTP nằm trong cùng record.

## SESSION-02 — Normal session idle deadline

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** SYS-PAR-06.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Normal session idle deadline. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/session → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** SQL expiry ~30 min; session GET 200
- **Actual:** {"session_id":92,"created_at":"2026-10-20T07:56:09.1237593","last_used_at":"2026-10-20T07:56:09.1461592","expires_at":"2026-10-20T08:26:09.1461592","revoked_at":null}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-02; before/after SQL và HTTP nằm trong cùng record.

## SESSION-03 — Valid request advances last-used and expiry

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** SYS-PAR-06.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Valid request advances last-used and expiry. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/session → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** SQL last_used_at and expires_at advance
- **Actual:** {"before":{"session_id":93,"created_at":"2026-10-20T07:56:09.3790330","last_used_at":"2026-10-20T07:56:09.3790330","expires_at":"2026-10-20T08:26:09.3790330","revoked_at":null},"after":{"session_id":93,"created_at":"2026-10-20T07:56:09.3790330","last_used_at":"2026-10-20T07:56:09.5729496","expires_at":"2026-10-20T08:26:09.5729496","revoked_at":null}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-03; before/after SQL và HTTP nằm trong cùng record.

## SESSION-04 — Expired SQL session rejected

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** MSG01. **SYS-PAR:** SYS-PAR-06.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** QA session expiry backdated; no real session modified.
- **Steps:** Expired SQL session rejected. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401; expiry not renewed
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"SESSION_INVALID","traceId":"0HNPEFS5VE5R9:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-04; before/after SQL và HTTP nằm trong cùng record.

## SESSION-05 — Revoked SQL session rejected

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Revoked SQL session rejected. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"SESSION_INVALID","traceId":"0HNPEFS5VE5RC:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-05; before/after SQL và HTTP nằm trong cùng record.

## SESSION-06 — Tampered authentication cookie

- **UC:** UC-001. **GBR:** GBR-001. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Tampered authentication cookie. Request thực: GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401; no identity created
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"SESSION_INVALID","traceId":"0HNPEFS5VE5RD:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-06; before/after SQL và HTTP nằm trong cùng record.

## SESSION-07 — Cookie security attributes

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Cookie security attributes. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Secure HttpOnly SameSite=Lax host-only /
- **Actual:** {"secure":true,"httpOnly":true,"sameSite":"Lax","path":"/"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-07; before/after SQL và HTTP nằm trong cùng record.

## SESSION-08 — Restricted session blocks other API

- **UC:** UC-001. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Restricted session blocks other API. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/health/database → 403. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 403 PASSWORD_CHANGE_REQUIRED
- **Actual:** {"status":403,"body":{"type":"about:blank","title":"Access denied","status":403,"code":"PASSWORD_CHANGE_REQUIRED","traceId":"0HNPEFS5VE5RI:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-08; before/after SQL và HTTP nằm trong cùng record.

## SESSION-09 — Revoked session UI returns login with exact MSG01

- **UC:** UC-001. **GBR:** GBR-001. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Revoked session UI returns login with exact MSG01. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Login page with session-expired dialog MSG01
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(text.includes('Your session has expired. Please log in again.'))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-09; [ảnh UI](screenshots/SESSION-09.png); before/after SQL và HTTP nằm trong cùng record.

## SESSION-10 — Account deactivation invalidates existing cookie

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Account deactivation invalidates existing cookie. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401 on next request
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"SESSION_INVALID","traceId":"0HNPEFS5VE5RO:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SESSION-10; before/after SQL và HTTP nằm trong cùng record.

## CSRF-01 — Anonymous CSRF bootstrap

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Anonymous CSRF bootstrap. Request thực: GET /api/auth/csrf → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; expected header; secure cookie
- **Actual:** {"status":200,"headerName":"X-CSRF-TOKEN"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CSRF-01; before/after SQL và HTTP nằm trong cùng record.

## CSRF-02 — Missing CSRF login

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing CSRF login. Request thực: POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; no session
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"CSRF_VALIDATION_FAILED","traceId":"0HNPEFS5VE5RQ:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CSRF-02; before/after SQL và HTTP nằm trong cùng record.

## CSRF-03 — Forged CSRF header

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forged CSRF header. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"CSRF_VALIDATION_FAILED","traceId":"0HNPEFS5VE5RS:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CSRF-03; before/after SQL và HTTP nằm trong cùng record.

## CSRF-04 — Missing CSRF logout does not revoke session

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing CSRF logout does not revoke session. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 400; GET /api/auth/session → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; session remains 200
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"CSRF_VALIDATION_FAILED","traceId":"0HNPEFS5VE5RV:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CSRF-04; before/after SQL và HTTP nằm trong cùng record.

## CSRF-05 — Missing CSRF password change

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing CSRF password change. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; credential unchanged
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"CSRF_VALIDATION_FAILED","traceId":"0HNPEFS5VE5S3:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CSRF-05; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-01 — Logout current session

- **UC:** UC-002. **GBR:** GBR-021, GBR-008. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout current session. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 204; GET /api/auth/session → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 204, revoked row, subsequent session 401
- **Actual:** {"status":204,"body":null}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-01; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-02 — Logout already expired session

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout already expired session. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 204 success
- **Actual:** {"error":"Expected values to be strictly equal:\n\n400 !== 204\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-02; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-03 — Logout already revoked session

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout already revoked session. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 204 success
- **Actual:** {"error":"Expected values to be strictly equal:\n\n400 !== 204\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-03; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-04 — Logout without authenticated cookie

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout without authenticated cookie. Request thực: GET /api/auth/csrf → 200; POST /api/auth/logout → 204. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 204 success with anonymous CSRF
- **Actual:** {"status":204,"body":null}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-04; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-05 — Logout one session leaves another valid

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout one session leaves another valid. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 204; GET /api/auth/session → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** A logout 204; B session 200
- **Actual:** {"otherSession":200}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-05; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-06 — Logout audit contains actor

- **UC:** UC-002. **GBR:** GBR-008. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Logout audit contains actor. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/logout → 204. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** LOGOUT record actor equals user
- **Actual:** {"error":"Expected values to be strictly equal:\n\nnull !== 43\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-06; before/after SQL và HTTP nằm trong cùng record.

## LOGOUT-07 — UI logout redirects to login

- **UC:** UC-002. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** UI logout redirects to login. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Logout click → /login; profile blocked
- **Actual:** {"url":"https://localhost:5185/login"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID LOGOUT-07; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-01 — Wrong current password

- **UC:** UC-003. **GBR:** GBR-019, GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Wrong current password. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400; hash unchanged
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"CURRENT_PASSWORD_INVALID","status":400,"code":"CURRENT_PASSWORD_INVALID","traceId":"0HNPEFS5VE5T0:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-01; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-02 — Reject password missing minimum 8

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** SYS-PAR-03, SYS-PAR-04.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Reject password missing minimum 8. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation; unchanged credentials
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5T3:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-02; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-03 — Reject password missing uppercase

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** SYS-PAR-03, SYS-PAR-04.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Reject password missing uppercase. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation; unchanged credentials
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5T6:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-03; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-04 — Reject password missing lowercase

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** SYS-PAR-03, SYS-PAR-04.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Reject password missing lowercase. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation; unchanged credentials
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5T9:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-04; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-05 — Reject password missing digit

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** SYS-PAR-03, SYS-PAR-04.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Reject password missing digit. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation; unchanged credentials
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5TC:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-05; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-06 — Reject password missing special character

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** SYS-PAR-03, SYS-PAR-04.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Reject password missing special character. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation; unchanged credentials
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5TF:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-06; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-07 — Confirmation mismatch API

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Confirmation mismatch API. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 validation
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"VALIDATION_FAILED","status":400,"code":"VALIDATION_FAILED","traceId":"0HNPEFS5VE5TI:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-07; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-08 — New password equals current

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** New password equals current. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 policy violation
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"PASSWORD_POLICY_VIOLATION","status":400,"code":"PASSWORD_POLICY_VIOLATION","traceId":"0HNPEFS5VE5TL:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-08; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-09 — Missing current/new/confirm

- **UC:** UC-003. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Missing current/new/confirm. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 400; POST /api/auth/change-password → 400; POST /api/auth/change-password → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400 each
- **Actual:** [{"field":"currentPassword","status":400},{"field":"newPassword","status":400},{"field":"confirmNewPassword","status":400}]
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-09; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-10 — Unauthenticated change password

- **UC:** UC-003. **GBR:** GBR-001. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Unauthenticated change password. Request thực: GET /api/auth/csrf → 200; POST /api/auth/change-password → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 401
- **Actual:** {"status":401,"body":{"type":"about:blank","title":"Authentication required","status":401,"code":"AUTHENTICATION_REQUIRED","traceId":"0HNPEFS5VE5TS:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-10; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-11 — Successful change updates hash, revokes other sessions and rotates current

- **UC:** UC-003. **GBR:** GBR-021, GBR-022, GBR-008. **MSG:** MSG08. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Successful change updates hash, revokes other sessions and rotates current. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 200; GET /api/auth/session → 401; GET /api/auth/session → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/auth/csrf → 200; POST /api/auth/login → 401. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; old cookie/session 401; new password works; audit
- **Actual:** {"changed":200,"otherSession":401,"oldCookie":401,"newPasswordLogin":200,"oldPasswordLogin":401}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-11; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-12 — Temporary password promoted to normal

- **UC:** UC-003. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Temporary password promoted to normal. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; POST /api/auth/change-password → 200; GET /api/health/database → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200; temporary expiry NULL; restricted false
- **Actual:** {"restricted":false,"user":{"user_id":46,"username":"qa_1792482958149_temporary","email":"qa_1792482958149_temporary@example.invalid","account_status":"ACTIVE","failed_login_count":0,"locked_until":null,"temporary_password_expires_at":null,"last_login_at":"2026-10-20T07:56:19.2386144"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-12; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-13 — UI wrong current password has field-level MSG03

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** UI wrong current password has field-level MSG03. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Current password field shows reason using MSG03
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(field.length&&field.some(t=>t.includes('Please check and try again.')))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-13; [ảnh UI](screenshots/CHANGE-13.png); before/after SQL và HTTP nằm trong cùng record.

## CHANGE-14 — UI lists unmet password rules

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** UI lists unmet password rules. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Field-level unmet complexity items, not generic form error
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(errors.length&&errors.some(t=>/uppercase\|lowercase\|digit\|8/.test(t)))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-14; [ảnh UI](screenshots/CHANGE-14.png); before/after SQL và HTTP nằm trong cùng record.

## CHANGE-15 — UI confirmation mismatch attached to confirm field

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** UI confirmation mismatch attached to confirm field. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** confirm error shown; no password update
- **Actual:** {"text":"Passwords must match."}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-15; before/after SQL và HTTP nằm trong cùng record.

## CHANGE-16 — UI successful change exact MSG08

- **UC:** UC-003. **GBR:** GBR-008. **MSG:** MSG08. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** UI successful change exact MSG08. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Password has been saved successfully.
- **Actual:** {"error":"Expected values to be strictly equal:\n+ actual - expected\n\n+ 'Password changed.'\n- 'Password has been saved successfully.'\n            ^\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID CHANGE-16; [ảnh UI](screenshots/CHANGE-16.png); before/after SQL và HTTP nằm trong cùng record.

## FORGOT-01 — Forgot instruction for 01

- **UC:** UC-001 Step 2.1. **GBR:** GBR-024. **MSG:** MSG14. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forgot instruction for 01. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Exact MSG14 inline; no account lookup/disclosure
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(body.includes('If you need your password reset, please contact the Administrator.'))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-01; [ảnh UI](screenshots/FORGOT-01.png); before/after SQL và HTTP nằm trong cùng record.

## FORGOT-02 — Forgot instruction for 02

- **UC:** UC-001 Step 2.1. **GBR:** GBR-024. **MSG:** MSG14. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forgot instruction for 02. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Exact MSG14 inline; no account lookup/disclosure
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(body.includes('If you need your password reset, please contact the Administrator.'))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-02; [ảnh UI](screenshots/FORGOT-02.png); before/after SQL và HTTP nằm trong cùng record.

## FORGOT-03 — Forgot instruction for 03

- **UC:** UC-001 Step 2.1. **GBR:** GBR-024. **MSG:** MSG14. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forgot instruction for 03. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Exact MSG14 inline; no account lookup/disclosure
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(body.includes('If you need your password reset, please contact the Administrator.'))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-03; [ảnh UI](screenshots/FORGOT-03.png); before/after SQL và HTTP nằm trong cùng record.

## FORGOT-04 — Forgot instruction for 04

- **UC:** UC-001 Step 2.1. **GBR:** GBR-024. **MSG:** MSG14. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forgot instruction for 04. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Exact MSG14 inline; no account lookup/disclosure
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(body.includes('If you need your password reset, please contact the Administrator.'))\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-04; [ảnh UI](screenshots/FORGOT-04.png); before/after SQL và HTTP nằm trong cùng record.

## FORGOT-05 — Instruction independent of account existence

- **UC:** UC-001 Step 2.1. **GBR:** GBR-024. **MSG:** MSG14. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Instruction independent of account existence. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** All submitted identifiers receive same message
- **Actual:** {"sameInstruction":true,"toast":"Please contact the Administrator to reset your password."}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED (previous four live flows)"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-05; before/after SQL và HTTP nằm trong cùng record.

## FORGOT-06 — Forgot empty input required validation

- **UC:** UC-001 Step 2.1. **GBR:** GBR-003. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Forgot empty input required validation. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Inline required error; no reset mutation
- **Actual:** {"text":"Username or email is required."}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID FORGOT-06; before/after SQL và HTTP nằm trong cùng record.

## PROFILE-01 — Profile renders SQL name and username

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Profile renders SQL name and username. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** QA active name/username visible
- **Actual:** {"name":"QA active","username":"qa_1792482958149_active"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PROFILE-01; before/after SQL và HTTP nằm trong cùng record.

## PROFILE-02 — Profile reload restores identity from API

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Profile reload restores identity from API. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** After reload same heading
- **Actual:** {"url":"https://localhost:5185/profile"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PROFILE-02; before/after SQL và HTTP nằm trong cùng record.

## PROFILE-03 — Anonymous profile redirected

- **UC:** UC-001. **GBR:** GBR-001. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Anonymous profile redirected. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** /login; no profile data
- **Actual:** {"url":"https://localhost:5185/login"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PROFILE-03; before/after SQL và HTTP nằm trong cùng record.

## PROFILE-04 — Profile displays SQL email

- **UC:** UC-001 supporting profile UI; not UC-011. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Profile displays SQL email. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Email matches database rather than blank placeholder
- **Actual:** {"error":"Expected values to be strictly equal:\n+ actual - expected\n\n+ ''\n- 'qa_1792482958149_active@example.invalid'\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PROFILE-04; [ảnh UI](screenshots/PROFILE-04.png); before/after SQL và HTTP nằm trong cùng record.
- **Giới hạn:** Supplemental UI/SQL contract test: SRS does not explicitly require the email field in this profile; not a standalone SRS violation.

## PROFILE-05 — Restricted user cannot render profile

- **UC:** UC-001. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Restricted user cannot render profile. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** /change-password instead
- **Actual:** {"url":"https://localhost:5185/change-password"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PROFILE-05; before/after SQL và HTTP nằm trong cùng record.

## HEALTH-01 — Direct Backend SQL health

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Direct Backend SQL health. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200 HotelOpsDB
- **Actual:** {"status":"ok","database":"HotelOpsDB","message":"Database connection successful."}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID HEALTH-01; before/after SQL và HTTP nằm trong cùng record.

## HEALTH-02 — Frontend proxy SQL health

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Frontend proxy SQL health. Request thực: GET /api/health/database → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200 HotelOpsDB
- **Actual:** {"status":200,"body":{"status":"ok","database":"HotelOpsDB","message":"Database connection successful."}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID HEALTH-02; before/after SQL và HTTP nằm trong cùng record.

## HEALTH-03 — Health read leaves users and sessions unchanged

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Health read leaves users and sessions unchanged. Request thực: GET /api/health/database → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Same QA users/sessions before and after
- **Actual:** {"unchanged":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID HEALTH-03; before/after SQL và HTTP nằm trong cùng record.

## HEALTH-04 — Health JSON returns no credential data

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Health JSON returns no credential data. Request thực: GET /api/health/database → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No connection string/password/hash
- **Actual:** {"status":200,"body":{"status":"ok","database":"HotelOpsDB","message":"Database connection successful."}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID HEALTH-04; before/after SQL và HTTP nằm trong cùng record.

## HEALTH-05 — Health authenticated user still reads old SQL

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Health authenticated user still reads old SQL. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/health/database → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200 HotelOpsDB
- **Actual:** {"status":200,"body":{"status":"ok","database":"HotelOpsDB","message":"Database connection successful."}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID HEALTH-05; before/after SQL và HTTP nằm trong cùng record.

## SEC-01 — Auth response excludes password/hash/session credentials

- **UC:** UC-001. **GBR:** GBR-008, GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Auth response excludes password/hash/session credentials. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No password/hash/auth-session raw token; CSRF token is intentional
- **Actual:** {"fields":["user","expiresAt","absoluteExpiresAt","mustChangePassword","csrfToken"],"csrfIntentionallyReturned":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-01; before/after SQL và HTTP nằm trong cùng record.

## SEC-02 — Audit excludes secret values

- **UC:** UC-001..003. **GBR:** GBR-008. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Audit excludes secret values. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No passwords or stored credential hashes in audit values
- **Actual:** {"auditRows":105,"noSecretValues":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-02; before/after SQL và HTTP nằm trong cùng record.

## SEC-03 — User/role APIs actually present

- **UC:** UC-008..011. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** User/role APIs actually present. Request thực: GET /api/users → 404; GET /api/roles → 404; GET /api/permissions → 404. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 404 means NOT IMPLEMENTED, not a permission PASS
- **Actual:** [{"endpoint":"/api/users","status":404},{"endpoint":"/api/roles","status":404},{"endpoint":"/api/permissions","status":404}]
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-03; before/after SQL và HTTP nằm trong cùng record.

## SEC-04 — Login body malformed does not echo supplied secret

- **UC:** UC-001. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Login body malformed does not echo supplied secret. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 400. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 400, redacted validation response
- **Actual:** {"status":400,"body":{"type":"about:blank","title":"Request validation failed","status":400,"code":"VALIDATION_FAILED","traceId":"0HNPEFS5VE5VJ:00000001"}}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-04; before/after SQL và HTTP nằm trong cùng record.

## SEC-05 — Direct email requests share username lock counter

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Direct email requests share username lock counter. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 401; GET /api/auth/csrf → 200; POST /api/auth/login → 423. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Email failures increment same SQL account; no FE bypass
- **Actual:** {"user_id":45,"username":"qa_1792482958149_lock","email":"qa_1792482958149_lock@example.invalid","account_status":"ACTIVE","failed_login_count":5,"locked_until":"2026-10-20T08:26:32.5936067","temporary_password_expires_at":null,"last_login_at":"2026-10-20T07:56:04.8233765"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-05; before/after SQL và HTTP nằm trong cùng record.

## SEC-06 — Runtime logs exclude plaintext credential values

- **UC:** UC-001..003. **GBR:** GBR-008. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Runtime logs exclude plaintext credential values. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No generated password, Identity hash or raw session credential logged
- **Actual:** {"logBytes":744509,"noCredentialValues":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID SEC-06; before/after SQL và HTTP nằm trong cùng record.

## ROLE-01 — Assigned SYS_ADMIN receives role-aware navigation

- **UC:** UC-001. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Assigned SYS_ADMIN receives role-aware navigation. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** UI exposes granted use cases from actual SQL role
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(links.length>0)\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID ROLE-01; [ảnh UI](screenshots/ROLE-01.png); before/after SQL và HTTP nằm trong cùng record.

## ROLE-02 — SQL user_roles preserved and readable

- **UC:** UC-011. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** SQL user_roles preserved and readable. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** QA active user has active SYS_ADMIN assignment
- **Actual:** {"assignments":[{"role_code":"SYS_ADMIN","is_active":true}]}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID ROLE-02; before/after SQL và HTTP nằm trong cùng record.

## ROLE-03 — Login includes calculated effective permissions for assigned role

- **UC:** UC-001. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Login includes calculated effective permissions for assigned role. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Role grants and scope available to frontend
- **Actual:** {"error":"The expression evaluated to a falsy value:\n\n  assert.ok(s.body.permissions?.length\|\|s.body.grants?.length\|\|s.body.principal?.grants?.length)\n"}
- **Result:** **FAIL**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID ROLE-03; before/after SQL và HTTP nằm trong cùng record.

## ROLE-04 — Role management API availability

- **UC:** UC-008..010. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Role management API availability. Request thực: GET /api/auth/csrf → 200; POST /api/auth/login → 200; GET /api/roles → 404. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Inventory confirms absent APIs, not permission enforcement
- **Actual:** {"status":404,"implemented":false,"permissionEnforcement":"NOT TESTED (no business API)"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID ROLE-04; before/after SQL và HTTP nằm trong cùng record.

## ROLE-05 — User without any assigned role rejected for business route

- **UC:** UC-001. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** User without any assigned role rejected for business route. Request thực: POST /api/auth/login → 200. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** FE blocks /rooms with no grants
- **Actual:** {"route":"https://localhost:5185/rooms","denied":true,"layer":"Frontend only; backend enforcement unavailable"}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"TESTED"}
- **Evidence:** all-test-cases.json, tìm ID ROLE-05; before/after SQL và HTTP nằm trong cùng record.

## WEATHER-01 — Weather template GET

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Weather template GET. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 200 five generated entries; not database-backed
- **Actual:** {"count":5}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID WEATHER-01; before/after SQL và HTTP nằm trong cùng record.

## WEATHER-02 — Weather fields

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Weather fields. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** date/temperatureC/temperatureF/summary
- **Actual:** {"fields":["date","temperatureC","temperatureF","summary"]}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID WEATHER-02; before/after SQL và HTTP nằm trong cùng record.

## WEATHER-03 — Weather read does not mutate SQL

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Weather read does not mutate SQL. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** QA users/session state unchanged
- **Actual:** {"unchanged":true}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID WEATHER-03; before/after SQL và HTTP nằm trong cùng record.

## WEATHER-04 — Weather unsupported write

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Weather unsupported write. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** POST returns 405
- **Actual:** {"status":405}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID WEATHER-04; before/after SQL và HTTP nằm trong cùng record.

## WEATHER-05 — Weather projection C to F

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Instance QA 7039/5185; SQL HotelOpsDB; isolated QA users only; see before/setup evidence.
- **Test data:** Generated QA identifiers; passwords/cookies/tokens redacted.
- **Steps:** Weather projection C to F. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Fahrenheit matches code conversion
- **Actual:** {"count":5}
- **Result:** **PASS**.
- **Layers:** {"HTTP":"TESTED","SQL":"OBSERVED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID WEATHER-05; before/after SQL và HTTP nằm trong cùng record.

## TEMP-01 — Regular credential state

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Regular credential state. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** NULL marker is Regular
- **Actual:** {"state":"Regular"}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-01; before/after SQL và HTTP nằm trong cùng record.

## TEMP-02 — Valid temporary state

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Valid temporary state. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** future deadline is TemporaryValid
- **Actual:** {"state":"TemporaryValid"}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-02; before/after SQL và HTTP nằm trong cùng record.

## TEMP-03 — Expired boundary state

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Expired boundary state. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** deadline <= now is TemporaryExpired
- **Actual:** {"state":"TemporaryExpired"}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-03; before/after SQL và HTTP nằm trong cùng record.

## TEMP-04 — Initial provision stored hash and 24-hour expiry

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Initial provision stored hash and 24-hour expiry. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Only QA user receives credential; stored expiry now+24h; plaintext strong
- **Actual:** {"length":24,"validityHours":24,"hashVerified":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-04; before/after SQL và HTTP nằm trong cùng record.

## TEMP-05 — One-time delivery cannot be read twice

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** One-time delivery cannot be read twice. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Second TakePasswordOnce throws
- **Actual:** {"secondDeliveryRejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-05; before/after SQL và HTTP nằm trong cùng record.

## TEMP-06 — Preparation cannot be applied twice

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Preparation cannot be applied twice. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Second ApplyAsync throws
- **Actual:** {"reuseRejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-06; before/after SQL và HTTP nằm trong cùng record.

## TEMP-07 — Initial provisioning refuses existing credential

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Initial provisioning refuses existing credential. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Existing hash preserved; no overwrite
- **Actual:** {"unchanged":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-07; before/after SQL và HTTP nằm trong cùng record.

## TEMP-08 — Admin reset rotates credential and revokes all sessions

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023, GBR-008, GBR-021. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Admin reset rotates credential and revokes all sessions. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** QA sessions revoked; expiry 24h; reset audit
- **Actual:** {"revoked":true,"validityHours":24}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-08; before/after SQL và HTTP nằm trong cùng record.

## TEMP-09 — Disposed preparation unusable

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Disposed preparation unusable. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** ApplyAsync throws without writes
- **Actual:** {"disposedRejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-09; before/after SQL và HTTP nằm trong cùng record.

## TEMP-10 — Provision/reset audit omits secrets

- **UC:** UC-004/UC-005 service only. **GBR:** GBR-023, GBR-008. **MSG:** . **SYS-PAR:** SYS-PAR-05.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Provision/reset audit omits secrets. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No delivered password or hash in audit values
- **Actual:** {"auditRows":6,"noCredentialValues":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID TEMP-10; before/after SQL và HTTP nằm trong cùng record.

## HASH-01 — Valid hash verifies

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Valid hash verifies. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Identity V3 verification succeeds
- **Actual:** {"verified":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-01; before/after SQL và HTTP nằm trong cùng record.

## HASH-02 — Wrong password fails

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Wrong password fails. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Failed verification
- **Actual:** {"rejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-02; before/after SQL và HTTP nằm trong cùng record.

## HASH-03 — Legacy sample hash rejected

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Legacy sample hash rejected. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Unsupported fake hash fails without exception
- **Actual:** {"rejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-03; before/after SQL và HTTP nằm trong cùng record.

## HASH-04 — Random salt generates unique hashes

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Random salt generates unique hashes. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Two hashes differ but both verify
- **Actual:** {"uniqueSalt":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-04; before/after SQL và HTTP nằm trong cùng record.

## HASH-05 — Missing stored hash fails safely

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Missing stored hash fails safely. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** NULL, empty and malformed hashes all fail
- **Actual:** {"rejected":3}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-05; before/after SQL và HTTP nằm trong cùng record.

## HASH-06 — DTO diagnostic strings redact secrets

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-019, GBR-008. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** DTO diagnostic strings redact secrets. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Request/response ToString omit passwords/tokens
- **Actual:** {"redacted":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID HASH-06; before/after SQL và HTTP nằm trong cùng record.

## POLICY-01 — Normal idle timeout matches SRS

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** SYS-PAR-06.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Normal idle timeout matches SRS. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** 30-minute idle window
- **Actual:** {"minutes":30}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID POLICY-01; before/after SQL và HTTP nằm trong cùng record.

## POLICY-02 — Restricted session follows 30-min idle parameter

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** SYS-PAR-06.
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Restricted session follows 30-min idle parameter. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No extra 15-minute cap specified by SYS-PAR-06
- **Actual:** {"error":"Actual restricted lifetime is 15 min, while SRS SYS-PAR-06 specifies 30 min without restricted-session exception."}
- **Result:** **FAIL**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID POLICY-02; before/after SQL và HTTP nằm trong cùng record.

## POLICY-03 — Hash format canonical and stable

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Hash format canonical and stable. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Random session credential valid; same raw yields stable hash
- **Actual:** {"canonical":true,"length":43}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID POLICY-03; before/after SQL và HTTP nằm trong cùng record.

## POLICY-04 — Malformed session credential rejected

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Malformed session credential rejected. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Invalid length/characters rejected
- **Actual:** {"rejected":3}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID POLICY-04; before/after SQL và HTTP nằm trong cùng record.

## POLICY-05 — Expiry boundary rejects session

- **UC:** UC-001/UC-003 internal. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** .
- **Preconditions:** Dedicated service-probe QA user; real SQL; caller/service invocation only; no HTTP API exposed for issuance.
- **Test data:** In-memory random credentials, never logged.
- **Steps:** Expiry boundary rejects session. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** At exact expires_at validation fails
- **Actual:** {"expiredBoundaryRejected":true}
- **Result:** **PASS**.
- **Layers:** {"SQL":"TESTED","HTTP":"NOT TESTED (service-only)","Frontend":"NOT TESTED (no admin API/UI integration)"}
- **Evidence:** all-test-cases.json, tìm ID POLICY-05; before/after SQL và HTTP nằm trong cùng record.

## PLAN-01 — Inject SQL outage/commit-unknown safely

- **UC:** UC-001/003. **GBR:** GBR-007, GBR-008. **MSG:** MSG06, MSG07. **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Inject SQL outage/commit-unknown safely. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No committed partial session/password/audit; correct rollback/unknown message
- **Actual:** {"reason":"Không gây lỗi instance SQL dùng chung; chưa có cơ sở dữ liệu fault-injection cô lập được duyệt."}
- **Result:** **BLOCKED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-01; before/after SQL và HTTP nằm trong cùng record.

## PLAN-02 — Concurrent wrong-password requests

- **UC:** UC-001. **GBR:** GBR-020. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Concurrent wrong-password requests. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Exact threshold under parallel load
- **Actual:** {"reason":"Đã thử tuần tự và gọi trực tiếp API; chưa thử tải đồng thời."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-02; before/after SQL và HTTP nằm trong cùng record.

## PLAN-03 — Concurrent password change/reset/revoke

- **UC:** UC-003. **GBR:** GBR-021, GBR-022. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Concurrent password change/reset/revoke. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** No stale credential can win race
- **Actual:** {"reason":"Chưa thực thi ca race; kiểm tra source transaction không thay thế chạy concurrency."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-03; before/after SQL và HTTP nằm trong cùng record.

## PLAN-04 — Real-time 30-minute idle expiry

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** MSG01. **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Real-time 30-minute idle expiry. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Real clock expiry follows SYS-PAR-06
- **Actual:** {"reason":"Đã backdate deadline riêng QA và kiểm tra sliding, chưa chờ đủ 30 phút thực."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-04; before/after SQL và HTTP nằm trong cùng record.

## PLAN-05 — All ten Permission Matrix actor columns

- **UC:** UC-001/002/003. **GBR:** GBR-002. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** All ten Permission Matrix actor columns. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** A/OM/O/HK Sup/HK Staff/ML/MS/SK/DH/REC all have auth Full
- **Actual:** {"reason":"Đã thử có SYS_ADMIN và không có role; chưa thử riêng đủ mười actor, ánh xạ role seed và SRS còn lệch."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-05; before/after SQL và HTTP nằm trong cùng record.

## PLAN-06 — Cross-branch/department backend access isolation

- **UC:** UC-011 and business UCs. **GBR:** GBR-002. **MSG:** MSG02. **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Cross-branch/department backend access isolation. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Server denies out-of-scope requests
- **Actual:** {"reason":"Các API nghiệp vụ/permission chưa triển khai, không thể chứng minh server scope enforcement bằng FE gate/404."}
- **Result:** **BLOCKED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-06; before/after SQL và HTTP nằm trong cùng record.

## PLAN-07 — 12-hour continuously active session

- **UC:** UC-001. **GBR:** GBR-021. **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** 12-hour continuously active session. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Continuous activity follows SRS sliding rule
- **Actual:** {"reason":"Source có absolute cap 12h ngoài SRS; chưa chờ/thực thi API ở mốc 12h."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-07; before/after SQL và HTTP nằm trong cùng record.

## PLAN-08 — Exact 8-character and 256/257-character boundary

- **UC:** UC-003. **GBR:** GBR-022. **MSG:** MSG03. **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Exact 8-character and 256/257-character boundary. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Minimum boundary and documented maximum handling
- **Actual:** {"reason":"Đã thử mật khẩu 7 ký tự, các nhóm ký tự và mật khẩu mạnh; chưa chạy toàn bộ boundary dương 8 và 256/257."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-08; before/after SQL và HTTP nằm trong cùng record.

## PLAN-09 — Administrator provisions/resets via UI

- **UC:** UC-004/005. **GBR:** GBR-023, GBR-024. **MSG:** MSG15. **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Administrator provisions/resets via UI. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Authorized one-time temporary credential handoff through UI
- **Actual:** {"reason":"Service đã thử trực tiếp, nhưng không có API/Admin UI nối backend để chạy UC đầy đủ."}
- **Result:** **BLOCKED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-09; before/after SQL và HTTP nằm trong cùng record.

## PLAN-10 — Production TLS/CORS/deployment

- **UC:** N/A. **GBR:** . **MSG:** . **SYS-PAR:** —.
- **Preconditions:** Xem lý do chưa thực thi.
- **Test data:** Không tạo dữ liệu cho ca chưa chạy.
- **Steps:** Production TLS/CORS/deployment. Xem setup/assertion theo ID trong run-qa.cjs hoặc service-probe/Program.cs.
- **Expected:** Approved production key storage and origin policy
- **Actual:** {"reason":"Chỉ kiểm thử Windows Development qua same-origin proxy/cert tự ký."}
- **Result:** **NOT TESTED**.
- **Layers:** {"HTTP":"NOT TESTED","SQL":"NOT TESTED","Frontend":"NOT TESTED"}
- **Evidence:** all-test-cases.json, tìm ID PLAN-10; before/after SQL và HTTP nằm trong cùng record.
