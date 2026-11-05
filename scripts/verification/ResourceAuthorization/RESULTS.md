# Task05 — Resource-specific Authorization results

**PASS WITH NOTES** cho adapter đại diện và phạm vi IN_PROGRESS đã triển khai. Không chứng nhận toàn bộ Housekeeping workflow hoặc resource authorization cho mọi module.

## A. Context Verification

Đã kiểm tra source Authorization Task01–04, DI/pipeline, các báo cáo verification, SQL Release catalog và các entity CleaningTask/CleaningAssignment/Room, OperationalTask/OperationalTaskAssignment, MaintenanceWork, Incident, MaterialRequest, StockDocument. Schema SQL thực tế được SELECT cho cleaning_tasks/cleaning_assignments, các CHECK constraints và resource → room → branch/assignment rows.

Khảo sát lựa chọn:

| Resource | Bằng chứng đã khảo sát | Phạm vi Task05 |
| --- | --- | --- |
| CleaningTask | RoomId → Room.BranchId; CleaningAssignments assignee/status/assigned_at/ended_at; UC-030/031/032 rõ current-assignee rule | Chọn một adapter CLEANING_EXECUTE cho IN_PROGRESS |
| OperationalTask | BranchId trực tiếp; OperationalTaskAssignments | Không triển khai; cần rule theo category/action/workflow riêng |
| MaintenanceWork | AssigneeId; IncidentId/DepartmentId nullable, không có direct BranchId trong entity | Không tự chọn nguồn branch cho mọi trường hợp; không triển khai |
| Incident | BranchId, ReportedBy, liên kết MaintenanceWorks | Không tự coi reporter là owner của mọi action |
| MaterialRequest | BranchId, RequesterId, các approval/fulfillment relationships | Không tự coi requester là người được quyền approve/fulfill |
| StockDocument | Source/DestinationWarehouse nullable, CreatedBy/ApprovedBy/PostedBy | Không tự chọn branch/owner cho các loại chứng từ khác nhau |

Nguồn nghiệp vụ: SRS draft UC-030 (chỉ assignee mới được làm sau reassign), UC-031 (current assignee), UC-032 (task In Progress và assigned to User), GBR-047/049, FE-04 Cleaning Task Detail/Execution. Revised role/matrix pending notes không được dùng để tạo quyền mới; adapter dùng đúng CLEANING_EXECUTE trong SQL Release hiện tại. Không thay role definitions/Permission Matrix.

## B. Resource Mapping và giới hạn bằng chứng

- Type `cleaning_task`, ID `cleaning_task_id`, permission `CLEANING_EXECUTE`.
- Branch authoritative: cleaning_tasks.room_id → rooms.branch_id; không dùng branch từ request hoặc primary branch của user.
- OWN: đúng một current assignment có assignment_status ACTIVE, ended_at NULL, assigned_at <= UTC now, và assignee_user_id đúng current user. Role/grant/branch-access vẫn được PermissionService kiểm tra riêng.
- Room phải Active; task phải IN_PROGRESS. Không xác minh ownership bằng created_by, cùng department/branch hoặc owner ID client.
- Cleaning assignments không có expires_at; ended_at/status thể hiện quan hệ đã đóng/thu hồi/reassign. Không phát minh expiry column. Future assigned_at không được coi là current.
- Nếu nhiều current assignments, fail-closed thay vì tự chọn latest/user phù hợp.

SQL CHECK: task chỉ có PENDING/IN_PROGRESS/COMPLETED/CANCELLED; assignment có ACTIVE/COMPLETED/REASSIGNED/CANCELLED. SRS có Assigned/On Hold/Pending Inspection khác schema. Không ánh xạ PENDING thành Assigned. Vì vậy **Start/Resume, PENDING và lịch sử COMPLETED không được adapter này cấp quyền**; cần chốt workflow/status mapping trước khi mở rộng. Chỉ quan hệ current-assignee cho execution IN_PROGRESS đủ căn cứ để triển khai ở đây.

Workday/work interval, blocker/checklist và state transitions không được xác nhận bởi kết quả Granted. Đây vẫn là scope authorization; future command workflow phải kiểm tra business preconditions và revalidate assignment trong transaction để tránh race với reassign.

## C. Implementation

Files source mới:

- `Security/Authorization/Resources/CleaningTaskAccessReader.cs`: read model, reader contract và EF projection authoritative, AsNoTracking/async/CancellationToken.
- `Security/Authorization/Resources/CleaningTaskResourceResolver.cs`: ràng buộc permission/type, trạng thái hỗ trợ, current-assignee/ambiguity checks; trả verified facts hoặc null, không tự Granted.

Program.cs thêm scoped reader và thay resolver registration từ UnresolvedPermissionResourceResolver sang CleaningTaskResourceResolver. Không sửa PermissionService, CurrentUserAccessor, handler/provider, Authentication hoặc EF-generated mappings/entities. Unsupported permissions/resource types tiếp tục null/fail-closed.

Thêm console project/Program.cs/README.md/RESULTS.md và preexisting-file-hashes.json trong thư mục này. Không thêm endpoint kiểm thử vào app chính.

## D. Security

