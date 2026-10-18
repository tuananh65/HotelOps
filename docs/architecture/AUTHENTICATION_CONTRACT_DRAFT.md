# HotelOps Authentication Contract DRAFT

- Phiên bản: v1 DRAFT, tổng hợp T1-B2; rà soát MINIMIZE DATABASE SCHEMA CHANGES.
- Ngày cập nhật: 2026-10-18.
- Trạng thái: **DRAFT — DEV1 quyết định toàn bộ Authentication; các chi tiết PROPOSED do DEV1 tự chốt. Nhiệm vụ hiện tại chỉ cập nhật tài liệu, chưa thực thi implementation.**
- Phạm vi: FE-01; UC-001 Login, UC-002 Logout, UC-003 Change Password; tích hợp thu hồi session khi Admin Reset/deactivate account.
- Giới hạn nhiệm vụ hiện tại: chỉ sửa tài liệu, không SQL/source/EF/frontend/Git mutations. Quy trình mới không yêu cầu xin duyệt kỹ thuật Authentication từng bước; không suy diễn Permission Matrix đã được phê duyệt.

## Quy ước trạng thái và nguồn tham chiếu

| Nhãn | Ý nghĩa |
|---|---|
| CONFIRMED | Quyết định/quyền sở hữu đã được developer xác nhận. |
| DEV1 DECIDED | DEV1 đã chốt phạm vi/giải pháp được ghi rõ; không tự mở rộng sang policy chưa chốt. |
| PROPOSED — DEV1 TO DECIDE | Đề xuất kỹ thuật Authentication để DEV1 tự quyết và ghi nhận trước khi triển khai phần phụ thuộc; không cần DEV2/FE/PM duyệt từng bước. |
| TEAM INTEGRATION / HANDOFF | Nội dung phối hợp/thông báo DEV2/FE, không phải approval gate cho DEV1. |
| OUT OF SCOPE — RBAC REQUIREMENTS PENDING | Permission Matrix, phân quyền vai trò và business scope chưa chốt, không được tự suy diễn qua quyền quyết định Authentication. |

**Quy trình hiện hành:** DEV1 thiết kế, chọn giải pháp, triển khai và kiểm thử toàn bộ Authentication. Quyết định mới thay thế yêu cầu xin DEV2/FE/PM phê duyệt từng quyết định kỹ thuật Authentication trong các bản trước và quy trình repo trước đó ở đúng phạm vi này. DEV1 tự chốt các đề xuất, ghi decision log; không tự coi mọi giá trị đề xuất đã được duyệt. Sau khi hoàn thành chức năng và trước khi merge thay đổi dùng chung, DEV1 gửi báo cáo tổng hợp contract/tác động/kiểm thử/deployment cho team. Handoff không phải vòng xin duyệt từng bước; quy tắc Git/merge và quyền nghiệp vụ ngoài Authentication vẫn giữ riêng.

Nguồn:

- [AGENTS.md](../../AGENTS.md).
- [SRS Change Notes](../requirements/SRS_CHANGE_NOTES.md).
- [SRS draft](../requirements/HotelOps_SRS_DRAFT.md), đặc biệt UC-001–UC-003, GBR-019–GBR-023 và SYS-PAR-01–SYS-PAR-06.
- [Use Case Specifications](../requirements/srs_sections/02_use_case_specifications.md), [Requirement Appendix](../requirements/srs_sections/05_requirement_appendix.md).
- [HotelOpsDbContext](../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), [User](../../src/HotelOps.Api/Entities/User.cs), [UserSession](../../src/HotelOps.Api/Entities/UserSession.cs), [AuditLog](../../src/HotelOps.Api/Entities/AuditLog.cs).
- Tóm tắt T0/T1-A/T1-B1 do developer cung cấp trong hội thoại; chưa có báo cáo riêng trong repository. Kết quả kết nối SQL ở T0 không phải kết quả kiểm chứng runtime của tài liệu này.

## 1. Confirmed Decisions

**CONFIRMED:**

- DEV1 sở hữu Cookie Authentication, Login/Logout/Current User API, `user_sessions`, session lifecycle, middleware/configuration, cookie policy, expiry/revocation và contract authentication với frontend.
- DEV2 sử dụng authentication contract do DEV1 cung cấp; không xây refresh-token flow riêng.
- `user_sessions` là bảng session chung cho Cookie Authentication.
- Không có yêu cầu bảo toàn session mẫu/cũ.
- DEV1 quyết định schema/EF cần thiết cho Authentication theo phương án B; tổng hợp tác động và thông báo team sau khi hoàn thành chức năng, trước khi merge thay đổi dùng chung.
- HotelOps chỉ có frontend web; deployment topology chưa được quyết định.
- DEV1 là người quyết định toàn bộ Authentication, chịu trách nhiệm thiết kế, kỹ thuật, implementation và kiểm thử; nhiệm vụ này chỉ chuẩn bị tài liệu.
- PROJECT-WIDE CONSTRAINT: MINIMIZE DATABASE SCHEMA CHANGES; ưu tiên tận dụng schema hiện tại, không hy sinh bảo mật để giảm cột.

**DEV1 DECIDED** (baseline do developer xác nhận, chưa thực thi trong nhiệm vụ này):

- Đổi tên user_sessions.refresh_token_hash thành session_key_hash; giữ kiểu/nullability/unique trừ lý do kỹ thuật bắt buộc được DEV1 đánh giá, ghi rõ và đưa vào báo cáo thay đổi.
- Bổ sung users.failed_login_count, users.locked_until, users.temporary_password_expires_at.
- Shared Authentication Service, CurrentUserAccessor và SessionGuard do DEV1 sở hữu, DEV2 sử dụng chung.
- Lifetime, lockout thresholds, temporary-password policy, CSRF và API details vẫn là đề xuất để DEV1 tự chốt; không còn chờ duyệt kỹ thuật từ DEV2/FE/PM.

Hiện trạng theo source và tóm tắt khảo sát:

- `user_sessions.refresh_token_hash` là NOT NULL; có expiry/revocation/activity timestamps.
- T0: SQL Server có `role_permissions.data_scope` nhưng EF chưa ánh xạ; chưa có cấu trúc chuyên biệt cho lockout, temporary password và concurrency.
- T1-A ưu tiên ASP.NET Core Cookie Authentication + SQL Server session validation.
- T1-B1 chưa tìm thấy consumer backend/SQL thực tế của `user_sessions`; không suy ra rằng không có consumer bên ngoài repository.
- Frontend snapshot có code authentication integration nhưng chưa xác nhận source hiện hành hoặc contract chính thức.
- Permission Matrix, Reception permissions và chi tiết Department Head scope vẫn pending theo Change Notes.

## 2. Proposed SQL Schema

### 2.1. Ràng buộc và khuyến nghị mới

**CONFIRMED — PROJECT-WIDE CONSTRAINT: MINIMIZE DATABASE SCHEMA CHANGES.** Ưu tiên database hiện tại trong toàn bộ phát triển; chỉ đề xuất thay schema khi thực sự cần cho nghiệp vụ/bảo mật. DEV1 chốt mọi thay đổi cần thiết trong phạm vi Authentication; chỉ mở rộng ngoài B khi có lý do bắt buộc được ghi nhận.

**DEV1 DECIDED: phương án B — Minimal Database Changes: đổi tên user_sessions.refresh_token_hash thành session_key_hash và bổ sung ba trường credential lifecycle trên users.** Giữ nguyên kiểu dữ liệu, nullability và tính unique hiện có của cột hash, trừ lý do kỹ thuật bắt buộc được giải thích và DEV1 chốt và ghi nhận. Không giữ thiết kế mở rộng C làm baseline triển khai.

### 2.2. user_sessions: chỉ đổi tên cột hash

| Cột hiện có / tên đích | Cách dùng và giới hạn thay đổi |
|---|---|
| session_id | Khóa bigint nội bộ; không dùng ID tuần tự làm credential. |
| user_id | Giữ FK; khớp user của protected ticket. |
| refresh_token_hash → session_key_hash | Đổi tên để lưu hash thật của session credential; giữ varchar(255), NOT NULL, unique hiện có. DEV1 DECIDED. |
| created_at | UTC lúc tạo, bất biến khi sliding; dùng tính cap nếu DEV1 chốt policy. |
| expires_at | Idle deadline hiện tại, có thể bị chặn bởi cap đã duyệt; giữ nguyên tên. |
| last_used_at nullable | Session mới gán created_at, touch không lùi; không ALTER nullability. Row cũ NULL không tự được coi là credential hợp lệ. |
| revoked_at | NULL chưa revoke, timestamp đã revoke; không bao giờ reset về NULL. |
| device_info | Giữ metadata hoặc NULL; không nhét state restricted/lockout vào đây. |
| ip_address, user_agent | Giữ metadata; không ràng buộc xác thực cứng vào IP/UA. |

Sinh 32 byte CSPRNG; raw credential Base64URL chỉ nằm trong protected authentication ticket. Đề xuất session_key_hash lưu chuỗi canonical sha256: + 64 ký tự hex lowercase của SHA-256 trên byte gốc (71 ký tự, phù hợp varchar(255)). Giữ unique index hiện có; collision thì sinh lại. Không dùng giá trị giả, fixed placeholder, password hash hoặc session ID thay secret. Password cần password hasher riêng. Không log raw credential/hash/cookie/CSRF token.

**DEV1 DECIDED / TEAM INTEGRATION / HANDOFF (DEV2):** đổi tên cột và semantics sang session credential; format/version marker vẫn PROPOSED. Đề xuất EF property RefreshTokenHash → SessionKeyHash, HasColumnName("session_key_hash"), cập nhật HasIndex và mọi tham chiếu. Không có refresh endpoint; không duy trì mapping tên cũ sau cutover.

Trước rollout xác minh constraints/triggers, consumer ngoài repo và data format. Không chạy refresh-token consumer cũ cùng cookie consumer trên tập session này. Session cũ không cần bảo toàn nhưng invalidation/cutover vẫn cần duyệt riêng; marker không tự chứng minh credential mới, phải có protected ticket đúng scheme/purpose/version. Không chuyển sample rows thành login hợp lệ. Cookie bị đánh cắp vẫn replay được đến khi hết hạn/thu hồi; SQL validation mỗi request chặn sau revoke.

### 2.3. Đánh giá các cột mở rộng

| Cột trong C | B cần thêm? | Thay thế an toàn / giới hạn |
|---|---|---|
| absolute_expires_at | Không | Nếu DEV1 chốt, deadline/policy snapshot trong protected ticket tính từ created_at; validate SQL và ticket mỗi request; renewal không kéo dài cap. Nhu cầu admin query/chỉnh deadline độc lập ticket tương lai mới cần review thêm. |
| is_restricted | Không | Derive từ users.temporary_password_expires_at non-NULL; protected restricted claim hỗ trợ fail closed khi mismatch. Mọi credential change revoke/rotate session cũ. |
| auth_version ở users/session | Không bắt buộc | User-row transaction lock serialize login/change/reset/revoke-all, recheck password hash sau khóa, revoke rows atomic và SQL validation mỗi request. Mọi writer phải theo protocol. |
| revocation_reason | Không | revoked_at quyết định validity; lý do ghi audit cùng transaction đúng mục đích traceability, không đọc audit làm operational state. |
| row_version ở users/session | Không bắt buộc | Conditional update, affected-row checks, transaction/SQL locks; không SaveChanges snapshot cũ ghi đè state. Cần concurrency tests thực. |

session_key_hash là rename cột cũ, không phải thêm cột song song hoặc đổi sang binary(32). Không thêm idle_expires_at, không bỏ device_info, không sửa kiểu timestamp/nullability. Giữ unique index hiện có trên cột hash sau rename đủ lookup. Index user_id/revoked_at chỉ đề xuất sau đo execution plan/volume/lock behavior, không mặc định bắt buộc; thêm index vẫn là schema change cần duyệt.

### 2.4. Cấu trúc tối thiểu còn thiếu cho SRS

Theo entities/T0, cần dữ liệu bền vững qua restart/multi-instance cho GBR-020/023; không lạm dụng audit_logs, account_status, updated_at, device_info hoặc system settings làm per-user credential store.

| Rule | Trường mới tối thiểu trong B | Semantics |
|---|---|---|
| Consecutive failed attempts | users.failed_login_count int NOT NULL DEFAULT 0 | Atomic increment/reset; rate limiting không thay thế counter SRS. |
| Timed lockout | users.locked_until datetime2(7) NULL | Unlock deadline, độc lập account status. |
| Temporary credential + expiry | users.temporary_password_expires_at datetime2(7) NULL | NULL: credential thường; non-NULL: temporary, kể cả khi expired. Chỉ clear khi đổi password thường thành công. |

**DEV1 DECIDED:** DEV1 chấp nhận ba cột users. Kiểu/default/check dưới đây vẫn là chi tiết để DEV1 tự chốt; lockout thresholds và temporary-password policy chưa vì vậy được DEV1 tự động chốt. Ba dữ liệu cần để đáp ứng các rule SRS tương ứng khi DEV1 chốt, không phải kết quả thực thi DDL trong nhiệm vụ này. Nếu khảo sát sau tìm thấy cấu trúc chuyên dụng tương đương thì tái sử dụng. Bảng credential-state riêng là lựa chọn khác nhưng thêm PK/FK/mapping, không ít thay đổi hơn ba fields trong baseline này.

Không cần must_change_password riêng cho yêu cầu temporary-only: expiry non-NULL đồng thời phân loại credential và lưu thời hạn; SessionDto.mustChangePassword được derive. Không clear expiry khi tự hết hạn, vì sẽ biến expired temporary password thành password thường. Force-change cho password thường không expiry hoặc one-time temporary redemption là yêu cầu bổ sung cần review, không giả định đã hỗ trợ.

### 2.5. Target delta DDL — KHÔNG THỰC THI

Chỉ biểu diễn cấu trúc đích tối thiểu; không phải migration/cutover script hoàn chỉnh trên database hiện hữu. Chưa có backfill credential hoặc script dependencies/rollout/rollback hoàn chỉnh. Phần rename bên dưới là lệnh đề xuất để review, tuyệt đối không thực thi trong nhiệm vụ tài liệu.

~~~sql
-- B: DEV1 DECIDED BASELINE — DOCUMENTATION ONLY, DO NOT EXECUTE
EXEC sys.sp_rename
    N'dbo.user_sessions.refresh_token_hash',
    N'session_key_hash',
    N'COLUMN';

-- Target additions; types/defaults/check also require technical review
ALTER TABLE dbo.users ADD
    failed_login_count int NOT NULL
        CONSTRAINT DF_users_failed_login_count DEFAULT (0),
    locked_until datetime2(7) NULL,
    temporary_password_expires_at datetime2(7) NULL;

ALTER TABLE dbo.users ADD
    CONSTRAINT CK_users_failed_login_count
        CHECK (failed_login_count >= 0);
~~~

Default expiry NULL không chứng minh sample/legacy password hợp lệ. Trước rollout cần inventory/provision-reset plan; không tự suy sample là temporary credential. Không tạo EF migration theo tài liệu này.

### 2.6. So sánh A / B / C

