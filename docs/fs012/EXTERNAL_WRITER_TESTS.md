# Kết quả writer boundary trên SQL thật

Trial duy nhất được ghi: HotelOps_Canonical_20261106_f120001_Fresh, owner DEV1_FS012. Không sửa principal/grant/schema shared, không reset các trial cũ. User thử nghiệm fs012_app WITHOUT LOGIN được impersonate trên connection riêng không pool, REVERT khi dispose; host thực tế là sysadmin. Các session/permission gates HTTP trong component proof là doubles, không phải bằng chứng authentication API.

## Kết quả cuối của suite boundary

`BOUNDARY_ATTEMPT07.json`: **24 checks**, gồm **23 protected/compatibility outcomes đúng kỳ vọng** và **1 privileged bypass dự kiến**; không gọi24/24 là bảo vệ toàn database.

| Nhóm | Bằng chứng thực tế |
| --- | --- |
| Authorized writer | Service dưới fs012_app tạo một Assignment/audit/operation/publication/intent trong caller transaction; năm record durable |
| Direct EF | SaveChanges insert, ExecuteUpdate assignee và ExecuteDelete Assignment bị SQL229 |
| Explicit transaction/ExecuteSql | UPDATE reason trong transaction riêng bị SQL229; rollback sạch |
| Raw ADO | Direct UPDATE bị SQL229 dưới cùng user, không dựa vào EF command interceptor |
| External same-principal connection | Connection độc lập sửa ended_at bị SQL229 |
| Evidence tampering | Xóa audit/publication/intent bị SQL229; đổi intent user_id bị SQL230 |
| Unapproved module/DDL | EXECUTE writer canary bị SQL229; ALTER procedure bị SQL3701. Receipt inventory kiểm thêm quyền EXECUTE hiệu lực các dbo procedures |
| Transaction ownership | Autocommit module và nested transaction bị SQL51941; adapter nhận physical transaction khác bị từ chối trước proof query |
| Caller rollback | Ghi bundle rồi rollback, cả năm count=0 |
| Raw provider commit | Gọi approved module rồi commit provider trực tiếp vẫn có đủ năm record; đây là lợi ích của module hoàn chỉnh dù bỏ qua EF commit hook |
| Missing audit | Fault constraint làm SQL547; XACT_STATE=0, commit bị từ chối, năm count=0 |
| Missing publication | Fault constraint làm SQL547; XACT_STATE=0, commit bị từ chối, năm count=0 |
| Concurrent writers | Hai connection cùng source/tag: một COMMITTED, một STALE_REJECTED; chỉ một bundle |
| UC028 | Restricted EF Create + cycle/readiness/audit PASS |
| UC032 | Restricted EF items/progress/audit ở100% PASS, vẫn IN_PROGRESS; không test photos dưới user này |
| Privileged writer | Admin connection đổi Reason, marker FS012_PRIVILEGED_BYPASS_003a4da735e643fe96c21977c5e0a07f còn persist, không thêm audit/op/publication/intent. Không suy rộng thành đã test đổi assignee bằng admin |

## Các lượt lỗi được giữ riêng

Không ghép các attempt thành một suite PASS. Attempt01 dừng ở SQL230 do assertion chỉ chấp nhận229; Attempt02 dừng ở DDL-denial3701 chưa được assertion nhận diện. Attempt03/04 gặp SQL596 class21 khi lifecycle connection impersonation chưa được dọn đúng. Helper được sửa dùng nonpooled connection và REVERT; không đổi connection runtime của app. Attempt05 dừng tại InvalidCastException do XACT_STATE cần CONVERT(int). Attempt06 dùng assertion quá hẹp chỉ cho state=-1; SQL thực tế tự rollback về0. Attempt07 chạy lại toàn bộ suite mới, chấp nhận0/-1 nhưng vẫn bắt buộc commit bị từ chối và fresh durable counts=0. Dữ liệu attempt trước được giữ; không reset/reseed để có PASS.

Inventory inspection đầu có false alarm do PowerShell đếm null JSON property như một phần tử; sửa bằng scalar COUNT(*) từ SQL. Final SQL_BOUNDARY_INVENTORY.json xác nhận residualFaultConstraintCount=0, user NOLOGIN, direct grants và effective dbo EXECUTE: module/verifier=1, unapproved writer canary=0. Không coi absence của property JSON là proof quyền hoặc fault cleanup.

## Commit/EF compatibility ngoài suite24

`COMMIT_FAULT_RESULTS.json`:4/4 provider acknowledgement/cancellation simulations trên SQL thật. Before/cancel-before không durable record rồi retry tạo đúng một bundle; after/cancel-after durable đủ bundle và recovery trả kết quả lịch sử, retry không duplicate. Dùng gate doubles; không phải network-disconnect test. `PHYSICAL_GUARD_RESULTS.json`:8 protected behavior checks đúng kỳ vọng +1 raw provider bypass xác nhận trên privileged host; universalPhysicalGuard=false. `NOTIFICATION_SQL_RESULTS.json`:6 constraint/rollback checks, không để test rows lại.

Chưa chứng minh app login hạn quyền deploy thực, dispatcher inbox dưới fs012_app, ảnh/file storage dưới user đó, network partition, SQL outage lúc recovery, kill worker đột ngột hoặc mọi writer của shared system. SQL principal tin cậy app; RBAC/shift/actor/fingerprint còn do service xác thực. Mutable task/delivery grants cho phép drift sau module ngoài guarded service path; cần review trước rollout. Chi tiết threat boundary ở SQL_WRITE_BOUNDARY.md.
