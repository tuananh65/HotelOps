# Kết quả Task02 — EF Core DataScope Mapping

**PASS WITH NOTES**: mapping và verification trên HotelOpsDB thực tế đạt yêu cầu; còn cảnh báo NuGet NU1900.

- EF property `DataScope`: string, required, varchar(20), column data_scope, default SQL ('BRANCH').
- SQL schema đã kiểm tra chỉ đọc: varchar(20) NOT NULL, default ('BRANCH'); CHECK cho phép ALL/BRANCH/DEPARTMENT/OWN. Không chỉnh schema hoặc dữ liệu.
- Live RBAC: 12 roles, 10 active, 62 permissions, 140 grants; ALL=8, BRANCH=81, OWN=51, DEPARTMENT=0.
- `dotnet build HotelOps.slnx`: PASS, 0 warnings, 0 errors.
- `dotnet test HotelOps.slnx --no-restore`: exit 0; solution không có test project, không báo đây là automated test suite đã chạy.
- DataScope console: **15/15 PASS**, gồm metadata và EF query/raw SQL đối chiếu từng role_permission_id, full entity materialization, PK/index/relationships.
- CurrentUserAccessor console: **14/14 PASS**.
- HTTP integration: Not Run, ngoài phạm vi mapping; không dùng console checks làm bằng chứng HTTP authentication.

Lần DataScope run trong sandbox gặp lỗi kết nối SQL encryption và warning NU1900. Chạy lại ngoài sandbox với nguyên cấu hình local đã kết nối và PASS 15/15; không đổi encryption/connection string. Warning NU1900 vẫn hiện do không tải được vulnerability metadata từ api.nuget.org, nên việc kiểm tra vulnerability online chưa được xác minh.

Sửa hai file generated theo scope đã duyệt: RolePermission.cs và HotelOpsDbContext.cs. Thêm project console, Program.cs, README.md và RESULTS.md trong thư mục này. Không thay Authentication, CurrentUserAccessor, catalog, Task03B hoặc dữ liệu nghiệp vụ. Hướng dẫn scaffold tương lai nằm trong README.

Không commit/push/merge/PR; giữ nguyên các thay đổi có sẵn.
