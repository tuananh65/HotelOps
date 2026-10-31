# UC-006 V1 Contract Matrix - Task57 current

## Task57 approved policy và delivery baseline

**D05 APPROVED V1 POLICY (developer Task57):** employee phải End Working Workday trước
Admin Deactivate, kể cả không còn open interval. Ended riêng lẻ không chứng minh source
assignments đã release. Không auto-end trong Deactivate, không tự cấp Admin forced-end;
employee không thể End cần operational exception được duyệt riêng.

**Delivery priority APPROVED:** ổn định backend đã triển khai trước; các workflow SRS thiếu
được giao task riêng. Đây không phải scope exclusion hoặc approval bypass responsibility.
D01–D04 và D06–D07 chưa có formal approval mới; giữ nguyên prior V1 PROVISIONAL approvals S7.
D05 chốt business policy nhưng chưa có authoritative Workday/interval model hoặc writer;
`WORKDAY_CONTRACT` vẫn giữ, không còn mô tả Working-alone policy là đang chờ quyết định.

Đầu ra thực thi: [deployment scope proposal SA57](UC006_V1_DEPLOYMENT_SCOPE.md) phân loại
source reachable/unregistered/missing/external, existing writer coverage E01–E08 và acceptance
plan; [deferred safety matrix F01–F10](UC006_DEFERRED_WORKFLOWS.md) chỉ rõ model/legacy/
external evidence cần cho task riêng. SA57/D06/D07 chưa được approved/activated. Ba release
gates vẫn pending; five business Unresolved/two Unsupported không đổi. Không runtime edits.

Các section nguồn/criteria Task56 bên dưới là evidence kế thừa; D05 row và Workday/C06 đã
cập nhật theo Task57. Proposal/history của quyết định khác không tự trở thành approval.

# Task56 evidence and contract proposal (retained, D05 updated by Task57)

Ngày: 2026-10-30. HEAD: `4a187320643d0c82f232c6b9dd5a667b8401c280`.
Trạng thái: **đề xuất contract V1 để nhóm duyệt; không phải phê duyệt release**.
Không thay runtime, không thêm gate, không áp dụng scope exclusion trong Task56.

## 1. Source of truth và mức phê duyệt

| ID | Tài liệu đã tìm/đọc | Giá trị sử dụng và giới hạn |
|---|---|---|
| S1 | [UC Specification](../../../docs/requirements/srs_sections/02_use_case_specifications.md), §1.6 UC-006; §11 UC102-112; §12 UC138-140; §13 UC114-123; UC024/025 | Có đầy đủ yêu cầu nghiệp vụ, actor, normal/exception flows. Đây là bản chuyển đổi của SRS gốc, không tự trở thành baseline V1 đã ký duyệt |
| S2 | [Business Rules, status catalog và transitions](../../../docs/requirements/srs_sections/05_requirement_appendix.md), GBR001/002/004/005/007/008/009/012/014/026-028; GBR123-156 | Nguồn quy tắc công việc, ownership, verification, workday và acknowledgment. Phân biệt vocabulary nghiệp vụ trong SRS với status SQL |
| S3 | [Overall Requirements](../../../docs/requirements/srs_sections/01_overall_requirements.md), BF05/BF06, §4.4 Permission Matrix | Matrix có Administrator Full cho Create/Update/Deactivate User; các role khác No. Tài liệu có chú thích NOT APPROVED; BF06 và UC120 có điểm cần thống nhất về confirmation |
| S4 | [Functional Requirements](../../../docs/requirements/srs_sections/03_functional_requirements.md), §11-13, §16.4 JOB04, §16.5 JOB05 | JOB05 tạo recurring task với default assignee; JOB04 tạo preventive maintenance occurrence. Đây là writer được yêu cầu trong SRS dù chưa có implementation tương ứng |
| S5 | [Original Word SRS](../../../docs/requirements/HotelOps_SRS_DRAFT.docx), [Reading Guide](../../../docs/requirements/SRS_README.md), [Change Notes](../../../docs/requirements/SRS_CHANGE_NOTES.md) | Đối chiếu trực tiếp Word xác nhận 6 rule: GBR028, 124, 133, 142, 144, 151. Reading Guide/Change Notes ghi rõ draft và permission revisions chưa hoàn tất |
| S6 | [Backend coordination 2026-10-20](../../../docs/coordination/HotelOps_Dev2_Backend_Sync_RBAC_2026-10-20.md), §1, §3, §5 | Có quyết định đã chốt: OM/DH ngang cấp và theo Branch; DH quản lý các bộ phận của Branch. Thiết kế departments.manager_user_id và Supervisor/Lead theo ca vẫn chưa chốt. Không lấy role name hoặc manager FK làm thay thế permission/SoD |
| S7 | [V1 PROVISIONAL đã ghi từ chỉ dẫn developer](V1_BUSINESS_CONTRACT.md), [Task47 technical decisions](DEV1_PREDICATE_DECISIONS.md), source/receipts Task53-55B | Đã có approval kỹ thuật/provisional: block current management, actual shift/workday responsibility, required next actor và unfulfilled mandatory acknowledgment; không mặc định COMPLETED an toàn. Giữ các approval này, không yêu cầu duyệt lại nguyên tắc; còn thiếu exact lifecycle/actor/model binding, không phải thiếu mọi quyết định V1 |
| D1 | [Database Design reference](../../../docs/database/DATABASE_SCHEMA.md), [Database Rules](../../../docs/database/DATABASE_RULES.md), [database README](../../../docs/database/README.md) | Có tài liệu thiết kế, không phải không có Database Design. Tài liệu tự ghi DRAFT/source-derived và chưa xác nhận live schema; một số cột/session terminology thuộc snapshot cũ |
| D2 | [HotelOpsDbContext](../../../src/HotelOps.Api/Data/HotelOpsDbContext.cs), [Entities](../../../src/HotelOps.Api/Entities), [canonical full schema](../../database/release/01_HotelOps_Full_Schema.sql), [contract patches](../../../database/patches) | Evidence implementation hiện tại. Status CHECK/FK không chứng minh business transition. Không chạy SQL, migration hoặc truy cập HotelOpsDB trong Task56 |
| P1 | [Approved DEV1 query](../../../src/HotelOps.Api/Services/Users/ApprovedDev1ResponsibilityQueries.cs), [integrated query](../../../src/HotelOps.Api/Services/Users/IntegratedDeactivationResponsibilityQueries.cs) | Predicates và uncertainty thực tế Task46-55B; không phải bản thay thế SRS |
| P2 | [DEV2 predicates](../../../src/HotelOps.Api/Services/Users/DeactivationResponsibilityQueries.cs), [state classifier](../../../src/HotelOps.Api/Services/Users/Dev2ResponsibilityStateQueries.cs) | Sáu nhóm responsibility đã có contract kỹ thuật; unknown/contradictory data vẫn fail closed |
| P3 | [Deactivate service](../../../src/HotelOps.Api/Services/Users/UserAccountDeactivationService.cs), [release acceptance](../../../src/HotelOps.Api/Services/Users/UserAccountDeactivationReleaseAcceptance.cs) | SQL actor/session/scope, retained locks, business review và release fuse trước mutation. Endpoint/DI Deactivate chưa đăng ký |

