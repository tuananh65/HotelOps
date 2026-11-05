# FS-002 — DEV1 Room Operations

Ngày: 05/11/2026 (Asia/Saigon). Trạng thái **PARTIAL — read integration implemented, live acceptance blocked**. Không báo FULLSTACK COMPLETE và không cộng điểm UC.

## 1. Baseline và phạm vi

- Branch: `feature/dev1-room-ops`.
- HEAD/baseline: `e7f84422a33a8a65c771efdfb248e8231e83bb6a`.
- Working tree ban đầu sạch. Không đổi branch, merge, sửa handover/main/develop; không sửa C#, DB schema, DEV2 hoặc module DEV3.
- DEV1: FE03/04/05/11/13 theo phân công FS-002; ownership cũ trong AGENTS không thay thế phân công này.
- Chưa commit/push. Các thay đổi để developer review tại working tree hiện tại.

## 2. Audit và nguồn

Đối chiếu tài liệu FS-001, handover A/B, full UC ledger, SRS/Change Notes và source controller/service/DTO/Room/DbContext. [FS001_INPUT_INVENTORY.json](FS001_INPUT_INVENTORY.json) ghi 50 file dưới `docs/fullstack-integration/` với SHA256; đây là inventory đầu vào, không phải chứng nhận nội dung proposal đã được áp dụng. JSON được parse khi tạo inventory; log/test receipts chỉ là kết quả lịch sử.

Nguồn chính:

- `docs/fullstack-integration/`: auth/session, source decisions, mapping 80 screens, pending approvals, blockers, environment, validation và publication.
- `scripts/verification/HANDOVER_CODEX_A_20261105/README.md`, `FINAL_HANDOVER_REPORT.md`, `ARTIFACT_INDEX.md`, `FULL_UC_LEDGER.*`.
- `scripts/verification/HANDOVER_CODEX_B_20261105/HANDOVER.md`: B-TASK-02 artifact thiếu; UC107 không có implementation approval. Không suy diễn source của B.
- `docs/requirements/HotelOps_SRS_DRAFT.md`, `SRS_CHANGE_NOTES.md`: UC027, housekeeping/inspection, task/IR; Reception/Department Head permissions chưa final.
- Notification và Room/Workday contracts đọc từ immutable handover `28f0a5e4f582bc56ba101b48915d33baadeb7b95`, đường dẫn `scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104_02/DEV1_NOTIFICATION_INTEGRATION_CONTRACT.md` và `DEV1_ROOM_WORKDAY_SHARED_CONTRACT.md`. Các payload không nằm trong checkout này; không copy archive/local-only payload vào nhánh feature.
- `scripts/verification/Housekeeping/TASK73_CONTRACT_PROPOSAL.md`, `TASK74_CONTRACT_PROPOSAL.md`, `TASK73_DEV2_NOTIFICATION_HANDOFF.md`; PermissionService contract và DEV1/DEV2 Workday/PM audit. Historical ownership/runtime statements được đối chiếu lại với FS-001/current source.

Các mâu thuẫn giữ nguyên để review:

1. UI prototype dùng readiness states; Room DTO trả legacy `OperationalStatusCode`. `READY` không chứng minh current inspection đã pass/no effective block; `AVAILABLE` không đồng nghĩa Vacant+Ready.
2. EF Room default AVAILABLE và SQL contract lịch sử default DIRTY khác nhau. Không sửa mapping/default hoặc chạy DDL trong task này.
3. SRS UC027 yêu cầu current-cycle/reason/source/action/status counts; room directory UC023 chỉ là nguồn đọc được phép dùng lại. Board hiện tại là phần chuẩn bị độc lập, không hoàn thành UC027 và không credit lại UC023.
4. Screen metadata không phải UC ledger/permission matrix: screen 70 gắn UC127 nhưng SRS UC127 là Receive Overdue Work Alerts thuộc DEV2; màn hình 47 là shared. Một số danh sách dùng UC hành động. Không sửa ledger để khớp UI.
5. Handover A mô tả fallback/security đã có ở source A; baseline FS-001 đã chủ động giữ source develop và năm NonController. Baseline thực tế được ưu tiên, không tự khôi phục security patch.

## 3. Screens và mapping

Kiểm kê **24 screens**: 16 room/housekeeping/inspection (23–38), 5 task/workload/overdue (66–70), 2 internal request (76–77), 1 shared reference-data (47). Có **23 screens trong nhóm DEV1/cộng tác** và 1 reference-data shared; Overdue/UC127 cần DEV2 review riêng.

