# Phạm vi bảo vệ SQL FS-012

FS-012 thêm đường ghi thử nghiệm có hạn quyền trên **HotelOps_Canonical_20261106_f120001_Fresh**; không triển khai vào shared/production. Entity sinh từ database, mapping hiện hành, API routes và role/permission catalog không đổi. Flag mặc định vẫn OFF. Chỉ Development + flag `FS012_PROVISIONAL` + exact database/owner/candidate/lifecycle/boundary markers mới dùng procedure mới.

`dbo.fs012_assign_with_evidence` chạy trong đúng một transaction đang committable do `BatchAsync` sở hữu. Procedure không tự BEGIN/COMMIT/ROLLBACK. Procedure lấy cùng application lock `HotelOps:RoomTypes:v1` như service trước khi khóa source, kiểm tra branch/room, cycle, rowversion, pending state, assignment hiện hành và operation trùng. Procedure tạo Assignment, cập nhật task, tạo audit, completed operation/result, publication và một recipient intent; cuối cùng gọi persisted-proof verifier. XACT_ABORT bật trong module. Fault tại audit/publication đã làm transaction tự rollback (XACT_STATE=0), commit bị từ chối và không lưu một phần bundle.

Service vẫn thực hiện xác thực session, permission, scope, ETag/fingerprint, due, eligibility/shift và kiểm tra lần cuối trước commit. Replay vẫn tra operation sau khi tái xác thực source/branch; procedure không thay replay bằng một lệnh Assign mới. EF guard còn kiểm tra ngay tại physical commit. SQL module không nhận tùy chọn bỏ audit/publication hoặc SQL text từ caller.

## Principal thử nghiệm

`fs012_app WITHOUT LOGIN` là user mới chỉ trong trial. GRANT SELECT/VIEW DEFINITION phục vụ app/metadata; GRANT EXECUTE chỉ approved assignment module và verifier. Không thêm user vào db_owner/db_datawriter, không tạo server login/grant. DENY Assignment INSERT/UPDATE/DELETE; DENY direct operation/publication DML; DENY audit UPDATE/DELETE và intent INSERT/DELETE. Chỉ các cột delivery/claim/retry được UPDATE trên intent; user/publication/channel không được đổi. Ownership chain trong procedure cho phép tạo bundle dù direct DML bị DENY. Không thêm trigger toàn cục; ba candidate triggers có từ baseline chỉ tồn tại trên trial mới.

Các GRANT ghi UC028/UC032 được liệt kê tường minh trong `10_SqlWriteBoundary.sql`. Core UC032 items/progress/audit ở 100% đã kiểm tra dưới user hạn quyền, vẫn IN_PROGRESS. Upload ảnh dưới user này chưa kiểm chứng; chưa cấp INSERT attachments. User cũng chưa được cấp INSERT notifications/notification_recipients, nên inbox delivery dưới user này chưa được hỗ trợ; live delivery dùng developer principal. Principal hiện là prototype cho phạm vi đã đo, chưa là tài khoản đủ quyền cho toàn backend hoặc dispatcher.

## Audit writer

| Writer | Hiện trạng và bảo vệ |
| --- | --- |
| EF SaveChanges | Producer cũ ở các tuple FS009–FS011 không đổi; FS012 gọi module. Direct Assignment insert bị SQL229 dưới fs012_app |
| EF explicit transaction | BatchAsync giữ transaction/authorization; module không commit; SQL denial vẫn áp dụng khi caller dùng transaction riêng |
| ExecuteSql/ExecuteUpdate/Delete | Direct Assignment DML bị SQL229; intent identity UPDATE bị SQL230; EF command guard là lớp bổ sung, không phải SQL parser |
| Raw ADO.NET | Cùng principal hạn quyền: direct Assignment update bị SQL229; raw provider commit sau approved module lưu đủ năm record, không phụ thuộc EF commit hook |
| Stored procedure | Direct EXECUTE grants chỉ gồm approved module/verifier; canary module dbo có khả năng ghi bị từ chối EXECUTE; ALTER canary bị từ chối quyền. Inventory quyền hiệu lực các dbo procedures được lưu riêng để kiểm tra quyền kế thừa/public |
| Direct/external connection | Connection độc lập dùng fs012_app bị chặn thay ended_at; không có grant riêng cho writer shared |
| Shared module | Source DEV1 WorkTime/deactivation/monitor/history/authorization đọc Assignment; không sửa các module đó. UC030/Reassign writer tương lai chưa được cấp đường ghi |
| dbo/sysadmin/owner hoặc principal khác có DML | Ngoài boundary; reason write độc lập đã persist mà không có thêm audit/operation/publication/intent |

## Giới hạn phải giữ khi review

Proof dùng `EXECUTE AS USER` trên connection của host quản trị, không phải đăng nhập bằng application login mới. Connection test không pool và REVERT trước dispose. Host có quyền REVERT/DDL và không nằm trong threat model của user hạn quyền. API live hiện dùng Windows integrated principal của developer, không tự hạ quyền thành fs012_app. **Không tuyên bố runtime hiện tại đã triển khai least privilege.** Cần mapping login/connection của môi trường triển khai và kiểm chứng riêng trước khi rollout.

Database user là trusted application writer, không phải end-user RBAC: actor/fingerprint/eligibility được service xác thực, SQL không chứng thực danh tính HTTP. User còn được UPDATE cleaning_tasks để UC028 hoạt động, UPDATE các cột delivery và INSERT audit cho workflows khác. Việc sửa task sau module qua raw SQL có thể làm source drift; module không tạo deferred constraint ở commit cho mọi writer. Không tuyên bố mọi future task/notification mutation đều được bảo vệ. Existing seeded assignments không có candidate proof không được backfill hay tự coi là compliant.

Constraint/FK/unique index bảo vệ cấu trúc, không đủ biểu đạt “phải có audit/publication tại COMMIT” cho mọi principal. DENY đơn thuần sẽ chặn EF producer cũ; module hoàn chỉnh là lý do đường mới tương thích. Trigger Assignment toàn cục bị loại khỏi triển khai vì chưa chứng minh với mọi writer/Database First. Không có schema migration hay thay đổi quyền shared nào.
