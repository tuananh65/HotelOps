# Task22 verification

## Build và regressions

- dotnet build HotelOps.slnx: PASS,0 warnings/0 errors; chạy lại --no-restore cũng PASS,0/0.
- dotnet test HotelOps.slnx --no-restore: exit0; solution không discover console verification harnesses, không coi đây là test coverage.
- PermissionService43 + CurrentUserAccessor14 + Authorization61 + ResourceAuthorization49 =167 checks PASS; không --live, không kết nối HotelOpsDB. HTTP authorization regressions dùng cookie/session doubles và production handler, không thay thế SQL-session tests.
- UserAccountDetail harness build PASS nhưng còn NU1900 (không lấy được NuGet vulnerability index), restore escalated cũng không giải quyết. Không tắt audit hoặc sửa dependency để che warning.
- UC141 harness build PASS,0 warnings/0 errors.

## SQL / HTTP execution

Direct SQL detail tests đã PASS: positive account projection, single query, no credential/access graph columns, no tracking, soft-delete hidden và cancellation. Disposable trial được tạo bằng guarded existing setup; không sửa canonical seed hoặc SQL sources.

Các lượt đầu: SSPI trong sandbox thất bại trước setup; escalated setup thành công. Detail và nguyên UC141 harness đều timeout20s ở protected request đầu sau CSRF readiness. Detail retry60s nhận phản hồi không đạt401. Không ghi các lượt này là PASS. Harness readiness sau đó yêu cầu protected /api/users trả401 và chỉ retry trong startup, assertions sau ready giữ nguyên.

Lượt cuối PASS: detail/list harness234/234; UC141 riêng144/144. Hai files task22-final-results.local.json và task22-list-final-results.local.json giữ local, không chứa credential/personnel raw rows. Harness mới thừa kế label “UC141 checks”;234 là tổng list+detail, không phải234 ca detail độc lập.

Đã thực thi HTTP thật trên Cookie Authentication + SQL sessions: anonymous/invalid cookie401; authenticated no permission/restricted/branch-bound ALL/inactive assignment/expired or future assignment/OWNER403, kể cả nonexistent target để tránh existence leak; ID0/negative/nonnumeric/overflow400; absent/deleted404; active/inactive/other-branch/null-scope200 với exact scalar+nested DTO whitelist và no-store; revoked grant403, revoked/expired SQL session401. BRANCH/OWN/unknown scope và inactive role có explicit grant paths được xác minh bằng PermissionService doubles, không tự sửa catalog để cấp grant test.

Direct SQL đã xác nhận đúng một query detail không credentials/access graph, no tracking/cancellation; read phase giữ nguyên users/user_roles/user_branch_access và catalog/master fingerprints. PrimaryBranch/Department IDs và values đối chiếu fixtures; phone kiểm tra cả non-null và null.

## Safety

Mỗi lượt dùng trial mới; không reset hoặc reuse personnel. Testfinally revoke sessions của owned identities, không drop database. Catalog/master fingerprints và users/assignment read-phase invariance được harness kiểm tra; SQL session activity là tác động hợp lệ của authentication.

SELECT hậu kiểm trên cả năm trial (kể cả failed attempts):0 unrevoked sessions. Trials giữ lại, không drop/reset. Task không kết nối HotelOpsDB; setup chỉ kiểm tra instance/database existence ở master trước khi tạo trial mới.

Final safety: baseline571 files, chỉ4 file có trước bị sửa đúng scope (UsersController, IUserDirectoryService, UserDirectoryService, UserDirectory/Program.cs readiness).567 file còn lại nguyên SHA-256, gồm Task20A/Task21 và local artifacts. Thêm DTO, project+harness detail,4 Markdown và2 local evidence JSON. Program.cs ứng dụng/DEV2/schema/catalog giữ nguyên. git diff --check PASS, index trống; Git chỉ cảnh báo LF sẽ normalize CRLF, không whitespace errors. Không commit/stage/push/merge.