**Chưa tìm thấy** baseline UC-006 V1 hoàn chỉnh đã được nhóm phê duyệt sau các thay đổi role/scope;
Permission Matrix V4 hoàn chỉnh có phê duyệt (coordination note nhắc V4 nhưng không chứa
matrix đầy đủ); quyết định phê duyệt mapping lifecycle SQL cho bảy workflow dưới đây; inventory
writer của deployment/import/job/external. Ba tên SQL cũ được database README nhắc tới không
phải bộ canonical release SQL hiện tại. Không coi tên file “Final”, build PASS hoặc seed SQL
là chứng nhận phê duyệt. Đây là thiếu **baseline/quyết định implementation**, không phải SRS
không có yêu cầu Workday/handover.

Word SHA256: `4AE3DCBC4E5C7C88041A6CA2F6124712B1E67B41397C8918ECDDC964B04309BD`;
6 đoạn đối chiếu được lưu trong `obj/task56-original-docx-excerpts.local.txt`.
[Task16 coverage audit](../Dev1BackendScope/README.md) cũng phân biệt endpoint gap với
product scope; việc xếp workflow vào backlog không phải approval loại khỏi V1.

## 2. Contract nền UC-006 đã có căn cứ

1. Actor lấy từ session SQL hợp lệ và permission/scope server-side; không tin actor/branch
   do client tự chỉ định (GBR001/002). Giữ contract kỹ thuật USER_MANAGE ALL/BRANCH đã được
   developer duyệt trước đó, deny OWN/self; không suy thêm quyền từ draft role matrix.
2. Deny self và bảo toàn ít nhất một eligible administrator sau thay đổi (GBR026/027).
   Dùng foundation LastAdmin hiện tại; không tự đơn giản hóa tiêu chuẩn đã được chấp thuận.
3. Chặn **open assigned work hiện tại**, kể cả ở branch khác với primary branch; phải
   reassign/handover đúng workflow trước khi deactivation (GBR028). Không tự reassign,
   cancel hoặc xóa business work trong UC-006.
4. Lý do tối thiểu 10 ký tự sau trim; version đã đọc phải còn đúng (GBR012/005). Strong
   ETag là contract kỹ thuật hiện có; Task56 không thiết kế/mở HTTP parser.
5. Chỉ đổi account status, giữ credentials/roles/memberships/business history. Revoke toàn
   bộ target sessions và audit actor/time/reason/before-after trong cùng transaction; mọi
   failure rollback (UC006, GBR007/008/009). Không log credential/session material.
6. Historical creator/requester/assignee FK, quyền có thể thực hiện một action trong tương
   lai, notification đã đọc và current assigned responsibility là các khái niệm khác nhau.
   Reopen/return/job là writer phải kiểm tra eligibility mới, không phải lý do giữ mọi lịch
   sử thành blocker vĩnh viễn. Điều này không chứng minh SQL COMPLETED đã là SRS Closed.

## 3. Ma trận bảy workflow

Scope dưới đây là **khuyến nghị cho toàn workflow**, không tuyên bố implementation đã xong.
IN_SCOPE_IMPLEMENTED chỉ áp dụng cho các subset được chỉ rõ ở §4. Không có workflow nào được
tự phân loại OUT_OF_SCOPE_NOT_IMPLEMENTED trong đợt này.

