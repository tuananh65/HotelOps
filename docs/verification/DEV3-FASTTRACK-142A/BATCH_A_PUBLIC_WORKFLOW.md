# Batch A public workflow — C70

SRS UC102/103/104 và GBR014/123/125/126/128 được áp dụng cho các luồng dưới đây. Đây là nghiệm thu các đường đi đã chạy, chưa phải nghiệm thu toàn bộ UC102/103.

| Đường đi | Bằng chứng trực tiếp | Trạng thái |
|---|---|---|
| Public Create: department/category/location, outcome, criteria, priority/plan | Runtime kiểm tra required/duplicate criteria, foreign scope, invalid reference, specialized/unclassified/inactive category, CSRF, concurrent creation key và audit rollback; browser tạo task thật | VERIFIED ad-hoc |
| Save and assign → executor lifecycle → independent Closed | Browser12 stages gồm cả Cancel; creator bị chặn tự verify; SQL giữ criterion PASS, first start, hai closed intervals và effort snapshot | VERIFIED path |
| Plan/deadline history | Assigned change yêu cầu reason; stale/concurrent update; rollback; old deadline giữ lại; executor đọc nhưng không sửa; browser reload giữ task URL và history | VERIFIED path |
| Cancel Pending Assignment/Accepted | Browser confirmation+reason; API stale/concurrent/audit rollback; ended assignment; đã có historical work thì từ chối; source access của old assignee bị thu hồi | VERIFIED path |
| Notification Plan/Cancel | Transactional audit intent; recipient reauthorization; duplicate deliveries converge; real hosted worker recovery và immutable source fingerprint | VERIFIED path |
| Recurring series/JOB05/Stop Series | Chưa có source/acceptance trong C70 | REMAINING |
| Linked Internal Request creation | UC118 public resource authorization chưa có; input non-null trả503, không đoán quyền | GATED |

Schema bổ sung hẹp:optional operational_task_definitions (outcome/SLA/idempotency) và task_category_processes (classification rõ ràng, thiếu mapping thì deny). Không đổi generated EF entity; không tạo lifecycle thứ hai. Criteria chỉ được khởi tạo cùng CREATE transaction rồi sealed bằng audit; Cancel dùng workflow version hiện có. Scripts chỉ chấp nhận đúng database disposable/instance/GUID/run; source restore và whole-clone cleanup đã chạy. Chưa triển khai trên shared/production.

API:GET creation-options,POST operational-tasks,GET/PUT {id}/plan,POST {id}/cancel. Mutations giữ session/role/permission, branch/department, CSRF; Plan/Cancel thêm strong ETag. UI dùng cùng request key khi kiểm tra lại kết quả Create chưa xác nhận; không tự retry mutation.

Reproduction dùng cùng ModelProbe --sprint-task-decline với clone có TaskPlanning capability; harness tự nối Entry/Decline/Assignment/Planning. Synthetic users/catalog/workday là fixture; task của workflow mới được tạo qua public browser/API. Không chạy installer hay harness trên database dùng chung.
