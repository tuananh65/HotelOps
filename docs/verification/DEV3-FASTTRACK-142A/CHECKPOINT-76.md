# FASTTRACK-142A-ACCELERATOR — Checkpoint 76

Hoàn thiện luồng đọc audit → lọc/phân trang → toàn bộ lịch sử record → so sánh từng trường trên canonical frontend. **UC136/137 vẫn PARTIAL**, không đồng nhất luồng đọc PASS với toàn bộ yêu cầu audit/retention đã hoàn thành.

CHECKPOINT=76
INITIAL_LOCAL_REMOTE=3cca13fdc949dad67a43feada29aad7946ba1e8c
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=19/144
PARTIAL=125/144
IMPLEMENTED_WORKFLOWS=Scoped/global Administrator audit list; metadata filters; record chronology; safe changed-field summaries; field-level before/after; creation-only and empty-list recovery
BACKEND_TESTS=620_PASS;11_SKIP;SOLUTION_AND_PROBE_BUILD_0_WARNINGS_0_ERRORS
FRONTEND_TESTS=131_AUDIT_SHARED_HOST_PASS;265_AUTH_REPORT_PASS
SQL_HTTPS_BROWSER=PASS;3_ACTOR_FLOWS;32_PARENT_CHECK_RECORDS;9_BROWSER_CHECK_RECORDS;AUDIT_SOURCE_FINGERPRINT_UNCHANGED
CLEANUP=PASS;ZERO_FIXTURE_ROWS;ZERO_HTTPS_HOSTS;NO_SCHEMA_OR_SHARED_DB_MUTATION

SRS UC136/137 và UC v3 yêu cầu Administrator; không dùng policy Operations Manager/Owner của UC135. `AuditAccess` yêu cầu SYS_ADMIN và AUDIT_VIEW trên cùng assignment hợp lệ, kiểm tra branch/current access. List, detail và options cùng policy; không thay permission catalog. Serializable transaction và user eligibility lock bảo vệ quyết định đọc. Global Administrator đọc được global/foreign history; branch Administrator không được đọc foreign/global entry. Owner không được phép chỉ vì có quyền báo cáo.

`changed_fields` được đọc từ cột thật, lọc bằng allowlist giống before/after; không trả tên password/token hoặc payload không được duyệt. Explicit business JSON null được giữ nguyên, phân biệt với giá trị không ghi/đã che. UI có summary, applied filters, bảng từng trường và thao tác mở lịch sử chính xác entity type/id, giữ branch scope nhưng bỏ period/action/actor/search phụ. Không có thao tác restore, sửa hoặc xóa audit.

| Acceptance được thực thi | Bằng chứng trực tiếp |
| --- | --- |
| Actor, branch/global scope, revoke | API checks và browser branch-admin/global-admin/denied-owner; options/detail/list cùng policy |
| Thứ tự mới nhất, phân trang | 27 scoped / 29 global fixture rows, 25-row page và trang kế không trùng |
| Filters và record chronology | Period + actor + entity + action + branch + search; record 42 gồm 26 scoped entries; history bỏ filters phụ, giữ branch |
| Before/after và redaction | Status transition, explicit closedAt null, reviewed changed names; secret fixture không ra response/UI |
| Creation-only, empty và recovery | Một CREATE entry; thiếu before được ghi rõ; MSG10 giữ applied filters, đổi filter phục hồi |
| Read-only | PATCH/DELETE nhận 404 từ canonical unmatched API fallback; SQL fingerprint toàn bộ fixture audit không đổi |
| Cleanup | Fixture/session/role/branch/audit rỗng; trusted HTTPS hosts đã dừng |

[Runtime](checkpoint76-runtime.json), [cleanup](checkpoint76-cleanup.json), [SQL identity/guards](checkpoint76-cleanup-sql.txt), [backend tests](checkpoint76-backend-tests.txt), [audit/shared-host tests](checkpoint76-frontend-tests.txt), [auth/report regression](checkpoint76-auth-report-tests.txt).

Hai trial đầu được giữ trong [earlier trials](checkpoint76-earlier-trials.json): harness dự đoán unsupported method 405 nhưng canonical API trả 404; sau đó waiter bắt nhầm initial global response. Đã sửa test theo source thực tế, không nới server authorization. Final trial3 PASS. Shared-host regression phát hiện assertion dashboard đếm mọi `/options`, gồm report admission mới từ C75; sửa chỉ đếm đúng dashboard endpoint, rerun 131 PASS.

**Remaining acceptance gaps:** JOB06/GBR164 retention 365 ngày chưa có scheduled cleanup được kiểm chứng. Source capability scripts của FE11 submission/verification/reopen còn có FK immutable receipts → audit_logs; không được tự xóa/cascade audit làm hỏng lịch sử. Disposable baseline hiện không cài các capability này, nên trial reader này không chứng minh khả năng retention trên fullstack schema hợp nhất. Các audit payload ngoài reviewed scalar allowlist vẫn được withheld; chưa chứng minh toàn bộ writer contracts và việc không ghi secrets tại nguồn. Vì vậy không nâng hai UC lên COMPLETE_VERIFIED từ các test đọc thành công.

Build/typecheck PASS; Vite vẫn có use-client/chunk-size warnings. Solution test exit0 không được tính là test discovery; 11 SQL tests SKIP được ghi riêng với real disposable trial. Không chạy lại npm ci vì lockfile không đổi.

NEXT_BATCH=UC006/010 current-responsibility và canonical deactivation/revocation; tiếp tục khép retention/source-audit gaps khi có contract phù hợp với immutable workflow receipts. Không dừng sprint vì các gaps riêng; deadline giữ 2026-11-16 17:50 UTC.

Rerun: build solution, ModelProbe và frontend rồi chạy `scripts/verification/Invoke-Dev3AuditTrial.ps1`. Script kiểm tra exact disposable instance/database/GUID/run và empty baseline; cleanup thuộc fixture riêng, không shared/production mutation. Canonical branch giữ nguyên, normal commit/push sau remote reconciliation.