| Workflow | UC/SRS requirement và evidence | Current source / writer | SQL support | Current responsible actor | Nonterminal / terminal theo SRS | Rule UC-006 V1 đề xuất | Scope recommendation | Remaining ambiguity / việc phải làm |
|---|---|---|---|---|---|---|---|---|
| Internal Request | S1 UC114-123; S2 GBR146-153; S3 BF06 | P1 có assignee predicate; không có InternalRequest controller/service. UC115/119/121/122 writers chưa triển khai | internal_requests.requester_id/status/receiving_department_id/completed_at/closed_at; assignments assignee_id/status/assigned_at/ended_at; events actor_id/event_note. Không có structured receiver ack | Pending Acceptance: receiving DH quyết định intake; In Progress: current handler; pending request handover: sender vẫn handler, receiver có action ack/reject. Requester không tự động là handler | Pending Acceptance/Accepted/In Progress chưa hoàn tất; Closed/Rejected/Cancelled là kết thúc cycle. Closed có thể reopen, không vì thế luôn là current duty | Block handler có current open assignment; giữ sender đến khi request acknowledgment **và assignment transfer** commit; pending designated receiver theo D04. Creator/requester FK đơn lẻ không block. Closed history không tự block; reopen phải revalidate last handler | IN_SCOPE_MISSING_IMPLEMENTATION | SQL DRAFT/SUBMITTED/COMPLETED chưa map đầy đủ SRS; UC120 closure vs BF06 requester confirmation cần D01; thiếu model ack/current-cycle và writer, không chỉ thiếu query |
| Line Manager | S1 UC024/025, S2 GBR041, S6 nói department stewardship/branch DH; S7 đã duyệt block current management. Không thấy clause SRS biến mọi reporting FK thành open work | P1 có LINE_MANAGER_DESIGNATION cho active employee; UserProfile whitelist không sửa manager, CreateAsync không gán manager; không có manager-transfer service | users.manager_user_id/deleted_at/employment_status; departments.manager_user_id/is_active là liên kết khác | Scalar line manager không đồng nghĩa DH, reviewer hay handler. Current management phải có authoritative designation, không suy từ role capability | Không có manager-duty lifecycle/terminal trong SRS. Account INACTIVE không kết thúc employment; deleted FK/history không chứng minh mọi duty đã release | Giữ approval S7 block current management; D02 chốt designation/history/release semantics và writer transfer cụ thể, không duyệt lại nguyên tắc. Giữ positive subset Task55B trong lúc chưa map đầy đủ | CONTRACT_UNRESOLVED | Cần inactive employment/history whitelist, authoritative release và transfer writer; không sửa role/department mapping để giả lập quyết định |
| Operational Task | S1 UC102-112; S2 GBR124/126-135; S3 BF05; S4 JOB05 | P1 có ACTIVE assignment với TODO/IN_PROGRESS/BLOCKED, ended history và ambiguity checks. Không có Task/verification/reopen/recurrence service | operational_tasks.status/completed_at; assignments assignee_id/assignment_status/ended_at; progress/dependencies. Không có authoritative verifier acceptance/current cycle mapping | Current assignee trong open cycle; Pending Verification có DH thực hiện quyết định và current assignment chưa được release bởi closure. Không gán duty cho mọi DH chỉ do role | Assigned/Accepted/In Progress/Paused/Pending Verification còn open; Closed/Cancelled kết thúc cycle. Pending Assignment không có owner nếu chưa chỉ định. Return về Accepted cùng assignee; reopen Closed tạo Assigned cùng hoặc người mới | Block current assignee đến khi accepted closure, cancellation hợp lệ hoặc actual reassignment release; pause và submit completion không giải phóng trách nhiệm. Verified Closed history không block; return/reopen/job phải lock và kiểm tra eligibility | IN_SCOPE_MISSING_IMPLEMENTATION | SQL COMPLETED không phân biệt Pending Verification/Closed; TODO gom nhiều state. Cần authoritative completion/verification/cycle representation; không thêm status hoặc coi completed_at là proof. JOB05 là yêu cầu có thật nhưng chưa có writer |
| Shift | S1 UC138/140; S2 GBR141/144; roster dùng cho operational coordination, không payroll | P1 có SHIFT_OPEN_ATTENDANCE từ actual timestamps; không có roster/attendance/shift-handover mutation controller/service | shift_assignments user_id/assignment_status, work_shifts scheduled times, shift_attendance actual_check_in/out + attendance_status. Defaults ASSIGNED/PLANNED/NOT_STARTED không phải transition spec | Roster employee; actual open activity thuộc employee. Shift receiver acknowledgment là awareness, không thay current assignee của task/request | SRS ShiftAssignment Scheduled/Cancelled; lịch hết giờ không chứng minh workday/duty kết thúc. Không có SRS rule nói roster tương lai tự là UC006 blocker | Giữ actual open activity evidence; không dùng scheduled_end để release. D03 quyết định roster tương lai là blocker hay thao tác cancel riêng UC138. Khuyến nghị xử lý roster bằng UC138 và đánh giá actual work riêng, không tự sửa roster trong Deactivate | IN_SCOPE_MISSING_IMPLEMENTATION | SRS Scheduled vs SQL ASSIGNED chưa có writer mapping; time/status/overnight và future roster policy cần chốt. UC140 ack không thay việc reassign từng item |
| Workday | S1 UC139; S2 GBR136-143; developer Task57 D05 APPROVED: Working blocks even no interval | Không có Workday/WorkInterval/UC139 writer. P1 giữ WORKDAY_CONTRACT; shift attendance không tự là Workday | Không có Workday/WorkInterval entity/table trong canonical/mappings; shift_attendance bắt buộc shift_assignment_id | Employee có Working day phải thực hiện End; không suy Admin forced-end | Working chưa end; Ended kết thúc daily record nhưng không release source assignments | D05 APPROVED: phải End trước Admin Deactivate kể cả zero open interval; không auto-end. Kiểm tra source responsibilities độc lập | IN_SCOPE_MISSING_IMPLEMENTATION | Policy đã chốt, còn thiếu authoritative representation/writer/SQL tests; employee không thể End cần operational exception approval riêng |
| Unscheduled Workday | S1 UC139 alternate No Shift Assignment; S2 GBR142 yêu cầu reason + thông báo DH; không cho hai Working days | Không có unscheduled writer; Unsupported hiện có, không phải query “zero rows” | Required shift_assignment_id không biểu diễn independent unscheduled day theo yêu cầu; không invent fake shift/FK | Employee bắt đầu Working không theo roster; DH nhận thông báo, không tự là owner của daily work | Working/Ended như scheduled day; Unscheduled là đặc tính ngày, không phải terminal state | Đánh giá cùng workday/actual-duty rule; không miễn trừ vì không có roster. Không lấy thiếu row attendance làm proof không có responsibility | IN_SCOPE_MISSING_IMPLEMENTATION | Cần model/mapping và UC139 writer hoặc một V1 scope change được duyệt kèm proof external path; không phải yêu cầu còn chưa biết |
| Mandatory Handover Acknowledgment | S1 UC119 và UC140; S2 GBR151 khác GBR144; S7 đã duyệt block unfulfilled mandatory next action. Asset UC069 là subtype riêng đã có implementation | Không có request/shift sender/receiver/ack writer. Asset CreateHandover/Accept/Reject có và đã được query, không dùng thay cho request/shift | internal_request_events chỉ attribution/free text; không có typed receiver/pending/ack transfer. asset_handovers.to_user_id/status/accepted_at chỉ hỗ trợ asset | Request: sender current handler đến commit ack+transfer; pending designated receiver có action. Shift: receiver ack awareness; từng item vẫn giữ assignee cho tới assign UC tương ứng | Pending Acknowledgment còn action; Acknowledged/Rejected là terminal cho **phiên bản handover**, không tự terminal cho source work. Rejected request handover giữ sender | Không release sender bởi Submit/notification/history. Block designated mandatory receiver còn pending theo S7; D04 bind chính xác subtype/state/user và cách resolve/retarget. Request Ack chuyển handler; shift Ack không clear source duties | IN_SCOPE_MISSING_IMPLEMENTATION (request/shift); asset subtype đã có | Cần authoritative receiver/ack và atomic assignment transfer, không hỏi lại approval chung về mandatory action. Không global-block historical acknowledged/rejected records |