| Tiêu chí | A: Không đổi database | B: Thay tối thiểu — khuyến nghị | C: Mở rộng ban đầu — không chọn baseline |
|---|---|---|---|
| Cookie/session/idle/revoke | Đủ bằng hash thật, timestamps, locks | Đủ như A | Đủ, nhiều state explicit |
| Full GBR-020/023 draft | Thiếu persistent lockout/temp metadata; không được gọi full SRS | Đủ dữ liệu cho counter/deadline/temp-restricted khi DEV1 chốt rule Authentication | Đủ, thêm flags/version riêng |
| Bảo mật | Core session có thể an toàn; không được bỏ rule để giữ 0 SQL | Giữ CSRF/hash/revoke; đòi kỷ luật khóa mọi writer | Version/rowversion hữu ích nhưng không thay transactions/validation |
| SQL/EF | 0 thay đổi | user_sessions rename 1 cột, giữ type/NOT NULL/unique; users thêm 3 fields + default/check; UserSession/User/DbContext và consumer references cập nhật | Session: thay hash, rename expiry, bỏ device_info, thêm 5 fields, đổi nullability/type/indexes; users thêm 6 fields; nhiều mappings |
| Absolute/restricted | Cap ticket nếu DEV1 chốt; thiếu temp state cho restricted theo SRS | Cap ticket + created_at; restricted derive temp expiry | SQL deadline/flag explicit |
| Phức tạp | Core vừa phải; external state store mới cần DEV1 đánh giá/chốt nếu muốn full SRS mà không SQL, không giả định có sẵn | Ít cutover, protocol concurrency nghiêm ngặt, contention per-user cần đo | Nhiều cutover/backfill/schema; thuận lợi truy vấn/admin/optimistic concurrency |
| BE2 | Review legacy hash semantics/shared middleware | Review rename breaking change + 3 users fields, shared service và lock/transaction protocol | Tác động lớn tới session/users mappings, tooling/deployment |

C cụ thể: session thêm absolute_expires_at, is_restricted, auth_version, revocation_reason, row_version; thay refresh_token_hash bằng session_key_hash; rename expires_at; bỏ device_info. Users thêm auth_version, failed_login_count, locked_until, must_change_password, temporary_password_expires_at, row_version. Chỉ lưu C làm phương án so sánh lịch sử, không còn DDL C trong đề xuất triển khai.

**B — DEV1 DECIDED:** Minimal Database Changes theo quyết định DEV1; rename có thêm chi phí đồng bộ so với B cũ giữ tên legacy, được DEV1 chấp nhận để phản ánh đúng credential semantics. Đây là baseline review tối thiểu trong phạm vi quyết định mới để đáp ứng đầy đủ lockout/temp rules khi DEV1 chốt và bảo toàn security requirements. A đủ core session, chưa đủ full SRS; C chưa có nhu cầu bắt buộc biện minh. Không tự coi SRS draft hoặc 12h/15m đã được DEV1 chốt.

### 2.7. EF Database First, index/constraint và consumers

**PROPOSED; TEAM INTEGRATION / HANDOFF (DEV2).** Rename là breaking change cho backend đang query tên cũ, dù giữ nguyên dữ liệu/type/nullability/unique.

- EF Database First: chuẩn bị diff UserSession.RefreshTokenHash → SessionKeyHash; mapping HasColumnName và biểu thức HasIndex theo property mới; giữ HasMaxLength(255), IsUnicode(false), requiredness và IsUnique. User thêm ba property/mapping tương ứng. Chỉ cập nhật sau khi DEV1 chốt chi tiết triển khai, không scaffold toàn bộ repo hoặc tạo EF migrations theo tài liệu này; review diff tránh kéo mapping unrelated, đặc biệt data_scope conflict.
- Unique index/constraint hiện tại cần giữ cùng definition/key column identity và uniqueness; rename cột không phải yêu cầu drop/recreate index. Tên constraint/index legacy có thể vẫn chứa tên cũ hoặc tên sinh tự động; giữ nguyên để giảm changes, mapping HasDatabaseName/constraint name theo SQL thực tế. Không đoán tên mới, không rename index chỉ vì naming. Kiểm tra metadata trước/sau và insert duplicate trong test DB để xác minh uniqueness còn hiệu lực.
- Kiểm tra dependencies trước rename: views, stored procedures/functions, triggers, computed/check expressions, jobs/ETL/report SQL, scripts, dynamic SQL, raw SQL/Dapper, seed/test fixtures, EF LINQ/property refs và ứng dụng ngoài repo. Không giả định sp_rename tự sửa nội dung mọi SQL module hoặc SELECT * metadata; dependency phải được cập nhật/refresh và verify trong script review riêng theo loại object.
- Không thêm compatibility duplicate column, view, trigger hoặc index mặc định; nếu bắt buộc hỗ trợ mixed versions thì đó là thiết kế khác cần review schema impact, không âm thầm mở rộng B.
- Frontend DTO không expose tên SQL nên không cần rename API field theo cột. CSRF/API vẫn pending FE review. DEV2 dùng service/accessor/guard, không query tên cũ trực tiếp.

### 2.8. Rollout đồng bộ database/backend và rollback để review

**PROPOSED — chỉ thực hiện sau khi DEV1 chốt chi tiết triển khai.** Ưu tiên coordinated maintenance cutover, vì rename không tương thích backend cũ. Không rolling deploy mixed versions trên database đã rename.

1. Inventory dependencies/instances/jobs, ghi lại metadata/constraint/index names, backup/restore plan và baseline credential state. DEV1 chốt schema delta, script dependencies, backend artifact, session invalidation và rollback; thử trên SQL test/staging trước production.
2. Chuẩn bị backend artifact khớp schema đích bằng workflow Database First được duyệt ở môi trường phù hợp; review diff, build/tests và smoke tests. Không đợi production rename xong mới bắt đầu chuẩn bị code.
3. Bật maintenance/chặn auth và dependent business writes, drain in-flight requests, dừng mọi old backend instance/consumer/job truy cập cột cũ. Không để process cũ reconnect giữa cutover.
4. Với script riêng đã duyệt, kiểm tra preconditions, thêm ba users fields/default/check và rename hash column; sửa dependencies bắt buộc. Dùng transaction cho DDL tương thích trong phạm vi đã thử nghiệm; không giả định cả deployment/app restart/backup cùng rollback bằng một SQL transaction. Không đổi kiểu/nullability/unique của hash.
5. Vô hiệu session/cookie cũ theo kế hoạch được duyệt, không convert sample credentials; xác minh temp/regular credential inventory. Không bật temp lifecycle nếu dữ liệu cũ chưa được phân loại an toàn.
6. Deploy toàn bộ backend artifact mới và dependent consumers; verify schema/EF alignment, uniqueness, no old-column queries, login/session/logout/change-password, lockout/temp state, SQL-outage 503 và DEV2 guard. Mở traffic sau khi checks đạt; theo dõi lỗi/lock contention.

Rollback phân biệt hai thời điểm:

- Trước khi mở traffic/new credential writes: giữ maintenance, rollback DDL transaction nếu còn trong transaction; nếu đã commit thì dùng script reverse đã duyệt đổi session_key_hash về refresh_token_hash, phục hồi dependency definitions và deploy đồng bộ artifact cũ. Ba users fields có thể để lại nếu artifact cũ tương thích; không tự DROP chỉ để schema giống cũ. Restore backup chỉ theo kế hoạch do DEV1 chốt và ghi nhận.
- Sau khi có login/reset/change-password writes: không rollback app đơn thuần, không chỉ rename ngược rồi cho consumer refresh-token cũ đọc session-key hashes. Dừng traffic/drain, revoke các session đã phát theo script đã duyệt; giữ password hash mới, failed counter/locked_until/temp expiry. Ưu tiên roll-forward hoặc rollback artifact đã được kiểm chứng vẫn enforce cùng security state. Nếu artifact cũ bỏ qua lockout/temp restriction hoặc không hỗ trợ hasher mới, không mở lại authentication với artifact đó.
- Rename ngược không đổi hash semantics; khôi phục format/consumer behavior cũ phải được chứng minh riêng. Không tự re-enable refresh flow, không tái kích hoạt session đã revoke. Client có thể phải login lại; không có yêu cầu bảo toàn session cũ.
- Không drop ba users fields/default/check khi có state còn cần bảo vệ. Rollback schema hủy dữ liệu credential-state hoặc restore backup mất password changes cần đánh giá riêng; không là rollback mặc định. Ghi nhận audit/backup consistency, access restrictions và kiểm chứng lại trước mở traffic.

DEV1 chịu trách nhiệm chốt và kiểm chứng rollback SQL/EF/source; nhiệm vụ hiện tại chỉ ghi thiết kế, không thực hiện bất kỳ bước nào.

## 3. Session Lifecycle

**PROPOSED theo B**, ownership/ràng buộc hạn chế schema là CONFIRMED.

### 3.1. Login

1. Validate CSRF/DTO, trim username nhưng giữ password; giải quyết collation/identifier collision rõ ràng.
2. Bắt đầu transaction ReadCommitted, resolve username/email theo SQL collation; không có hoặc ambiguous thì dùng đường dummy verification chung.
3. CredentialLockoutService khóa users bằng UPDLOCK, HOLDLOCK rồi verify hash mới nhất, eligibility, lock/temp state; giữ lock đến commit. Không verify ngoài lock hoặc tự retry credential state cũ.
4. Increment counter khi sai hoặc reset khi thành công theo policy, kiểm tra lock/temp expiry. Create session với created_at=last_used_at=UTC now, expires_at theo idle/cap đã được DEV1 chốt, revoked_at=NULL; audit và last_login_at cùng transaction.
5. Commit rồi phát protected cookie; restricted claim derive từ temp state; cap/policy snapshot chỉ khi được duyệt.

Response/cookie issuance mất sau commit có thể để session không được dùng, tồn tại đến expiry/revocation; không thể atomic SQL commit với HTTP delivery. Task 9A không retry/compensate tự động. DEV1 DECIDED: trình duyệt có normal session hợp lệ bị chặn Login 409, phải Logout trước chuyển tài khoản; restricted guard vẫn chặn Login 403. Không tự revoke session thiết bị khác; không bổ sung chính sách giới hạn số session. Login và reset lấy cùng user lock: session tạo trước reset bị revoke, login sau reset phải verify credential mới, không cần auth_version.

### 3.2. Validation và conditional touch mỗi request

ValidatePrincipal verify/decrypt ticket rồi kiểm tra SQL. Baseline correctness: transaction ngắn, user lock trước session lock/update, serialize theo user bằng UPDLOCK/HOLDLOCK; phải kiểm chứng isolation thực tế, không chỉ BeginTransaction mặc định/in-process lock.

- Canonical session_key_hash khớp đúng user; revoked_at IS NULL; database now < expires_at.
- User chưa deleted, account eligibility hợp lệ. Derive restricted từ temp expiry; expired temporary bị chặn.
- Restricted claim và user state mismatch thì fail closed, không tự nâng quyền; mọi credential transition revoke/rotate.
- Nếu cap đã được DEV1 chốt, verify protected deadline từ created_at/lifetime snapshot; renewal giữ nguyên cap.
- Conditional update last_used_at/expires_at với unrevoked/unexpired predicates, kiểm tra affected rows; zero thì reject/recheck, không mặc định thành công. Không ghi các fields stale khác.
- Lấy database UTC sau khi có locks, không để activity time lùi. Không cache valid result qua requests. Async/CancellationToken; AsNoTracking cho read-only phù hợp.

Với unsafe methods, chỉ touch sau security/CSRF checks; nếu validation và touch tách pha phải revalidate dưới transaction touch. Không có phép touch nào clear revoked_at.

### 3.3. Sliding, absolute và background polling

| Policy | Đề xuất / nguồn | Trạng thái |
|---|---|---|
| Normal idle | 30 phút | DEV1 DECIDED / Task 7 |
| Normal absolute | 12 giờ từ created_at, không cần cột mới | DEV1 DECIDED / Task 7 |
| Restricted absolute/idle | Tối đa 15 phút từ created_at, không vượt temporary expiry | DEV1 DECIDED / Task 7 |
| Remember me | Không có v1 | DEV1 DECIDED / Task 7 |

last_used_at lấy max(current activity UTC, last_used_at hoặc created_at). expires_at = last_used_at + idle timeout DEV1 đã chốt, cap bởi protected absolute deadline nếu DEV1 chốt và temp expiry khi temporary. Không cập nhật created_at, không hồi sinh expired/revoked rows.

GBR-021 draft nói mỗi valid request kéo dài idle: baseline không throttle. Background polling có credential cũng kéo dài dù không có UI interaction; không thể hứa 30 phút không thao tác sẽ logout trong khi polling sống. Không heartbeat chỉ để giữ phiên. Loại polling/đổi activity cần TEAM INTEGRATION / HANDOFF (FE) / TEAM INTEGRATION / HANDOFF (DEV2) / PROPOSED — DEV1 TO DECIDE; server-owned policy, không tin client header. Review session/bootstrap/dashboard/notification polling cùng nhau.

Nếu absolute cap đã được DEV1 chốt: SlidingExpiration=false, ticket expiry=cap; SQL quản sliding idle. Nếu không dùng absolute cap, cần review cookie renewal riêng giữ SQL làm nguồn idle validity; không ticket 30 phút cố định khiến hết sớm, không mặc định built-in half-window sliding tương đương mỗi request. Test renewal song song/out-of-order. Không thêm cột để tránh quyết định policy này.

### 3.4. Logout / revocation

User lock trước session lock; conditional SET revoked_at=now WHERE revoked_at IS NULL và audit cùng transaction, commit rồi xóa cookie. Missing/expired/revoked logout idempotent 204, audit không trùng; SQL failure 503. Revoke-all update mọi row user chưa revoked khi giữ cùng user lock, không cần version/reason columns. Lý do ghi audit đúng mục đích traceability, không đọc audit quyết định validity.

### 3.5. Change Password / Admin Reset / deactivate

UC-003 draft tiếp tục phiên hiện tại bằng rotation: verify input/password/policy; transaction khóa user rồi session, recheck hash snapshot và validity; lưu hash mới, clear temp expiry, revoke tất cả session cũ, tạo normal session mới và audit; commit rồi cookie/CSRF mới. Không auth_version trong B.

Reset: lưu temporary hash/expiry, revoke-all và audit cùng transaction; deactivate: đổi account state hợp lệ và revoke-all atomic. Mọi writer kể cả provisioning/reset/rehash phải theo user-lock protocol; direct SQL ngoài protocol phá cam kết, DEV1 chốt; thông báo team trước merge operational controls. Rehash kỹ thuật vẫn recheck snapshot/locks nhưng không tự coi là user password change để revoke-all.

Không clear temp expiry vì hết hạn; không account_status/audit làm temporary flag. Quyền ai được Admin Reset/deactivate là OUT OF SCOPE — RBAC REQUIREMENTS PENDING; DEV1 triển khai cơ chế credential/session revocation, không tự gán quyền từ matrix draft.

### 3.6. Restricted access không cột is_restricted

User temp expiry non-NULL tạo protected restricted claim. Mỗi request derive SQL state, enforce allowlist GET session, GET csrf, POST change-password, POST logout; business APIs khác 403 PASSWORD_CHANGE_REQUIRED. Temp expired làm session invalid; normal claim không bỏ qua user temporary state. Change-password revoke mọi cookie cũ và cấp normal mới; restricted cookie cũ không tự nâng quyền. Không cần users.must_change_password cho rule temporary-only.

### 3.7. Concurrency không rowversion/auth_version

