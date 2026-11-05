# Task76 — Focused DEV1/DEV2 handoff

Local review document; chưa gửi ra ngoài, chưa sửa DEV2 source hoặc shared contract. Bổ sung [Task75 handoff](../CleaningMonitor/DEV2_HANDOFF.md), không phải adapter đã được duyệt.

## Ownership và phạm vi

DEV1 giữ cleaning/inspection/rework producers, source authorization và business/audit transitions. DEV2 giữ FE14 notification storage/delivery/retry và Incident/Maintenance/Asset writers. Shared lifecycle/schema/DI chỉ đổi qua patch được duyệt riêng.

UC029/041/046/037 giữ PARTIAL; UC044/045 giữ NOT_IMPLEMENTED theo ledger. Không reimplement assignment core, không dùng audit/enqueue thay cho delivery.

## Contract cần DEV2 trả lại để review

| Hạng mục | Quyết định/evidence cần có |
|---|---|
| Adapter | Actual API/type + ownership; input committed operation key, source type/id/revision, actor, branch, authoritative recipient, occurred UTC và safe action reference. DEV1 không tự đặt một interface cạnh tranh trong production. |
| Atomic publication | Durable retry intent cùng business/audit transaction hoặc một protocol reliability tương đương được review. Delivery failure không hủy business result đã commit (GBR010/CF08); intent persistence failure không được để success giả với mất event. |
| Delivery | Durable in-app record cho đúng recipient, đọc được qua authorized API, action/source reauthorized. Notification không cấp thêm quyền. Queue acceptance không tính delivered. |
| Idempotency | Uniqueness theo committed operation/source revision/recipient; replay API/producer/consumer không tạo duplicate. NotificationId-only unique không ngăn producer tạo hai notification cho cùng event. Phân biệt một business retry với một mutation mới. |
| Retry | SRS JOB03 có chu kỳ5 phút; xác nhận execution/restart, retryable/permanent failure, retention/exhaustion/recovery và ownership. Không tự thêm enum/status hoặc retry policy khác vào schema. |
| Failure | Rolled-back mutation không có orphan deliverable event; delivery outage giữ business result + durable retry intent. Test SQL fault, process restart, duplicate delivery và concurrent producers trên disposable trial. |
| Recipient eligibility | Chốt handling khi người nhận deactivate/đổi branch/mất source permission; không chuyển người nhận tùy ý hoặc nới authorization để test PASS. |

## Cleaning cancellation — UC037

SRS §4.10 alternative Cancel chỉ cho trạng thái đủ điều kiện trước khi bắt đầu; xác nhận reason và authorization/branch ở server, cùng concurrency token/revision. Giữ current assignee làm authoritative recipient của cancellation event nếu có; event key phải gắn đúng committed cancellation, retry không nhân bản event/audit.

Chốt rõ unassigned cancellation có recipient hay không và handling khi assignee mất eligibility. Không tự phát minh recipient dự phòng. Cần source permission recheck trên action; stale “Start” action từ assignment notification không được khởi động task đã cancel.

Cancel phải atomically cập nhật task/assignment state cần thiết, room status/history, audit và publication intent. Audit/intent write fault phải rollback toàn bộ mutation; delivery fault sau commit phải được retry. Cần real cancel-vs-start/reassign/block race acceptance. Không thêm Cancel endpoint chỉ để tăng progress khi các điều kiện này chưa có.

## Room lifecycle / effective blocks

Chốt stored status so với projected SRS status, current cleaning/inspection/rework cycle, effective-block interval boundaries và precedence Blocked > Ready/Cleaning. Existing CHECK rooms không chứa INACTIVE; không tự thay enum hay map Inactive thành OUT_OF_SERVICE.

Review retained read/lock order giữa DEV1 transitions và DEV2 Incident/Maintenance/RoomBlock/Asset writers; no deadlock/lost update/stale placement acceptance. Bằng chứng source cụ thể: IncidentService.ValidateCreateAsync chỉ kiểm room/branch, chưa IsActive; AssetService.MoveAsync kiểm IsActive nhưng đọc bằng AsNoTracking trước ghi dưới transaction mặc định, chưa chứng minh retained room lock đến commit. Source review này không phải race test đã chạy.

UC022 không thể bảo đảm “inactive room cannot receive new work” từ riêng DEV1 endpoint nếu Incident writer vẫn nhận inactive room. DEV2 chuẩn bị patch writer của mình; DEV1 chuẩn bị lifecycle consumer patch riêng sau khi contract được duyệt.

## Exit criteria

Actual adapter và contract được owner review; exact shared diff được duyệt; disposable-SQL/real-HTTPS evidence cho atomic publication, failure/retry/restart/idempotency và cross-branch/source authorization; lifecycle race proof. Chỉ sau đó cân nhắc completion batch UC029/041/046/037. Chưa có exit criterion nào được coi PASS chỉ từ tài liệu này.
