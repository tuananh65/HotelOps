# FE04-R05 — SQL verified original cleaner candidate

Verdict: **PARTIALLY_BLOCKED**. Tiến độ ước lượng: **90%**. Candidate đã kiểm tra SQL và sẵn sàng publish source; chưa có full UC034/FE05 integration acceptance. Báo cáo R04 được giữ nguyên như lịch sử, kết quả hiện tại theo R05 này.

## Integrity và publication scope

Base: `d37e99f42eaaf3089eb510c6298296a0f0743faa`.
Feature branch riêng: `feature/dev1-fe04-original-cleaner-binding`.
Giữ nguyên byte cả 7 file R04; SHA256 trước/sau trong `R04-integrity.json`. Không redesign, đổi entity/mapping/DI/permission catalog, sửa UC035/036 authority hoặc backfill. Chỉ thêm verification source/evidence R05.

Exact inventory dự kiến commit (14 files):

```text
docs/fe04-r04/RESULT.md
scripts/database/release/fe04-cleaner-r04/01_CleanerAttributionCandidate.sql
src/HotelOps.Api/Services/Housekeeping/CleaningClosureBoundary.cs
src/HotelOps.Api/Services/Housekeeping/CleanerAttributionContract.cs
src/HotelOps.Api/Services/Housekeeping/CleanerAttributionSource.cs
tests/HotelOps.Dev1.RegressionTests/CleanerAttributionSqlTests.cs
tests/HotelOps.Dev1.RegressionTests/CleanerAttributionTests.cs
docs/fe04-r05/RESULT.md
docs/fe04-r05/R04-integrity.json
docs/fe04-r05/sql-results.json
scripts/verification/FE04-R05/Program.cs
scripts/verification/FE04-R05/Provision.ps1
scripts/verification/FE04-R05/CleaningFixture.sql
scripts/verification/FE04-R05/SOURCE_MANIFEST_BASE.json
```

Commit SHA và remote identity được báo sau commit/push; không tự ghi một SHA chưa tồn tại. Không merge/deploy/force-push. Git EOL normalization nếu có không được coi là thay đổi logic; integrity manifest ghi byte worktree gốc.

## SQL trial và kết quả thực tế

Fresh database: `HotelOps_Canonical_FE04_R05_20261110_Fresh`, local `DESKTOP-JI8U50T/SQLEXPRESS`; không tái sử dụng hay sửa R02/ENV001/shared HotelOpsDB. Provision từ 18 source files có normalized SHA256 đã xác minh, FS013–FS016 substrate, hai R03 authority/projection candidates nguyên bản. Marker:

```text
HotelOps.FE04.UnblockR01.Owner = DEV1_DISPOSABLE
HotelOps.FE04.R05.Source = d37e99f42eaaf3089eb510c6298296a0f0743faa
```

Owner fresh R05 là `sa` (SID 0x01); không cấp sysadmin/db_owner cho runtime. Instance/database/marker/source/owner được guard trước mỗi harness command. Provision từ chối overwrite target đã tồn tại. R04 installer chạy nguyên bản, procedure definition thực tế so khớp nguyên văn candidate. Không có shared DDL hay ứng dụng được deploy.

- **SQL harness: 23 PASS / 0 FAIL / 2 NOT_RUN**, chi tiết `sql-results.json`.
- Installation, 6 trusted FK metadata và FK rejection, EXECUTE AS OWNER, immutable UPDATE/DELETE: PASS.
- Principal completion WITHOUT LOGIN: SELECT theo từng bảng nguồn, UPDATE cleaning_tasks, INSERT cleaning_progress_logs, EXECUTE capture; không db_owner/schema ownership. Direct attribution INSERT/UPDATE/DELETE bị SQL229: PASS.
- Principal reader riêng WITHOUT LOGIN: SELECT theo bảng nguồn và VIEW DEFINITION của capture procedure; không EXECUTE hoặc DML. Reader signature hoạt động, capture/mutation bị từ chối: PASS.
- Single cleaner qua **capture participant thật**, reassignment giữ original assignment/user/completion revision, multiple assignment/progress contributors = null: PASS.
- Reader từ chối wrong branch/Inspection/current cycle, source bị đổi, revoked/inactive cleaner: PASS.
- Capture eligibility từ chối revoked cleaner; owner procedure từ chối missing completion source và thiếu caller transaction: PASS.
- Constraint fault khi ghi audit hoặc notification sau capture rollback task completion/log/attribution cùng caller transaction: PASS ở **participant level**, không phải full service staging acceptance.
- Duplicate capture không ghi đè; concurrent capture bị serialize, caller thứ hai nhận rejection sau commit: PASS ở **capture level**.
- **6 SQL scenarios R04 nguyên bản: 6 PASS / 0 FAIL**, sau khi seed một synthetic room không có open task. Không đổi test file, không disable trigger. Local receipt: `tests/HotelOps.Dev1.RegressionTests/TestResults/fe04-r05-r04-sql-seeded.trx`.

Các lần lỗi đã giải quyết: owner Windows ban đầu không resolve gây SQL15404, sửa owner duy nhất R05 sang sa; controlled Inspection fixture dùng OUTPUT trực tiếp gặp SQL334 do INSERT trigger, sửa riêng harness dùng SCOPE_IDENTITY; R04 SQL suite lần đầu thiếu free-room seed nên 6 test fail trước bước capture, sau bổ sung fixture mới cả 6 pass. Receipt lần đầu giữ trong local `obj/fe04-r05`. Không sửa 7 file R04 để làm test pass.

## Build/tests và giới hạn

