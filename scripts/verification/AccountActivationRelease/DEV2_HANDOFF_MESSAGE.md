# Đề nghị DEV2 review contract chặn Account Deactivation

Chào DEV2,

BE1 đã hoàn thiện phần **Activate Account** của UC-006. **Deactivate vẫn BLOCKED và chưa có endpoint thực thi.** Đề nghị DEV2 cùng xác nhận các trách nhiệm đang mở và protocol transaction/locking trước khi BE1 triển khai deactivate. Tài liệu này là đề nghị review, chưa ghi nhận DEV2 chấp thuận và không yêu cầu tự hủy/xóa công việc để gỡ blocker.

Inventory trên isolated canonical trial ghi nhận71 FK về users,53 bảng và45 CHECK constraints. Chi tiết: [Task35 inventory](../UserAccountStatus/BUSINESS_RULES.md), [Task36 handoff](../UserAccountActivation/DEV2_DEACTIVATION_HANDOFF.md). FK người tạo/người ghi lịch sử không mặc nhiên là công việc đang mở; các logical source links và jobs/import/SQL writers cũng cần rà soát riêng.

## Module/writer cần DEV2 xác nhận

| Module, bảng/cột chính xác | Writer/workflow hiện có | Predicate/quyết định còn thiếu |
|---|---|---|
| Maintenance: maintenance_works.assignee_id, incident_id -> incidents | MaintenanceWorkService.Commands.AssignWorkAsync; Workflow Start/SubmitResult/Return/Verify; CreateWorkAsync | PENDING/IN_PROGRESS/ON_HOLD/COMPLETED/CLOSED/CANCELLED là giá trị đã quan sát, không phải danh sách open/closed được phê duyệt. COMPLETED còn chờ review không? Ai giữ nghĩa vụ review/rework? Assign RequireUserAsync hiện chỉ kiểm tra tồn tại. |
| Material Requests: material_request_approval_cycles.assigned_approver_id/request_id -> material_requests; material_requests.requester_id | MaterialRequestService.Commands.Save, Command submit/resubmit/request-information/approve/reject/cancel/close-remainder; Cycle/Approver/Participant | Xác định current revision/cycle, PENDING/NEEDS_INFORMATION và parent status nào còn nghĩa vụ; APPROVED có còn pending issue/receipt/remainder không? Participant kiểm tra ACTIVE nhưng chưa giữ user eligibility lock đến commit. Catalog/request applock không thay thế khóa user. |
| Asset: asset_handovers.to_user_id/from_user_id; assets.custodian_user_id | AssetService.Handover.CreateHandoverAsync, AcceptHandoverAsync/RejectHandoverAsync -> FinalizeHandoverAsync | Source dùng PENDING/ACCEPTED/REJECTED. Cần xác nhận nghĩa vụ người giao/nhận pending và custody sau accept; thời điểm được coi là bàn giao hoàn tất. Target validation hiện theo convention kiểm tra tồn tại. |
| Warehouse manager: warehouses.manager_user_id | InventoryService.Masters.SaveWarehouse | Active warehouse manager có bắt buộc chuyển giao trước deactivate không? AnyAsync ACTIVE/nondeleted hiện không giữ khóa manager qua commit. |
| Inventory: stock_documents.created_by/approved_by/posted_by; stocktakes.counted_by/approved_by; stocktake_lines.counted_by; stock_transfer_receipts.received_by; material_fulfillments.fulfilled_by/received_by | InventoryService.Documents/Stocktakes/ReceiptIntegration; MaterialRequestService.Issue | Phân biệt historical actor với pending approval/count/receipt responsibility. Xác nhận exact lifecycle predicates và workflow đổi người phụ trách; chưa có user-open-work adapter đã duyệt. |
| Replenishment: material_replenishments.requested_by/closed_by; material_replenishment_receipt_lines.linked_by | ReplenishmentService.Create, Close, Receive/receipt-link/post | Xác nhận REQUESTED/PARTIALLY_RECEIVED và các trạng thái hiện có theo UC; requester nào còn trách nhiệm, closed_by/linked_by nào chỉ là lịch sử. Không áp dụng ngược warehouse-deactivation rule thành user policy. |

