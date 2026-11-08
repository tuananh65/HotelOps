# FE05-FAST-002 — PARTIALLY_VERIFIED

Parent `e6c49fa1003a2587f471f1305f9ef2cda14c9336`, worktree
`D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST001`, branch
`feature/dev1-fe05-execution-history`. Worktree sạch trước task. Không reset/clean.

## Chức năng mới

- **UC041 read/navigation VERIFIED:** danh sách Inspection của session actor, search và
  server pagination; hiển thị inspector/version/phase/due; mở trực tiếp execution và đọc
  lại detail/ETag từ API execution. Không nhận actorId từ client.
- **UC041 planning VERIFIED:** danh sách cleaning cycle chờ Inspection/reinspection theo
  trạng thái persisted và không có Inspection mở; mở existing planning endpoint để xem
  checklist, candidates và chọn candidate preview. Không suy luận current cycle từ latest
  timestamp/ID. Planning là read snapshot, không cấp authority để assign.
- **UC041 writer BLOCKED:** không có submit assignment từ UI, không gọi writer trong live.
  `HousekeepingAssignmentsService.AssignInspectionAsync` hiện trả
  `HousekeepingWriteDto.Notification=NOT_INTEGRATED`; mandatory in-app delivery/durable
  retry chưa có accepted DEV2 integration. Exact contract gap tại
  `scripts/verification/Housekeeping/TASK73_DEV2_NOTIFICATION_HANDOFF.md`, source commit
  `29eee0fe1465266b26d5cb837989e1f5f62193dc`. Cần protocol atomic staging/publication,
  recipient/source identity, delivery/retry semantics và verification theo contract đó.
  Không bypass hoặc tạo publisher/outbox/writer khác. Existing writer vẫn giữ nguyên.
- **UC043 severity VERIFIED:** selector dùng id/code/name do backend trả, không nhập ID
  thủ công, không hard-code IDs; chỉ active SEVERITY MINOR/MAJOR, khớp defect writer.
  Backend vẫn validate tại submit. Lookup lỗi không ngăn đọc execution; defect không
  thể submit khi chưa có selection hợp lệ. 401/403 vẫn đi qua feature session boundary.

## APIs mới, chỉ GET

| Route dưới `/api/branches/{branchId}` | Quyền hiện hữu | Response |
|---|---|---|
| `/inspection-severities` | INSPECTION_PERFORM | `[{id,code,name}]` |
| `/inspections/assigned` | INSPECTION_PERFORM + current session actor only | `{items,total,page,pageSize}` |
| `/inspection-assignment-tasks` | INSPECTION_VIEW, giống existing inspection planning reader | `{items,total,page,pageSize}` |

Collections nhận `search`, `page` (>=1), `pageSize` (1–100), chống overflow; search tối đa
150 ký tự và không control chars. Branch/session/authorization trước và sau query, active
branch và existing locks/transaction qua `BatchAsync`; không tạo authority song song.
Read-only EF queries AsNoTracking/async/CancellationToken; không audit/write DB.
Catalog severity dùng chung nhưng API admission theo branch của người gọi; không ngụ ý
catalog có ownership riêng theo branch. HTTP conventions: HTTPS required, cookie auth,
no-store, ProblemDetails; 400 invalid filters, 401 session, 403 permission, 409 inactive,
503 unavailable/schema. Không sửa DbContext, entities, SQL, Program.cs hay DI.

Existing GET `/api/branches/{branch}/cleaning-tasks/{task}/inspection-options` được reuse
cho preview, không nhân bản eligibility query. UI ghi rõ raw shift records không phải
attendance proof, chỉ hiển thị trang candidates do endpoint trả. Assignment vẫn cần
backend recheck mọi điều kiện và xử lý concurrent/duplicate assignment sau khi gate mở.

## Verification

- Backend solution build và InspectionReadLive harness build: PASS, **0 warning/error**.
  Warning nullable đầu task đã sửa. `dotnet test HotelOps.slnx --no-build` exit 0 nhưng
  solution chỉ chứa API, **không có tests được discover**; không tính làm test credit.
- Frontend build PASS; Vite warning React Router `use client` bị bỏ qua như baseline.
- New read client/model tests: **21/21 PASS**.
- New browser tests: **8/8 PASS** (interception, không phải live).
- Directly affected FAST001 browser flows: **5/5 PASS**; không chạy lại history hoặc
  toàn bộ old suite. Cập nhật old live runner selector binding nhưng không rerun old suite.
- **9 unique live cases PASS qua 2 lượt:** lượt đầu Sign-In và severity PASS, collection
  lỗi 500. Sửa collection query để filter/order/page trước constructor DTO projection;
  build lại. Lượt tiếp theo login bootstrap + 7 cases còn lại PASS, severity được đánh dấu
  NOT_RERUN với receipt trước. Không tuyên bố một lượt sạch 9/9.
- Live coverage: assigned actor/branch, search/page/empty, UI open execution, queue loại
  task có Inspection mở, planning preview, read concurrency, active/inactive severity,
  selector submit, inactive severity rejection, reload/persistence, 401/403 và wrong branch.
- Assignment đúng/sai người, assignment write concurrency/conflict và notification:
  **NOT_RUN / BLOCKED**, không lấy read tests thay cho writer verification.

SQL local `DESKTOP-JI8U50T\SQLEXPRESS`, version `16.0.1000.6`, trial mới
`HotelOps_Canonical_20261108_103655_8b2980a9_Fresh`, owner
`e0935dad-a179-4dd7-8ea0-67aedcea1e5f`, 0 users trước synthetic fixtures.
Existing disposable installers được reuse; không sửa shared SQL scripts, không D4 patch.
Real SignInScreen, HTTPS API 7447/frontend 7448, Secure/HttpOnly cookie và CSRF thật.
Ephemeral test certificate được proxy pin, browser chấp nhận local certificate.
Identity/owner guard trước mutations. Persistence: 2 Inspection, 3 cleaning tasks, 1 FAIL
result, 1 defect với severity backend-selected, 3 audit rows, không assignment write.
Owned processes đã dừng, các trial và receipts cũ được giữ nguyên. Không deadlock-free claim;
chỉ xác minh 4 concurrent reads; không stress hoặc writer concurrency acceptance.

## Giới hạn và tiến độ

Task ước tính **80%**. UC041 **70%** (read/planning/navigation usable; assignment/notification
chưa nghiệm thu), UC043 **95%**, UC042 **95%**, UC048 **95%**. Không tăng % hai UC không
được triển khai lại. Tất cả là ước tính dựa evidence, không UC ACCEPTED.

UI branch identity vẫn nhập theo integration boundary hiện có. Candidate preview hiện dùng
trang đầu của existing planning reader; không coi đó là danh sách eligibility cuối cùng.
Shared navigation/router/auth/CSRF không đổi trong FAST002; UC042 execution chỉ đổi
severity-loading/selector boundary. Không sửa FE04/DEV2/DEV3-owned logic, UC044–047,
notification, feature flags, D4 hoặc shared SQL. Source manifest bao gồm tracked và
untracked payload trước commit; receipts chỉ allowlist sanitized artifacts.