## 4. Các quyết định V1 có thể triển khai và các quyết định cần duyệt

### Rule có căn cứ rõ từ UC/SRS

- **R01 - ownership:** blocker theo current designated responsibility, không theo mọi
  historical FK hay toàn bộ role capability. Reassignment kết thúc assignment cũ và thiết
  lập assignment mới atomically; không có khoảng trống ownership.
- **R02 - task completion:** submit -> Pending Verification, chưa phải Closed. Return từ
  verification phục hồi Accepted cho cùng assignee; writer phải kiểm tra assignee còn eligible.
  Cần authoritative acceptance proof mới được dùng Closed để bỏ current blocker.
- **R03 - request handover:** pending/rejected giữ current handler cũ; acknowledged chỉ
  chuyển responsibility khi assignment transfer commit cùng transaction. Không tin event
  author, free-text note hoặc notification read làm proof.
- **R04 - shift handover:** acknowledgment chỉ awareness. Sender vẫn bị block bởi item
  còn assigned cho mình dù snapshot đã acknowledged; dùng UC030/053/104/115 để chuyển item.
- **R05 - history/reopen:** accepted Closed cycle và released assignment là history; khả
  năng reopen trong tương lai không biến history thành duty hiện tại. UC121 reopen kiểm tra
  last handler eligible; không tự đổi handler hay kích hoạt account. Nếu last handler
  INACTIVE thì từ chối theo active-reference/ownership rule; thay handler khi reopen cần một
  approval khác vì GBR153 chỉ định last handler. UC106 cho phép same/new assignee nhưng cả
  hai trường hợp phải eligibility-lock người sẽ nhận work.
- **R06 - pause/end:** Pause All đóng interval chứ không reassign/cancel work. Ended workday
  không chứng minh source tasks đã Closed hoặc chuyển owner.
- **R07 - future creation:** JOB05/04 và mọi import tạo occurrence phải revalidate default
  assignee sau User lock. Default assignee stored trong series/schedule không cho phép tạo
  responsibility cho INACTIVE user. Khuyến nghị skip/reject occurrence transaction và ghi
  lỗi vận hành khi assignee không eligible; fallback/unassigned behavior của JOB05 phải được
  duyệt, không tự suy ra từ JOB04 có nhánh Pending Assignment.

**IN_SCOPE_IMPLEMENTED ở cấp subset:** current Operational/IR assignee predicates; valid
ended assignment history exclusion; manager-designation positive evidence theo V1 kỹ thuật
đã giữ ở Task55B; actual open-attendance evidence. Chúng đã có SQL evidence Task55B nhưng
không chứng minh full workflow/terminal mapping. Sáu DEV2 predicates và ReturnWork protocol
giữ technical approval/evidence trước đó. Không có căn cứ mới để sửa runtime trong Task56.

### Decision register - D05 approved Task57; các đề xuất cụ thể khác chưa formal approved

