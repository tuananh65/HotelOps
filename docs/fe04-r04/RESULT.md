# FE04-R04 — Original cleaner binding candidate

Verdict: **PARTIALLY_BLOCKED**. Tiến độ ước lượng: **75%**. Source candidate và contract tests hoàn tất; SQL/deployment chưa xác minh. Không xác nhận N01 accepted hoặc FE04 UC035/036 accepted.

## Baseline

Worktree: `D:/SEP490/Projects/HotelOps-DEV1/obj/fe04-unblock-r01`.
Branch hiện có: `feature/dev1-fe04-real-rework-authority`.
Base R03: `d37e99f42eaaf3089eb510c6298296a0f0743faa`.
R04 chưa commit; không tạo/chuyển nhánh, push, merge hoặc chạy DDL.

Đã đối chiếu SRS draft UC034–036, Change Notes, generated entities và DbContext. Không sửa permission matrix, EF entities/mappings, DI, HTTP routes, consumer authority hoặc Rework proof.

## Existing source tái sử dụng

`cleaning_assignments`: exact assignment/task/user, assigned_at, ended_at, assignment_status.
`cleaning_progress_logs`: exact progress/task, COMPLETED, 100%, actor và thời điểm.
UC034 hiện kiểm tra sole active assignment, current cycle, execute authorization và đóng interval/ghi completion/audit/notification trong caller transaction.

History riêng lẻ chưa có immutable primary designation hoặc completion revision binding. `assigned_by` là người giao việc; `logged_by` là người ghi log. Không dùng hai trường này hay latest assignment để bầu primary. Closure notification receipt có assignment/progress/revision nhưng vẫn provisional, chưa là primary-cleaner authority.

## Contract

- Candidate `dbo.fe04_cleaner_attributions`: một immutable row cho original cleaning task (cũng là cycle identity hiện tại), room, branch, exact assignment, cleaner user, completion progress, completion UTC, bản sao task rowversion tại completion, writer transaction ID, contract version 1.
- Capture chỉ ở completion mới UC034, sau task/log mutation và trước audit/mandatory notification/outer commit. Không gọi từ replay, blocker hoặc rework completion.
- Chỉ ghi khi mọi assignment history cùng một assignee và mọi progress contributor cùng người đó. Procedure kiểm tra lại history, sole ACTIVE assignment, task/current cycle/room, exact COMPLETED progress và thời điểm. Nhiều người: không ghi binding, reader trả null.
- Chính sách bảo thủ: reassignment sang người khác trước completion cũng không được tự xác định primary, kể cả người trước có thể chưa cleaning. Chưa có quy tắc được duyệt để chọn primary trường hợp này.
- Dùng caller SERIALIZABLE transaction, không commit riêng. Lỗi ghi rollback cùng completion. Table từ chối UPDATE/DELETE và ordinary direct INSERT; procedure dùng ownership context. Runtime EXECUTE grant phải được duyệt riêng.
- Reassignment sau completion/rework không ghi đè row. Reader chỉ kiểm tra exact assignment được lưu và source links ban đầu, không chọn latest assignment.
- `ReadInspectionCleanerAttributionAsync(connection, transaction, inspectionId, branchId, ct)` dành cho trusted backend caller đã kiểm tra quyền Inspection. Đòi đúng context connection/transaction, SERIALIZABLE, exact inspection/task/room/branch/current cycle và completion source còn nguyên.
- Reader tái sử dụng eligibility hiện có: account/employment/department/branch grant/role/shift. Revoked/inactive/hết eligibility: null, giữ lịch sử. Caller nhận persisted completion revision, không thay bằng task rowversion sau Inspection/Rework.
- Schema chưa cài: completion cũ theo baseline nhưng attribution không khả dụng. Cycle cũ không có row: null. Không suy diễn/backfill.
- R04 không thay source digest/proof, không cấp UC035/036 authority và chưa nối vào FE05 consumer.

## Exact files