[SCREEN_API_MAPPING.md](SCREEN_API_MAPPING.md) và [SCREEN_API_MAPPING.json](SCREEN_API_MAPPING.json) có từng Screen → UC frontend/ledger → component/file → mock calls → controller/API → DTO → DB entities → permission → integration/gap. [API_ROUTES.json](API_ROUTES.json) liệt kê 42 method/routes candidate: 33 active, 9 inactive, chỉ trong các controller liên quan được chọn; không phải 42 API đủ bao phủ toàn bộ DEV1. Toàn API vẫn 31 controllers/186 method entries theo DI probe.

| Nhóm | Backend hiện có | Phần thiếu |
|---|---|---|
| Room read | GET `/api/rooms`, `/{roomId}`, `/{roomId}/history` | Approved readiness dimensions/current-cycle, reason/block projection và chuyển trạng thái |
| Cleaning Monitor | Recovery GET `/api/branches/{b}/cleaning-monitor`, `/{kind}/{id}` | Controller inactive; cancel/lifecycle/notification chưa đủ |
| Cleaning assignment | Per-task assignment-options; POST cleaning task assign | Collection/create/reassign/start/blocker/complete workflow; notification delivery |
| Cleaning progress | GET/PUT per-task progress, initialization/evidence | Không thay thế execution/state/time workflow |
| Inspection | Checklist create/detail/update/publish/retire; planning per task; execution read/start/results/finish/defects | Checklist collection, inspection queue, approve/reject, reinspection/current-cycle |
| Rework | POST inspection reworks, history | Rework list/detail recovery inactive; receive/start/complete/time/lifecycle |
| History | Active inspection history và room turnover history trong OperationalHistoryController | Replacement recovery cần review; không bắt buộc bật recovery để dùng legacy read |
| Operational tasks | Progress initialization/GET/PUT/evidence | Task list/create/assign/accept/decline/start/pause/complete/report APIs |
| Internal requests | Entities/mappings tồn tại | HTTP create/route/accept/progress/handover/complete/reopen/escalate/history/withdrawal |

## 4. Phase A và first slice

Đã triển khai foundation **cho read-only DEV1**:

- Base `/api`, override `VITE_DEV1_API_BASE_URL` chỉ chấp nhận same-origin path. Proxy/TLS validation và cookie/session contract giữ nguyên.
- GET gửi `credentials: include`, `cache: no-store`, không redirect tới HTML/login ngoài ý muốn, AbortSignal; không gửi actor/role/token lấy từ prototype.
- Phân biệt 401/403/400/503/network/malformed JSON, không tự retry hoặc fallback sang mock.
- Typed RoomDirectory request adapter/response DTO; kiểm response shape, branch, pagination, ID an toàn JavaScript. Không silently round SQL bigint vượt Number.MAX_SAFE_INTEGER.
- Loading/error/empty/retry/pagination; hủy request cũ và không hiển thị result của bộ lọc/chi nhánh cũ.

Screen 23 `/room-readiness` đã bỏ đọc room/location/type từ mock. Dùng phòng/floor/type từ GET `/api/rooms?branchId=...&page=...&pageSize=20&sort=floor`, filter số phòng và mã trạng thái persisted. Nhóm tầng chỉ trong trang hiện tại; tổng API là tổng filter, không giả là readiness counts toàn khách sạn. Không mở RoomDetail prototype bằng ID API.

Chi nhánh nhập tay hoặc query `branchId`, không lấy scope từ mock user/Shell. Đây là filter untrusted; backend vẫn xác thực session, ROOM_VIEW và branch/parent lineage trong SQL. UI shell/login/các screen khác vẫn prototype. Màn hình giải thích giới hạn room readiness và yêu cầu phiên máy chủ khi gặp 401.

**Thực sự nối code:** screen 23 → read adapter → active RoomsController → RoomDirectoryService → HotelOpsDbContext. **Chưa nghiệm thu runtime UI → API → DB.** Không có UC hoàn thành mới. Phase A write transport/CSRF bootstrap-refresh/protected routes/session UI còn phụ thuộc DEV3; task này chỉ cung cấp GET, không cung cấp mutation helper thiếu CSRF.

## 5. Validation mới trong FS-002

