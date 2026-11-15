# UC103 — Set Task Priority and Deadline

COMPLETE_VERIFIED tại C71 trên disposable SQL và trusted HTTPS. Phạm vi là UC103 theo UC DETAIL/SRS; không suy ra UC102 hoặc toàn FE11 đã complete.

| Acceptance | Bằng chứng |
|---|---|
| Department Head, current branch/department và permission | Manager gate dùng role mapping hiện có cùng TASK_MANAGE/TASK_VIEW; public API executor/anonymous/foreign branch bị từ chối. Không hard-code role IDs. |
| Priority/start/deadline/duration và history | C70 planning assertions/browser được chạy lại trong C71; previous deadline được giữ, assignee đọc được plan, assigned change thiếu reason bị từ chối. |
| Cancel trước khi có work | Public confirmed Cancel và SQL assertions chạy lại; Accepted cancel thành công, task từng ghi work bị chặn, current assignment được kết thúc atomically. Cancelled plan không sửa được. |
| Stop Series ngay từ task thuộc series | C71 API trả series identity cho manager, không mở series authority cho executor; browser mở generated task plan rồi Stop; API từ chối task không thuộc series. |
| Chỉ chặn tương lai, không đổi task cũ | Fingerprint tasks/criteria/versions/assignments/intervals/task audit trước-sau Stop không đổi; simulated future JOB05 tạo0; final SQL giữ 9 Assigned và 1 Cancelled sau Stop. |
| Terminal task context | Cancelled occurrence không được dùng để Stop Series; UI ẩn series action khi canManage=false (Closed/Cancelled); series vẫn Active sau rejection. |
| CSRF, concurrency, rollback | Missing CSRF/ETag, stale và concurrent Stop; Stop/JOB05 race; fault-injected audit rollback; create/idempotency và generation rollback; positive calendar/end-date tests. |
| Audit + notification | Stop audit và selected-task current assignee intent cùng transaction; delivery và source opening kiểm tra lại quyền; concurrent delivery một receipt. Hosted worker phục hồi browser Stop event ghi trong receipt worker trực tiếp. |
| UI và persistence | Browser Repeat → generated task → executor view → manager Stop from plan → reload Inactive, existing task giữ assignment. Không dựa synthetic SQL task creation cho luồng này. |
| Regression và cleanup | [Checkpoint71](CHECKPOINT-71.md), [runtime](checkpoint71-runtime.json), [final SQL](checkpoint71-final-sql.json), [cleanup](checkpoint71-cleanup.json). |

Stop từ danh sách series không gắn một task thì chỉ dừng lịch; không suy đoán recipient từ assignment lịch sử. Luồng UC103 từ task gắn đúng current assignment để gửi thông báo. Notification không cấp quyền đọc mới.

JOB05 kiểm thử bằng SQL thật với scheduling clock mô phỏng tương lai; không phải bằng chứng timer chạy ở giờ thật00:45. Hosted notification delivery là tiến trình API thật, opt-in, đã chạy và dừng. Các Plan/Cancel worker receipts tại C70 là bằng chứng lịch sử được phân biệt với C71 Stop worker.

Schema bổ sung chỉ gồm operational_task_series và task_series_occurrences, liên kết vào task hiện có; workflow vẫn dùng task_progress_versions. No generated EF edits/migrations. Series/occurrence identities bất biến; unique(series,due_date) ngăn trùng. Monthly ngày31 bỏ qua tháng không có ngày đó, không tự dời ngày. Khi end date hết, next_due_at=null; không tự tạo trạng thái khác.

Worker production mặc định tắt. Môi trường demo được xác minh có thể bật TaskSeriesGeneration__Enabled=true và TaskNotifications__Enabled=true sau khi cài capability tương thích. JOB05 chạy00:45 Asia/Ho_Chi_Minh, phục hồi sau restart trong ngày; tối đa100 occurrences/series/run để giữ công bằng, backlog còn lại được log và xử lý lần sau. Không triển khai/seed production trong checkpoint này.

UC102 còn thiếu public linked Internal Request (UC118). UC106/new cycle và workload/effort là Batch B tiếp theo.
