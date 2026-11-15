# FASTTRACK-142A-ACCELERATOR — Checkpoint 75

Nối canonical Reports với API snapshot/CSV đã có. Đây là vertical slice sử dụng thật; **không phải nghiệm thu toàn bộ UC135**. Không thay schema, EF entity, catalog quyền hoặc shared SQL.

CHECKPOINT=75
INITIAL_LOCAL_REMOTE=1eb1797af1a07d056ed7c6437c51ae12180946d3
NEW_COMPLETE_VERIFIED=[]
TOTAL_COMPLETE_VERIFIED=19/144
PARTIAL=125/144
IMPLEMENTED_WORKFLOWS=/reports → API actor admission → branch/department/UTC preview → frozen snapshot pagination → CSV → persisted export audit; empty report/header export; denied/revoked actor handling
BACKEND_TESTS=610_PASS;11_SKIP;SOLUTION_AND_PROBE_BUILD_0_WARNINGS_0_ERRORS
FRONTEND_TESTS=244_AUTH_PASS;18_REPORT_CLIENT_PASS;3_MOUNTED_REPORT_BROWSER_PASS
SQL_HTTPS_BROWSER=PASS;3_ACTOR_FLOWS;34_PARENT_CHECK_RECORDS;12_BROWSER_CHECK_RECORDS;4_BROWSER_CSV_EXPORTS_WITH_SQL_AUDIT
CLEANUP=PASS;ZERO_FIXTURE_ROWS;ZERO_HTTPS_HOSTS;NO_SHARED_DB_MUTATION

Nguồn nghiệp vụ: SRS UC135, GBR161–163, SYS-PAR12/20 và approved actor decision. Navigation chỉ xuất hiện sau `/api/reports/options` chấp nhận actor. API tiếp tục kiểm tra Operations Manager/Owner và REPORT_VIEW cùng eligible role path. DEPT_MANAGER có REPORT_VIEW bị từ chối, không thu hồi permission toàn hệ thống. Không ảnh hưởng UC136/137.

UI hiển thị snapshot, phạm vi/bộ lọc và chỉ tiêu thiếu; phân trang 25 dòng mỗi section, xuất toàn snapshot. Đổi bộ lọc loại preview cũ. Mỗi POST lấy CSRF mới; export chỉ nhận CSV, tên file từ identity đã kiểm tra. Không tự retry mutation; lỗi export giữ snapshot để người dùng retry; revoke xóa preview; session authority thay đổi hủy request và ẩn dữ liệu.

**Bằng chứng trực tiếp:** [runtime](checkpoint75-runtime.json) chạy production frontend qua trusted HTTPS vào đúng disposable database. DEPT_MANAGER bị chặn UI/navigation/API; Owner và Operations Manager đều preview 26 task, phân trang 25+1, tải CSV và header-only CSV. Mỗi browser export có đúng một audit; logout revokes browser session trong SQL. API export giữ byte snapshot cũ sau source progress thay đổi; thu hồi Owner chặn cached export, không thêm success audit. Report reads/exports giữ nguyên business rows. [Cleanup](checkpoint75-cleanup.json) và [SQL identity/guards](checkpoint75-cleanup-sql.txt).

[Report tests](checkpoint75-report-tests.txt) gồm 18 client tests và 3 mounted-browser tests với HTTP giả lập (không tính thành SQL evidence): revoke export, failure/retry cùng snapshot, session invalidation. [Auth tests](checkpoint75-auth-tests.txt) có 244 PASS. [Backend](checkpoint75-backend-tests.txt) 610 PASS/11 SKIP; solution test exit 0 không có test discovery riêng. Production build/typecheck PASS; Vite vẫn cảnh báo use-client và bundle >500 kB (~779 kB). Không rerun npm ci vì lockfile không đổi.

[Earlier trials](checkpoint75-earlier-trials.json): hai trial đầu lỗi accessible label của select, cleanup PASS; sửa bằng aria-label, không nới quyền. Trial3 PASS trước mở rộng phân trang; trial4 là bằng chứng final. Một lần test harness offline lỗi esbuild CSS output configuration, đã sửa và rerun PASS; không phải lỗi production build.

UC135 còn thiếu đầy đủ RPT01–07 indicators, filters chuyên biệt và XLSX/PDF. Các report source thiếu vẫn công khai `missingSources`, không ghi 0 giả. Vì vậy không tăng COMPLETE_VERIFIED. FE13 authority/lifecycle mapping, UC111 correction permission và UC105 requester provenance tiếp tục giữ đúng gaps C74; không suy đoán contract.

NEXT_BATCH=Tiếp tục FE15 audit UC136/137 trên nền tảng đã có để đóng acceptance độc lập; sau đó hoàn thiện RPT indicators bằng provenance đã xác minh và UC006/010 current-responsibility guards. Deadline sprint giữ 2026-11-16 17:50 UTC; goal còn active.

Rerun: build solution, ModelProbe và frontend trước; chạy `scripts/verification/Invoke-Dev3ReportsTrial.ps1`. Script từ chối nếu exact disposable identity/empty baseline không đúng hoặc port đang dùng; không provision shared DB. Synthetic HTTPS commits được cleanup bù trong transaction của harness; không tuyên bố rollback một HTTP commit từ transaction khác. Commit/push logical batch lên canonical sau remote reconciliation; HEAD cuối được xác minh trong receipt gửi người dùng.