| Check | Kết quả | Giới hạn/warning |
|---|---|---|
| `dotnet build HotelOps.slnx` | Passed | 0 warnings, 0 errors; không sửa C# |
| `dotnet test HotelOps.slnx` | Exit 0 | Solution chỉ chứa API, 0 test cases; không coi đây là xUnit pass |
| Explicit `HotelOps.Dev1.RegressionTests.csproj` | Passed 93/93 | NU1900: vulnerability feed NuGet không truy cập được |
| `npm ci --offline --no-audit --no-fund` | Passed, 199 packages | Inherited deprecations; core-js/esbuild install scripts blocked bởi npm policy; build/test vẫn chạy được |
| `npm run build` | Passed | React Router use-client ignored; bundle chunks >500kB |
| `npm test` | Passed 20/20 | 13 prototype regression + 7 DEV1 transport/DTO tests; fetch stub, không SQL |
| DEV1 Playwright/Edge | Passed 5/5, exit 0 | HTTP interception; UI pagination/filter/401/403/503/stale-clear/empty; HTTP fixture server không chứng minh Secure cookie |
| Scoped Prettier 6 source/test/config files | Passed | Full lint không được dùng để bulk format |
| `npm run lint` | Failed | 116 inherited formatting files; baseline 117, board nay được format. Không sửa 116 file ngoài scope |
| DI/route probe | Passed | 31 active controllers, 186 entries, 0 normalized collisions; shared participant cùng scoped instance; NU1900; không mở SQL |
| Live HTTPS/API/SQL | Not Run | Chưa có target trial được xác nhận và real-session UI; không đọc secrets để đoán DB, không bypass authorization |

Browser harness ban đầu có load wait/StrictMode fixture và locator lỗi; đã sửa, chạy lại 5/5. Automatic webServer teardown bị treo trong host này, các lượt đó được dừng; lượt PASS cuối dùng Vite riêng do task khởi động trên port 5189 và `DEV1_TEST_EXTERNAL_SERVER=1`. Server riêng của task đã dừng (exit 0); không kill dịch vụ worktree khác. Scoped formatter từng báo config chưa format sau chỉnh sửa, đã format và check lại PASS.

Để chạy lại browser tests trên host này:

```powershell
# Terminal 1, frontend/: port 5189 phải trống; strictPort=true.
node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5189
# Terminal 2, frontend/
$env:DEV1_TEST_EXTERNAL_SERVER = '1'
node node_modules/@playwright/test/cli.js test --config playwright.dev1.config.ts
# Dừng server của Terminal 1 sau khi xong. Edge phải được cài.
```

Live acceptance tiếp theo cần HTTPS frontend/backend, certificate trusted, target SQL thử nghiệm được xác nhận và tài khoản ROOM_VIEW theo branch. Kiểm 200 data/empty, 401 expired, 403 cross-branch, pagination/filter và đối chiếu read SQL. Không chạy startup tự động trên connection string chưa xác định vì StartupSchemaValidator kết nối DB; session validation có thể touch SQL session dù endpoint nghiệp vụ là GET.

## 6. Blockers theo owner

**DEV3:** real cookie/session login & session state/protected navigation/401 handoff; HTTPS local setup; permission grants trong trial; role matrix Reception/Department Head; controller activation review; Workday/schema/rowversion/PauseAll/unscheduled và UC113 time dependency. Không sửa module của DEV3 để giả lập session.

**DEV2:** notification B01/B02/C01 exact revised DDL/EF/DI + durable intent/retry/dedup/source-read/recipient authorization; incident/effective-block/writer protocol; maintenance/PM transaction/time participant. Read directory hiện tại không cần publisher, nhưng assignment/inspection/rework và Room Ready acceptance phụ thuộc các hợp đồng này. Không sửa DEV2 source, không coi NOT_INTEGRATED là delivery.

**Shared/business:** current-cycle identity, occupancy/readiness/effective blocks, current inspection/final completer, audit/concurrency/transaction coordination; SQL default conflict; trial database/schema chưa chứng minh. UC107 vẫn BLOCKED_EXTERNAL, không có explicit implementation YES.

## 7. Route/security diffs cần review (chưa áp dụng)

