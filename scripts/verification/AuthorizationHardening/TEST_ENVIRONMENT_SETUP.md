# Local setup — DEV1/DEV2 và clean checkout

## Prerequisites

.NET SDK theo global.json, Windows Development (cookie infrastructure hiện yêu cầu Windows/DPAPI), SQL Server **2022+** và Full Schema/RBAC Seed chuẩn trong scripts/database/release. Actual validation ở SQL Server 16.0.1000.6 Express, compatibility 160. Không tắt NuGet auditing khi restore lỗi mạng; retry với network permission phù hợp.

Không cần RbacUpgradeDeployment, RbacUpgradeTrial, deployment-state hoặc credentials/session của DEV1. Commit source cần cả Shared/ReadOnlyVerification.cs và linked Compile entries trong bốn csproj. Local receipts/provisioning checkpoints/fixtures JSON và secrets không được copy tùy tiện vào Git để làm setup hoạt động.

## A. Live read-only verification trên HotelOpsDB

DBA review [PROVISION_READ_ONLY_REVIEW_ONLY.sql](PROVISION_READ_ONLY_REVIEW_ONLY.sql), không tự chạy. Principal riêng không có sysadmin/db_owner/server admin/impersonation/execute/write/DDL grants, không kế thừa nhóm quyền rộng. Draft chỉ tạo DB user từ existing approved server login, grants SELECT trên các bảng cần đọc và giới hạn columns users; không có password/login secret. Existing user phải inspect, không overwrite. Cần xác nhận server đích trước review execute. Không chạy template bằng automation hoặc migration.

Thiết lập **ConnectionStrings:AuthorizationReadOnly** bằng .NET User Secrets ID **HotelOps.Authorization.Verification**, hoặc environment **ConnectionStrings__AuthorizationReadOnly**. Giá trị phải do developer nhập riêng qua secret editor/local environment, Initial Catalog=HotelOpsDB; không truyền giá trị secret trong CLI args/chat/log. Không sửa ConnectionStrings:HotelOpsDb của API và không sao chép application connection làm credential read-only.

Nếu thiếu config hoặc principal có quyền ghi/DDL/execution/admin: runner in BLOCKED, exit 2 trước SQL assertions. ApplicationIntent=ReadOnly không làm account thành read-only. Chỉ khi preflight chấp nhận và live suites thực chạy Passed mới có thể báo đã xác minh least privilege. Guard sync/async chặn nonquery/batch/write và SaveChanges; không cấp quyền để tránh block.

```powershell
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj -- --model-only
dotnet run --project scripts/verification/DataScope/DataScope.Verification.csproj
dotnet run --project scripts/verification/PermissionService/PermissionService.Verification.csproj -- --live
dotnet run --project scripts/verification/Authorization/Authorization.Verification.csproj -- --live
dotnet run --project scripts/verification/ResourceAuthorization/ResourceAuthorization.Verification.csproj -- --live
```

DataScope model-only không mở connection. Ba package còn lại bỏ --live để chạy offline/doubles/loopback. Live assertions có fixture IDs lịch sử của HotelOpsDB; nếu dữ liệu thay đổi hợp lệ, kiểm tra expectation với developer, không reset dữ liệu chính để test pass.

## B. Integration database độc lập

Tên được cho phép cố định **HotelOps_Authorization_Test**. DEV2 dùng SQL instance riêng/test DB được cấp phép. Không trỏ về HotelOpsDB hoặc tùy tiện đổi target name. SQL test runtime principal nên có quyền cần thiết chỉ trong test DB; quyền CREATE DATABASE/provision schema tách riêng, do DBA/developer phê duyệt. Task08 chưa tự cấp các quyền này.

Lưu **ConnectionStrings:Integration** trong User Secrets ID **HotelOps.Authorization.Integration.Task06** hoặc env **ConnectionStrings__Integration**. Expected server canonical có thể đặt **Integration:ExpectedServer** trong secrets hoặc **HotelOpsIntegration__ExpectedServer** trong env; nếu không đặt dùng DataSource trong connection. Receipt/SQL guard kiểm tra SERVERPROPERTY(ServerName), DB_NAME, DB_ID, ngày tạo và marker. Dùng canonical SERVER hoặc SERVER\INSTANCE; alias/port format cần cấu hình DataSource tương thích và ExpectedServer canonical, không nới guard để chạy nhầm DB.