| Decision | Đề xuất V1 cụ thể | Căn cứ / lý do cần quyết định | Điều kiện chuyển thành implementation |
|---|---|---|---|
| D01 - Request closure/confirmation | Dùng UC120 chi tiết: receiving DH Close sau mandatory linked work Closed, không pending handover và có result note; requester có quyền reopen nhưng không tự có pending approval sau mọi closure | BF06 mô tả confirmation bởi bên yêu cầu, UC120/121 không có một confirmation state/action riêng. Không thêm blocker requester hay confirmation model chỉ từ câu summary | Nhóm xác nhận precedence BF06/UC120; định nghĩa authoritative closure/cycle và map status SQL, rồi positive/history/reopen SQL tests |
| D02 - Manager relationship | Giữ S7: current management phải transfer trước deact. Dùng nondeleted employee employment ACTIVE và explicit manager FK làm positive subset hiện có; account INACTIVE không kết thúc employment. Chốt whitelist nào của employment/history đủ để release và bằng chứng transfer | UC006/GBR028 không nói mọi reporting FK là assigned work; UC024/025/GBR041 và S6 không đồng nhất department manager, line manager và DH role. Approval nguyên tắc S7 đã có, còn thiếu exact binding/release | Chốt HR/operation lifecycle mapping; triển khai manager-transfer writer Users-first và audit trước khi complete contract. Không yêu cầu duyệt lại nguyên tắc block current management; không tự xóa predicate hiện tại |
| D03 - Future roster | Khuyến nghị roster chưa bắt đầu không tự thành open-work blocker; cancel/replan qua UC138, không UC006. Actual work/day/interval vẫn kiểm tra riêng | UC138 cho cancel future assignment; GBR028 liệt kê open assigned work, không tự định nghĩa future schedule là duty đang thực hiện | Nhóm xác nhận staffing policy; chốt SQL Scheduled/ASSIGNED/Cancelled mapping, time boundaries và Users-first roster writer. Nếu muốn bắt buộc cancel toàn bộ future roster, thêm rõ vào contract trước khi code |
| D04 - Pending receiver binding | Giữ S7: resolve/reject/retarget **designated mandatory** pending receiver obligation trước deact; terminal handover history không block. Bind receiver và pending/terminal policy theo subtype/cycle; không dùng mọi receiver FK hoặc role capability | UC119 transfer và UC140 awareness khác nhau nhưng đều có action cho designated receiver; broad mandatory-action blocker đã được developer duyệt. Vấn đề là model/actor binding chứ không phải hỏi lại có block mandatory action hay không | Authoritative representation, approved state/cycle mapping và atomic writer. Không mở rộng asset contract bằng giả định; không cần duyệt lại ba nguyên tắc đã trả lời ở Task53 |
| D05 - Working day không có interval — APPROVED V1 POLICY Task57 | Developer yêu cầu employee End Working Workday trước Admin Deactivate, kể cả không còn open interval; Ended không chứng minh source assignments đã release | UC139 End và Task57 explicit approval chốt Working-alone boundary. Không auto-end và không invent Admin forced-end authorization | Còn thiếu Workday/interval model/writer và tests; giữ WORKDAY_CONTRACT/Unsupported, không tự tạo schema. Employee không thể End cần operational exception actor/SoD/audit được duyệt riêng |
| D06 - Scope exclusions | Không đề xuất OUT_OF_SCOPE như kết luận hiện tại. Nếu nhóm thật sự cắt UC139/119/140 khỏi V1, phải ghi versioned scope amendment và chứng minh deployment/import/job/external paths không tạo obligation đó | Các UC này có trong original Word và SRS, không thể gọi là “SRS không yêu cầu”. Checkout không có writer chưa đủ chứng minh deployment | Signed scope baseline + writer/access/schema inventory; xử lý mọi legacy representation; thay predicate/Unsupported có review và tests, không dùng runtime flag |
| D07 - Release evidence/promotion | Duyệt trình tự ở §7: pre-release protocol acceptance trước, full internal UC006 sau source promotion và trước endpoint enablement | Guard hiện chặn mutation; không thể yêu cầu full success làm bằng chứng tiên quyết để mở chính guard đó. Standalone race không được đổi nhãn thành UC006 success | Nhóm chấp thuận acceptance boundaries; ba gate vẫn pending cho tới khi đủ toàn bộ evidence và source change được review. Không clear gate trong Task56 |

## 5. Writer inventory: actual, specified, external và unknown

Inventory này bao phủ checkout hiện tại và các writer được SRS chỉ định. Không tuyên bố đã
kiểm kê được deployment ngoài repository. `UNKNOWN` cần owner cung cấp proof, không phải
“không có writer”. Actors/FK/navigation reads sau feature lock cũng thuộc lock-order review,
kể cả command chỉ đóng/release work.