| Controller | Diff cụ thể cần quyết định | Security/compatibility review |
|---|---|---|
| CleaningMonitorController | Bỏ `[NonController]` mới tạo GET collection và `/{kind}/{id}`; hiện không có active replacement | Cookie/HTTPS + CLEANING_VIEW branch/global; staff OWN/source-read không tự được cấp; query/timezone/pagination/DTO review. Không thêm Cancel/publisher vào GET |
| TurnoverHistoryController | Recovery trùng 3 GET `/api/rooms/{roomId}/turnover-history...` của OperationalHistoryController; phải chọn thay thế đúng 3 actions/service, giữ inspection-history | DTO/evidence availability/private file authorization/error handling/dual CLEANING_VIEW+INSPECTION_VIEW; không chỉ bỏ NonController gây collision |
| RoomTypeDirectoryController (DEV3) | Trùng GET `/api/room-types` và `/{id}` trong RoomTypeBatchController | Exact route/DTO/schema/grants/strong develop session gates; review thay thế actions, không bật song song |
| BranchAdministrationController (DEV3) | POST branches, GET edit, PUT trùng graph legacy; dependency GET/deactivate POST mới | Giữ deactivation fail-closed/GBR033; writer coordination và rowversion; chọn legacy replacements trước activation |
| WorkdaysController (DEV3) | Bỏ NonController thêm GET current/own-shifts, POST start và `/{workdayId}/end` | ATTENDANCE_RECORD/SHIFT_VIEW own-only resolver, schema deployment; unscheduled/PauseAll còn unavailable; không suy ALL đọc user khác |

Security proposals khác vẫn chờ DEV3: fallback authenticated policy, explicit public health/Development OpenAPI, WeatherForecast template removal, UTC SqlSessionPolicy patch. Không áp dụng chúng và không suy authenticated-only là đủ business permission. Frontend GET foundation không yêu cầu thay Program.cs, CORS, auth schemes hoặc database grants.

## 8. Roadmap và thao tác ghi đề xuất

1. **Room Readiness:** nghiệm thu GET directory với session/HTTPS/trial; sau contract được duyệt mới bổ sung current-cycle/reason/block/status counts. UC027 vẫn blocked.
2. **Cleaning Task:** review activation monitor read/timeline, reuse UI list/detail; không triển khai create/cancel khi thiếu lifecycle.
3. **Cleaning Execution:** nối GET progress rồi đề xuất **PUT `/api/branches/{branch}/cleaning-tasks/{id}/progress` (UC032)** là thao tác ghi đầu tiên sau read acceptance: snapshot items, Percent/Items/Note/evidence, current assignee IN_PROGRESS, strong If-Match, atomic audit; không chuyển trạng thái khi progress=100. Cần DEV3 CSRF lifecycle + target trial/schema/assignment/ETag. Test stale412/missing428/ownership403/validation400/rollback. Không tự triển khai write trong FS-002.
4. **Inspection:** reuse execution read/results và checklist detail/history đã có; finish không là approve/reject. Collection queue và conclusion writer cần contract riêng.
5. **Rework:** reuse request/history khi notification/current-cycle approved; thêm receive/execution/complete với time authority.
6. **Room Ready confirmation:** chỉ sau passed current inspection/no effective block và coordinator/audit/concurrency approved; không direct status picker.
7. **Operational Tasks:** progress read/write reuse; task lifecycle/UC107/accept-decline/assignment/time cần approval và missing APIs, không lấy progress làm full workflow.
8. **Internal Requests:** chốt request/routing/scope/status/concurrency/notification/history+withdrawal trước endpoints; entities tồn tại không nghĩa API sẵn.

Ưu tiên tiếp theo trước phát triển write: DEV3 cung cấp session UI + approved HTTPS/trial configuration, rồi chạy read acceptance. Nếu gate này chưa mở, read history/checklist/progress adapters là các phần có thể chuẩn bị độc lập theo mapping.

## 9. Files thay đổi

Modified:
- `frontend/.env.example`.
- `frontend/src/screens/hotel/RoomReadinessBoardScreen.tsx`.

Created:
- `frontend/src/api/dev1/client.ts`, `rooms.ts`.
- `frontend/tests/dev1-api.test.mjs`, `dev1-room-read.spec.ts`.
- `frontend/playwright.dev1.config.ts`.
- `docs/fs002/README.md`, `generate-audit.mjs`, `FS001_INPUT_INVENTORY.json`, `SCREEN_API_MAPPING.md`, `SCREEN_API_MAPPING.json`, `API_ROUTES.json`, `VALIDATION.json`.

Không xóa file. Build output/node_modules/test reports nằm trong ignore rules; không đưa generated binaries/private cert/secrets vào Git. Diff source đã review; whitespace check Passed cho 2 tracked modified và 12 file mới. Git có cảnh báo LF→CRLF khi normalize working copy; không có whitespace error còn lại. Không stage/commit/push.
