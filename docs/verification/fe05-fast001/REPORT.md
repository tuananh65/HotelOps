# FE05-FAST-001 — PARTIALLY_VERIFIED

## Baseline và implementation

Worktree `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST001`, branch
`feature/dev1-fe05-execution-history`, tạo từ fetched
`origin/integration/fullstack-base` **29eee0fe1465266b26d5cb837989e1f5f62193dc**.
Không merge/cherry-pick. Checklist commit `5f1cde86b5b6b730832da6207ef6eb3d3b15cfc9`
được giữ nguyên ở nhánh checklist; task này không sao chép router/auth cũ từ đó.

| UC | Implementation mới và khả năng sử dụng | Evidence | Ước tính / verdict |
|---|---|---|---|
| UC042 | Mở Inspection theo branch/ID được giao; Start, assigned checklist/version, PASS/FAIL, notes, evidence, Save Draft, Finish, Pending Conclusion; mandatory/FAIL→defect checks, If-Match, explicit reload sau stale hoặc kết quả write chưa xác định | Focused API/model, browser và live Start/Save/Finish PASS cho hai Inspection; reload và SQL persistence PASS | 95% — VERIFIED trong phạm vi tested |
| UC043 | Chọn criterion FAIL đã lưu; description/location, active severity reference ID, upload evidence, xem defects/evidence; chỉ Inspecting/Pending Conclusion, không có sửa/xóa defect sau conclusion | Live defect/evidence PASS; concluded/Pending Conclusion browser boundaries PASS; backend giữ quyền/phase authority | 85% — PARTIALLY_VERIFIED: thiếu severity selector read contract |
| UC048 | Collection search/filter/page, detail, version/results/defects/housekeepers/reworks/cycle inspections/evidence; download qua authorized endpoint; loading/empty/error/401/403 | Live history/detail/download PASS, đúng bytes; branch isolation/anonymous/denied account PASS; focused search/filter/pagination PASS | 95% — VERIFIED trong phạm vi tested |

Task ước tính **90%**: implementation và live verification đã thực hiện, còn severity
selector và review/acceptance của Project Leader. Các tỷ lệ là ước tính, không phải ACCEPTED.

## Existing backend được reuse

- `HousekeepingWorkflowController`, `InspectionWorkflowDtos`, `InspectionExecutionService`:
  GET `/api/branches/{branch}/inspections/{id}/execution`, POST `start`, PUT `results`,
  POST `finish`, POST `defects`. Writes dùng ETag rowversion strong quoted **16 uppercase
  hex** từ response mới nhất. Assigned version được giữ nguyên; không tự chọn latest version.
- `OperationalHistoryController`, `OperationalHistoryDtos`, `OperationalHistoryService`:
  GET `/api/inspection-history`, `/{id}?branchId=...`,
  `/{id}/evidence/{attachmentId}?branchId=...`.
  Page envelope `{items,total,filters}`; page/pageSize nằm trong `filters`.
  Filters branchId/roomId/inspectorId/housekeeperId/from/to/result/search/page/pageSize;
  dates gửi UTC; pageSize 1–100. Result filter là conclusion PASS/FAIL/CONDITIONAL,
  không tự suy luận từ criterion outcomes.
- Existing AuthProvider/SignInScreen/session/CSRF conventions. Feature adapter dùng
  credentials include, no-store, redirect error; bootstrap `/api/auth/csrf`, header
  `X-CSRF-TOKEN`. Shared auth HTTP client không hỗ trợ query/If-Match/binary; adapter
  FE05 chỉ thực hiện transport cho feature, không tạo auth hoặc permission authority.
- INSPECTION_PERFORM + assigned inspector được backend recheck; history/download dùng
  INSPECTION_VIEW và branch scope. Không suy luận quyền từ tên role ở frontend.

## Routes và ownership

- `/inspections` — mở identity được giao.
- `/inspections/:branch/:id/execution` — UC042/043.
- `/inspections/history` và `/inspections/history/:branch/:id` — UC048.

`AuthRouteHost.tsx` chỉ thêm 3 imports và 4 route mounts (15 lines). Không đổi existing
DEV3 routes/auth/session/CSRF behavior. Không sửa Program.cs, backend implementation,
generated entities, FE04/DEV2/DEV3 implementations, SQL/schema, flags, D4 hoặc UC045.
Màn hình mới không import prototype store hoặc mock permission/session. Các prototype
cũ được giữ nguyên, không dùng làm runtime source cho các routes này.

