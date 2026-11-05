# COORD-FINAL-01 — quyết định chung DEV1/DEV2

Gói này là handoff kỹ thuật, không phải deployment hoặc nghiệm thu Notification V1. Nguồn quyết định có thẩm quyền là yêu cầu COORD-FINAL-01 chủ dự án cung cấp ngày 04/11/2026 (Asia/Saigon). Source snapshot lấy trực tiếp working tree; kết quả nhất quán cuối cùng ghi trong HANDOFF_VALIDATION.json. Không thay source/database/ledger.

## Thứ tự và giới hạn hiệu lực

Yêu cầu hiện tại → các quyết định đã phê duyệt phù hợp yêu cầu này → SRS_CHANGE_NOTES → SRS nghiệp vụ → schema/mapping/source hiện hữu. Task79 B12 là review proposal chưa áp dụng; không dùng nó như interface Notification đã triển khai. Các báo cáo Task78–86 là evidence có thời điểm, không thay thế current source.

Các nguyên tắc bên dưới được chốt cho thiết kế và hành vi. Chúng **không phê duyệt tự động** DDL, EF mapping, Program.cs, permission grants, source-read adapter, commit interceptor hoặc lifecycle coordinator cụ thể.

## Notification V1

| Quyết định đã chốt | Hệ quả bắt buộc khi DEV2 viết exact diff |
|---|---|
| A01 interface ACCEPTED | Giữ contract đã được chấp nhận. Bản namespace/signature/source A01 không hiện diện trong các worktrees đã đọc tại capture trước; bản này chỉ xác minh lại source DEV1 chính; DEV2 đưa chính source đó vào revised package, không để DEV1 đoán interface |
| A02 structural: DEV2 báo hoàn thành | Giữ trạng thái reported; chưa chứng nhận source/runtime/acceptance trong checkout DEV1 |
| B01 ba bảng | notification_operation_reservations, notification_publications, notification_delivery_intents; chỉ bảng delivery_intents là retry queue |
| Caller-owned transaction | Business, audit, operation completion, mandatory publication/intents cùng DbContext và transaction; participant không tự commit |
| Stable operation identity | Client stable GUID Idempotency-Key; UNIQUE(actor_user_id, operation_key); versioned canonical fingerprint; authorized replay và original result identity |
| Publication immutable | Không sửa event/source/recipient facts khi retry; superseding event phải là publication/transition được thiết kế riêng |
| Deduplicate intents | Identity từ publication + exact recipient/channel và source event revision; không dùng latest assignment để recompute recipient |
| Mandatory staging failure | Thất bại persist intent/publication trước outer commit phải rollback business và audit |
| Delivery failure sau commit | Business đã commit được giữ; durable retry, không chạy lại business writer |
| DELIVERED | Phải có durable inbox evidence khớp actual schema/FK, không chỉ một cờ hoặc enqueue thành công |
| Retention | Không tự purge reservation ledger trong V1; không tự backfill events lịch sử |
| Retry | JOB03 mỗi 5 phút; exponential backoff, giới hạn delay 60 phút. Cadence không có nghĩa mọi item retry sau đúng 5 phút; cần xét next-due và claim concurrency |
| Terminal reasons | SUPERSEDED, RECIPIENT_INACTIVE, RECIPIENT_NOT_FOUND, RECIPIENT_OUT_OF_SCOPE, SOURCE_READ_DENIED, SOURCE_NOT_FOUND |
| Dependency outage | DB/policy/source lookup transient failure không được terminalize thành denied/not-found |

SRS cũ có mô tả assignment vẫn được giữ khi gửi notification thất bại. Áp dụng phân biệt đã chốt: delivery thất bại **sau commit** giữ assignment và retry; mandatory durable intent persistence thất bại **trước commit** rollback. Không tiếp tục mở lại business decision này chỉ vì Task85 từng ghi pending reconciliation.

B01 executable DDL REVISE; B02 EF REVISE; C01 commit guard/DI REVISE; Notification runtime NOT ACCEPTED. B02 additive Database First; không sửa generated NotificationRecipient để ép navigation/composite key. SQL composite FK được duyệt kiến trúc, exact DDL/mapping vẫn phải review.

## Room lifecycle V1

Ba chiều độc lập: Occupancy, Housekeeping Readiness, Effective RoomBlock. Occupied có thể đồng thời mang cảnh báo Blocked. Không suy một chiều còn lại từ legacy operational_status một cách âm thầm.

- Không Ready nếu chưa passed current inspection hoặc còn effective block.
- ExpectedReleaseAt không tự release; incident Closed/Cancelled chỉ release block của chính incident đó.
- Tối đa một unreleased block mỗi (RoomId, IncidentId); tối đa một unreleased manual block mỗi Room.
- Check-in cần Active + Vacant + Ready + không effective block.
- Coordinator tham gia transaction do caller sở hữu; không tự commit.
- Reception check-in/out/move, room history, inspection/rework và incident/block updates phải nhất quán, có audit.

Các quyết định này chưa duyệt Room migration/coordinator/shared DI cụ thể. Hai conflict thực tế: EF default AVAILABLE khác SQL release default DIRTY; SQL UX_room_blocks_active hiện unique chỉ room_id khi released_at IS NULL, chặt hơn cardinality V1. DEV2 có đủ source để đề xuất exact SQL/EF compatibility changes, chưa được áp dụng.