Đường tích hợp được kiểm tra: Authorization Handler → PermissionService → CleaningTaskResourceResolver → reader. Branch/ownership từ backend; route chỉ là resource identifier. Cross-branch/role-path isolation vẫn do core kiểm tra. Current-assignee restriction của CLEANING_EXECUTE không bị ALL giả lập bỏ qua. Unknown/absent/incompatible resource đều không tiết lộ chi tiết qua response (403 generic trong test HTTP authenticated).

Không có cache facts giữa lần evaluate/request. Test doubles thay assignment từ ACTIVE sang CANCELLED/REASSIGNED và lần tiếp theo bị deny. SQL thật không bị thay để thử tình huống này. Reader exceptions không bị nuốt thành facts: PermissionService trả Unavailable và handler forbid. Restricted/inactive user/role/missing resolver/context đều bị từ chối. Multiple requirements vẫn được kiểm tra đầy đủ.

## E. Testing thực tế

| Kiểm tra | Kết quả |
| --- | --- |
| dotnet build HotelOps.slnx | PASS, 0 warnings/errors |
| dotnet test HotelOps.slnx --no-restore | Exit0; solution chưa có framework test project |
| ResourceAuthorization --live | **59/59 PASS** |
| Adapter/core với doubles | 33 assertions |
| HTTP loopback integration | 16 assertions, production adapter/core/handler/cookie middleware, persistence doubles |
| Live SQL read-only | 10 assertions, gồm EF reader + adapter + PermissionService với grants thật |
| Authorization regression (không --live) | **61/61 PASS**; không gọi đây là 65 checks |
| PermissionService regression --live | **50/50 PASS** |
| CurrentUserAccessor regression | **14/14 PASS** |
| DataScope regression sau chạy lại riêng | **15/15 PASS** |
| git diff --check | PASS |

SQL bằng chứng thực tế: task2 là IN_PROGRESS, room thuộc Branch1; user6 có assignment ACTIVE/chưa kết thúc; assignment user7 là REASSIGNED và đã ended. EF adapter xác nhận user6, từ chối user7. PermissionService kết hợp grant thật cấp scope cho user6 và từ chối user7 dù user7 có grant của HK_STAFF. Query missing resource/mismatched permission fail-closed; không có entity được tracking.

HTTP kiểm tra anonymous401; assigned200; former assignee403; absent resource403; resource ngoài role branch403; giả branch/owner không thay đổi facts; multiple-role mixing; restricted/inactive user/role; ALL không bỏ current-assignee rule; missing context; requirement thứ hai; reader timeout và assignment đổi giữa hai requests.

Live core dùng CurrentUserAccessor double để cô lập EF/resource integration, không phải cookie SQL session của user mẫu. HTTP host dùng synthetic protected tickets/ephemeral keys/session data doubles và reader double; production resolver/core/handler/middleware thật. Không chứng nhận HTTP với SQL sessions thật, HTTPS/browser behavior hoặc toàn bộ workflow.

DataScope lần đầu timeout 60 giây sau 13 assertions, tại full entity materialization khi các regression đang chạy đồng thời. Không sửa test/timeout/schema để ép pass; chạy lại riêng cùng lệnh đã PASS đủ15. Lỗi timeout ban đầu được ghi nhận, không tính là kết quả pass của lần đó.

Các console projects còn NU1900 do không tải được vulnerability metadata của api.nuget.org; không tắt Auditing. Solution build không có warnings/errors. Test lỗi database/revocation dùng doubles, không gây lỗi server hoặc UPDATE vào DB chính.

## F. Remaining Work

- Chốt trạng thái cleaning workflow, nhất là PENDING/Assigned/On Hold/Pending Inspection; adapter hiện chỉ IN_PROGRESS.
- Business command checks: workday/interval/checklist/blocker/SoD và atomic assignment revalidation.
- CLEANING_VIEW/history, assignment-management, rework/inspection và các resource adapters khác cần permission/action-specific evidence riêng.
- Rollout policies/endpoint feature loaders theo từng API và kiểm thử HTTP với SQL sessions/EF persistence thật.
- Legacy Department Retirement ngoài phạm vi, chưa thực hiện.

## G. Git Status

Giữ baseline hash của **237 files có sẵn**. Rà soát cho thấy chỉ Program.cs thay đổi trong baseline để wiring reader/resolver; các source/report cũ còn nguyên. Diff DbContext/RolePermission và các DI calls trước đó vẫn thuộc Task01–04, không ghi đè. Wav/untracked ngoài phạm vi được giữ nguyên.

Chỉ thêm các files Task05 và sửa wiring Program.cs. Không ghi SQL business data, migration, seed, scaffold, schema/catalog/config change, commit/push/merge/PR.

## H. Final Verdict

**PASS WITH NOTES**: đã triển khai và kiểm chứng resource adapter thật cho cặp CLEANING_EXECUTE/cleaning_task ở IN_PROGRESS, có đường tích hợp handler/core/adapter và SQL read-only. Không kết luận PASS toàn bộ Authorization hoặc workflow; các giới hạn trạng thái và integration nêu trên phải được xử lý trước rollout rộng.