1. `src/HotelOps.Api/Services/Housekeeping/CleaningClosureBoundary.cs` — completion capture hook.
2. `src/HotelOps.Api/Services/Housekeeping/CleanerAttributionContract.cs` — backend DTO/rules.
3. `src/HotelOps.Api/Services/Housekeeping/CleanerAttributionSource.cs` — capture gate và inspection-bound reader.
4. `scripts/database/release/fe04-cleaner-r04/01_CleanerAttributionCandidate.sql` — additive table, immutable trigger, caller transaction procedure, disposable-only interlock; không backfill/runtime grant.
5. `tests/HotelOps.Dev1.RegressionTests/CleanerAttributionTests.cs` — contract và transaction requirement tests.
6. `tests/HotelOps.Dev1.RegressionTests/CleanerAttributionSqlTests.cs` — opt-in rollback-only SQL component scenarios; không DDL.
7. `docs/fe04-r04/RESULT.md` — báo cáo này.

## Validation

- Build: **Passed**, 0 warnings / 0 errors với `dotnet build HotelOps.slnx --artifacts-path D:/SEP490/Projects/HotelOps-DEV1/obj/fe04-r04-artifacts --no-restore`.
- Build ban đầu trong sandbox Failed do Access denied khi ghi obj. Lần artifact build trong sandbox cũng bị Access denied và NU1900 do không truy cập được NuGet vulnerability feed. Build ngoài sandbox đã Passed; không đổi project/configuration.
- `dotnet test HotelOps.slnx --artifacts-path ... --no-restore`: exit 0 nhưng solution chỉ chứa API, không discover regression tests; đã chạy test project trực tiếp.
- Regression project cuối: **308 Passed, 0 Failed, 11 Skipped**. TRX: `tests/HotelOps.Dev1.RegressionTests/TestResults/fe04-r04.trx`.
- Focused CleanerAttribution: **16 Passed, 0 Failed, 1 Skipped** (SQL theory group). Contract tests kiểm tra single cleaner/repeated history, ambiguous reassignment/multiple contributors, revoked/inactive eligibility flag, wrong branch/room/cycle/task, missing source/identity, revision và transaction requirement. Boolean contract checks không phải SQL authorization proof.
- SQL component theory đã chuẩn bị single, reassignment sau completion, multiple history, wrong cycle, rollback và persisted revision. **Not Run**: chưa triển khai candidate hoặc bật SQL trial. Chưa xác minh SQL syntax/constraints/permissions/query behavior hay rollback thực tế của R04.
- SQL tests cần disposable target được duyệt riêng có R03 owner marker và R04 candidate schema, room không có open task, cleaning type và hai user seed. Opt-in: `FE04_R04_SQL_TRIAL=APPROVED_DISPOSABLE_ONLY`; connection local qua `FE04_R04_SQL_CONNECTION`. Fixture writes nằm trong rollback transaction, chỉ là synthetic component evidence, không cấp authority dữ liệu thực.
- Không thêm/sửa HTTP endpoint. HTTPS verification: **Not Run**.
- `git diff --check`: Passed. Không sửa module/file ngoài danh sách.

## Blockers còn lại

1. Review/approve candidate schema, writer EXECUTE grant và isolated deployment riêng; candidate chưa áp dụng. Không tự chạy shared DDL.
2. Chạy SQL tests và service completion rollback với audit/notification fault trên candidate thực; xác minh trigger/permission behavior, source revision và concurrency/reassignment.
3. Xác minh SQL reader với revoked/inactive, wrong inspection/branch/cycle, superseded task và trusted FE05 caller integration. Pure tests không thay kiểm tra này.
4. Chưa có quy tắc được duyệt để chọn primary trong cycle nhiều cleaner; tiếp tục null/fail-closed, không backfill.
5. DEV2/FE05 cần review contract và trả receipt cho exact source revision. D08-R2 chưa thể đóng chỉ bằng source candidate chưa deploy/test SQL.

Source chưa đạt điều kiện xác nhận authoritative runtime binding hoàn chỉnh; chưa tạo commit.
