# Kết quả kiểm chứng thực chạy

- dotnet build HotelOps.slnx --nologo: Passed, exit0, 0 warnings/0 errors.
- dotnet test HotelOps.slnx --nologo: exit0, không discover test (solution chỉ chứa API); không tính là test pass.
- dotnet run --project tests/HotelOps.Incident.UnitTests/HotelOps.Incident.UnitTests.csproj --no-launch-profile: Passed, exit0, 49 assertions, zero SQL connection attempts.
- UC049_NODE_MODULES trỏ dependency đã có ở repository gốc (read-only), node --test frontend/tests/incidents/uc049.test.cjs: Passed, exit0,34/34; tsc strict noEmit và focused browser bundle Passed. Chạy Chrome headless qua Playwright.

Backend: real actor/cookie shape và actual PermissionService với permission data fixture; noSQL interceptor; placement/asset business policy cases; anonymous options qua MVC; thiếu/giả CSRF qua antiforgery thực. Frontend: actual candidate route dưới AuthProvider/BrowserRouter đã pin, fetch/session fixture, parser/href, direct/reload/history, invalid/no grant/expired context, retained room, create DTO/CSRF, POST denial/unknown outcome không retry. Case unknown/deleted/inactive/stale/cross-branch UI dùng cùng status400 fixture, không phải xác minh SQL từng loại.

Lần chạy đầu backend thất bại vì antiforgery TLS precondition của fixture HTTP; sửa harness đánh dấu request HTTPS. Lần đầu frontend hook thất bại do native bundler discovery trong sandbox; dùng resolver Node read-only. Kết quả cuối thay thế các lần đó; không che lỗi cũ.

SQL integration, live sessions/grants, transaction rollback/concurrency, actual options query results và full integrated frontend npm build: NOT_RUN. Runtime regression list/detail/status/triage: NOT_RUN; source methods khớp baseline. Xem TEST_RECEIPTS.json và receipts/*.txt.