- Users row serialize login/counters/create/touch/revoke/reset/change/deactivate; luôn user rồi session. Tránh giữ lock khi hash computation dài; recheck hash sau khóa.
- SQL locks/transaction + predicates/affected rows kiểm soát lost update; không dùng timestamps làm version, không SaveChanges stale snapshots.
- Multi-instance dùng SQL locks chung; test isolation thực/deadlocks/retry/cancellation. Giữ transaction ngắn, không external network dưới lock. Contention per-user cần load test; tối ưu protocol phải chứng minh lại correctness.
- Hai change-password chỉ một commit; request thứ hai bị revoked và 401/conflict. Login verify cũ sau reset fail recheck; touch không hồi sinh revoke.
- Validation sau revocation commit bị chặn, không hủy ngược request đã qua kiểm tra. Sensitive DEV2 mutation cần SessionGuard trong cùng transaction giữ user lock đến commit; TEAM INTEGRATION / HANDOFF (DEV2).
- Retry cả transaction với revalidation; commit outcome không rõ không retry mutation mù quáng/nhân đôi audit. Không thêm rowversion để che thiếu protocol.

## 4. Login / Logout / Current User / Change Password APIs

**PROPOSED; TEAM INTEGRATION / HANDOFF (FE).** JSON camelCase; bigint trả string; timestamp ISO 8601 UTC hậu tố Z; auth responses no-store; errors application/problem+json. Không trả EF entity hoặc access/refresh/session credential. Không redirect HTML trên /api.

### 4.1. Core Authentication DTO tối thiểu

```ts
type AuthUserDto = {
  id: string;
  username: string;
  name: string;
};

type SessionDto = {
  user: AuthUserDto;
  expiresAt: string;          // idle expiry tại thời điểm response
  absoluteExpiresAt: string; // cap UTC bắt buộc; không đòi cột SQL
  mustChangePassword: boolean;
  csrfToken: string;          // token cho identity hiện tại
};
```

Core DTO không chứa roleNames, grants, branches, departments hoặc data scope. Email/avatar/profile có thể bổ sung theo yêu cầu FE, không bắt buộc cho authentication tối thiểu. `mustChangePassword` phục vụ credential lifecycle, không phải business permission.

### 4.2. Authorization extension riêng — ngoài phạm vi Authentication

**OUT OF SCOPE — RBAC REQUIREMENTS PENDING.** Tách read model authorization khỏi core authentication, ví dụ endpoint tương lai GET /api/auth/authorization; route và DTO chưa chốt.

- Có thể cung cấp role labels, effective permissions/use cases và scope đã phê duyệt.
- Không coi AccessGrantDto/grants trong T1-B2 hoặc frontend snapshot là contract RBAC cuối cùng.
- Cần xác nhận mapping permission → use case, data_scope, branch/department access, expiry/revocation và quyền global.
- Không dùng null ngầm mang nghĩa “tất cả” khi chưa thống nhất; không suy Department Head chỉ quản lý một department.
- Restricted session không được gọi authorization/business APIs ngoài allowlist.
- FE grants chỉ điều khiển hiển thị; backend vẫn authorize từng request với dữ liệu hiện tại.

Authentication có thể được kiểm thử độc lập; chưa có authorization extension không đồng nghĩa RBAC hoặc business navigation đã nghiệm thu.

### 4.3. Endpoint matrix

| Endpoint | Authentication | CSRF | Thành công |
|---|---|---|---|
| GET /api/auth/csrf | Anonymous hoặc current identity | Không cần request token | 200 CsrfDto |
| POST /api/auth/login | Không bắt buộc | Bắt buộc | 200 SessionDto + cookie |
| GET /api/auth/session | Normal/restricted hợp lệ | Không | 200 SessionDto |
| POST /api/auth/logout | Không bắt buộc session còn hiệu lực | Bắt buộc | 204 |
| POST /api/auth/change-password | Normal/restricted hợp lệ | Bắt buộc | 200 SessionDto + cookie mới |

GET session chính là Current User/session API v1; không đề xuất thêm /me trùng chức năng.

### 4.4. Login

```json
{ "username": "username-or-email", "password": "..." }
```

DEV1 DECIDED / Task 9A: username required, trim, tối đa 150 ký tự đầu vào; password required, tối đa 256, không normalize/truncate. Không nhận role/branch hoặc rememberMe. API/DTO Login và Current User triển khai tại Task 9A; Logout/Change Password đã bổ sung tại Task 9B. Chi tiết authoritative tại 10.7–10.8.

| HTTP | Code | Điều kiện |
|---|---|---|
| 400 | VALIDATION_FAILED | Input thiếu/sai |
| 400 | CSRF_VALIDATION_FAILED | CSRF thiếu/sai |
| 401 | INVALID_CREDENTIALS | Unknown, wrong password, inactive/deleted; cùng thông báo |
| 423 | ACCOUNT_LOCKED | Chỉ sau password đúng; parameters.lockedUntil |
| 403 | TEMPORARY_PASSWORD_EXPIRED | Temporary password đúng nhưng hết hạn |
| 429 | AUTH_RATE_LIMITED | Có Retry-After |
| 503 | AUTH_SERVICE_UNAVAILABLE | Không thể xác minh/lưu session |

DEV1 DECIDED / Task 9A: 423 ACCOUNT_LOCKED và parameters.lockedUntil chỉ sau supported password đúng + account eligible. Sai password vẫn 401. Session normal đang hợp lệ: 409 AUTHENTICATED_SESSION_EXISTS; restricted vẫn 403 PASSWORD_CHANGE_REQUIRED theo guard. 429 là contract tương lai: chưa triển khai rate limiting trong Task 9A.

### 4.5. Session / Current User

GET không body; 200 SessionDto. 401 AUTHENTICATION_REQUIRED nếu không có usable ticket; 401 SESSION_INVALID nếu ticket đọc được nhưng SQL session missing/expired/revoked, restricted-state mismatch hoặc user không còn hợp lệ. Không cam kết phân biệt expired/revoked cho mọi cookie vì handler có thể reject ticket trước SQL. SQL unavailable trả 503, không 401. B không có auth_version để kiểm tra.

### 4.6. Logout

DEV1 DECIDED / IMPLEMENTED Task 9B. POST không body, không bind session ID từ request; 204 và xóa auth cookie sau commit revoke, hoặc khi đã xác định anonymous/invalid. 400 CSRF_VALIDATION_FAILED; 503 nếu SQL không xác định được validity/revocation/commit. Anonymous logout vẫn validate CSRF. Nếu identity thay đổi khiến token cũ không hợp lệ, bootstrap lại trước khi thực hiện lại. Không Touch Logout; chi tiết 10.8.

### 4.7. Change Password

```json
{
  "currentPassword": "...",
  "newPassword": "...",
  "confirmNewPassword": "..."
}
```

DEV1 DECIDED / IMPLEMENTED Task 9B. 200 SessionDto normal + cookie mới sau commit; revoke toàn bộ session cũ kể cả session hiện tại. Mật khẩu mới 8–256 ký tự UTF-16, hoa/thường/số/special, confirmation ordinal, không trùng credential hiện tại; không truncate/normalize. Các lỗi: 400 VALIDATION_FAILED/CURRENT_PASSWORD_INVALID/PASSWORD_POLICY_VIOLATION/CSRF_VALIDATION_FAILED; 401 AUTHENTICATION_REQUIRED/SESSION_INVALID; 409 AUTH_STATE_CHANGED; 503 AUTH_SERVICE_UNAVAILABLE. 429 AUTH_RATE_LIMITED vẫn là contract tương lai, chưa triển khai rate limiting. Chi tiết 10.8; không echo password.

### 4.8. CSRF bootstrap và errors

GET /api/auth/csrf trả:

```json
{ "csrfToken": "...", "headerName": "X-CSRF-TOKEN" }
```

Dùng IAntiforgery.GetAndStoreTokens; antiforgery cookie do server đặt, request token lưu memory FE. Không tiết lộ account existence; rate limit có thể trả 429; service failure có thể trả 503.

```json
{
  "type": "about:blank",
  "title": "Authentication failed",
  "status": 401,
  "code": "INVALID_CREDENTIALS",
  "detail": "Thông tin đăng nhập không hợp lệ.",
  "traceId": "...",
  "errors": {},
  "parameters": {}
}
```

Error codes là đề xuất API mới, không phải permission/status hiện hữu. Mapping MSG của frontend snapshot cần FE review; không thay ngầm code. Không trả SQL exception, stack trace hoặc secret.

## 5. Cookie & CSRF Security

**PROPOSED; cấu hình deployment TEAM INTEGRATION / HANDOFF (DEV2); topology TEAM INTEGRATION / HANDOFF (FE).**

| Thuộc tính | Đề xuất |
|---|---|
| Scheme | HotelOpsCookie |
| Auth cookie | __Host-HotelOps.Auth |
| HttpOnly / Secure | true / Always |
| SameSite | Lax |
| Path / Domain | / ; không set Domain, host-only |
| Persistence | IsPersistent=false |
| Ticket expiry | Theo policy DEV1 đã chốt; cap trong protected ticket nếu có; 12h DEV1 chưa chốt |
| Handler SlidingExpiration | false khi có absolute cap DEV1 đã chốt; nếu không, review renewal riêng; SQL quản idle |
| Antiforgery cookie | __Host-HotelOps.Antiforgery; HttpOnly, Secure, Path /, host-only, SameSite Lax |
| CSRF header | X-CSRF-TOKEN |

Browser close không revoke SQL; browser restore có thể khôi phục session cookie. SQL lifetime/revocation vẫn quyết định hiệu lực.

### 5.1. ASP.NET Core pipeline và CSRF identity

- AddAuthentication/AddCookie; UseAuthentication trước UseAuthorization; restricted guard trước business execution.
- Dùng CookieAuthenticationEvents để validate principal và chuẩn hóa challenge/forbid thành ProblemDetails; kiểm thử API không redirect HTML.
- Controller APIs phải validate antiforgery rõ ràng cho unsafe methods (POST/PUT/PATCH/DELETE). Chỉ đăng ký antiforgery service không bảo vệ mọi endpoint tự động.
- Login cũng cần CSRF để chặn login CSRF; bootstrap dùng identity trước login.
- Sau Login/Change Password, `SignInAsync` phát cookie nhưng không được giả định HttpContext.User tự đổi thành principal mới. Trước khi cấp token trong SessionDto, đặt/đảm bảo HttpContext.User là principal mới rồi gọi GetAndStoreTokens.
- Nếu không cấp token trong response theo cách trên, phải dùng flow FE gọi bootstrap sau khi nhận cookie mới; cần chọn một flow nhất quán. Baseline tài liệu: SessionDto trả token cho principal mới.
- Token của identity cũ không được tái dùng sau đổi account/login; FE thay token cùng session state. Sau logout/bootstrap anonymous cần token phù hợp anonymous identity.
- Không tự replay unsafe request chỉ vì CSRF lỗi; kiểm soát kết quả mutation và tránh duplicate.

### 5.2. SQL unavailable khác authentication failed

**PROPOSED:** cookie sai/expired hoặc SQL xác nhận session invalid là 401; SQL timeout/network/unavailability chưa thể xác định validity là 503 AUTH_SERVICE_UNAVAILABLE. Không biến service outage thành INVALID_CREDENTIALS, không xóa cookie hay báo logout thành công giả.

Cơ chế ASP.NET Core dự kiến:

1. Trong ValidatePrincipal/session service, phân biệt kết quả invalid đã xác định với exception hạ tầng. Invalid dùng RejectPrincipal và flow 401; lỗi hạ tầng không RejectPrincipal rồi tiếp tục như anonymous.
2. Chuyển lỗi DB availability/timeout đã phân loại thành exception chuyên biệt (ví dụ AuthServiceUnavailableException); để middleware xử lý lỗi bên ngoài authentication pipeline bắt và ghi ProblemDetails 503, kết thúc request trước controller.
3. Exception handler phải đăng ký trước UseAuthentication để bắt cả lỗi phát sinh từ cookie events; chỉ bắt ở controller không đủ. Không chỉ ghi response trong event rồi để pipeline tiếp tục.
4. Login/logout/change-password service áp dụng cùng mapping; transaction rollback khi lỗi trước commit. Không catch mọi SqlException thành authentication failed hoặc mọi bug thành 503.
5. Log diagnostic có traceId, không credential; lỗi lập trình không thuộc unavailable trả 500 theo error handling. RequestAborted cancellation không tự quy thành SQL outage.
6. EF transient retry nếu dùng phải bao toàn transaction phù hợp; revalidate concurrency/idempotency. Commit outcome không rõ không được tự retry mutation một cách mù quáng.

FE giữ state tạm thời nhưng chặn hành động phụ thuộc server khi 503; cho retry có kiểm soát, không chuyển về login chỉ vì outage. **TEAM INTEGRATION / HANDOFF (FE) / TEAM INTEGRATION / HANDOFF (DEV2).**

### 5.3. Data Protection và deployment

- Persist key ring qua restart/redeploy; các instances cùng môi trường chia sẻ key ring và application name.
- Tách Development/Staging/Production; mã hóa keys at rest, giới hạn quyền đọc, không commit keys.
- Giữ key cũ đủ giải mã ticket còn lifetime; mất keys có thể invalid toàn bộ cookie, không bypass validation.
- Development: HTTPS localhost, frontend proxy /api cùng origin, Secure cookie; SQL connection dùng User Secrets.
- Production: HTTPS/HSTS, reverse proxy, trusted forwarded headers chỉ từ proxy đã cấu hình; shared/persistent keys.

Reference architecture đề xuất, chưa phải deployment đã chọn:

```text
Browser -> https://hotelops.example/
              /       -> frontend
              /api/*  -> ASP.NET Core API -> SQL Server
```

Khác origin sau này: explicit CORS allowlist + credentials, không wildcard; FE credentials include. Cross-origin cùng site có thể giữ Lax; cross-site cần review SameSite=None; Secure và third-party cookie restrictions. CSRF/origin validation vẫn cần; ưu tiên cùng-origin proxy nếu browser chặn cookies. Không tự bổ sung JWT/refresh flow.

Tham chiếu kỹ thuật:

