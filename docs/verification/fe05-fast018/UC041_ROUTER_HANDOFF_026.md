# FE05-UC041-026 RESULT và bàn giao DEV3

- WORKTREE_BRANCH: `D:/SEP490/Projects/HotelOps-DEV1-FE05-FAST018` / `feature/dev1-fe05-source-consolidation`.
- Reference HEAD: `940b26b06f4bd19c457cdfea71a81a6d2c59ae19`.
- IMPLEMENTATION_SHA / source COMMIT_SHA: `ce23c8f8608941caa4e388c282f7535596e16abe`.
- READY_FOR_ROUTER_INTEGRATION: YES — source để DEV3 tích hợp và kiểm thử trong host được duyệt; không phải phê duyệt activation production.
- READY_FOR_FE03: NO.

## Router handoff

Public route được PL duyệt: `/inspections/assignment`.
Named export: `InspectionAssignmentDestination` từ `frontend/src/features/inspectionAssignment/InspectionAssignmentDestination.tsx`.

Props: `enabled?: boolean` mặc định **false**; `onOpenInspection(branchId, inspectionId)` do router owner cung cấp theo execution destination đã được duyệt. Component cần nằm trong React Router. `enabled` chỉ là activation switch phía host, không cấp quyền và không kích hoạt writer. Không lấy switch từ query/user input. Khi false, component không gọi API.

Query: `branchId` bắt buộc; `roomId` tùy chọn. Cả hai là số nguyên dương dạng decimal canonical, trong giới hạn JavaScript safe integer. Không chấp nhận trùng key, rỗng, dấu, phần thập phân, exponent, leading zero, unsafe integer hoặc query key khác. Không nhận task/assignee/version từ URL. Parser nằm ở `navigation.ts`; constant `inspectionAssignmentPath` giữ exact path. FE03 chỉ tạo URL sau integration acceptance.

DEV3 sở hữu authenticated/protected route, sign-in và khôi phục local URL sau đăng nhập. Component không giả lập auth. API giữ cookie/session, HTTPS, branch permission và CSRF middleware hiện có. Backend candidate controller vẫn **NonController**; registration/activation API và production route cần lane được phê duyệt riêng. Chỉ mount component không làm endpoint candidate trở thành public.

Context hợp lệ tải queue từ `/api/branches/{branchId}/inspection-assignment-candidate?roomId={roomId}` (bỏ query nếu không có room). Mỗi task phải được người dùng chọn; không tự chọn kể cả một candidate. Nhiều candidates được giữ nguyên. Options GET `/{taskId}` và preflight POST `/{taskId}` gửi roomId; branch-only queue vẫn gửi roomId của task được chọn khi đọc options. Assigned inspection chỉ mở theo thao tác tường minh và persisted InspectionId.

Backend dùng BatchAsync hiện có để kiểm tra session/branch INSPECTION_PERFORM; queue còn yêu cầu INSPECTION_VIEW. Room đọc AsNoTracking trong transaction hiện có, phải thuộc branch và room/floor/area còn active. Query queue lọc room trên server. Options/preflight resolve task qua room/branch/current cycle và từ chối task không khớp room. Không thêm permission hay sửa schema. URL không phải authority.

Lỗi: invalid URL chặn trước fetch; malformed room query 400; anonymous 401; denied branch 403; room không tồn tại/khác branch hoặc task mismatch 404; room inactive 409; stale revision 412; thiếu If-Match 428; notification chưa accepted 503. UI không fallback sang branch khác, không tự retry mutation, không báo success từ unexpected 2xx. Khi đổi task/reload: xoá source, lựa chọn, due/reason và confirmation. Khi đổi URL/Back: remount theo location key/search, huỷ request options cũ và bỏ response đã abort.

Notification blocker giữ nguyên `UC041_ASSIGNMENT_NOTIFICATION_NOT_ACCEPTED`. Xem `NOTIFICATION_AND_LIFECYCLE.md`: Assigned event/profile/recipient policy, pre-Inspection reservation, staging/durable receipt/replay/guard, schema/command activation vẫn chưa nghiệm thu. Không thêm writer hay bypass. Tài liệu `UC041_INTEGRATION_PROPOSAL_025A.md` là lịch sử trước approval; quyết định navigation 026 và handoff này thay thế trạng thái proposal cho phần đã duyệt, không phê duyệt các dependency còn lại.