## Verification thực tế

| Check | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx` | PASS, 0 warning / 0 error |
| `dotnet build scripts/verification/InspectionLive/InspectionLive.csproj` | PASS, 0 warning / 0 error |
| Frontend `npm run build` | PASS; warning React Router `use client` bị Vite bỏ qua |
| `node --test tests/inspection-api.test.mjs` | **39/39 PASS** |
| Browser interception | **11 unique cases PASS qua 3 lượt**: lượt đầu 7 PASS / 2 lỗi locator; sửa nhãn Result và locator alert, chạy lại đúng 2 ca PASS; thêm 2 phase-boundary tests PASS |
| Live E2E | **9/9 PASS**, real Sign-In + cookie + ASP.NET + owned SQL; không interception |
| Independent SQL persistence | 2 PENDING_CONCLUSION, 1 FAIL + 1 PASS criterion result, 1 defect, 2 attachments, 2 sessions, 7 Inspection audit rows |

Live lượt đầu dừng vì assertion login mặc định 5 giây trước mutation Inspection.
Giữ receipt lỗi; tăng timeout harness lên 120 giây, restart backend trên cùng owned
trial, không reset DB. Lượt sau 9/9 PASS. Case thứ hai cũng xác minh stale UI bằng
một write hợp lệ cạnh tranh: ETag cũ bị 412, controls bị khóa và chỉ explicit reload
mới cho phép tiếp tục. Không báo hệ thống deadlock-free hoặc mutation retry an toàn.

Runtime: SQL local `DESKTOP-JI8U50T\SQLEXPRESS`, version `16.0.1000.6`, database mới
`HotelOps_Canonical_20261108_100612_c882a0ce_Fresh`, owner
`228c43c9-5cad-4cf1-8010-02374be0b828`. Trước fixtures: 0 users.
HTTPS API `https://127.0.0.1:7447`, frontend `https://127.0.0.1:7448`.
Backend connection được override chỉ đến owned trial; không lấy shared connection
để chạy mutation. Guard identity/owner trước các mutations. Cookie auth Secure,
HttpOnly, SameSite=Lax; CSRF thiếu bị 400; stale bị 412; unauthorized 401; wrong branch
và denied account 403. TLS browser chấp nhận ephemeral local certificate; proxy pin
backend certificate. Không chứng minh certificate trust/deployment ở shared runtime.
Owned runtime processes đã dừng; database được giữ nguyên, không xóa các trial cũ.

Không chạy lại historical suites không bị ảnh hưởng. npm ci có cảnh báo deprecated
transitive packages và blocked postinstall cho core-js/esbuild; dependency versions
không đổi, build và tests đã chạy được. Không che giấu hai browser failures ban đầu
hoặc timeout live lượt đầu.

## Dependencies / acceptance còn lại

1. **Severity selector:** không có existing GET severity-reference API trong controller
   baseline. UI không hard-code MINOR/MAJOR IDs; có fallback nhập active reference ID
   được backend validate. Cần approved read-only lookup contract thích hợp quyền
   INSPECTION_PERFORM; không yêu cầu MASTER_DATA_MANAGE và không tạo API ở task này.
2. **Assigned identity navigation:** backend chưa có collection assigned-inspection
   selector trong scoped contract được dùng; người dùng mở bằng branch/inspection ID
   hoặc history link. Chưa triển khai assignment UC041 hoặc tạo fixture thành production flow.
3. **Shared navigation:** minimal routes đã mount và real Sign-In đã verified trên exact
   candidate. DEV3 có thể bổ sung menu/dashboard link đến `/inspections` và
   `/inspections/history` trong integration riêng; không cần auth framework thay thế.
4. **Coverage limits:** live fixtures synthetic nhỏ, không load/deadlock/concurrency
   stress. Stale interleaving được kiểm chứng, không khẳng định race-free mọi writer.
   Không mở conclusion writer; fixture đầu vào không chứng minh UC041/044/045/047.
5. Shared merge/deploy và UC acceptance cần Project Leader quyết định. UC039/040 giữ
   90% theo baseline, không nhận thêm acceptance credit từ task này.

Exact source files và SHA256 nằm trong `source-manifest.csv` (bao gồm added files,
không chỉ git diff). Sanitized receipts được allowlist từ TestResults; không copy
runtime.private.*, PFX/PEM private keys, password, cookies hoặc connection secrets.