Nhờ kiểm tra cả entry point controller, service nội bộ, reopen/resubmit/reassignment, scheduled jobs, import và direct SQL. Các dòng trên là inventory source hiện có, chưa phải chứng nhận bao phủ mọi writer hoặc phê duyệt predicate. Không đề xuất thêm trạng thái công việc mới.

## Protocol cần hai bên thống nhất trước khi sửa source

1. Discover affected user IDs; giữ UPDLOCK,HOLDLOCK trên các user liên quan theo thứ tự numeric, rồi khóa resource/work và re-read version/ownership. Phải review thứ tự này với catalog/request/warehouse/asset/incident locks hiện tại; không chèn user lock vào một thứ tự ngược có sẵn rồi tuyên bố an toàn.
2. Mọi writer tạo/chuyển/mở lại trách nhiệm phải xác minh assignee/approver/receiver/manager ACTIVE, nondeleted dưới khóa đó và giữ đến commit. Kiểm tra ACTIVE bằng ordinary read rồi INSERT sau đó, hoặc chỉ dựa vào FK, là chưa đủ.
3. Deactivate giữ target user lock, kiểm tra đúng các current obligations cùng row/key ranges theo predicate đã duyệt, sau đó status/rowversion + revoke sessions + audit trong một transaction. Không tự đổi role, credential, scope hay hủy assignment. Writer đến sau phải đọc được INACTIVE và từ chối nhận trách nhiệm mới.
4. BE1 chịu trách nhiệm coverage cleaning/rework/operational/internal request, inspection/shift/department duties và invariant last-admin. Minimum policy đã duyệt là còn principal ACTIVE/nondeleted, login-eligible với cả USER_MANAGE/ALL và ROLE_MANAGE/ALL hiệu lực, không branch-bound; không count theo tên SYS_ADMIN/OWNER. Mọi capability-removal writer phải tham gia serialization phù hợp.

## Race tests liên module cần chạy trên disposable DB

- Assignment/approval/handover/manager writer giữ eligibility lock trước: deactivate đợi, sau commit phải thấy obligation và từ chối.
- Deactivate giữ target lock trước: writer đợi, sau deactivate commit phải thấy INACTIVE và từ chối; không có obligation mới lọt vào sau check.
- Reassign/reopen/resubmit/current-cycle switch trong lúc deactivate; kiểm tra parent status, EndedAt/current assignment và handover/custody không bị đánh giá từ snapshot cũ.
- Deactivate rollback do audit/session failure: status, rowversion, session revocation và audit rollback toàn bộ; writer tiếp tục theo state thực tế, không dùng partial outcome.
- Concurrent đổi manager/receiver/approver, scope/role/grant revocation và hai admin vô hiệu hóa nhau; invariant last-admin không chỉ là một COUNT rồi UPDATE.
- Chứng minh SQL blocking_session_id/LCK_M waits bằng nhiều connection, test cả hai thứ tự, stale token và deadlock/cancellation rollback. Mock/delay đơn thuần không đủ bằng chứng.

Nhờ DEV2 trả lại: (a) exact table/FK/current-revision/open-work predicates và nguồn UC được duyệt; (b) tất cả writer được bao phủ; (c) lock order/protocol đã review và phần source DEV2 cần thay đổi; (d) bằng chứng isolated integration/race/rollback; (e) quyết định nghiệp vụ còn cần PO/security owner. Các câu hỏi về review, custody, pending receipt/approval và nghĩa vụ manager hiện chưa đủ căn cứ để BE1 tự quyết định.

BE1 không sửa DEV2, không thay HotelOpsDB/RBAC, không coi review này là approval chạy production migration. Chỉ mở Deactivate khi cả contract nghiệp vụ, writer coordination, last-admin và verification đều được chứng minh. Xin review và phản hồi các mục còn thiếu trước khi hai bên tích hợp.
