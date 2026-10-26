# Known limits / remaining deployment work

- Trial-only rollout:HotelOpsDB không patch;new EF model không tương thích unpatched schema khi load User. Không deploy/run against main DB trước shared rollout approval.
- Catalog vẫn chỉ USER_MANAGE/ALL;BRANCH/OWN support đã có nhưng không provision role grants. Scope tests với grant doubles được phân biệt với real SQL/HTTP tests.
- Profile snapshot là user-row version,không version hóa role/membership aggregate. Concurrent grant/membership revocation không tự cancel in-flight transaction;next request re-evaluate. Không mở rộng shared auth writer protocol trong task.
- Users auth updates có thể làm profile ETag stale;client phải read lại. Missing token428,stale412 theo If-Match semantics,không nhầm409 email conflict.
- NU1900 trên verification project vẫn còn do vulnerability service unavailable.
- API harness pins local temporary cert,không chứng nhận browser CORS/production TLS/least-privilege SQL. Fixture passwords/cookies/CSRF chỉ memory;local receipts chỉ labels/hashes/counts.
- UC011,credential reset/status/role/scope assignments và DEV2 modules giữ nguyên. Không commit/push/merge hoặc xóa untracked files.