- `dotnet build HotelOps.slnx --artifacts-path .../obj/fe04-r05-regression --no-restore`: **Passed, 0 warning / 0 error**. Lần đầu no-restore với artifact directory mới gặp NETSDK1004; đã restore rồi build lại thành công.
- `dotnet test HotelOps.slnx --artifacts-path ...`: exit 0, solution API-only không discover regression project; đã chạy project trực tiếp.
- Regression project: **308 PASS / 0 FAIL / 11 SKIP** khi SQL/live opt-in tắt, TRX `fe04-r05.trx`. Sáu SQL scenarios chạy riêng như trên.
- `git diff --check`, 7-file byte integrity và normalized hashes của 18 substrate files: PASS trước publication.
- Git có advisory LF→CRLF khi staging; không đổi cấu hình EOL. Trailing whitespace trong fixture R05 mới đã được bỏ và final staged diff check Passed. Không có C# build warnings.
- Full UC034 CompleteCleaningAsync với audit/mandatory notification staging và concurrent/replay commands: **NOT_RUN**. Existing ClosureAvailableAsync/ResumeTrialInstalled chỉ nhận exact database FS015/FS016; fresh R05 không qua gate. Không đổi tên trial, giả database identity, tắt/nới gate hoặc sửa trial khác. Private capture participant được gọi bằng reflection chỉ trong controlled harness; không tính là public command acceptance.
- FE05-owned authorization pipeline/actual domain writer/HTTPS: **NOT_RUN**. Public reader signature được gọi thật bằng controlled backend caller với principal read-only, không thay thế việc FE05 kiểm tra quyền người dùng và tích hợp domain transaction.
- Fixture chỉ là synthetic component evidence trong owned R05; không biến FAILED/log/assignment giả thành authoritative business receipt. Không backfill dữ liệu cũ.

## Cleaner contract và FE05 integration

```csharp
// RoomTypeCreationService — trusted backend only, not HTTP model binding
Task<CleanerAttribution?> ReadInspectionCleanerAttributionAsync(
    DbConnection connection, DbTransaction transaction,
    long inspection, long branch, CancellationToken ct);

// Returned immutable source identity
CleanerAttribution(
    long CleaningTaskId, long RoomId, long BranchId,
    long AssignmentId, long CleanerUserId, long CompletionProgressId,
    DateTime CompletedAtUtc, byte[] CompletionRevision,
    long WriterTransactionId);
```

1. FE05 pin exact published feature commit. Candidate SQL cần deployment approval riêng cho môi trường tiếp theo; publication không cấp quyền shared deployment. Runtime phải đọc được source tables/metadata; chỉ completion writer được EXECUTE capture, FE05 reader không cần quyền đó.
2. FE05 xác thực actor và quyền thao tác Inspection đúng branch trước lời gọi. Reader không nhận actor và không tự thay thế authorization của FE05.
3. Dùng cùng scoped HotelOpsDbContext, cùng physical connection và caller-owned **Serializable** transaction, không ambient transaction. Truyền `db.Database.GetDbConnection()` và `db.Database.CurrentTransaction.GetDbTransaction()`, inspection ID hiện tại và branch đã authorize. Không mở context/transaction khác chỉ để resolve attribution.
4. Null = missing/ambiguous/ineligible/stale source: fail-closed. Không fallback latest assignment, assigned_by, last logged_by hay rework assignee. History nhiều cleaner trước completion vẫn null; reassignment sau completion không thay original binding.
5. Bind toàn bộ task/room/branch/assignment/user/progress/time/**CompletionRevision** vào contract riêng của FE05 khi được duyệt. CompletionRevision là bản sao persisted task rowversion tại UC034, không phải current task rowversion hoặc inspection rowversion; WriterTransactionId là completion writer transaction, không phải FE05 current transaction.
6. FE05 sở hữu commit/rollback và phải xử lý null trước domain mutation phụ thuộc cleaner. R05 không tự thêm trường vào Rework source proof/digest/receipt, không cấp UC035/036 authority và không có independent commit. Cần acceptance riêng cho actual caller/command wiring.

## Tái lập verification

Provision script là exact-target local trial tool, không phải deployment installer. Target hiện tồn tại nên chạy lại Provision phải dừng NO_OVERWRITE. Không xóa target tự động. Script và 18-file manifest dùng paths relative từ repo root, hash-check trước CREATE DATABASE; mọi substrate batch sau đó có guards. SQL harness cài candidate nếu chưa có, nếu có thì kiểm tra module definition; chỉ tạo fixture mới trong đúng owned R05.

Tạo **temporary ignored harness** dưới `obj/fe04-r05-harness` (không thêm project vào solution), dùng nội dung project sau:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../scripts/verification/FE04-R05/Program.cs" />
    <ProjectReference Include="../../src/HotelOps.Api/HotelOps.Api.csproj" />
  </ItemGroup>
</Project>
```

Trước khi chạy từ repo root, đặt `FE04_R05_SOURCE_ROOT` = absolute repo root và `FE04_R05_EVIDENCE_ROOT` = thư mục artifact local đã tạo. Harness chỉ kết nối exact R05 local target với Integrated Security, Encrypt=True, TrustServerCertificate=False. Không có connection secret/token/password được lưu trong source. Bất kỳ FAIL nào làm process exit 1. Không bật SQL tests cho database khác.

## Remaining blockers / UC impact

Cần approved fresh-target runtime gate hoặc trial strategy riêng để chạy full UC034 command, mandatory notification/audit failure injection và command concurrency/replay; không tự sửa gate trong R05. Cần actual FE05-owned caller integration/authorization/transaction receipts. Chưa có quy tắc primary cho cycle nhiều cleaner nên giữ null. Candidate publication giải quyết source availability và SQL component evidence; **không tuyên bố N01 accepted, D08-R2 đóng hoàn toàn hoặc UC035/036 accepted**.
