# Bàn giao tài khoản Project 2

Đã triển khai phạm vi `plans/261123-ux-role-refinements/accounts.md`. Không sửa shared files, schema, grants; không ghi primary hoặc dừng listener. Parent tiếp tục tích hợp và kiểm tra toàn hệ thống.

## Đã triển khai

- `UserService`: mã `NV` + số tăng từ user id lớn nhất, kiểm tra mã đã tồn tại; SQL application lock trong transaction Serializable tuần tự hóa các lần tạo qua API, unique index hiện hữu chặn trùng. Username/email được kiểm tra sau trim, giữ nguyên uniqueness của SQL Server. Cho phép người khác nhau cùng họ tên.
- Tạo user + BRANCH_ACCESS primary + EMPLOYMENT nếu chọn bộ phận + audit trong một transaction. Kiểm tra chi nhánh/bộ phận active và cùng branch. Không tự gán role.
- `GET users` không còn cắt thầm ở 500 người; có `departments` từ membership đang hiệu lực (EMPLOYMENT, DEPARTMENT_AUTHORITY hoặc BRANCH_ACCESS có department), không suy đoán từ role. Bộ lọc ưu tiên field này, vẫn hỗ trợ response cũ.
- Form tạo bỏ nhập employeeCode, có phone, branch, optional department, phát hiện trùng username/email đang tải, checklist mật khẩu. Bảng thêm STT liên tục theo trang và điện thoại, tìm theo phone, giữ count/phân trang.
- Hồ sơ riêng chỉ sửa FullName/Email/Phone theo Actor.Id, kiểm tra RowVersion, audit và unique email. DTO không nhận id/role/branch/status. Tất cả role đã đăng nhập sử dụng được; phiên bị hạn chế vì mật khẩu tạm vẫn phải đổi mật khẩu trước theo middleware hiện hữu.

## Tích hợp do parent sở hữu

1. `app.tsx`: import default `PersonalProfile` từ `./personal-profile`; hiển thị `<PersonalProfile close={() => setOpen(false)} />` từ menu tài khoản cho mọi role, không yêu cầu USER_VIEW/USER_MANAGE.
2. `auth.tsx`: đổi minLength 12 thành 8 cho mật khẩu mới; dùng `PasswordChecklist`, `passwordCriteria`, `accountMessage` từ `./account-policy`. Checklist nhận `password`, optional `currentPassword`, optional `id`. Khi đổi mật khẩu, truyền currentPassword để hiện điều kiện khác mật khẩu cũ.
3. Chính sách chung: 8–128 UTF-16 code units, ít nhất A–Z, a–z, 0–9 và một Unicode punctuation/symbol; whitespace không tính ký tự đặc biệt. Đổi mật khẩu phải khác mật khẩu hiện tại. Backend đã áp dụng cả tạo và đổi mật khẩu; `Common.cs` giữ DTO cũ nhưng controller dùng DTO mới trong `AccountRequests.cs`.
4. Có thể đưa mapping lỗi trong `account-policy.tsx` vào `api.ts` chung nếu cần dùng ngoài các component này; không đổi mã lỗi.

## API

- `GET /api/v2/users/creation-options`: USER_MANAGE global như create hiện hữu; `{branches:[{branchId,name}], departments:[{departmentId,branchId,name}]}`.
- `POST /api/v2/users`: `{username,fullName,password,branchId,email?,phone?,departmentId?}`; bỏ employeeCode, branchId bắt buộc. Trả ResourceDto chứa employeeCode sinh ra. Tất cả ghi và audit commit cùng nhau.
- `POST /api/v2/auth/password`: route không đổi, DTO `ChangeAccountPasswordRequest`, minimum8 và kiểm tra policy server.
- `GET /api/v2/auth/profile` và `PUT /api/v2/auth/profile`: GET trả `{userId,employeeCode,username,fullName,email,phone,rowVersion}`. PUT nhận `{rowVersion,fullName,email,phone}`, trả cùng shape/version mới; chỉ actor hiện tại.
- 400: PASSWORD_POLICY_NOT_MET, INVALID_USERNAME, INVALID_ACCOUNT_NAME/EMAIL/PHONE/BRANCH/DEPARTMENT, INVALID_ROW_VERSION; DataAnnotations cũng trả 400.
- 409: USERNAME_ALREADY_USED, EMAIL_ALREADY_USED, STALE_ROW_VERSION, TRANSACTION_RETRY_REQUIRED; unique constraint race vẫn có fallback DUPLICATE_RECORD từ middleware hiện hữu. 401/403 giữ auth/grant hiện hữu.

## Kiểm chứng

- Full build/test solution: Not Run theo phân công (parent tích hợp).
- Compile API `/t:Compile`: Failed bởi 6 phương thức trùng trong `WorkdayController.cs` ngoài phạm vi: BatchCandidates, AssignBatch, Configuration, CreateShift, UpdateShift, Calendar. Không sửa file của delegate khác.
- `npx tsc --noEmit`: Passed. Prettier đã format 4 tệp frontend sở hữu.
- Compile riêng 8 tệp C# tài khoản bằng Roslyn/reference assemblies hiện có: Passed. Hai warning CS1701 do Microsoft.Data.SqlClient tham chiếu System.Data.Common/System.Runtime 9.0 trong bộ reference net10; đây là compile riêng, không thay thế solution build. Lỗi tên biến truy vấn `departments` phát hiện ban đầu đã sửa thành `directoryDepartments` rồi compile lại thành công.
- Harness C# chạy đúng `AccountRequests.cs`: Passed 11 ca policy (đủ/thiếu từng tiêu chí, Unicode symbol, độ dài 128/129, null), 7 ca DTO (branch/department/email/phone/password). Test xUnit tương ứng đã thêm để parent chạy. Harness ban đầu dùng validation property thay vì constructor record của MVC; đã sửa và chạy lại thành công. Output harness ban đầu bị Windows giữ file khi fail; lần chạy xác nhận dùng `PolicyChecksVerified.dll` riêng, không dừng process.
- `node frontend/tests/compact-account-refinements.mjs`: Passed. API hoàn toàn intercepted, không login/SQL thật. Kiểm tra phone/STT liên tục/count, DEPARTMENT_AUTHORITY filter, duplicate username/email, password đúng 8 ký tự, same-name được chấp nhận, branch/dept payload, self-profile không có grant quản trị, safe-fields-only, 409 giữ bản đang nhập/khóa lưu/reload, không tràn trang 1440/768/375px.
- Test browser ban đầu lỗi selector combobox và harness nạp React/auth context HMR không cùng module; đã sửa harness và chạy lại thành công, không sửa sản phẩm để lách kiểm tra.
- SQL thực/transaction concurrency và HTTP backend mới: chưa chạy; không ghi primary. Parent cần kiểm tra trên DB restore đã backup.
- Các ca SQL cần chạy sau tích hợp: create song song không trùng mã; duplicate username/email; branch/dept sai không để lại user/membership/audit; PUT profile chỉ đổi actor, không đổi userId/role/status/branch, stale RowVersion trả 409.

## Tệp thay đổi

- Frontend: `admin.tsx`, `account-filters.tsx`; mới `account-policy.tsx`, `personal-profile.tsx`, `frontend/tests/compact-account-refinements.mjs`.
- Backend: `Services/UserService.cs`, `Services/AccountManagementService.cs`, `Services/AuthService.cs`, `Controllers/UsersController.cs`, `Controllers/AuthController.cs`, `Contracts/UserDirectory.cs`; mới `Contracts/AccountRequests.cs`.
- Test mới: `tests/HotelOps.Compact.Tests/AccountRefinementTests.cs`.