## Workday UC139 V1

- B1.1 scheduled own Start/Current/Own-Shifts/End giữ nguyên; ATTENDANCE_RECORD không cấp quyền thao tác Workday người khác kể cả BRANCH/ALL.
- Unscheduled reason tối thiểu 10 ký tự; một primary eligible Department Head của branch; không Department.ManagerUserId fallback, không broadcast.
- Recipient cần cross-user Workday source-read permission thực sự. Own-only route hiện tại không đáp ứng yêu cầu này.
- Pause All atomic: pause source hợp lệ, close interval, history/audit và End Workday cùng một transaction. Một item không pause được thì rollback toàn bộ.
- DEV2 sở hữu Maintenance/PM participant. Không thêm PM PAUSED hoặc dùng ShiftAttendance thay WorkInterval.

SQL A1 đã enforce một WORKING day và một OPEN interval/user. Runtime A1 hiện chỉ nhận CLEANING/REWORK/TASK, không nhận maintenance kinds dù SQL vocabulary rộng hơn. B1.2 chưa tích hợp; UC139 BLOCKED_EXTERNAL.

## UC012 / GBR-033

DEV1 sở hữu Branch Deactivation; DEV2 sở hữu thay đổi trong writers FE06–10 của mình. Writers tạo/open/reopen nghiệp vụ phải kiểm Branch Active trong retained transaction/locking protocol chung. Deactivate kiểm dependencies và conditional status update cùng transaction. Cả Create→Deactivate và Deactivate→Create cần SQL tests; SQL1205 là FAIL kể cả retry sau đó thành công.

Không hard-delete/cancel open work để vượt blocker. Không coi mọi active/due PM schedule là open work: chỉ mở rộng predicate từ actual open-work semantics có SRS/source evidence. Chưa có phối hợp writers thì Deactivate tiếp tục fail closed.

## Authority và ownership

Authentication ≠ Employment Eligibility ≠ Business Responsibility ≠ Source-Read Permission. Notification không trao quyền vào source. Account/employment/assignment/branch có hiệu lực phải được kiểm độc lập; history/completed/cancelled sources không được vô tình mất quyền đọc chỉ do không còn executing ownership.

DEV2 có thể tự triển khai DTO/service/controller/test nội bộ FE06/07/08/09/10/14/15 nếu không đổi shared interface/schema/DI/RBAC hoặc DEV1 transaction/lifecycle. Không cần DEV1 duyệt từng file nội bộ. Exact shared changes phải chỉ rõ file/hunk/impact/acceptance và được chủ dự án phê duyệt; handoff này không cấp blanket approval.

Baseline giữ 26/75 UCs, 60/256 điểm = 23.4375%. Không bắt đầu Task87, không sửa UC classifications.

## Quy tắc song song bổ sung — có hiệu lực cho bản handoff này

Codex B đang làm các UC DEV1 độc lập trong worktree riêng. COORD-FINAL-01 chỉ kiểm chứng current source của worktree DEV1 chính và shared contracts liên quan DEV2. Không bắt đầu implementation thuộc Codex B; không lấy source từ worktree B làm current runtime DEV1.

Danh sách worktrees được khám phá lại bằng `git worktree list --porcelain` tại cả preflight và postflight, không dùng số lượng hoặc danh sách đường dẫn cố định. `evidence/PARALLEL_PREFLIGHT.json` và `evidence/PARALLEL_POSTFLIGHT.json` ghi exact path, HEAD, branch, index SHA256 và status từng worktree. Bản này chỉ đọc metadata Git ở các worktree ngoài DEV1 chính; không capture lại source của Codex B.

Không sửa/reset/clean/checkout/chuyển branch/đồng bộ/di chuyển/xóa worktree Codex B. Không ghi Git config hoặc index. Hoạt động đồng thời của B, nếu có, được ghi riêng; không nhận là thay đổi do COORD-FINAL-01 và không tuyên bố đã kiểm chứng implementation của B.

Trước tích hợp Codex B phải có bàn giao exact diff, base HEAD/branch, danh sách file và SHA256; review conflicts với toàn bộ local patches DEV1/shared contracts hiện tại; xin approval riêng cho phạm vi tích hợp cụ thể. Chỉ tích hợp và chạy verification phù hợp sau approval. Cùng HEAD không có nghĩa cùng working-tree source. Không tự động đồng bộ branch hoặc áp dụng diff của B.

Các kết luận về tìm kiếm ở worktrees trong bản capture trước chỉ là evidence lịch sử. Bản handoff này không khẳng định source của worktree B vẫn sạch hoặc không có publisher tại thời điểm B đang làm việc. Source/runtime được bàn giao vẫn là current DEV1 chính, đã đối chiếu hash trước/sau. Thay đổi source được dựa vào trong DEV1 chính phải dẫn tới `SNAPSHOT_INCONSISTENT`; thay đổi metadata của B chỉ được ghi nhận riêng khi không làm thay đổi source được dựa vào.

Không thay đổi approval boundary: business/architecture decisions đã chốt không tự cấp quyền áp dụng shared DDL/EF/DI/RBAC/coordinator. DEV2 có thể triển khai các thành phần nội bộ thuộc ownership theo contract; shared diff vẫn phải có file-specific review và approval riêng.

