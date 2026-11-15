# UC113 — Review Task Workload and Time Logs

COMPLETE_VERIFIED tại C73 trên disposable SQL và trusted HTTPS. Đối chiếu UC DETAIL113, SRS11.11, GBR015/135/140. Không suy ra UC111 correction đã hoàn thành.

| Acceptance | Bằng chứng trực tiếp |
|---|---|
| Actor and current scope | Department Head hoặc Operations Manager theo SRS11.11; active role có TASK_VIEW trên chính grant path hợp lệ, active account/branch access. Existing catalog có cả hai actor; không thêm quyền. API positive cả hai actor; executor own-only, foreign branch và revoked roles bị từ chối. Department Head giữ branch scope đã được duyệt. |
| Employee workload | SQL query sort name/user ID; open/current-assignment, overdue theo GBR135, distinct tasks có Accepted completion trong kỳ, closed interval effort và provisional riêng. Hai nhân viên qua bốn processing cycles; một task không bị đếm trùng cho cùng người. |
| Filters and pagination | Department, employee, workflow state, priority, deadline, period, overdue và keyword; default20/max100. Positive combined filters, hai page không lặp nhân viên; invalid physical status, reversed dates và page size bị từ chối. Empty MSG10. |
| Employee drill-down | Scoped task list, persisted intervals và workdays được phân trang. Browser đi từ workload sang task/time-log detail; task link dùng canonical route hiện có. |
| Time semantics | SYSUTCDATETIME snapshot, hotel dates Asia/Ho_Chi_Minh thành half-open UTC range; closed effort không lấy shift duration. Current open interval có provisional>0; Submit/Verify chuyển sang closed effort, provisional0. Date-period clipping và SQL-second boundary contract có focused tests. |
| Shift-time authorization | SHIFT_VIEW được kiểm tra riêng và kiểm tra lại trước final commit gate; TASK_VIEW không tự cấp shift data. Khi scope SHIFT_VIEW đổi OWN, response không trả shift detail. Browser hiển thị task time và workday time riêng. |
| Read-only consistency | Reuse current-session validation và Serializable boundary. Workload GET không ghi business/audit. Không có mutation route, nên không áp dụng CSRF/If-Match riêng cho hai GET. Lifecycle fixtures vẫn dùng CSRF/strong ETag chuẩn. |
| Browser and database | [Runtime](checkpoint73-runtime.json):346 records,40 workload checks,8 browser flows/58 stages; workload riêng5 stages. [Final SQL](checkpoint73-final-sql.json):4 immutable submissions/completed assignments,0 open intervals. |
| Cleanup | [Cleanup](checkpoint73-cleanup.json):ba owned clones/backups đã xóa; source rows/capabilities0, host/private files0. UC113 không đổi schema/entity/migration. |

API: GET `/api/branches/{branch}/task-workload` và GET `/api/branches/{branch}/task-workload/{employee}`. UI: mở task hoặc task list đã được scoped-read, chọn **Review workload and time logs**. Period tối đa366 ngày mỗi query; giữ các filter trên màn hình. Current workflow chưa có authoritative mapping thì trả503, không suy Assigned từ legacy TODO.

Fixture overdue dùng deadline synthetic tạm thời trên đúng public-created task trong clone, khôi phục trong finally; đây là bằng chứng phép đọc overdue, không phải acceptance cho sửa deadline. Lifecycle Reopen/Accept/Start/Submit/Verify dùng public HTTPS thực. Không thêm permission vào catalog; thử revoked role/SHIFT_VIEW chỉ thay các grant synthetic trong clone và khôi phục.

Hai lượt trước không tính PASS: trial1 EF không compose được positional record projection; đã đổi sang member initialization. Trial2 browser đọc row count khi filter request chưa xong; harness nay chờ response và rendered state. Lượt3 hoàn thành cả business checks và browser; [failure metadata](checkpoint73-earlier-trials.json) được giữ lại.

Backend610 PASS/11 SKIP; frontend task73 PASS; Auth/Router host175 PASS. Full841 tại C72 là bằng chứng lịch sử, không khai thành lượt C73. Vite còn use-client và chunk>500KB; EF20602/10102 hiện hữu. UC111 My Time Log/correction còn thiếu; SRS work_interval.correct chưa có mapping trong catalog hiện tại.