| ID / ownership | Writer path | Tạo/khôi phục responsibility hoặc ảnh hưởng lock graph | Current order / evidence / việc còn thiếu |
|---|---|---|---|
| W01 DEV1 actual | UserAccountCreationService.CreateAsync; UserRoleAssignmentService.AssignAsync; UserBranchAssignmentService.AddAsync; UserProfileService.UpdateAsync | Tạo identity/access hoặc thay profile; không gán current task/request/shift/manager. Role grant không tự là work assignment | Source giữ global guard/Users universe hoặc retained Users trước authority/reference writes; profile chỉ name/email/phone. Không coi account/access writer tests là business-writer acceptance |
| W02 DEV1 actual | UserAccountActivationService.ActivateAsync | Khôi phục khả năng tác nghiệp, không tự tạo assignment | Users actor/target sorted -> authority/session/version reread -> account/audit. Task55B có actual commit/wait/stale/cancel/post-flush rollback; không certifies recurring/import writers |
| W03 DEV1 actual | AdminCredentialResetWorkflow.ResetAsync; TemporaryCredentialService; AuthenticationCredentialWorkflow | Credential/session/admin eligibility, không gán work. Có User/FK/audit access trong global lock graph | Giữ foundation hiện có; không sửa authentication/shared locks trong Task56 |
| W04 DEV1 specified, missing | Cleaning assign/reassign/rework, inspection/defect ownership; UC030/035/041/046/047 | Gán hoặc restore assignee/inspector/defect owner | Có entities/read predicates; không tìm thấy corresponding production mutation service/controller. Cần writer inventory và test khi triển khai, không simulated writer PASS |
| W05 DEV1 specified, missing | UC104 assignment, UC105 return, UC106 reopen; UC102/103 recurring-series configuration; **JOB05** generation | Task ownership, same-assignee return/reopen và default-assignee occurrence | SRS GBR124/126/127/133/134 xác định rõ. Cần discover old/new/default participants -> sorted retained Users -> Task/series locks -> reread cycle/status/version/default -> eligibility -> write. JOB05 phải được kiểm kê dù chưa có IHostedService |
| W06 DEV1 specified, missing | UC115 assign/route, UC119 submit/ack/reject, UC121 reopen, UC122 handler escalation | Handler/receiver obligations; acknowledgment transfer; restore last handler | UC115 route changes receiving department, không chuyển bằng event author. Lock sender/receiver/last/new handler trước Request/Handover; reread routing/current cycle; ack và assignment transfer atomic |
| W07 DEV1 specified, missing | UC138 roster; UC139 start/end/Pause All; UC140 shift handover | Shift/workday/current intervals và pending receiver action | Không có controller/service/hosted implementation. Users participants trước schedule/day/interval/item locks; Pause All không reassign source items. Không map attendance sang unscheduled Workday bằng giả định |
| W08 DEV1 policy missing | Manager/department transfer, employment lifecycle affecting current designations | users.manager_user_id; departments.manager_user_id; active employee/current manager policy | D02 và S6 manager design còn mở. Không tìm thấy production writer; không tạo writer mới chỉ để chạy race |
| W09 DEV2 actual | MaintenanceWorkService.AssignWorkAsync / ReturnWorkAsync -> ExecuteCorrectiveAsync (Commands/Workflow) | Gán assignee; COMPLETED -> IN_PROGRESS phục hồi current responsibility | Proposed/retained assignee User lock trước Incident -> Work, authoritative reload/drift checks. Return hotfix ở HEAD; Task55A/B scoped SQL PASS. Actor reads/detail joins sau feature vẫn cần toàn graph review |
| W10 DEV2 actual, graph coverage | MaintenanceWorkService.CreateWorkAsync/StartWorkAsync/ResumeWorkAsync/HoldWorkAsync/SubmitResultAsync/VerifyWorkAsync/CancelWorkAsync/CreateLogAsync; IncidentService.CreateIncidentAsync/TriageIncidentAsync/ChangeIncidentStatusAsync | CreateWork tạo assignee NULL; start/resume hoạt động trên existing current assignment; generic Incident reopen không đồng nghĩa gán user. Actor/FK/detail User reads có thể sau resource locks | Nhiều path Incident/Work-first rồi RequireUser hoặc actor read; không đồng nhất với ReturnWork đã sửa. Cần empty/new owner, release-only, incident/asset linkage và actor read races; không sửa DEV2 |
| W11 DEV2 actual | InventoryService.SaveWarehouse (Masters) -> Begin | Thay/khôi phục active warehouse manager | Discover old/new manager, sorted User locks trước Catalog/Warehouse, eligibility và manager drift reread. Begin/Actor khi owner preparation không có vẫn thuộc global graph; test NULL manager và activate warehouse |
| W12 DEV2 actual | AssetService.CreateHandoverAsync; AcceptHandoverAsync -> FinalizeHandoverAsync | Tạo pending receiver; accept chuyển custodian | Receiver/old+new User locks trước Asset/Handover, reread participant drift và eligibility. Tạo asset không tự gán custodian; ApplyMaster không có custodian field |
| W13 DEV2 actual, graph coverage | AssetService.CreateAssetAsync/UpdateAssetAsync/AssignLocationAsync/TransferLocationAsync/ChangeConditionAsync/RejectHandoverAsync/RetireAssetAsync | Chủ yếu master/release/location/history; rejection giữ old custody, retirement không restore. User validation/audit/FK reads ảnh hưởng lock graph | Asset-first actor/validation reads trong các path chưa được full global acceptance; không bỏ qua vì path không tạo assignee mới |
| W14 DEV2 actual | MaterialRequestService.Save, Command submit/resubmit/request-information/reapproval, Cycle/Latest | DRAFT/NEEDS_INFORMATION requester duty; latest PENDING approver duty, old revisions thành history | Actor/requester/selected/current approver Users trước Catalog/Request/cycle locks; authoritative discovery/role-branch/latest-cycle reread. Cần cả reapproval, approver selection drift và request-information phục hồi requester |
| W15 DEV2 actual, graph coverage | Other InventoryService document/stocktake/adjustment/return writers; MaterialRequestService.Issue; ReplenishmentService writes and Inventory posting integration | Không tự gán bảy DEV1 workflow trên, nhưng Catalog/resource locks, Actor/User/FK/detail reads có thể tạo cycle với global Users snapshot | Inventory Begin thường Catalog -> Actor khi không prepare owners; cần caller/callee complete transaction graph, không chỉ predicate-producing methods |
| W16 DEV2 specified, implementation incomplete | **JOB04** preventive schedule/occurrence generation và default-assignee configuration (S4 §16.4) | Tạo future maintenance occurrence từ schedule/default assignee | Schema có preventive schedule/run; không thấy registered generator. Cần Users-first generation, eligibility, duplicate occurrence protection; không dùng Task/Workday absence để loại job |
| W17 External/import/job known code artifacts | Canonical/release seeds/patches; verification harness fixture inserts; reviewed deployment/rehearsal SQL dưới scripts | Có thể trực tiếp INSERT/UPDATE Users/assignments/manager/custody data; các harness là test setup, không phải production service | Phân biệt offline guarded provisioning với online writer. Không thực thi trong Task56. Nếu dùng online/import thì phải tham gia protocol hoặc bị loại khỏi deployed write surface bằng proof |
| W18 UNKNOWN deployment | SQL Agent jobs, external ETL/HR import, admin direct SQL, triggers/procedures ngoài canonical source, deployed services khác | Có thể gán/restore bất kỳ ownership field hoặc alternative Workday/ack representation | Chưa có authoritative deployed inventory/DB access model. Source không có hosted registration và canonical source không có generator/procedure tương ứng không chứng minh production không có. Cần owner/DBA inventory, không yêu cầu secrets |

