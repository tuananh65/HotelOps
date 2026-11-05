# TASK87 — Implementation report

Trạng thái: DONE cho automated regression foundation; không phải SQL/full-UC acceptance.

Tạo project .NET 10 xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1 và adapter 3.1.1 tại `tests/HotelOps.Dev1.RegressionTests/`. Các project tests hiện có là DEV2 console runners, không phù hợp mở rộng. Không sửa solution, production dependency graph hoặc runtime. Vì vậy `dotnet test HotelOps.slnx` exit 0 nhưng không discover suite mới; phải chạy project hoặc runner Task87.

Kết quả cuối: 91 discovered, 91 Passed, 0 Failed, 0 Skipped. TRX `results-final/task87.trx`, counters JSON và coverage matrix kèm theo. Phân loại: unit/controller in-process + service fake dependencies; không framework HTTP host, không SQL/HTTPS integration. Không dùng EF InMemory. Các ca SQL chưa được cấp trial mới nằm trong NOT_RUN_SQL_GATE, không đưa vào số Passed/Skipped.

Build `dotnet build HotelOps.slnx`: PASS, 0 errors, 2 NU1900 warnings do vulnerability feed NuGet không truy cập được. Không tắt audit/security checks. Restore sandbox đầu lỗi NU1301 api.nuget.org:443; restore được duyệt quyền mạng thành công. Lỗi test fixture ban đầu CS7036 và 8 lỗi InlineData nullable long đã sửa; giữ log/TRX thất bại, không ghi đè thành PASS. Chưa phát hiện production bug bằng các tests đã chạy; không kết luận runtime không có bug ngoài coverage.

Coverage: Auth20, UC01222, UC03722, UC03815, UC14210, shared controller cases2. Xem matrix để tránh hiểu validation/filter acceptance là SQL result acceptance. API 400/401 được kiểm tra bằng gọi controller trực tiếp; hosted routing/model binding/CSRF cho các endpoint này chưa được framework test. Existing security HTTP runners dùng loopback với doubles, không real SQL sessions.

Preflight: đúng `D:/SEP490/Projects/HotelOps`, branch `feature/BE1-database-integration`, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`; inventory 5 worktrees. Đã đọc AGENTS, Change Notes, Task84/85-B/86 report/matrix/receipts, source DTO/service/controller/mappings liên quan và kiểm tra registrations UC012/037/038/142/Workday trong Program.cs. SRS draft không tìm thấy exact UC labels bằng rg; không tự suy thêm pending permissions. Không tiếp cận protected SQL hoặc trial cũ.

Postflight: 1129 existing source/artifact files có hash giữ nguyên. 5 worktrees giữ HEAD/index/status, sau khi loại đúng hai thư mục Task87 mới. Read-only inspection của hai worktrees khác cần `git -c safe.directory=<exact path>` theo lệnh; không đổi global Git config. Không commit/push/merge/reset/stash; diff --check PASS. Thay đổi cũ của người dùng được giữ nguyên.

Files tạo mới:
- tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj
- tests/HotelOps.Dev1.RegressionTests/BoundaryTests.cs
- tests/HotelOps.Dev1.RegressionTests/AuthorizationTests.cs
- tests/HotelOps.Dev1.RegressionTests/UpdateValidationTests.cs
- tests/HotelOps.Dev1.RegressionTests/README.md
- scripts/verification/Task87/: runner, năm deliverables yêu cầu, preflight/postflight/worktree snapshots, log build/restore/test/regression, receipts và TRX/summary.
Không sửa/xóa file đã tồn tại trước task. bin/obj là build output, không phải source deliverable.

Tiến độ giữ 26/75 UC, 60/256 điểm; không nhận thêm UC. Đề xuất Task88: framework SQL integration fixtures trên disposable trial mới được duyệt để chạy duplicate/concurrency/audit rollback và read scope/order/page/DTO mapping; sau đó cân nhắc hosted API tests. Không tự bắt đầu Task88; không mở Deactivate/Cancel lifecycle hoặc phụ thuộc DEV2.
