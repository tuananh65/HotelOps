# UC106 — Reopen Assigned Task

COMPLETE_VERIFIED tại C72 trên disposable SQL và trusted HTTPS. UC DETAIL/SRS 11.5, GBR-012/126/134 được đối chiếu riêng; không suy ra toàn FE11 hoàn thành.

| Acceptance | Bằng chứng trực tiếp C72 |
|---|---|
| Department Head, current permission/branch/session | Reuse role mapping và TASK_MANAGE/TASK_VIEW; anonymous, executor và foreign branch bị từ chối. Assignee phải active, đúng department/branch và có executor role; inactive/revoked role bị từ chối. |
| Closed, reason, remediation, same/new assignee | 43 Reopen assertions trong runtime; reason ngắn/control/oversize, instructions rỗng và assignee không hợp lệ bị từ chối. API Reopen cùng executor; browser Reopen sang executor khác. |
| Assigned, new processing cycle, Accept lại | Reopen reset current progress/criterion results; giữ first actual start và các interval cũ. Start trước Accept trả409. Hai chu kỳ tiếp theo đi Accept → Start → Submit → independent Verify → Closed. |
| Previous results/history | Ba immutable submission snapshots và ba completed assignments; canonical UI đọc được kết quả chu kỳ đầu sau completion lần ba. Historical evidence URL gắn submission ID và vẫn dùng current source authorization. |
| Segregation and effort | Return giữ nguyên chu kỳ; chỉ proven Reopen event mở chu kỳ mới. Performer của chu kỳ trước có thể verify chu kỳ mới nếu hiện là manager hợp lệ và không thực hiện chu kỳ mới; current performer vẫn bị chặn. Effort cộng tất cả closed intervals, không tính paused time. |
| Atomic transaction, CSRF/ETag, concurrency | Missing CSRF400, missing ETag428; concurrent same-version requests chỉ một200/một412; retry412. Fault tại audit và criterion reset rollback toàn bộ workflow fingerprint. |
| Audit and notification | Hai Reopen events có matching audit. Concurrent delivery cùng event tạo một recipient. Quyền bị thu hồi chặn delivery/source. Hosted worker thực chạy phục hồi một committed event, không đổi source snapshot. |
| Database integrity and cleanup | Sáu foreign keys trusted; UPDATE immutable history bị trigger từ chối trong transaction rollback. Ba clone/backup đã xóa; nguồn disposable rỗng đã rollback capability; host/private evidence0. |

[Runtime](checkpoint72-runtime.json):307 records,43 Reopen checks,7 browser flows/53 stages; riêng Reopen browser có7 stages. Dùng task được tạo từ public UI trong cùng harness. [Final SQL](checkpoint72-final-sql.json), [hosted worker](checkpoint72-reopen-worker.json), [cleanup](checkpoint72-cleanup.json).

Schema bổ sung hẹp: một bảng `task_reopen_events` bất biến liên kết submission đã Accepted, assignment mới, interval cutoff và audit. Giữ `task_progress_versions` là workflow authority. Chỉ mở ngoại lệ reset criterion trong transaction Reopen có provenance; không tắt CHECK/trigger, không sửa generated entities hoặc migration. Isolated install có identity guard, idempotency và empty-source rollback; không triển khai shared/production.

Các lượt trước không tính PASS: trial1 lỗi browser selector; trial2 thiếu logout cuối browser. [Earlier trial receipts](checkpoint72-earlier-trials.json) giữ kết quả lỗi. Lượt cuối có persisted logout/revocation và cleanup. Backend test từng bị DLL lock khi host đang chạy, đã rerun sau khi dừng host. Auth/Router từng chạy sai cwd, phải dùng thư mục frontend.

UC105 còn thiếu requester provenance MAINTENANCE/INSPECTION/CLEANING; các đường này tiếp tục fail-closed. UC111 correction và UC113 workload là phần tiếp theo của Batch B.