Các public wrappers và shared private executors của cùng row là một họ writer nhưng từng
operation vẫn phải có case evidence; không suy closure của cả family từ một method PASS.
Canonical order cho participant writer: **discover ngoài lock chỉ để chọn participant ->
transaction -> Users distinct ascending -> feature locks theo thứ tự cố định -> authoritative
reread/version/participant/cycle -> eligibility/business guard -> write/audit -> commit**.
Giữ nguyên global-admin applock/Users-universe-first foundation trước row/feature locks ở các
workflow có dùng nó. Không nâng row locks lên Users universe sau feature locks.

## 6. Test specification có outcome cụ thể

Đây là acceptance cases cần chạy khi implementation đủ, không phải test PASS mới của Task56.

| Case | Fixture / competing action | Required outcome |
|---|---|---|
| C01 Current vs history | Current ACTIVE assignment so với valid ended prior assignment, creator/requester-only FK, Closed cycle | Current owner bị block; history không tự block. Unknown/contradictory mapping vẫn unresolved |
| C02 Task submit/verify/return | Assignee submit; DH Accept hoặc Return | Submit chưa release; Accept + authoritative Closed release đúng cycle; Return cùng assignee phải eligible và tạo current duty atomically |
| C03 Request handover | Sender A, receiver B; Submit/Reject/Acknowledge, có source assignment | Pending/Reject giữ A; Ack chuyển đúng một owner sang B trong cùng tx. Không release A bởi notification; B INACTIVE thì transfer từ chối |
| C04 Shift acknowledgment | Snapshot chứa item vẫn assigned A; B Ack snapshot | A vẫn bị block bởi item; chỉ actual per-item reassignment giải phóng A. Terminal snapshot không terminal source work |
| C05 Reopen after historical closure | Last handler/assignee đã INACTIVE; actual UC121/UC106 reopen | Không tái tạo work cho INACTIVE. UC121 không âm thầm chọn handler mới; UC106 chỉ chọn new assignee theo approved request và eligibility |
| C06 Roster/workday | Future schedule, past scheduled end, actual open interval, Working không interval, Ended nhưng task Paused | D05 đã approved: Working block cả zero interval; Ended vẫn kiểm tra Paused/source item. D03 roster còn chờ duyệt; không suy release từ clock, không auto-end/forced-end/reassign trong UC006 |
| C07 Manager transfer | Active employee chỉ định target; inactive login; deleted/historical reference; unknown employment | Apply D02 đúng policy đã duyệt; inactive login không tương đương terminated employment; unmapped states không clear |
| C08 Recurring/default jobs | JOB05/04 discover default assignee, User bị deactivate hoặc default đổi trước feature lock | Authoritative reread phát hiện drift; INACTIVE không nhận occurrence. No duplicate khi rerun; reject/skip transaction không partial assignment/audit |
| C09 Protocol writer-first | Actual writer giữ retained User lock đến commit; contender review/acquire Users | DMV waiter/owner + lock type + timestamp; contender sau commit thấy current blocker/owner. Dùng real writer, không fabricated evaluator |
| C10 Protocol inactive-first | Owned test transaction giữ User lock rồi commit INACTIVE; actual writer có stale tracked User | Writer đợi trước feature, SQL reread từ chối; source work/history unchanged. Ghi nhãn standalone protocol, không phải successful Deactivate |
| C11 Faults/drift | Cancel/timeout/deadlock, stale participant/linkage/status/cycle; audit/commit fault sau actual SQL flush | Whole tx rollback; release locks/owned connection; explicit fresh retry rereads everything; no automatic stale retry or duplicate audit |
| C12 Full internal UC006 | Sau approved business contracts và release promotion: clean target, valid actor, eligible admin còn lại | Success thật: INACTIVE + changed rowversion + all target sessions revoked + one atomic correct audit; credentials/roles/memberships/assignments unchanged; LastAdmin/audit faults rollback |