- [Microsoft Cookie Authentication (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
- [Microsoft Antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
- [Microsoft Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

## 6. Frontend Integration

**TEAM INTEGRATION / HANDOFF (FE):** source frontend mới nhất và contract thực tế chưa xác nhận. Chỉ dùng local Git snapshots làm tham khảo; không fetch/checkout/merge hoặc chỉnh sửa frontend.

- origin/fron-end tại 76f1be8 có authentication API integration.
- origin/feature/react-frontend tại 9d220a9 là snapshot khác.
- Trong khảo sát T1-B2, origin/main ref local không có frontend/src/api/types.ts; không coi các refs là cùng một phiên bản hoặc phản ánh remote hiện tại.

| Snapshot tham khảo | Điểm cần FE xác nhận |
|---|---|
| Login {username,password}, /api/auth/login | Giữ route/body; username nhận cả email. |
| /api/auth/session, /api/auth/logout | Giữ route; logout 204 và CSRF bắt buộc. |
| /api/auth/password | Chuyển /api/auth/change-password; không tự thêm alias. |
| PasswordRequest current/new, response void | Thêm confirmation; nhận SessionDto sau rotation. |
| SessionDto.principal có scope/grants | Core DTO đề xuất dùng user tối thiểu; authorization riêng, cần adapter/đổi types. |
| accessToken optional, bearer mode | V1 cookie-only; không refresh flow. |
| credentials include, CSRF header đã có | Bổ sung bootstrap trước login, cập nhật token theo identity mới. |
| primary branch/department không nullable | Profile/authorization extension phải tôn trọng nullable và schema đã xác minh. |
| grants null nghĩa all | Chưa là RBAC contract; review semantics scope riêng. |
| Login navigate /profile | Restricted phải đi Change Password. |
| 403 forbidden chung | Phân biệt PASSWORD_CHANGE_REQUIRED và CSRF_VALIDATION_FAILED. |
| Rate-limit chưa có ErrorKind riêng | Xử lý 429/Retry-After, 423/lockedUntil và 503. |

Flow đề xuất: startup GET session; nếu anonymous lấy CSRF rồi login; lưu core user/session/token trong memory; restricted chỉ change/logout; change-password thay session/token; 401 clear state và về login; 503 không coi là logout; logout 204 clear private cache/token/state.

Nhiều tab/account switch cần broadcast hoặc reload session; response cũ không được khôi phục account vừa logout. Snapshot có sessionRevision nhưng wiring thực tế cần xác nhận. Background polling và việc expiresAt là snapshot, không phải deadline bất biến, phải review cùng sliding policy.

## 7. DEV2 Integration Contract

**CONFIRMED:** DEV2 dùng authentication của DEV1, không tạo refresh-token/session flow riêng.

**DEV1 DECIDED:** Shared Authentication Service, CurrentUserAccessor và SessionGuard do DEV1 sở hữu, DEV2 dùng chung. Interface/transaction ownership, authorization integration và rollout vẫn **TEAM INTEGRATION / HANDOFF (DEV2)**.

**PROPOSED; TEAM INTEGRATION / HANDOFF (DEV2):**

- Shared Authentication Service là entry point chung cho credential/session lifecycle, không nhân bản logic ở module DEV2.
- CurrentUserAccessor cung cấp user ID và session context đã xác minh cho request; không để DEV2 parse cookie hoặc tự quản user_sessions.
- Dùng chung cookie scheme, 401/403/503 ProblemDetails và restricted-session enforcement.
- Mọi unsafe API dùng cookie tuân CSRF contract chung, gồm các module DEV2.
- SessionGuard hỗ trợ revalidation trong transaction nhạy cảm; thống nhất DbContext/transaction ownership, lock ordering và cancellation.
- Authorization evaluator là thành phần riêng; role/scope phải theo rule đã approved, không suy quyền từ identity hoặc frontend grants.
- Audit Authentication do DEV1 quản lý trong audit_logs hiện có; thống nhất action naming/metadata với DEV2 FE-15. Không log secret.
- DEV2 không tự triển khai Admin Reset/revoke-all bằng query rời rạc; dùng lifecycle service của DEV1 để đảm bảo user-lock protocol/revoke-all/transaction, không phụ thuộc auth_version.
- Global middleware/CSRF sẽ ảnh hưởng API DEV2; cần phối hợp rollout để không làm hỏng các writes đang tích hợp.
- SQL outage chặn nghiệp vụ và trả 503, không tiếp tục với principal không xác minh được.

## 8. Implementation Plan

**Kế hoạch implementation do DEV1 thực hiện trong task riêng; không có approval gate kỹ thuật từng bước.** Giữ single project HotelOps.Api; không thêm Application/Domain/Infrastructure projects.

| File/folder dự kiến trong src/HotelOps.Api | Mục đích |
|---|---|
| Controllers/AuthController.cs | HTTP endpoints, cookie/CSRF responses |
| DTOs/Auth/* | Core auth request/response; không EF entity |
| Services/Authentication/* | Credential/session lifecycle, transactions, audit |
| Security/HotelOpsCookieEvents.cs | SQL principal validation, challenge/forbid |
| Security/RestrictedSessionMiddleware.cs | Enforce restricted allowlist |
| Security/CurrentUserAccessor.cs | Context cho DEV2 |
| Security/SessionGuard.cs | Revalidation trong transaction |
| Security/ApiAntiforgeryFilter.cs | CSRF validation/ProblemDetails |
| Security/AuthServiceUnavailableException.cs | Phân loại service outage |
| Middleware/AuthenticationExceptionHandler.cs hoặc error handler đã có | Bắt outage trước authentication và trả 503 |
| Options/AuthenticationOptions.cs | Policy/cấu hình và validation |
| Program.cs | DI/pipeline; DEV1 chốt shared change; handoff trước merge |
| appsettings*.json | Non-secret settings; không connection string cá nhân |
| Entities/User.cs, Entities/UserSession.cs | Mapping 3 users fields và rename property thành SessionKeyHash sau khi DEV1 chốt chi tiết triển khai |
| Data/HotelOpsDbContext.cs | Mapping 3 users fields; SessionKeyHash/HasColumnName/HasIndex cập nhật, giữ type/nullability/unique và index name hiện có |
| HotelOps.Api.csproj | Chỉ khi lựa chọn dependency thực sự cần |

### 8.1. Task triển khai theo thứ tự thực tế

Các task dưới đây thuộc implementation sau nhiệm vụ tài liệu hiện tại. DEV1 không cần xin duyệt kỹ thuật từng bước; mỗi quyết định chưa chốt phải được DEV1 ghi lại trước phần code/test phụ thuộc. Database-first không có nghĩa rename production trước khi chuẩn bị backend artifact.

| Thứ tự | Task / đầu ra | Điều kiện hoàn tất |
|---|---|---|
| 1 | Database Schema: inventory SQL metadata/dependencies/consumers; chuẩn bị và kiểm chứng script rename refresh_token_hash → session_key_hash, thêm 3 users fields/default/check cùng reverse/rollback; áp dụng trên DB development/test trong task được giao | Giữ type/nullability/unique; không thay thêm schema nếu không cần; xác định credential/sample handling; script và backup/rollback notes rõ |
| 2 | EF Core Database First: cập nhật UserSession.SessionKeyHash, User với 3 fields, DbContext/HasIndex/column mappings và consumers liên quan | Diff đúng phạm vi, không migration/architecture refactor; build HotelOps.slnx; không kéo data_scope ngoài scope |
| 3 | Chốt Authentication decision log: hasher/work factor, lockout, temp validity/generation, idle/absolute/restricted lifetime, polling, account eligibility, rate limit và core DTO/errors | DEV1 chọn giá trị cụ thể, đối chiếu SRS và ghi lý do khác biệt; 12h/15m không tự thành mặc định đã chốt |
| 4 | Credential service: hashing/verify/rehash, atomic failed counter/lockout, temporary expiry và password change/reset hooks | Không plaintext logs; không expired temp → regular; không tự gán quyền admin reset |
| 5 | Shared Authentication Service: session create/validate/touch/revoke/rotate, user-lock protocol, transactions, audit, expiry | Concurrency/reset/login/logout tests trên SQL; state không hồi sinh, retry an toàn |
| 6 | Cookie middleware/Data Protection/CSRF/restricted guard/outage handler | Token theo identity mới; SQL unavailable trả 503 và dừng pipeline; keys/config/environment rõ |
| 7 | CurrentUserAccessor + SessionGuard và DI | DEV2 nhận interface/context, cancellation và transaction/locking contract; không tự parse cookie/refresh flow |
| 8 | AuthController, DTO và API contract: login/logout/session/change-password/csrf | Status/error/validation/no-store đúng, entity không exposed, API tests; CSRF mọi unsafe write |
| 9 | Tích hợp/kiểm thử browser với frontend tham chiếu hoặc test client | Xác minh cookie/CSRF, restricted UI flow, 401/403/423/429/503, multi-tab/polling; FE source chưa xác nhận không ngăn backend implementation, phải ghi rõ giới hạn |
| 10 | Regression/security/load validation và final diff | dotnet build HotelOps.slnx; dotnet test HotelOps.slnx khi tests liên quan có sẵn; SQL/concurrency/browser checks, warning/failure/limitations trung thực |
| 11 | Hoàn thiện báo cáo bàn giao và thông báo team sau khi chức năng hoàn thành, trước merge shared changes | Gói contract/SQL/EF/service/FE/test/deployment/breaking changes theo mục 11; không tự commit/push/merge |

Không sửa module khác chỉ để build pass. Không tự tạo project/layer mới. Nếu cần test project mới hoặc thay ngoài Authentication, xử lý đúng scope task và quy tắc repo còn hiệu lực. RBAC extension là task riêng khi requirements sẵn sàng; không là gate cho core Authentication.

## 9. Acceptance Criteria

**PROPOSED:** acceptance chỉ áp dụng theo các policy DEV1 đã chốt và ghi nhận; không phải kết quả kiểm thử hiện tại.

| Nhóm | Test cases / điều kiện đạt |
|---|---|
| Schema tối thiểu | B chỉ rename hash column, giữ type/NOT NULL/unique; thêm 3 users fields sau khi DEV1 chốt chi tiết triển khai; EF/consumer không còn query tên cũ, không placeholder hash. |
| Legacy hash semantics | Canonical SHA-256 credential lookup/unique đúng trên SQL collation thực; consumer cũ không chạy chung; sample/legacy rows không tự login được. |
| Temporary state | Non-NULL expired vẫn temporary và bị chặn qua restart/multi-instance; chỉ change-password hợp lệ clear expiry; không audit/account_status làm state store. |
| Lock protocol | Mọi credential/session writer dùng user-then-session locks trên isolation thực; failed counter không lost update; load test contention và retry không ghi đè stale state. |
| Login | Username/email hợp lệ tạo session/audit atomic; cookie và core DTO đúng; không lộ secret. |
| Enumeration/lockout | Unknown/wrong/inactive cùng generic 401; locked details chỉ sau password đúng; counter atomic, đúng policy đã duyệt. |
| Credential protection | Cookie sửa bị chặn; session ID/hash SQL không đủ login; không log credential. |
| Idle/sliding | Trước hạn kéo dài đúng; tại now >= expiry bị chặn; polling theo rule đã duyệt; expired không hồi sinh. |
| Absolute/restricted | Không request nào vượt absolute cap DEV1 đã chốt; không coi 12h/15m đã được DEV1 chốt. |
| Logout | Revoke trước 204; replay 401; lặp idempotent; SQL failure 503. |
| Password change | Hash/temp expiry/revocation/new session/audit atomic dưới user lock; credential cũ invalid, không cần auth_version. |
| Reset/deactivate | Revoke-all atomic; temp expiry đúng; không lưu plaintext; permission theo approval riêng. |
| Restricted | Direct business API trả 403; không phụ thuộc frontend navigation. |
| Concurrency | Touch/logout không hồi sinh session; login/reset không phát stale session; hai password changes một commit; retries không nhân đôi audit. |
| CSRF identity | Anonymous token dùng login; response token khớp principal mới; token cũ không dùng lại sau account switch; logout/bootstrap anonymous đúng. |
| CSRF coverage | Thiếu/sai token chặn unsafe APIs cả DEV1/DEV2; GET không mutation nghiệp vụ. |
| Outage handling | SQL unavailable trong ValidatePrincipal được outer handler trả 503 và dừng pipeline; không controller execution, không cookie deletion/401 giả. |
| API | Không HTML redirect; ID string, UTC, no-store, errors đúng contract; core DTO không ngầm cấp RBAC. |
| Deployment | Restart giữ keys/session; multi-instance cùng môi trường đọc cookie; môi trường khác không đọc được. |
| Frontend | Startup restore, restricted routing, 401/423/429/503, multi-tab, token rotation, polling được FE xác nhận. |

Concurrency/index/transaction phải kiểm thử trên SQL Server thực/test instance; EF InMemory không chứng minh được. Sau implementation C# bắt buộc dotnet build HotelOps.slnx; chạy dotnet test HotelOps.slnx khi có tests liên quan; báo mọi warning/failure và giới hạn API/DB runtime verification.

## 10. Quyết định DEV1 cần tự chốt và phạm vi ngoài Authentication

Không có hàng nào dưới đây yêu cầu DEV2/FE/PM duyệt từng bước kỹ thuật Authentication. DEV1 ghi lựa chọn vào decision log khi bắt đầu task liên quan; phối hợp tích hợp không tự biến thành approval gate.

| Nội dung | Trạng thái / hành động |
|---|---|
| Phương án B | DEV1 DECIDED: rename session hash giữ type/NOT NULL/unique; thêm 3 users fields; không mở rộng schema nếu không cần |
| Shared Authentication Service / CurrentUserAccessor / SessionGuard | DEV1 DECIDED về sở hữu và dùng chung; interface/transaction contract do DEV1 chốt, đưa vào handoff DEV2 |
| Types/default/check, hash encoding, SQL lock/isolation và rollback details | PROPOSED — DEV1 TO DECIDE; kiểm chứng SQL thực, ghi rationale |
| Absolute / restricted lifetime | DEV1 DECIDED: normal 12h, restricted tối đa 15m và không vượt temporary expiry; Task 7 xem 10.4 |
| Idle / polling | DEV1 DECIDED: normal idle 30 phút; valid request có thể touch nhưng không vượt absolute; middleware touch sau security checks, không tự miễn trừ polling |
| Lockout | DEV1 DECIDED: 5 lần sai liên tiếp / 30 phút, valid success reset counter=0/deadline=NULL, UTC; Task 6A xem 10.2. Rate limiting riêng vẫn chưa chốt |
| Temporary password | DEV1 DECIDED: 24 giờ, CSPRNG, Identity V3, handoff một lần, non-NULL expiry luôn temporary; Task 6B xem 10.3; UI/secure delivery và restricted endpoint enforcement chưa triển khai |
| Password hashing/policy | Task 5 đã triển khai Identity V3 / 100.000 iterations và rehash signal, xem 10.1; length/complexity/input caps/provisioning và benchmark production vẫn chưa chốt |
| Authentication API/DTO/CSRF | PROPOSED — DEV1 TO DECIDE; FE nhận contract cuối, không cần duyệt từng endpoint trước backend implementation |
| Deployment/keys/retention/audit | DEV1 chốt giải pháp Authentication và ghi operational requirements; môi trường hosting chưa có thì nêu phần chưa kiểm chứng |
| FE source và integration thực tế | TEAM INTEGRATION / HANDOFF: chưa xác nhận latest source; không coi snapshot là contract approved; ghi adapter/routes/UI changes cần FE làm |
| Permission Matrix / Reception / Department Head / business scopes | OUT OF SCOPE — RBAC REQUIREMENTS PENDING; không tự cấp quyền hoặc sửa permission records/policies từ matrix draft |
| Ai được Admin Reset/deactivate | Quyền nghiệp vụ thuộc RBAC ngoài scope; cơ chế revoke/session khi reset/deactivate thuộc Authentication của DEV1 |
| role_permissions.data_scope SQL/EF conflict | Ghi rõ trong handoff; không sửa mapping RBAC hoặc hoàn thiện grants dưới danh nghĩa Authentication |

### 10.1. Decision log — Task 5 Password Credential Foundation (2026-10-18)

**DEV1 DECIDED / IMPLEMENTED trong phạm vi Task 5:**

- `IPasswordCredentialService` / `PasswordCredentialService` trong Services/Authentication; HashPassword và VerifyPassword dùng ASP.NET Core `PasswordHasher<User>`, không tự viết thuật toán. Đăng ký scoped trong Program.cs; chỉ cấu hình PasswordHasherOptions, không AddIdentity/UserManager/Identity schema hoặc authentication middleware.
- Identity V3, baseline 100.000 PBKDF2 iterations; .NET 10.0.8 tạo V3 với HMAC-SHA512, salt ngẫu nhiên. Dùng format có version/parameters của framework, không prefix tự chế. Cấu hình nằm trong DI; không thay csproj/dependency hoặc database.
- Kiểm tra envelope trước framework verifier: Base64, marker 0x01, header big-endian hợp lệ, PRF framework 0/1/2, salt/subkey tối thiểu 16 byte; giới hạn stored string 255 theo SQL và iteration không quá 1.000.000 để chặn payload gây công việc không giới hạn. Đây là format validation, không phải password hashing/comparison tự viết. Hash mới dùng iterations cấu hình trong khoảng 100.000–1.000.000; tăng ngoài cap phải review cả service và compatibility.
- V3 hợp lệ có work factor thấp hoặc PRF cũ được framework kiểm chứng trước khi trả SuccessRehashNeeded. Identity V2, plaintext-as-hash, format không xác định hoặc malformed bị Failed; không fallback hoặc tự nâng cấp legacy chưa xác minh. Format/header phù hợp không chứng minh password đúng; framework verifier vẫn quyết định.
- Verify trả nguyên PasswordVerificationResult; SuccessRehashNeeded là xác minh thành công cần thay hash. Caller tương lai phải tạo hash mới bằng HashPassword và persist có user-lock/recheck snapshot trong transaction; service không tự sửa User.PasswordHash, không SaveChanges/log, không tăng version hoặc thu hồi session chỉ vì technical rehash. Task 5 chưa có caller persistence.
- Không trim/normalize/truncate password; reject null/empty hash input và fail verification khi input thiếu. Chưa triển khai password strength policy, lockout, temporary password, login/logout/session/RBAC. HashPassword không thay thế policy validator tương lai.

**Khảo sát credential, chỉ đọc:** source hiện chỉ có PasswordHash nullable, mapping varchar(255); chưa tìm thấy code tạo user, seed/provisioning hoặc verifier trước Task 5. SELECT khảo sát 26 users chỉ xuất thống kê: **26 non-Base64/unclassified, 0 Identity V3 candidates**. Không xuất hoặc lưu raw hash/password. Không suy thuật toán từ độ dài, không khẳng định toàn bộ là plaintext/placeholder; chỉ kết luận service mới không hỗ trợ các giá trị này. Không thử đăng nhập hoặc sửa credential hiện tại.

**Validation:** dotnet build HotelOps.slnx Passed, 0 errors, 4 warnings NU1902/NU1903 cho System.Security.Cryptography.Pkcs 9.0.4 (lặp restore/build), không sửa dependency ngoài phạm vi. Harness tạm ngoài repo Passed **26/26** checks: đúng/sai password, salt, output không plaintext, fit SQL, no entity mutation, null/empty/malformed/unsupported/V2 rejection, lower-work-factor V3 SuccessRehashNeeded, wrong-password không được rehash, caller-generated replacement, không downgrade stronger V3, invalid PRF/iterations/salt, bounds và no normalization. Harness không kết nối SQL. Repository chưa có automated credential test project; không coi temporary harness là regression suite được lưu trong repo. Thời gian tổng harness khoảng 518 ms trên máy development, không phải benchmark load/production đã được chấp nhận.

**Chưa chốt / việc tiếp theo:** benchmark và quyết định work factor production; password strength/length/input limits; lockout/temp expiry/delivery và các policy session/API/CSRF còn theo mục 10. Để 26 users hiện tại login an toàn, cần task provision/admin-reset được giao riêng: tạo credential thật qua service, persistence/audit/temporary flag-expiry và session revocation atomic, phân phối temporary password an toàn, bắt đổi ở restricted flow. Không convert chuỗi legacy không biết nguồn sang V3 như thể biết password gốc. Task này không reset/update bất kỳ user, tạo session hoặc đổi schema nào.

Tham chiếu format/rehash: [PasswordHasher .NET 10.0.8 source](https://github.com/dotnet/aspnetcore/blob/v10.0.8/src/Identity/Extensions.Core/src/PasswordHasher.cs), [PasswordHasherOptions.IterationCount](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasheroptions.iterationcount).

### 10.2. Decision log — Task 6A Failed Login & Account Lockout (2026-10-18)

**DEV1 DECIDED:** 5 lần sai liên tiếp khóa 30 phút; success hợp lệ reset failed_login_count=0, locked_until=NULL. Dùng UTC và SQL state, không account_status cho temporary lock. Policy triển khai: request sai khi đang khóa không tăng counter/không gia hạn deadline; hết hạn (now >= locked_until) bắt đầu chu kỳ mới, lần sai tiếp theo là 1. Đúng password nhưng chưa hết khóa không được Verified. Những semantics chu kỳ/không gia hạn là quyết định kỹ thuật của DEV1 trong task, không cấp thêm quyền RBAC.

**IMPLEMENTED:** ICredentialLockoutService/CredentialLockoutService, policy thuần CredentialLockoutPolicy, TimeProvider.System qua DI (có thể thay clock khi test). Time lấy sau user lock bằng GetUtcNow().UtcDateTime; SQL datetime2 đọc lại có Kind Unspecified được diễn giải là UTC, không convert theo local timezone. Các flow tương lai phải dùng cùng UTC source; kiểm soát lệch đồng hồ giữa instances khi triển khai nhiều node.

- VerifyAsync yêu cầu caller-owned SQL Server transaction với ReadCommitted/RepeatableRead/Serializable, không Snapshot; context sạch, không User tracked. User lookup bằng ID do Login resolver tương lai tìm, FromSqlInterpolated parameterized SELECT WITH (UPDLOCK,HOLDLOCK), AsNoTracking; lấy user lock trước session lock và giữ đến commit/rollback của caller. Password verify thực hiện trên row mới nhất dưới lock để reset đồng thời không vượt qua verification cũ. Chi phí PBKDF2 nằm trong lock ở baseline an toàn này, cần load test trước tối ưu.
- Callback isEligibleForLogin bắt buộc, chỉ đọc; Login Service tương lai kiểm tra account/deleted/temporary state dưới cùng transaction. Không tự invent eligibility rules hoặc triển khai temporary/session trong Task 6A. Re-read credential/eligibility sau callback, recheck exact hash/state; callback không được mutate entity, transaction hoặc pending changes. Exceptions phải rollback transaction, không commit nửa workflow.
- ExecuteUpdate chỉ SET failed_login_count/locked_until, có expected-state predicates/affected row check; không SaveChanges snapshot user. Khi lock mới bắt đầu, ghi audit LOGIN_LOCKOUT trong cùng transaction, chỉ counter/deadline/user reference, actor chưa xác thực để NULL; không password/hash/plaintext trong logs/audit. Không ghi audit lock trùng cho request đang khóa.
- Unknown user và failure thông thường trả cùng InvalidCredentials, không deadline/rehash flag; unknown dùng dummy Identity V3 hash để tránh bỏ hẳn password work. Không cam kết timing tuyệt đối bằng nhau, đặc biệt malformed legacy có đường verify nhanh; rate limiting/dummy work cho mọi rejection cần review khi xây Login API. Locked/deadline chỉ trả sau password đúng và callback eligibility hợp lệ. Result nội bộ không tự cấp cookie/quyền đăng nhập.
- Caller phải COMMIT ordinary failures để lưu counter/audit; với Verified phải hoàn tất session/login/rehash cần thiết trong CÙNG transaction rồi commit, sau đó mới phát cookie. Service không tự commit/rollback/retry, không tạo session. Mọi password-reset writer phải dùng user-first lock protocol và revoke sessions; rehash-needed chỉ được propagate khi Verified. Không dùng service nhiều lần trên cùng attempt hoặc commit sau exception.

**Kiểm thử Task 6A:** harness tạm ngoài repo 16/16 checks Passed: sai 1–4, lần 5, đúng/sai trong thời gian khóa, ineligible không lộ khóa/reset, boundary expiry, reset thành công, overflow, UTC, không mutate temp/hash và thiếu transaction fail trước kết nối. Clock giả không phải chờ 30 phút. Trên SQL development chỉ chạy SELECT trong hai kết nối: UPDLOCK/HOLDLOCK cạnh tranh timeout 1222 khi lock còn giữ, thành công sau nhả lock; transaction chỉ đọc rollback để nhả khóa, không DDL/DML.

**Giới hạn cần giữ rõ:** chưa có database test riêng được xác nhận, nên CHƯA chạy concurrent failed-login writes/reset race/audit persistence hoặc service end-to-end trên SQL. Kiểm chứng read-only locks chứng minh blocking mechanism trên SQL hiện tại, không thay thế test lost updates toàn workflow. Không tự gọi EF InMemory/parallel policy simulation là bằng chứng concurrency SQL. Cần test trên database test riêng: 5+ parallel wrong attempts tạo đúng một lock/audit, counter không lost update, reset/login ordering, rollback counters/audit, temp/hash invariance, correct-password locked disclosure và rehash signal. Chưa nghiệm thu phần concurrent-write runtime cho đến khi các tests này thực thi.

Task 6A không sửa 26 users/10 sessions, schema, frontend, RBAC hoặc API. Build solution Passed với 0 errors, 4 warnings NU1902/NU1903 Pkcs 9.0.4 như trước; không có credential-test project trong repo. Các lifetime/password strength/temp delivery/CSRF và API policies khác vẫn chưa chốt như mục 10. Mục 10.1 mô tả scope lịch sử Task 5; phần lockout hiện đã được chốt bởi mục này.

### 10.3. Decision log — Task 6B Temporary Password & Credential Provisioning/Reset (2026-10-18)

**DEV1 DECIDED:** temporary password hiệu lực 24 giờ UTC, CSPRNG, Identity V3 qua PasswordCredentialService; không fixed password/plaintext storage/log. Non-NULL temporary_password_expires_at luôn temporary kể cả expired; không tự clear khi hết hạn hoặc rehash/verify. Chỉ đổi sang password thường thành công mới được clear. Admin Reset revoke mọi session cũ cùng transaction. Handoff chỉ một lần qua kênh bảo mật tương lai; không tự cấp quyền Admin Reset.

**IMPLEMENTED trong Task 6B:**

- ITemporaryCredentialService/TemporaryCredentialService: Prepare(userId) sinh 24 ký tự từ alphabet 64 ASCII bằng RandomNumberGenerator.GetInt32 (144 bit trước rejection nhỏ để có upper/lower/digit/- hoặc _); hash ngoài transaction, không verify legacy. Không dùng fixed prefix/suffix hoặc timestamp làm randomness. Đây là composition cho mật khẩu được sinh, không phải final password-strength policy cho mật khẩu do user chọn.
- GetState phân biệt Regular (expiry NULL), TemporaryValid (now < expiry), TemporaryExpired (now >= expiry); SQL datetime2 Kind Unspecified được coi UTC, Local bị reject. Regular chỉ là phân loại state, không khẳng định hash hỗ trợ. Hàm thuần đọc trạng thái, không clear expiry.
- ApplyAsync(prepared, actorUserId, InitialProvisioning/AdminReset) tự sở hữu transaction ReadCommitted, yêu cầu context sạch/không ambient transaction. Target là user row đã tồn tại; InitialProvisioning chỉ khi chưa có hash và temporary state. Legacy có giá trị hash phải dùng AdminReset rõ ràng. Không tạo user ở task này. Deleted user bị chặn; các account scope/permissions còn do caller được authorize ở task RBAC riêng.
- User UPDLOCK,HOLDLOCK trước sessions; SET đúng password_hash/temp expiry/failed count=0/locked_until=NULL, không ghi đè thuộc tính user khác. Deadline tính tại Apply sau khi có lock, không phải lúc Prepare. ISessionRevocationService/SessionRevocationService là điểm revoke-all dùng chung: cùng scoped DbContext/caller transaction, giữ/reacquire user lock trước UPDATE các session unrevoked, giữ nguyên revoked_at của row đã revoked. Không tạo/validate hoặc hồi sinh sample session; Session Service tương lai phải dùng abstraction này, không chép logic revoke-all.
- Audit CREDENTIAL_RESET/CREDENTIAL_PROVISION cùng transaction, actor do caller cung cấp sau authorization; chỉ metadata expiry/counters/revoked count, không password hoặc hash. Kết quả chỉ trả sau CommitAsync thành công. Exception/commit-unknown không phát plaintext, không tự retry; transaction còn mở được dispose/rollback, audit pending được detach. Commit outcome không rõ phải điều tra trước thao tác tiếp theo.
- CredentialLockoutService recheck temporary expiry sau callback tại thời điểm ra quyết định: expired không Verified/không reset counter như success; temporary còn hạn Verified có MustChangePassword=true. Đây là tín hiệu nội bộ, chưa là session/cookie hoặc restricted API authorization. Session guard tương lai phải enforce đổi password trước mọi business API. Technical rehash không clear temporary expiry.

**Plaintext lifetime/handoff:** PreparedTemporaryCredential là preparation trong memory, consume một lần; Dispose bỏ reference, ToString redacted, không public password/hash property. IssuedTemporaryCredential trả sau commit, TakePasswordOnce lấy secret một lần, Dispose bỏ reference. Không serialize/log/cache object hoặc string nhận được, không URL/file/telemetry/ordinary API DTO. System.Text.Json mặc định không xuất secret nhưng không phải permission serialize object. Managed strings không bảo đảm zeroize bộ nhớ; caller phải giới hạn lifetime, không giữ kết quả trong session/cache. Handoff mất sau commit cần reset mới rõ ràng, không khôi phục plaintext từ database. Không giữ SQL transaction trong lúc user/admin xem hoặc hệ thống ngoài gửi credential.

**Đã kiểm thử:** harness ngoài repo 47/47 Passed; 8 lần generation khác nhau/composition, verify hash đúng, không lộ secret qua ToString/default JSON, NULL/valid/expired/boundary UTC, expiry không tự thành regular, unsupported legacy bị từ chối, preparation/handoff single-use/dispose, revoker thiếu transaction fail trước kết nối, không track/mutate entity khi Prepare/GetState. Constructor issued trong harness được gọi bằng reflection chỉ để test container handoff, không giả lập reset đã commit. Harness không kết nối SQL.

**Not Run:** reset/provision SQL writes, revoke-all/audit persistence, rollback khi revocation/audit lỗi, exact user field invariance, lockout/reset coordination và reset/login race end-to-end. Chưa có database test riêng được xác nhận; không ghi vào 26 users/10 sessions của HotelOpsDB, không gọi concurrency đã nghiệm thu. Cần test riêng: sessions active/revoked/many devices, tất cả audit/credential/session thay đổi cùng commit hoặc rollback; concurrent login/reset ở cả thứ tự; clock boundary; initial provision không overwrite credential; lỗi FK actor; commit-unknown và duplicate attempt. Build solution Passed, 0 errors, 4 warnings NU1902/NU1903 Pkcs 9.0.4 như trước.

**Kế hoạch cấp credential development cho 26 legacy users — CHỈ ĐỀ XUẤT, CHƯA ÁP DỤNG:**

1. Xác nhận target development/backup và danh sách UserId cho phép; actor/target scope được authorize; không suy quyền từ Permission Matrix draft hoặc gán role để bypass.
2. Hoàn thiện SQL workflow tests trên database test riêng, Session Service/validation + restricted guard + change-password và secure one-time delivery trước khi cấp credential để sử dụng thật. Login/temp flag không tự cấp quyền business.
3. Dùng công cụ/operator flow được giao riêng, không seed/reset lúc API startup; không bulk reset mặc định. Chọn từng account trong allowlist, explicit AdminReset cho legacy; không hash lại chuỗi legacy như thể đó là password gốc.
4. Prepare ngoài transaction; Apply transaction ngắn cho đúng account, audit actor/target/time/revoke count. Sau commit chỉ handoff một lần theo kênh được xác nhận; không ghi plaintext ra terminal logs, repo, file, email thường hoặc shared checklist.
5. Test first login vào restricted flow, đổi password thường thành công mới clear temporary expiry; revoke/rotate đúng session contract. Quá 24h phải reset mới; delivery thất bại không đọc lại secret.
6. Báo cáo số account provisioned/reset và kết quả, không credential; dừng khi lỗi, không tự retry commit-unknown hoặc sửa 26 tài khoản còn lại. Việc thực thi phải là task có scope/target rõ, không nằm trong Task 6B.

**Phạm vi chưa làm:** API Admin Reset/authorization, Login/Logout/Cookie, secure delivery UI, password change thường, session creation/validation và SQL workflow tests. Không đổi schema/EF/frontend/Permission Matrix hoặc dữ liệu hiện tại. Mục 10.1/10.2 là lịch sử task; temporary policy hiện được chốt tại mục 10.3.

### 10.4. Decision log — Task 7 SQL Session Lifecycle (2026-10-18)

**DEV1 DECIDED:** normal idle 30 phút, absolute 12 giờ từ created_at; restricted tối đa 15 phút từ created_at và không vượt temporary_password_expires_at; valid request có thể sliding idle trong cap. Không Remember Me, JWT/access-token/refresh-token flow. Background authenticated polling vẫn có thể là activity; chưa có ngoại lệ tự đặt. Chính sách 12h/15m không còn là đề xuất chờ chốt trong các phần lịch sử trước.

**IMPLEMENTED:** ISqlSessionService/SqlSessionService với CreateSessionAsync, ValidateSessionAsync, TouchSessionAsync, RevokeSessionAsync và RevokeAllSessionsAsync. RevokeAllSessionsAsync delegate ISessionRevocationService đã dùng bởi TemporaryCredentialService, không sao chép UPDATE revoke-all. DI scoped, TimeProvider UTC như Task 6A/6B; không tự khởi động worker/API hoặc thao tác database khi đăng ký DI.

- SessionCredential sinh 32 byte CSPRNG, biểu diễn raw bằng canonical Base64URL 43 ký tự; SQL chỉ nhận sha256: + 64 hex lowercase SHA-256 trên 32 byte gốc. Canonical validation từ chối sample/refresh-token/chuỗi khác format; raw không dùng session_id và không fallback sang 10 legacy rows. Không convert dữ liệu cũ. Không đổi schema.
- SessionReference là integration object nội bộ để đọc/ghi protected ticket tương lai, không HTTP DTO. Factory FromProtectedTicket chỉ được gọi SAU Data Protection authentication của scheme/purpose/version; raw chỉ qua ExportCredentialForProtectedTicket, không property serialize. CreatedSqlSession loại Ticket khỏi default JSON, ToString redacted. Caller tuyệt đối không log/serialize credential/string trả từ export, không đưa URL/audit/DTO. Managed strings không bảo đảm zeroize; lifetime phải ngắn, không cache raw ngoài protected cookie. Không bật EF sensitive-data logging; sanitize SQL errors khi tích hợp HTTP/logging.
- CreateSessionAsync chỉ nhận kết quả Verified có CredentialVerificationProof nội bộ một lần, gắn user/hash snapshot/temp expiry/EF transaction ID. Proof chỉ do CredentialLockoutService cấp, không client tự gửi; record copy không tạo thêm lượt consume. Re-lock user và so sánh exact hash/temp state, ACTIVE account, chưa deleted/locked/temp expired. Không tái dùng proof sau reset hoặc trên transaction khác. Trong Login tương lai, tạo session trước technical rehash nếu rehash làm đổi hash snapshot, hoặc verify lại để nhận proof mới; không vô hiệu check để thích nghi.
- Mọi operation yêu cầu caller-owned SQL Server transaction (ReadCommitted/RepeatableRead/Serializable), context sạch không tracked entries. User UPDLOCK/HOLDLOCK trước session UPDLOCK/HOLDLOCK; async/CancellationToken, AsNoTracking đọc lại SQL. Caller rollback mọi exception, không commit workflow nửa chừng; service không tự commit/retry. Login phải giữ transaction từ password verify đến insert session và commit, sau commit mới phát cookie. Per-request middleware tương lai phải mở/commit transaction ngắn cho validate/touch, không giữ lock chờ HTTP/external delivery.
- Validate kiểm tra user ACTIVE/chưa soft-delete (không tự gán quyền/đặt thêm employment semantics), đúng user/hash/created_at của ticket, restricted state khớp temp non-NULL, chưa revoked/idle expired/absolute expired và chronology hợp lệ. Deadline absolute được tính lại từ created_at và current SQL temp expiry, không tin client cap; ticket chỉ mang restricted claim/creation identity đã được bảo vệ. Validate chỉ đọc; Touch riêng chỉ gọi sau security/CSRF checks phù hợp trong task Cookie.
- Touch cập nhật đúng last_used_at/expires_at bằng conditional UPDATE với expected created/expiry/lastUsed, unrevoked và deadline còn hạn; affected rows khác 1 là concurrency error, không tự retry hoặc hồi sinh session. created_at bất biến, last_used_at không lùi kể cả clock lùi, idle deadline không vượt cap. Revoke một session idempotent, giữ timestamp đã revoked; có thể revoke session expired/inactive để logout, không cần nâng nó thành valid trước.
- Create/revoke/revoke-all ghi audit cùng caller transaction, không credential/hash. Revoke-all audit khi có row thay đổi. Session sample không được coi authenticated bởi ID hoặc hash trong database; phải có raw secret khớp V3-purpose protected ticket được flow mới tạo. Phiên reset bị revoke trước commit reset; mọi credential/session writer phải giữ user-first protocol.
- SessionStoreUnavailableException phân loại các SQL connectivity/availability/timeout errors, không trả IsValid=false khi chưa xác định được SQL validity. Schema/permission/deadlock/concurrency errors propagate riêng; caller cancellation không giả thành outage. BeginTransaction thất bại ở caller nằm ngoài wrapper này, Login/middleware tương lai cũng phải map lỗi ở đó. API 503/ProblemDetails/không xóa cookie sẽ triển khai sau; Task 7 chưa có middleware. Không coi mọi SqlException là authentication failed.

**Kiểm thử:** dotnet build HotelOps.slnx Passed, 0 errors, 4 warnings NU1902/NU1903 Pkcs 9.0.4 như trước. Harness tạm ngoài repository 71/71 Passed: 20 random credentials/hash canonical checks, unsupported inputs, normal window/idle boundary, sliding cap 12h, activity không lùi, revoked refusal, restricted 15m/temp cap/expired, state mismatch, legacy rejection, user inactive/deleted, chronology, redacted/default JSON, SQL error-number classification và missing-transaction fail trước kết nối. Không có kết nối SQL hoặc ghi dữ liệu trong harness. Không gọi việc tạo random keys khác nhau là bằng chứng entropy thống kê; entropy dựa CSPRNG 32 byte.

**Not Run / chưa nghiệm thu SQL runtime:** create/touch/revoke/revoke-all SQL writes, actual EF insert/conditional-update SQL translation/affected rows, duplicate hash failure path, audit persistence/rollback, proof replay/reset/login race, touch/revoke concurrency, service outage integration và commit-unknown handling. Chưa có database test riêng được xác nhận; không test ghi trên HotelOpsDB development. Phép thử lock chỉ đọc ở Task 6A không thay thế các workflow tests này. Cần regression tests SQL riêng cho transaction commit/rollback và các thứ tự concurrent operations trước nghiệm thu concurrency.

**Việc tiếp theo:** Login orchestration và identity-safe DTO/errors, CookieAuthenticationEvents validate/touch, Data Protection purpose/key ring, cookie HttpOnly/Secure/SameSite/nonpersistent, CSRF bootstrap/rotation, restricted route enforcement, password-change rotation, SessionGuard/CurrentUserAccessor cho DEV2 và secure credential provisioning. Không triển khai RBAC/Permission Matrix hoặc các thành phần đó trong Task 7. Không tạo session thật, sửa 26 users/10 sessions, schema, frontend hay Git trong nhiệm vụ này.

### 10.5. Decision log — Task 8A Cookie Authentication Infrastructure (2026-10-18)

**IMPLEMENTED / DEV1 DECIDED:** scheme HotelOpsCookie, cookie __Host-HotelOps.Auth, HttpOnly=true, Secure=Always, SameSite=Lax, Path=/, Domain unset, SlidingExpiration=false, không JWT/refresh-token. Ticket helper tạo IsPersistent=false/AllowRefresh=false và ExpiresUtc không vượt absolute SQL cap; không Remember Me. Không thêm RBAC/fallback policy cho các endpoint hiện có.

**Pipeline và identity:** Program đăng ký authentication/authorization, events scoped; AuthenticationExceptionMiddleware ở ngoài UseAuthentication, rồi InvalidCookieMiddleware, UseAuthorization, controllers. Existing anonymous endpoints không có cookie vẫn giữ hành vi cũ. Invalid/tampered/expired cookie hoặc principal SQL invalid bị chặn 401 trước endpoint, kể cả endpoint hiện chưa có Authorize; không tự tạo session hoặc tự xóa cookie. Task Login sau này cần xử lý recovery/re-login/anonymous CSRF bootstrap với cookie cũ invalid rõ ràng, không nới guard cho business endpoints.

- HotelOpsCookieTicket chuẩn bị ticket/claims version 1, user ID, session ID, credential, restricted flag, created ticks; thiếu/trùng claim, sai định dạng hoặc sai scheme/identity bị từ chối. Helper chỉ được gọi sau commit session; nó không tự chứng minh transaction đã commit. Không đưa ticket/principal chứa credential vào HTTP DTO hoặc logs.
- HotelOpsCookieEvents dùng ICookieSessionValidator/CookieSessionValidator: mở transaction SQL ReadCommitted ngắn, gọi ISqlSessionService.ValidateSessionAsync và commit read-only; query SQL mỗi request, không cache liên request. Kiểm tra snapshot SQL khớp user/session ID, raw credential qua hash lookup, creation identity, restricted state và ticket expiry <= SQL absolute cap. Chỉ giữ claims đã hiểu, không nhận role claims làm quyền. Request-local SessionSnapshot trong HttpContext.Items chỉ cho các bước sau của cùng request, không thay validation ở request kế tiếp.
- Không điều chỉnh SessionReference/SQL schema để thêm session ID: ticket có session ID, events đối chiếu với SessionSnapshot trả từ SQL; session_id một mình không đủ. Legacy rows không được chấp nhận chỉ vì tồn tại; phải có protected ticket đúng purpose/scheme/version và raw key hợp lệ khớp SQL. Không fallback/sample conversion.
- Cookie handler kiểm tra protected ticket validity và expiration; SQL vẫn xác minh idle/current user state. Cookie ticket absolute expiry có thể bị framework serialize xuống độ chính xác giây (hết sớm một phần giây, không kéo dài cap). ValidatePrincipal không tự renew hoặc Touch. Task 8B sẽ đặt Touch SAU security/CSRF/restricted checks; hiện request hợp lệ chưa tự gia hạn SQL idle. Restricted claim chưa là Restricted Guard, nên không gọi Task 8A là authentication feature hoàn chỉnh để cấp business access.

**Data Protection development:** Windows Development dùng %LOCALAPPDATA%/HotelOps/DataProtectionKeys/Development, kiểm tra nằm ngoài Git working tree; persistent key ring, ApplicationName HotelOps.Authentication.Development.v1 và DPAPI current Windows user bảo vệ at rest. Không hardcode key/credentials hoặc lưu keys trong repo. Cùng account/machine có thể mở cookie sau restart, không hứa key ring này dùng được cho user/machine khác. Không xóa keys cũ khi rotate.

**Production/deployment chưa cấu hình:** registration hiện fail closed khi không phải Windows Development. Đây là giới hạn startup có chủ đích: không tự chạy production với ephemeral/plaintext key ring. Trước deployment phải triển khai cấu hình persistence/shared keys, encryption certificate/key service, app isolation/access/backup/rotation phù hợp topology; không copy DPAPI development keys sang production hoặc đổi environment thành Development để bypass. Chưa kiểm thử production/multi-instance/container hoặc TLS/reverse-proxy behavior.

**Errors:** challenge 401 AUTHENTICATION_REQUIRED, cookie invalid 401 SESSION_INVALID, forbid 403 FORBIDDEN đều application/problem+json/no-store, không Location/HTML redirect. SQL session-store unavailable trả 503 AUTH_SERVICE_UNAVAILABLE từ outer handler và kết thúc pipeline, không reject identity/signout/xóa cookie hoặc gọi business endpoint. Adapter bao cả connection/BeginTransaction availability errors ngoài SqlSessionService; cancellation do client không gán thành outage. Lỗi programming/schema khác vẫn generic 500 INTERNAL_SERVER_ERROR. Chỉ log error type/trace ID, không exception message/object/claims/cookie/raw key/session hash; HTTP không có SQL details. Nếu response đã bắt đầu thì handler không cố ghi lại body/status; authentication validation xảy ra trước business response.

**Validation Task 8A:** build solution Passed 0 errors, 4 warnings NU1902/NU1903 System.Security.Cryptography.Pkcs 9.0.4 như trước. Harness tạm ngoài repo chạy Kestrel loopback, framework Cookie handler/Data Protection thật và fake ICookieSessionValidator; **24/24 checks Passed**: options/DI, anonymous endpoint, malformed/tampered/expired cookie, missing credential/session ID, malformed ID, invalid/revoked fake result, SQL session ID/restricted mismatch, valid cookie không renew và validation mỗi request, 401/403 JSON/no redirect, fake outage503 khác programming500, business endpoint không chạy khi lỗi và không Set-Cookie xóa phiên. Fresh host/service provider đọc lại cookie từ persistent encrypted key ring và kiểm tra XML encryptedSecret (DPAPI); đây là restart host/provider trong harness, không reboot Windows hoặc restart HotelOps.Api production. Harness HTTP loopback gửi Cookie header thủ công: không phải browser nghiệm thu Secure/SameSite qua HTTPS.

**Not Run:** SQL validation adapter/runtime/outage/concurrency integration trên SQL thật; session writes vẫn chưa test trên database test riêng. Harness fake không chứng minh SQL query/locking/transaction thành công. Không khởi động HotelOps.Api, không kết nối hoặc sửa 26 users/10 sessions trong task; không migrations/scaffold/schema/frontend/RBAC. Chưa có automated test project được lưu trong repo.

**Task 8B/Login cần nối:** CSRF bootstrap/identity rotation, restricted endpoint allowlist/guard, Touch sau các checks, invalid-cookie recovery cho auth endpoints, DTO/error contracts và identity-safe Login/Logout/ChangePassword orchestration; dùng HotelOpsCookieTicket sau SQL commit và SignInAsync với đúng scheme/properties. CurrentUserAccessor/SessionGuard cho DEV2 và production key deployment vẫn là task riêng. Không cấp credential thật cho legacy accounts trước khi hoàn tất các guard/workflow tests cần thiết.

Tham chiếu framework: [Microsoft Cookie Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0), [Microsoft Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0). Explicit redirect overrides chuẩn hóa JSON thay vì dựa vào endpoint detection của framework.

### 10.6. Decision log — Task 8B CSRF, Restricted Guard & Session Touch (2026-10-18)

**IMPLEMENTED:** ASP.NET Core IAntiforgery và pipeline validation thực tế, RestrictedSessionMiddleware, SessionTouchMiddleware; không thêm Login/Logout/ChangePassword APIs hoặc RBAC. Source hiện có chỉ GET weather/database-health; không phát hiện endpoint dùng JWT/bearer hoặc giao thức ngoài Cookie cần exemption. Không thêm ngoại lệ cho module DEV2 hoặc AllowAnonymous unsafe endpoint.

**Pipeline cuối:** outer AuthenticationExceptionMiddleware → HTTPS redirection → UseAuthentication (SQL ValidatePrincipal) → InvalidCookieMiddleware → ApiCsrfMiddleware → RestrictedSessionMiddleware → UseAuthorization → SessionTouchMiddleware → endpoint. Routing của ASP.NET Core xác định endpoint để authorization hoạt động; không thêm fallback/permission policy. Authorization bị từ chối cũng không Touch. Business mutation chỉ chạy sau Touch thành công; không coi guard này thay SessionGuard/transaction authorization cho sensitive workflows tương lai.

**Antiforgery:** cookie __Host-HotelOps.Antiforgery, Secure Always, HttpOnly, SameSite Lax, Path=/, không Domain; header X-CSRF-TOKEN. ApiCsrfMiddleware bắt buộc đúng một header token và gọi ValidateRequestAsync cho mọi /api unsafe method, kể cả anonymous và future DEV2 writes. Không chấp nhận form-token fallback khi thiếu header; missing/invalid trả 400 CSRF_VALIDATION_FAILED dạng ProblemDetails/no-store, không redirect/log token/Touch. GET/HEAD/OPTIONS không validate request token; các unsafe methods khác cũng không mặc định được bypass. Chưa có exemption cho non-cookie protocols; nếu thêm protocol mới phải thiết kế riêng trước khi miễn CSRF.

**Bootstrap contract đã có:** GET /api/auth/csrf, AllowAnonymous; 200 JSON { csrfToken, headerName: "X-CSRF-TOKEN" }, no-store; antiforgery cookie do server đặt. Không có business DML. Anonymous không Touch; authenticated bootstrap sau guard có thể Touch session activity như API request hợp lệ. Riêng GET bootstrap với cookie auth malformed/expired/SQL-invalid được đi dưới anonymous identity để lấy token mới; SQL outage vẫn trả 503 từ authentication, không giả anonymous. Không miễn unsafe login/logout/change-password. Invalid cookie trên các endpoint khác vẫn 401; Login task sau phải thiết kế explicit re-login/recovery đối với cookie cũ, không mở bypass cho business endpoints.

**Identity/session binding:** SessionAntiforgeryAdditionalDataProvider thêm binding user ID/session ID/created ticks (không raw credential/hash) trong protected antiforgery token, bên cạnh user binding mặc định. Anonymous token không dùng cho authenticated identity; token của session A không dùng cho session B kể cả cùng UserId, nên rotation ChangePassword cần token mới. HotelOpsAntiforgery.Issue là helper cho controller API tương lai: SAU SQL commit/SignInAsync, caller phải gán HttpContext.User bằng principal mới trước khi gọi Issue; không giả định SignInAsync tự thay User. Không đọc snapshot cũ để cấp token mới, không log/cache request token ngoài memory FE. Bootstrap response được phép serialize đúng kênh HTTP no-store, ToString redacted.

**Restricted allowlist chính xác theo method/path** (case-insensitive path theo routing, chấp nhận trailing slash, không prefix match): GET /api/auth/session; GET /api/auth/csrf; POST /api/auth/change-password; POST /api/auth/logout. HEAD/OPTIONS không tự được thêm vào restricted allowlist chỉ vì CSRF-safe. Guard dùng SessionSnapshot đã qua SQL validation trong request, không client flag/header; thiếu snapshot cho authenticated request trả 401. Restricted ngoài allowlist trả 403 PASSWORD_CHANGE_REQUIRED, không Touch. Unsafe request sai CSRF có thể trả 400 trước restricted check; đây là thứ tự pipeline, không miễn CSRF trên restricted endpoints. Task này chỉ triển khai route bootstrap; ba route còn lại là contract cho task API sau.

**Touch:** ICookieSessionToucher là adapter interface mới tối thiểu, cùng scoped CookieSessionValidator/DbContext với validator. Adapter mở transaction SQL ngắn và gọi SqlSessionService.TouchSessionAsync, không sao chép logic expiry/update/revocation. SQL service re-read/lock user trước session và conditional UPDATE, giữ absolute/restricted cap, không lùi last_used_at/đổi created_at/hồi sinh expired/revoked. Touch chỉ áp dụng authenticated /api requests sau CSRF/restricted/authorization; anonymous/invalid-cookie/CSRF-failed/restricted-denied không Touch. Touch result phải khớp snapshot/user/session/created/restricted/absolute đã validate; invalid hoặc state change giữa validate/touch trả 401 và dừng endpoint. Outage (kể cả BeginTransaction/commit) trả 503 AUTH_SERVICE_UNAVAILABLE, không business mutation/renew/xóa cookie; lỗi khác vẫn 500. Cookie SlidingExpiration vẫn false, ticket không bị kéo dài. Validation và Touch là hai transaction nên vẫn có cửa sổ request đã qua checks trước reset; sensitive business workflow cần revalidate trong transaction riêng ở task SessionGuard.

**Validation Task 8B:** dotnet build HotelOps.slnx Passed, 0 errors, 4 warnings NU1902/NU1903 Pkcs 9.0.4. Harness ngoài repo dùng real framework Cookie/Antiforgery + controller bootstrap thật + fake SQL validator/toucher và pure SqlSessionPolicy: **62/62 checks Passed**. Bao gồm anonymous/invalid-cookie recovery bootstrap, cookie options/headers/no-store, POST/PUT/PATCH/DELETE missing/invalid/valid CSRF, không Touch hoặc chạy endpoint khi bị chặn, GET/HEAD/OPTIONS không token, restricted allowlist/method mismatch, expired temp/state mismatch/revoked/idle expired, Touch cap, fake outage503/invalid-between-validation-touch401, anonymous→authenticated token rejection và same-user session rotation token rejection rồi bootstrap mới thành công. Các route logout/change-password/session trong harness chỉ là stub ngoài repo, không phải API đã triển khai.

**Lỗi harness và giới hạn:** lần build harness đầu thiếu namespace Entities, đã sửa ngoài repo; lần chạy đầu còn concrete SQL adapter trong DI fake, đã remove trong harness. Thử TLS loopback bằng certificate tạm thất bại SSL unexpected EOF, chưa xác định nguyên nhân; sau đó chạy Kestrel HTTP loopback với middleware CHỈ TRONG HARNESS gán Request.Scheme=https để kiểm tra Antiforgery Secure behavior. Không có bypass HTTPS hoặc trust-certificate change trong HotelOps.Api. Không gọi kết quả này là kiểm thử TLS/browser SameSite/CORS thành công. SQL workflow/concurrency/outage thực tế vẫn **Not Run**, chưa có database test riêng được xác nhận; fake success không chứng minh EF SQL writes/concurrency đã đạt. Không mở connection hoặc thay đổi 26 users/10 sessions của HotelOpsDB trong task.

**Bàn giao FE/DEV2 sau khi Authentication hoàn thành:** FE bootstrap token theo identity hiện tại, giữ memory và gửi X-CSRF-TOKEN + credentials cho mọi unsafe /api write, cập nhật token sau login/rotation; không tự replay mutation sau 400/503. Restricted UI chỉ đi allowlist nhưng backend vẫn enforce; xử lý PASSWORD_CHANGE_REQUIRED riêng forbidden. DEV2 cookie-based API writes tự chịu CSRF pipeline chung, không exemption riêng/refresh flow; dùng shared validated identity và transaction guard khi có task tương ứng. Cần SQL integration tests, TLS/browser test, production key/deployment configuration, Login orchestration và các API còn thiếu trước khi cấp credential hoặc nghiệm thu Authentication hoàn chỉnh.

### 10.7. Decision log — Task 9A Login & Current User APIs (2026-10-18)

**DEV1 DECIDED — IMPLEMENTED, SQL workflow chưa nghiệm thu.** AuthenticationLoginService / IAuthenticationLoginService đăng ký scoped, sở hữu transaction và trả LoginResult integration riêng, không serialize credential. AuthController chỉ xử lý HTTP/cookie/CSRF; DTO ở DTOs/Authentication. Không triển khai Logout/Change Password hoặc RBAC.

**Identifier và eligibility:** request {username,password}; giới hạn 150/256, required, username trim, password giữ nguyên. Username SQL varchar(100) và email varchar(150) có UNIQUE riêng nhưng không bảo đảm uniqueness giữa hai cột. Lookup OR theo collation SQL thực tế, Take(2), không dùng ký tự @ để quyết định field, không lowercase/case-fold ở C#. Zero/multiple candidates dùng dummy verification và cùng 401 INVALID_CREDENTIALS, không tăng nhầm counter. Sau supported password đúng, dưới user lock kiểm tra lại resolution và account ACTIVE + deleted_at NULL; inactive/deleted vẫn cùng 401. Không tự suy ra employment status hay quyền role. Việc mapping/truy vấn được đối chiếu source Database First; chưa kiểm chứng lookup/cross-collision trên SQL thật trong task này.

**Transaction:** clean DbContext, không ambient/existing transaction; ReadCommitted. CredentialLockoutService lấy users UPDLOCK,HOLDLOCK, verify mới nhất, xử lý 5/30 và tạo proof gắn transaction. Ordinary failures commit counter/audit trước phản hồi; exception không retry, disposal rollback transaction còn mở. Session creation dùng cùng proof/transaction và recheck credential state, user lock trước session. SESSION_CREATE + LOGIN_SUCCESS audit và last_login_at cùng transaction. SuccessRehashNeeded chỉ hash lại bằng PasswordCredentialService sau khi proof được consume; update có điều kiện đúng hash/temp snapshot, affected rows = 1, không clear temporary expiry hoặc ghi đè user snapshot. Chỉ trả success sau CommitAsync thành công. Commit failed/unknown không cấp cookie/success, không tự retry/rollback bù; phải điều tra trạng thái SQL thật. Reset và Login serialize bằng cùng user lock; SQL race/deadlock behavior chưa nghiệm thu. Các consumer đổi credential phải tiếp tục tuân thủ protocol này.

**Errors:** 400 VALIDATION_FAILED (không echo model values/binding exception chứa password); 400 CSRF_VALIDATION_FAILED; 401 INVALID_CREDENTIALS chung unknown/wrong/unsupported/inactive/deleted/ambiguous. Password đúng + account eligible đang khóa: 423 ACCOUNT_LOCKED, parameters.lockedUntil UTC. Temporary đúng nhưng hết hạn: 403 TEMPORARY_PASSWORD_EXPIRED, không clear marker. Nếu deadline trôi qua giữa các checks, session service vẫn fail closed; boundary/race SQL cần kiểm thử. Availability exception ở query/BeginTransaction/commit được wrap SessionStoreUnavailableException → 503 AUTH_SERVICE_UNAVAILABLE. Lỗi programming/schema/deadlock khác vẫn 500, không giả thành 401/503. Không log password/hash/cookie/credential. Rate limiting 429 chưa triển khai.

**Cookie và CSRF:** sau service commit mới HotelOpsCookieTicket.Create + SignInAsync, nonpersistent, absolute cap 12h hoặc min(15 phút, temporary expiry); SQL idle vẫn authoritative. Gán HttpContext.User bằng principal mới trước HotelOpsAntiforgery.Issue. SessionDto trả token mới bound user/session, token anonymous hoặc session cũ bị reject; giữ antiforgery cookie an toàn, không cần đổi cookie đó để đổi request-token identity. Response chỉ user {id:string,username,name}, expiresAt, absoluteExpiresAt (không nullable), mustChangePassword, csrfToken; UTC Z, no-store, không grants/scopes/EF entities/credential.

**Stale-cookie recovery:** chỉ GET /api/auth/csrf và POST /api/auth/login được đi tiếp anonymous khi cookie invalid/expired/revoked. Login vẫn qua CSRF middleware; FE bootstrap lại với cookie hiện tại để có anonymous token, sau đó Login; token authenticated cũ không dùng lại. Không signout/xóa cookie để che lỗi SQL, không skip authentication validation. Cookie validator gặp outage vẫn chặn trước Login bằng 503. Cookie mới phát khi Login thành công thay cookie cũ.

**Existing-session policy:** normal session hợp lệ + CSRF đúng → 409 AUTHENTICATED_SESSION_EXISTS, không gọi Login service/tạo session/revoke session cũ; chuyển tài khoản phải Logout (Task 9B). Restricted session bị guard chặn Login bằng 403 PASSWORD_CHANGE_REQUIRED; không mở rộng allowlist. Normal valid request có thể Touch theo policy hiện hành trước 409; CSRF failed/restricted denied không Touch. Không tự xóa các session thiết bị khác. Hai request Login anonymous đồng thời vẫn có thể tạo hai session riêng sau serialize user; v1 không có single-session guarantee. SQL commit thành công nhưng HTTP delivery fail có thể để session không dùng, được giới hạn lifetime; chưa có compensation workflow.

**GET /api/auth/session:** Authorize HotelOpsCookie; normal/restricted được phép. Middleware Validate + Touch giữ nguyên. Service mở transaction riêng, gọi ValidateSessionAsync lại rồi đọc profile AsNoTracking trong cùng user/session locks; không dùng profile client hoặc snapshot trước reset. Sai session ID/invalid → 401 SESSION_INVALID; không cookie → 401 AUTHENTICATION_REQUIRED; outage → 503. Trả SessionDto + token bound current principal, không Set-Cookie auth mới. Snapshot phản ánh thời điểm transaction; revocation sau commit không thể thu hồi một response đã gửi.

**Validation:** dotnet build HotelOps.slnx **Passed**, 0 errors, 4 warnings NU1902/NU1903 của System.Security.Cryptography.Pkcs 9.0.4 (restore/build lặp cùng hai advisories). Harness ngoài repo **120/120 checks Passed**, gồm API validation, cookie issuance/no-cookie-on-failure, identity-bound CSRF, stale-cookie recovery, 409 session-switch policy, restricted DTO/guard, 401/403/423/503 và programming error 500, no-store/no-redirect; password và lockout pure checks. Lần harness đầu fail vì áp kỳ vọng no-store của auth response lên business endpoint stub 204; đã sửa kỳ vọng trong harness, không thay cache policy sản phẩm. Không có test project trong solution. Harness ngoài Git dùng real Cookie/Antiforgery/controllers/middleware, fake Login orchestration + SQL validator/toucher; pure PasswordCredentialService và CredentialLockoutPolicy. Fake commit failure kiểm tra không phát cookie nhưng không chứng minh SQL commit ambiguity handling. Unknown/ambiguous lookup, SQL writes/audit/rehash, Login-reset concurrency, deadlock và SQL availability thật: **Not Run** (chưa có database test riêng được xác nhận). Không truy cập/ghi HotelOpsDB hoặc cấp credential/session cho 26 users/10 legacy sessions. Browser HTTPS/SameSite/CORS thực tế: **Not Run**; harness HTTP loopback chỉ gán scheme=https trong test, không sửa security policy ứng dụng.

**Tiếp theo Task 9B:** Logout/revocation + clear cookie; Change Password transaction/revocation/rotation/token mới; hoàn thiện provisioning có kiểm soát, SessionGuard/CurrentUserAccessor theo task được giao; SQL test DB riêng, browser HTTPS và production Data Protection/deployment trước nghiệm thu. Latest FE source vẫn chưa xác nhận; cần bàn giao DTO user thay principal/grants, bootstrap và token rotation, lỗi 400/401/403/409/423/503; không coi FE snapshot là contract đã tích hợp.

### 10.8. Decision log — Task 9B Logout, Change Password & Rotation (2026-10-18)

**DEV1 DECIDED — IMPLEMENTED, SQL workflow/concurrency chưa nghiệm thu.** AuthenticationCredentialWorkflow / IAuthenticationCredentialWorkflow scoped sở hữu transaction Logout/ChangePassword riêng, phối hợp ISqlSessionService và shared ISessionRevocationService, không thêm schema/mapping hoặc quyền RBAC. Thêm ChangePasswordRequest, ChangePasswordResult integration (không phải DTO), PasswordChangePolicy và AuthenticationAudit. AuthController xử lý HTTP/cookie, không chứa business workflow. Những nhận xét “chưa triển khai API” trong log các task trước phản ánh thời điểm task đó; mục này thay thế trạng thái hiện hành cho hai API.

**Password policy v1:** DEV1 quyết định tối thiểu 8, hoa/thường/số/special, khác password hiện tại. SRS GBR-022/SYS-PAR-03/04 xác nhận minimum/complexity, không nêu maximum; password_hash varchar(255) lưu hash, không phải giới hạn độ dài plaintext. Chốt maximum 256 để đồng bộ Login hiện có, không truncate/normalize; current/new/confirm đều required, max256. Length tính UTF-16 theo StringLength/.NET hiện tại; Unicode upper/lower/digit được nhận, special là Unicode punctuation/symbol, whitespace không được tính special; không nhận control characters trong password mới. Spaces được bảo toàn. Confirmation so sánh ordinal; mismatch 400 VALIDATION_FAILED. New password cùng current credential qua PasswordCredentialService → 400 PASSWORD_POLICY_VIOLATION. Hash/verify chỉ dùng PasswordCredentialService Identity V3, không fallback legacy. Sai current password → 400 CURRENT_PASSWORD_INVALID, không thay đổi login counter/lock state; policy 5/30 chỉ áp dụng Login. Thành công reset failed_login_count=0, locked_until=NULL. Chưa bổ sung rate limiting cho Change Password, cần hoàn thiện/kiểm thử chống abuse trước deployment.

**POST /api/auth/logout:** AllowAnonymous, không request DTO/body binding/session ID do client chọn; luôn qua CSRF. Identity hợp lệ dùng protected reference, transaction ReadCommitted, SqlSessionService.RevokeSessionAsync khóa user trước session, conditional revoke và audit; false chỉ là SQL xác nhận missing/already revoked. Commit xong mới SignOutAsync HotelOpsCookie → 204 no-store, principal về anonymous. Không revoke thiết bị khác, không cấp token mới trong 204; FE xóa token session trong memory và GET bootstrap anonymous trước mutation tiếp theo. Authenticated principal thiếu reference fail closed 401. Cookie invalid/expired/revoked được đi tiếp qua InvalidCookieMiddleware riêng cho đúng POST Logout; CSRF cũ có thể không còn khớp anonymous, FE bootstrap rồi thử lại. Không dùng raw ticket chưa xác minh hay legacy row để khôi phục session. Anonymous/no usable ticket đã xác định thì 204 không cần mở SQL; đây không phải khẳng định đã revoke một session chưa biết. Nếu cookie cần SQL validation mà SQL outage, pipeline 503 trước controller, không xóa cookie. Revoke/commit outage cũng 503, không success/SignOut giả. Tampered/unreadable cookie chỉ được xóa, không suy ra credential từ client. SessionTouch skip đúng POST Logout để expiry/revoke giữa validation và action không biến thao tác idempotent thành 401; validation CookieEvents, CSRF và Restricted Guard vẫn giữ nguyên.

**POST /api/auth/change-password:** bắt buộc HotelOpsCookie + CSRF; normal/restricted được guard cho phép, Touch policy hiện tại giữ nguyên. Service validate request/policy rồi mở ReadCommitted trên clean DbContext, không ambient/existing transaction. ValidateSessionAsync khóa users UPDLOCK,HOLDLOCK trước session, đối chiếu user/session ID/created/restricted. Đọc user AsNoTracking dưới lock, verify current password và chống reuse, hash mới. Validate lại session sau hashing, đọc lại hash/temp/account/deleted state và kiểm tra expiry ngay trước mutation. Expired temporary không được clear marker hoặc tạo normal session; recovery/reset riêng. Update có điều kiện hash/temp/account/deleted, affected rows=1, chỉ password_hash/temp expiry/counter/locked_until/updated_at. Không dùng tracked snapshot ghi đè profile hay last_login_at.

Trong cùng transaction: clear temporary_password_expires_at, reset lockout; shared SessionRevocationService.RevokeAllAsync revoke tất cả session cũ (kể cả legacy rows của user đó); tạo single-use CredentialVerificationProof nội bộ gắn transaction ID + hash mới + temp NULL sau verified mutation/revocation; SqlSessionService.CreateSessionAsync consume proof và re-read credential trước session insert. Không mở public proof factory, không sửa interface SQL Session Service/nhân bản logic create hoặc revoke-all. Assert normal state, session ID và raw credential khác phiên cũ; session mới idle30m/absolute12h. SESSION_CREATE audit và PASSWORD_CHANGE audit cùng transaction. Chỉ CommitAsync thành công mới trả CreatedSqlSession; controller phát protected cookie, gán User principal mới, trả SessionDto mustChangePassword=false và CSRF token mới bound session mới. Token cũ không hợp lệ với cookie mới; cookie cũ đã revoke không dùng được cho mutation. Không nâng claim của restricted cookie cũ.

**Concurrency/errors:** mọi writer theo user-first lock; reset/change/login cùng user serialize. Reset/change commit trước làm session/credential cũ bị từ chối ở lần revalidation sau. Change commit trước thì reset sau revoke cả session mới; cookie vừa phát có thể đã invalid và request sau phải kiểm tra SQL. Row conditional/concurrency failure → 409 AUTH_STATE_CHANGED sau disposal transaction; không retry. Session đã invalid → 401, không bắt buộc phân biệt nguyên nhân reset/revoke cho client. SQL availability error query/Begin/commit → 503; programming/schema/deadlock khác vẫn 500. Commit failed/unknown không cookie/success, không retry hoặc transaction bù mù quáng; transaction chưa commit rollback khi dispose, commit-unknown phải điều tra trạng thái thực tế. SQL và HTTP không atomic: response/cookie delivery fail sau commit có thể để người dùng phải Login/recovery và một session mới không dùng đến, bounded bởi lifetime. Không khôi phục password/session cũ để che lỗi.

**Audit compatibility — metadata SQL đã xác minh READ-ONLY:** đã xác minh READ-ONLY trên SQL Server Express của môi trường development, database HotelOpsDB. audit_logs.action varchar(40) NOT NULL, entity_type/entity_id varchar(100) NOT NULL; old_values/new_values/changed_fields là nvarchar(max) nullable; occurred_at datetime2 NOT NULL; actor/branch FK nullable. CK_audit_logs_action trusted/enabled chỉ nhận CREATE/UPDATE/DELETE/APPROVE/REJECT/CANCEL/LOGIN/LOGOUT. CK old_values/new_values/changed_fields yêu cầu NULL hoặc ISJSON=1, đều trusted/enabled. Các task trước đã bỏ sót ràng buộc action và JSON changed_fields: labels LOGIN_SUCCESS/SESSION_CREATE/LOGIN_LOCKOUT/CREDENTIAL_RESET... không thể ghi trực tiếp vào action, chuỗi CSV changed_fields cũng không hợp lệ. Đã sửa tất cả caller Authentication liên quan, không nới CHECK hoặc đổi schema: Login action LOGIN, single-session revoke action LOGOUT, lockout/provision/reset/password change/session creation/revoke-all action UPDATE; event chi tiết ở new_values.authenticationEvent. changed_fields serialize JSON array, Old/New values JSON hoặc NULL; không ghi password/hash/raw credential/cookie vào audit. AuthenticationAudit factory dùng chung; ActorUserId có ở login/change/provision/reset như trước, event session/lockout không giả danh actor chưa xác minh. Việc sửa audit các service trước chỉ đổi representation, không mở rộng nghiệp vụ Admin Reset hoặc tự thực thi provisioning.

**Validation thực hiện:** dotnet build HotelOps.slnx **Passed**, 0 errors, 4 warnings NU1902/NU1903 Pkcs 9.0.4 có sẵn. Harness ngoài repo **287/287 checks Passed**, bao gồm checks Task 9A và Task 9B: Cookie/CSRF/controllers/middleware thật; fake credential workflows/store/commit error, pure PasswordCredentialService/lockout/password policy/session policy. Kiểm tra Logout normal/restricted/anonymous/invalid, bootstrap và token cũ sau Logout; failure503 không delete cookie; Change current password sai, confirmation/complexity/reuse/length256/257, restricted→normal, cookie/session ID/key mới, old cookie/token bị từ chối, 409/503 không phát cookie, expiry chặn và marker giữ nguyên. Audit factory/session audit output được kiểm tra against CHECK action/JSON/length vừa đọc; chưa INSERT audit thật. Một warning CS0649 chỉ ở harness do field không dùng đã được xóa; lần build harness cuối chỉ còn warnings NuGet cũ. Harness không mở SQL, HTTP loopback gán scheme=https chỉ trong test, chưa chứng minh TLS/browser cookie policy thực tế.

**Not Run / trước E2E:** SQL ghi/rollback/audit INSERT/commit ambiguity, concurrent changes, reset-vs-change, Logout-vs-Touch thật **Not Run**, chưa có test DB riêng được xác nhận. Fake workflows thành công không chứng minh AuthenticationCredentialWorkflow SQL execution/locking đã đạt. Harness chỉ mô phỏng state/revoke/rotation/errors; check Touch sau revoke dùng pure policy, không phải SQL concurrency test. Không dùng 26 users/10 sessions development để thử, không reset/provision user thật. Task này chỉ đọc sys metadata audit, không DDL/DML. Cần database test riêng + fixture credentials V3 hợp lệ, SQL workflow/concurrency suite, provisioning/delivery được kiểm soát, browser HTTPS/FE contract mới nhất, production Data Protection/deployment và shared guard/accessor theo task được giao trước nghiệm thu toàn bộ Authentication. Source FE hiện hành vẫn chưa xác nhận; FE cần xử lý các tab bị revoke sau Change Password và xóa token sau Logout. Không đồng nghĩa RBAC/Permission Matrix đã được duyệt.

## 11. Báo cáo bàn giao và checklist trước merge

### 11.1. Nội dung báo cáo sau khi hoàn thành Authentication

DEV1 tổng hợp và thông báo cho team **sau khi hoàn thành chức năng, trước khi merge thay đổi dùng chung**. Đây là bàn giao/tác động tích hợp, không là yêu cầu xin phê duyệt kỹ thuật từng bước. Thông báo team sẽ do developer thực hiện hoặc giao nhiệm vụ gửi riêng; không tự gửi bằng công cụ trong task tài liệu.

| Hạng mục | Nội dung bắt buộc trong báo cáo |
|---|---|
| Database schema | Rename cột session; 3 users columns, kiểu/nullability/default/check; index/constraint trước/sau; scripts/preconditions/credential handling; mọi deviation có lý do |
| EF Core | File/property/mapping/index references đã đổi; Database First procedure; diff ngoài phạm vi nếu có; xác nhận không EF migrations |
| Authentication APIs/DTO | Method/routes, requests/responses/examples, statuses/error codes, auth/CSRF requirements, core auth vs authorization extension |
| Cookie/CSRF/session | Giá trị cuối do DEV1 chốt, sliding/polling/absolute/restricted behavior, login/logout/change/reset revocation, CSRF bootstrap/identity rotation, key ring và environment config |
| Shared services cho DEV2 | Interfaces, DI/ownership, CurrentUserAccessor context, SessionGuard transaction/lock ordering/cancellation, audit/error integration và ví dụ dùng; không refresh flow riêng |
| FE integration | Contract/schema khác snapshot, route/password DTO/session.user changes, credentials/CSRF, restricted navigation, 401/403/423/429/503, multi-tab/cache/polling; latest FE chưa xác nhận và tests cần FE chạy |
| Test results | Lệnh build/test, Passed/Failed/Not Run, warnings, SQL/API/browser/concurrency/load evidence, môi trường, limitations/known issues; không gọi RBAC fully verified |
| Breaking changes | Cột/property cũ không dùng được, mixed backend versions không hỗ trợ, cookie/session invalidation, API DTO changes, consumers cần update |
| Deployment/rollback | Thứ tự coordinated cutover, maintenance/drain, backup/keys/config, dependencies, smoke checks, rollback trước/sau credential writes và rollback không làm mất security state |
| RBAC ngoài scope | Pending Permission Matrix/data_scope/role rules; xác nhận không tự gán permission hay mở rộng quyền từ core Authentication |

### 11.2. Checklist triển khai và bàn giao — không phải Approval Checklist

| Chủ thể | Trách nhiệm / đầu ra | Trạng thái hiện tại |
|---|---|---|
| DEV1 | Quyết định và thực hiện toàn bộ Authentication; B và shared-service ownership đã chốt, chi tiết policy còn tự chốt | Quyền quyết định CONFIRMED; chưa implementation trong task này |
| DEV1 | Ghi decision log, kết quả build/tests, scripts/EF/API/security/service contract và deployment/rollback notes | Chưa thực hiện; theo task implementation |
| DEV1 → DEV2 | Thông báo shared DB/EF/service/CSRF/SessionGuard impact trước merge | Chưa bàn giao; không là gate duyệt kỹ thuật từng bước |
| DEV1 → FE | Bàn giao API/DTO/cookie/CSRF/UI integration và limitations của snapshot | Chưa bàn giao; FE source thực tế chưa xác nhận |
| DEV1 → team/PM | Thông báo behavior cuối và RBAC/business requirements ngoài scope còn thiếu | Chưa bàn giao; không yêu cầu PM duyệt kỹ thuật Authentication |
| Developer phụ trách merge | Theo quy trình Git/team còn hiệu lực; chỉ sau khi có báo cáo/thông báo shared changes | Không commit/push/merge trong task hiện tại |

### 11.3. Validation của nhiệm vụ tài liệu

Chỉ chỉnh sửa file Markdown này; không source/EF/frontend/SQL/migration/scaffold. Build/tests: **Not Run**, vì chỉ thay tài liệu. API/SQL runtime chưa được kiểm chứng trong nhiệm vụ này; T0 connectivity là thông tin developer cung cấp. Quy trình cũ chờ approval từng bước Authentication đã được thay thế; không tự coi các đề xuất kỹ thuật chưa chốt là DEV1 DECIDED hoặc Permission Matrix đã approved.