## FILES_CHANGED (so với reference HEAD, gồm patch 025A đã duyệt)

1. `frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx` — room context, clear form/source, huỷ options request.
2. `frontend/src/features/inspectionAssignment/InspectionAssignmentDestination.tsx` — mới, destination export và activation guard.
3. `frontend/src/features/inspectionAssignment/navigation.ts` — mới, parser và exact path.
4. `frontend/src/features/inspectionAssignment/client.ts` — room query và response consistency.
5. `src/HotelOps.Api/Controllers/InspectionAssignmentCandidateController.cs` — room query binding/validation, giữ NonController.
6. `src/HotelOps.Api/Services/Housekeeping/InspectionAssignmentDomainComponent.cs` — room membership/activity, queue filter, task mismatch/preflight validation; writer không đổi.
7. `frontend/tests/inspection-assignment.test.mjs` — parser/client regressions.
8. `frontend/tests/inspection-assignment.spec.ts` — context, direct-open, Back, lỗi quyền/stale/mismatch.
9. `frontend/tests/fixtures/inspection-assignment-destination.html` — fixture mới.
10. `frontend/tests/fixtures/inspection-assignment-destination.tsx` — fixture router riêng, không mount application route.
11. `tests/HotelOps.Checklist.CollectionTests/InspectionAssignmentContextTests.cs` — mới, HTTP-boundary tests trong project FE05 có sẵn.
12. `docs/verification/fe05-fast018/UC041_INTEGRATION_PROPOSAL_025A.md` — lưu proposal lịch sử từ lượt trước.
13. `docs/verification/fe05-fast018/UC041_ROUTER_HANDOFF_026.md` — tài liệu này, commit documentation riêng tham chiếu exact source SHA ở trên.

Không đổi shared router, FE03, shared SQL, entities/mappings, DTO hay notification writer. Không push.

## Validation và giới hạn

- BUILD_STATUS: PASS — `dotnet build HotelOps.slnx`, 0 warning/error; FE05 Vite build PASS; TypeScript `--noEmit` PASS.
- UNIT_TESTS: PASS — client/parser 31/31; existing FE05 collection project 38/38, gồm 10 HTTP-boundary cases mới. `dotnet test HotelOps.slnx --no-restore` exit 0 nhưng solution chỉ chứa API, không tính là assertion coverage; chạy test project riêng như trên.
- BROWSER_TESTS: PASS — 16/16 Edge; fixture HTTP interception, không phải real-auth/database/public-route E2E. Bao gồm clear task form sau 200/404/412, 503/unknown không retry, invalid/disabled không fetch, direct-open 401/403/404/409/412, multiple candidates không tự chọn, Back trả queue sạch, mismatched room không hiện form.
- Lượt browser đầu: 8 PASS/8 FAIL vì test glob bắt nhầm module `/src/api/...` của Vite. Đã sửa interceptor chỉ bắt `/api/...`; chạy lại toàn bộ 16 PASS. Kết quả thất bại ở `TestResults/fe05-uc041-026/browser`, kết quả cuối ở `browser-final`; không tính lượt đầu là PASS.
- Warning còn lại: Node bỏ qua NO_COLOR do FORCE_COLOR. Không có build warning C#.
- API review: giữ methods/routes và NonController; kiểm tra DTO độc lập, cancellation, AsNoTracking, branch/session boundary và HTTP error mapping. Unit controller tests không thực thi ASP.NET middleware/model binding đầy đủ.
- Database: đã đối chiếu Room, DbContext mappings, source joins/current-cycle và BatchAsync; **chưa chạy room-context queries trên SQL Server thật**. Không thực hiện DDL/data writes.
- BLOCKERS: DEV3 route/auth integration và API activation approval; real authenticated room/branch/task cases, direct-open/Back qua public route; accepted notification composition và committed-assignment E2E. Không tuyên bố production READY hay FE03 READY từ fixture tests.