## 7. Ba release gates: criteria đóng và bằng chứng

Gate là acceptance của implementation, không phải duty do target sở hữu. Không được close
vì build, empty query, predicates PASS hoặc không thấy controller. Các criteria sau là đề xuất
boundary cần D07 duyệt; **không thay đổi hoặc clear pending list hiện tại**.

| Gate | Exact pre-release acceptance criteria | Evidence phải lưu | Hiện có / còn thiếu |
|---|---|---|---|
| DEV1_WRITER_COORDINATION | W01-08 và JOB05 có inventory theo deployed scope; mỗi actual create/reassign/return/reopen/ack/default generation path không thể tạo duty cho user ineligible; participant locks và reread đúng protocol; external paths của DEV1 được coordinate hoặc chứng minh không deployed | Source revision + method/operation list, actor/participant set, lock trace, actual SQL C09/C10/C11 cho từng operation, rollback snapshots; explicit inventory owner cho import/jobs | Task55B account activation races là evidence W02, không phải W04-08/JOB05. Thiếu implementations/business decisions và deployed external inventory; PENDING |
| DEV2_INTEGRATED_SQL_ACCEPTANCE | W09-16 complete: real writer cùng helper/schema/transaction graph; runtime evaluator nhận đúng current/history/latest cycle sau commit; actor scope/SoD không bị fake trong integration fixture; all participant/state/approval drift và reactivation paths fail closed; actual Deactivate denial/lifetime integration được kiểm chứng trong build còn fuse | Per-family real SQL C01/C05/C08-C11, SPIDs/waits, atomic state/audit/session-target preservation; distinguish standalone protocol vs actual service-denial tests. No mocked clear evaluator | ReturnWork Task55A/B scoped PASS và 29 classifier cases là một phần evidence; warehouse/asset/MR/other operations chưa full integrated acceptance. PENDING |
| GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE | Complete cross-module graph gồm Users, global admin, catalog, resource, FK/navigation/actor/detail reads. Không có feature-first User dependency tạo inverse cycle với existing Users TABLOCKX; compatible protocol được thiết kế/duyệt cho từng entry point kể cả release-only/no-owner path | Source/plan/isolation assumptions, complete transaction ownership graph, both-order actual SQL barriers/DMVs cho W09-15 + auth/account/catalog participants; bounded cancel/timeout và injected deadlock cleanup. No unexpected deadlock trong canonical order | ReturnWork User->Incident->Work và account activation scoped proofs không cover toàn graph; actor reads trong Incident/Maintenance/Assets/Inventory còn cần review/fix bởi owner. PENDING |

### Tránh vòng lặp release acceptance

1. Chốt D01-06 và representation/writer implementation; chạy protocol cases bằng **actual
   writers** và retained User transactions. Actual internal Deactivate trong build chưa release
   chỉ được dùng để chứng minh denial/authority/retained-lock lifetime, không success mutation.
2. Hoàn tất và review ba bộ pre-release evidence theo D07. Standalone C10 vẫn mang nhãn
   standalone; không “nâng” nó thành C12. Gate owner xác nhận evidence đủ và có reviewed
   **source promotion** ghi nhận acceptance; không dùng test switch hoặc disable predicate.
3. Sau promotion hợp lệ và runtime contracts đều clear mới chạy C12 qua actual internal
   service. Guard lúc đó đã có evidence PASS; đây không phải bypass để lấy bằng chứng mở guard.
   Endpoint vẫn unregistered. Nếu C12 fail, không công bố acceptance; sửa/review lại failure
   và acceptance assumptions, không mở HTTP.
4. Full UC006 success/audit/LastAdmin/session acceptance là milestone sau promotion, trước
   endpoint enablement của một task khác. Không yêu cầu C12 thành tiên quyết của chính fuse
   đang cấm C12; cũng không gọi pre-release protocol PASS là full UC006 PASS.

## 8. Kết luận thực thi Task56

- Business requirements Workday/unscheduled/ack/verification/recurring jobs đã được tìm thấy,
  đọc và đối chiếu Word; không tiếp tục mô tả tất cả là “SRS thiếu mapping”. Phần lớn là
  **IN_SCOPE_MISSING_IMPLEMENTATION**, riêng line-manager UC006 invariant là decision rõ ràng.
- Historical Task56: khi đó D01-07 còn được đề xuất. Task57 đã duyệt D05; hiện còn D01–D04/D06–D07 cần formal decision với exit criteria ở trên,
  không phải yêu cầu một mapping chung chung. Không có OUT_OF_SCOPE được tự áp dụng.
- Chưa có source-of-truth mới phê duyệt chính xác SQL lifecycle/role/terminal mapping, nên
  Task56 không sửa runtime hoặc thêm safety gate. Existing 5 runtime Unresolved, 2 Unsupported
  và 3 pending release gates giữ nguyên. Không truy cập SQL/HotelOpsDB.
- Evidence cũ đọc lại: Task55B contracts64, service53, ReturnWork57, state/readiness62; đều
  là kết quả **Task55B**, không phải SQL/build/test mới của Task56. Full UC006 vẫn BLOCKED.
- Verdict: **PARTIAL**. Contract proposal có rule và acceptance criteria có thể giao việc;
  chưa có đủ approval/implementation/deployment evidence để tuyên bố CONTRACT READY.