Không đưa connection string/password vào source, shared appsettings hay command args. Prepare-Secrets sinh ngẫu nhiên password ứng dụng `it06.*` vào secret store riêng, không đọc main secrets. Passwords có thể thay bằng env Passwords__assigned, Passwords__former, Passwords__none, Passwords__restricted, Passwords__inactive, Passwords__mixed, Passwords__branchb, Passwords__revoked khi runtime; nếu dùng env-only phải giữ chúng khớp fixture đã tạo. Không reset account khi secret bị mất.

Từ clean checkout:

```powershell
# Cấu hình integration connection riêng qua secret editor/environment trước.
./scripts/verification/HttpSqlIntegration/Prepare-Secrets.ps1
# Chỉ dùng flag này sau khi được phép tạo test DB chưa tồn tại:
./scripts/verification/HttpSqlIntegration/Prepare-Database.ps1 -CreateIfMissing
dotnet restore scripts/verification/HttpSqlIntegration/HttpSqlIntegration.csproj
dotnet run --project scripts/verification/HttpSqlIntegration/HttpSqlIntegration.csproj --no-restore
./scripts/verification/HttpSqlIntegration/Verify-Preservation.ps1
```

Không có flag: setup chỉ kiểm tra DB đã có; DB thiếu thì dừng. Có DB nhưng không receipt/marker hoặc identity/hash sai: dừng để inspect provenance, không auto-adopt/drop/reset/restore/reseed. Có partial checkpoint hoặc fixture data không có manifest: dừng, không replay. Fresh create dùng nguyên Full Schema 01 + canonical Seed 03 với đổi tên DB trong bộ nhớ; không Upgrade. CREATE duy nhất từ master được server/absence guard; mọi schema/seed batch sau đó guard vào DB test. Không truy cập main database để preparation/fingerprint trong quy trình hiện tại.

Receipt và fixture manifest được tạo local. Existing DB của DEV1 có 8 account và credentials riêng không phải môi trường DEV2 có thể reuse mà không được duyệt; ưu tiên instance/test DB riêng được cho phép. Không copy fixture IDs/credentials của DEV1 rồi giả định ownership.

Runtime đầu tiên chỉ tạo fixtures nếu chưa có users/branches chưa được quản lý. Các lần sau xác minh manifest/username, không reset data. Fixture gồm 8 accounts, 2 branches, 4 tasks IN_PROGRESS và role/assignment cases theo catalog hiện có; không đổi grant matrix. Temporary credential fixture có deadline 7 ngày; hết hạn/mất password thì báo blocker để review lifecycle, không tự thay credentials.

Kestrel chỉ loopback HTTPS, certificate riêng pin ở client, DP ephemeral chỉ test host. Production cookie events/validator/login/touch/revocation và SQL services vẫn dùng thật. File lock ngăn runner cùng package; không khóa mọi external process. Cleanup dùng FE-01 logout/revoke exact sessions ghi trong journal, giữ audit/history; INACTIVE fixture phục hồi trạng thái. Process kill hoặc mất SQL có thể ngăn finally: kiểm tra sessions trong DB test với quyền phù hợp, không tự reset DB.

## C. Validation và phạm vi bằng chứng

```powershell
./scripts/verification/AuthorizationHardening/Run-Validation.ps1 -IncludeLiveReadOnly
git diff --check
```

Results/log mới nằm trong AuthorizationHardening/obj/validation (ignored). Script ghi BLOCKED riêng, không đổi thành Passed; không chạy provisioning hoặc HTTP SQL writes tự động. Chạy SQL integration riêng sau khi xác minh config/ownership. Run-Regressions cũ vẫn fail sớm nếu read-only config thiếu; dùng runner Task08 để xem đủ trạng thái.

Task08 đã chạy reuse existing DB an toàn và source-only preparation refusal test. Fresh provisioning trên database thứ hai/máy DEV2 chưa thực thi; không reset/tạo DB mới chỉ để minh họa clean checkout. Dotnet test solution chưa discover framework test project; console runners phải chạy riêng.
