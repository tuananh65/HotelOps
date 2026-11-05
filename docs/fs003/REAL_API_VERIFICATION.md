# FS-003 — Real API verification

**Real HTTP API: NOT RUN. Real SQL-backed E2E: BLOCKED_EXTERNAL.** Không có backend listener/authenticated session được khởi động cho nghiệm thu này. GET /api/rooms không có kết quả real HTTP mới để báo 200/401/403. SQL metadata connection attempt không phải API test và chưa thành công.

## Acceptance matrix

| Case | Automated fixture coverage | Real HTTPS/API/SQL |
|---|---|---|
| 1 Authenticated success | DTO/GET/PUT responses bằng interception | Blocked: chưa có trial/account/session |
| 2 Anonymous rejected | Board401 và progress401 fixtures | Not Run; không khẳng định live401 |
| 3 Missing permission403 | Board/progress fixtures | Not Run |
| 4 Branch isolation | Wrong-branch DTO rejected, no prototype fallback | Backend SQL grants/lineage chưa live verified |
| 5 Pagination | Board request/query + returned page fixtures | Real count/page consistency Not Run |
| 6 Loading/error/empty | Browser fixture delayed GET/empty/error; post-save refresh failure | Not Run live |
| 7 Invalid DTO | Node malformed shape/tag/scope, browser wrong branch | Không inject lỗi vào server thật |
| 8 No mock fallback | Browser no tiles/form on failures | Source implemented; runtime live Not Run |
| 9 Stale cancellation | AbortSignal Node; canceled CSRF prevents PUT; delayed old board branch ignored | SQL wait/cancel/rollback Not Run |
| 10 UI matches backend | Browser fixed DTO fields, save+refresh | UI→real backend→SQL Not Run |

## Evidence boundaries

- Node tests stub fetch; browser route interception returns fixed DTO/status/ETag and fixture CSRF. Đây không phải cookie authentication verification, real403 authorization hoặc DB transaction proof.
- `src/HotelOps.Api/Controllers/RoomsController.cs` active + source ROOM_VIEW/session/scope gates có thể inspect, nhưng source/build không chứng minh live response.
- Task67/Task74/Handover receipts là historical; không cộng vào results mới. Backend xUnit93 không mở SQL trial trong FS-003.
- Không authentication bypass, fake principal, cookie injection, disabled authorization hoặc TLS-validation bypass trong runtime. Test interceptors chỉ tồn tại dưới frontend/tests.

## Runbook sau khi được xác nhận môi trường

1. Developer xác nhận disposable target/no production data, connection principal và quyền auth/session-touch/write acceptance. Đối chiếu StartupSchemaValidator cùng progress/Housekeeping optional schema, không tự install/migrate.
2. Cấu hình local HTTPS cert/key/CA và backend URL khớp proxy. Khởi động API thật bằng launch settings/URLs được chọn; không bỏ startup schema gate.
3. Dùng test account thật và CSRF/login contract đã có; kiểm session qua GET /api/auth/session. Không dùng prototype sign-in.
4. Board `/room-readiness?branchId=<allowed>`: đối chiếu phòng/type/floor/status với SELECT read-only cùng filter; paging real DB, empty filter. Kiểm anonymous, expired, no ROOM_VIEW và forbidden branch.
5. Cleaning `/cleaning/tasks/<actual-id>?branchId=<branch>`: GET snapshot/strong HEX ETag; update selected items/note/photo với CSRF+If-Match. Kiểm rowversion/log/audit persistence, 100 remains IN_PROGRESS; concurrent stale412/missing428/invalid400/ownership403/inactive409. Đây là SQL write acceptance, cần scope cho phép riêng.
6. Thu thập chỉ route/method/status/counts/redacted assertions/commit IDs. Không HAR/raw headers/cookies/CSRF/password/base64 evidence. Không publish dumps/users data.

Mỗi case cần đánh dấu riêng; chỉ ghi END_TO_END_PASS khi UI, real HTTPS API và SQL cùng có evidence mới. Nếu startup/connectivity thất bại, giữ fail/blocker rõ ràng, không chuyển sang interception rồi đổi nhãn thành live PASS.
