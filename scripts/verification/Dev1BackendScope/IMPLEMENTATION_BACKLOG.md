# Backlog triển khai DEV1

Các số dưới đây là thứ tự task kỹ thuật, không phải UC hoặc permission mới. Chỉ đề xuất, chưa triển khai hay phê duyệt quyền. Tách reads khỏi writes để không buộc task đầu tiên giải quyết mọi schema/workflow pending.

| Ưu tiên | Scope / UC | Dependencies / DB readiness | Độ phức tạp & test độc lập | Giá trị / gate trước coding |
|---|---|---|---|---|
| 1 | User directory read slice — UC-141 | USER_VIEW hiện có; User/Branch/Department/UserRole/Role đã map; fixtures auth còn thiếu | Thấp–vừa; query + deny/allow + pagination | Hỗ trợ admin/FE, tránh cấp credential/assignment. FIRST_IMPLEMENTATION_PLAN |
| 2 | Room Type list UC-142; branch structure UC-013; room directory UC-023 — tách task theo nhóm | RoomType/Branch/Floor/Area/Room/Department; read permissions cho structure/type list cần mapping xác nhận; ROOM_VIEW cho room directory | Vừa; scoped reads, join/filter, inactive/historical | Cung cấp lookup FE và nền cho Cleaning; không mở Owner theo matrix cũ |
| 3 | Access review UC-011 và read lookups phục vụ assignments | ROLE_MANAGE vs USER_VIEW cho target-user permission review cần xác nhận; evaluator hiện caller-only | Vừa–cao; effective grant paths/scope/expiry, fail-closed | Hỗ trợ DEV2/debug quyền; không giả identity hoặc union scopes sai |
| 4 | User admin UC-004/005, rồi UC-006/007/010 | Atomic provisioning vs credential-service transaction; uniqueness, last-admin/self-admin, open-work reassignment, user concurrency chưa chốt | Cao; SQL atomicity/races/session revocation, owned fixtures | Tạo staff hợp lệ; không xử lý create-account như CRUD User đơn giản |
| 5 | Hotel structure CRUD UC-012/014–022/024–026; RoomType UC-017–019 | MASTER_DATA_MANAGE catalog; rowversion conflict; cross-module references; policy canonical 2 branches/4 departments | Vừa–cao; soft deactivate + FK guards + race tests | Sau khi chốt có cho mở rộng canonical branch/department và cơ chế concurrency |
| 6 | Role/grant administration UC-008/009 và mutation phần còn lại UC-010 | ROLE_MANAGE; fixed catalog vs dynamic role creation; last-admin; immediate revocation | Cao; effective access + privilege loss races | Không tự thêm role/permission ngoài catalog Task 15; approval riêng trước shared auth/schema change |
| 7 | Cleaning reads UC-037/038; checklist UC-039/040 và inspection history UC-048 | CLEANING_VIEW/INSPECTION_VIEW/CHECKLIST_MANAGE; room fixtures + checklist versioning; read permission mapping turnover cần xác nhận | Vừa; historical views, stable paging/version binding | Có thể tách read tasks trước full workflow, không coi adapters là API |
| 8 | Shift scheduling/attendance UC-138/139; operational reference UC-063/064 phối hợp | SHIFT_MANAGE/ATTENDANCE_RECORD hiện có; DH scope clarification; shared ref/SLA; assignments/timezone | Vừa–cao; per-branch independent fixtures | Cấp nền shift/eligibility cho work assignment; không payroll |
| 9 | Work-interval/schema/permission decision cho UC-111/140 + GBR-138–144 | Thiếu interval/correction/handover models; `work_interval.correct` không trong catalog | Gate thiết kế; cross-module approval bắt buộc | Chốt trước khi hứa start/pause/reassign/time/handover đầy đủ; không ép vào progress note |
| 10 | Cleaning assignment/execution/rework UC-028–036, Inspection UC-041–047 | Room lifecycle/Reception rules, SoD, shift + intervals, adapters/dispatch; schema constraints không mô tả đủ transition | Cao; state-machine, transaction/audit, concurrency, two assignees, deny actions | Hoàn thiện room-turnover flow sau gates; phối hợp room blocks với DEV2 |
| 11 | Operational task workflow UC-102–113 | TASK_MANAGE/EXECUTE/VIEW catalog nhưng matrix execute xung đột; criteria/recurrence/intervals và verification SoD cần chốt | Cao; owned fixtures, reassignment, reopen, pause/overlap | Không copy Maintenance workflow hoặc dùng ROLE bypass |
| 12 | Internal requests UC-114–123 | IR_CREATE/PROCESS/IR_OVERDUE_HANDLE; IR view permission mapping còn thiếu; receiving DH branch scope; handover/link storage | Cao; atomic events/audit, acknowledgment, closure deps | UC-118 link qua contract DEV2, notification event qua owner FE-14 |
| 13 | UC-027 room readiness/occupancy integration | Cleaning + Inspection + Incident/block authoritative state; pending Reception/transition approval | Cao; concurrent sources, failed inspection, blocked READY prevention | Không cho PUT status tùy ý; source events/atomic recalculation phải chốt |

## Nhánh có thể tiến hành độc lập và nhánh chờ quyết định

- Độc lập nhất: UC-141 list read; sau đó UC-142 list và các scoped read đã chốt permission. Không cần schema mới hoặc sửa service DEV2.
- Phối hợp ngay khi DEV2 cần: auth resource dispatcher, common error mapping và binding trước authorization. Đây là shared infrastructure workstream riêng, không tự kèm vào user-directory task.
- Đang bị gate: mutation concurrency, fixed-vs-custom role catalog, room lifecycle/Reception, work intervals/correction/handover, reference/SLA ownership. Ghi rõ quyết định trước implementation, không tự chọn enum/status/permission.
- DEV2 giữ toàn quyền task Inventory Adjustment/Reversal. Sự tồn tại Adjustment/Reverse code trong baseline không xác nhận đã đáp ứng contract riêng đang sửa.

## Fixtures tối thiểu cho các đợt sau

Canonical database có 2 branch/8 department/15 references/catalog nhưng **0 user/session/assignment**, không có room/floor/UOM/warehouse business fixtures. Không tự chạy SQL Task 16.

1. User-directory: positive authenticated USER_VIEW actor theo grant catalog đã được duyệt, authenticated actor không USER_VIEW, restricted/inactive/expired/revoked cases; target users ở hai branch/departments, nhiều roles, inactive và soft-deleted rows; đủ >20 rows để test paging. Passwords sinh runtime qua auth service, không vào Git.
2. Structure: floors/areas/room types/rooms active và inactive, tham chiếu mở để test deactivation; không copy ID cũ.
3. Cleaning/inspection: HK staff/supervisor và inspector khác performer, shift assignment, room, refs, immutable checklist version + defects/rework; nguồn Incident block do DEV2 fixture contract cung cấp.
4. Tasks/work-time/internal request: requester/handler/verifier độc lập, shift/timezone, category/request type, valid linkage. Chỉ tạo interval/handover fixtures sau khi schema/permission được duyệt.

Các integration runners hiện hữu có target guards riêng; không tắt guard để trỏ về canonical trial. Test fixture provisioning và runtime credential handoff là bước riêng có kiểm soát.
