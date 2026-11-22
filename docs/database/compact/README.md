> Lưu trữ thiết kế v1 (40 bảng). Database hiện tại đã chuyển sang [Compact v2 (37 bảng)](../compact-v2/README.md); xem tài liệu v2 để biết schema và kiểm chứng mới nhất.

# Đánh giá HotelOps và database thu gọn

Ngày đánh giá: **21/11/2026**. Mã nguồn: nhánh `integration/dev1-dev2-dev3-unblock-20261114`, commit `095d1b82`.

## Kết quả và phạm vi

Đã tạo **HotelOpsCompactDB** độc lập trên SQL Server local, **40 bảng** so với **149 bảng** của HotelOpsDB: giảm 109 bảng, khoảng **73,2%**. Người dùng đã chọn giữ database cũ và không chuyển dữ liệu. Database mới có schema, ràng buộc và view; không có tài khoản, quyền được cấp sẵn hay dữ liệu nghiệp vụ mẫu tồn lại.

Đây là thiết kế lưu trữ mới cho các nhóm nghiệp vụ hiện tại. **Ứng dụng hiện tại chưa tương thích với database mới.** Chưa đổi connection string, scaffold EF, sửa API hoặc chạy lại toàn bộ use case trên schema này. Việc có bản đồ 149 bảng không đồng nghĩa đã chuyển đầy đủ 1.310 cột hay đã chứng minh tương đương nghiệp vụ.

## Đánh giá hệ thống hiện tại

| Quan sát đã kiểm tra | Tác động | Hướng xử lý |
|---|---|---|
| Backend một project ASP.NET Core/.NET 10, EF Core Database First; frontend React/TypeScript/Vite | Cấu trúc phù hợp ứng dụng nội bộ; thay DB ảnh hưởng cả EF và truy vấn SQL viết trực tiếp | Giữ cấu trúc project, chuyển theo module và hợp đồng API |
| Database thật: 149 bảng, 1.310 cột, 65 trigger, 8 stored procedure, 1 function, 1 view | Quy tắc nằm cả trong SQL lẫn C#; chỉ scaffold lại không đủ | Kiểm kê trigger/procedure và chuyển quy tắc có chủ đích |
| Có 90 tên bảng khác nhau trong các lời gọi `ToTable` của DbContext; còn bảng được truy cập qua SQL trực tiếp | Không thể dùng số entity để đại diện toàn bộ DB | Bản đồ dựa vào `sys.tables` thực tế, kiểm đủ 149/149 |
| Nhiều bảng assignment/progress/history/outbox lặp theo từng workflow | Sửa một cơ chế chung phải sửa nhiều module; các bản vá mở rộng liên tục | Dùng work_items/work_assignments/work_events và một outbox theo recipient |
| Cleaning/inspection/rework có `status` và `workflow_status`; operational task còn có bảng `task_progress_versions` | Nguồn trạng thái dễ lệch; legacy TODO không chứng minh đã Assigned | Compact lưu trạng thái nghiệp vụ theo `kind`; dùng các state đã tìm trong code/constraint, không suy diễn từ legacy status |
| `StartupSchemaValidator` chủ yếu kiểm tra `users.row_version`, không kiểm toàn bộ hợp đồng DB | Qua startup gate không có nghĩa mọi module chạy được | Không trỏ ứng dụng cũ sang Compact; cần gate riêng khi chuyển backend |
| Nhiều capability/hash/schema marker riêng cho từng gói phát triển | Kiểm soát phát hành chặt nhưng làm môi trường khó tái tạo | Schema mới có installer thống nhất, manifest và phiên bản extended property |
| EffectiveRoomReadiness tách occupancy khỏi bằng chứng cleaning/inspection | Đây là quy tắc phải giữ; không thể coi `READY` lưu trong rooms là đủ | Giữ turnover, cleaning parent, inspection/rework, blocks và room events; chuyển thuật toán readiness trước khi dùng Reception |
| SRS còn các phần pending; Change Notes ngày 20/11 đã duyệt phạm vi check-in/check-out/move | Dễ dùng tài liệu cũ để cấp nhầm quyền | Không seed role/permission/grant; bảo toàn các quyết định đã được duyệt khi chuyển service |

Các số liệu trên là kiểm tra cấu trúc và đọc code, **không phải kết quả benchmark, kiểm thử bảo mật toàn diện hoặc chứng nhận 144 use case đã hoàn tất**. Chưa đo độ trễ API, tải đồng thời, kế hoạch truy vấn hay chỉ số sử dụng từng bảng; số bảng lớn tự nó không chứng minh hệ thống chậm.

## Danh mục đúng 40 bảng

| STT | Bảng | Vai trò |
|---:|---|---|
| 1 | branches | Chi nhánh |
| 2 | departments | Phòng ban trong chi nhánh |
| 3 | locations | Tầng/khu vực, cây giới hạn hai cấp |
| 4 | room_types | Loại phòng theo chi nhánh |
| 5 | rooms | Phòng, occupancy/operational status, turnover và con trỏ cleaning |
| 6 | users | Tài khoản, thông tin nhân viên, khóa đăng nhập |
| 7 | memberships | Quyền truy cập chi nhánh, membership phòng ban, thẩm quyền quản lý; phân biệt kind |
| 8 | roles | Vai trò |
| 9 | permissions | Danh mục quyền |
| 10 | role_permissions | Quyền của vai trò và data scope |
| 11 | user_roles | Gán vai trò theo người/chi nhánh/thời hạn |
| 12 | user_sessions | Phiên đăng nhập, token hash, security stamp |
| 13 | reference_values | Danh mục có phân loại, đơn vị/quy đổi về đơn vị gốc, cấu hình |
| 14 | assets | Hồ sơ và vị trí/người giữ tài sản hiện tại |
| 15 | asset_events | Lịch sử điều kiện, bàn giao, di chuyển tài sản |
| 16 | checklist_templates | Checklist và phiên bản |
| 17 | checklist_items | Tiêu chí từng phiên bản |
| 18 | work_templates | Mẫu/lịch lặp, bảo trì định kỳ, scope/checklist |
| 19 | work_items | Cleaning, rework, inspection, incident, maintenance, task, internal request |
| 20 | work_links | Liên kết nguồn, phụ thuộc, reinspection, blocker |
| 21 | work_assignments | Phân công và lịch sử phân công |
| 22 | work_events | Tiến độ, quyết định, nộp/duyệt, reopen, handover, chứng cứ |
| 23 | work_checklist_items | Snapshot tiêu chí và kết quả từng công việc |
| 24 | inspection_findings | Lỗi kiểm tra và lần rework xử lý |
| 25 | room_blocks | Khóa phòng có nguồn công việc và thời gian hiệu lực |
| 26 | room_events | Lịch sử trạng thái, turnover, phòng bị ảnh hưởng |
| 27 | warehouses | Kho |
| 28 | inventory_items | Vật tư và đơn vị gốc |
| 29 | inventory_policies | Ngưỡng tồn và episode cảnh báo |
| 30 | inventory_documents | Header yêu cầu vật tư, bổ sung, kiểm kê và chứng từ kho |
| 31 | inventory_document_lines | Dòng vật tư, số lượng, quy đổi, dòng nguồn |
| 32 | stock_movements | Sổ kho bất biến theo đơn vị gốc |
| 33 | document_events | Lịch sử/phiên duyệt/snapshot dòng chứng từ |
| 34 | shifts | Mẫu ca và ca được lên lịch |
| 35 | workdays | Phân ca, bắt đầu/kết thúc ca, handover |
| 36 | work_intervals | Khoảng giờ thực hiện công việc |
| 37 | time_corrections | Yêu cầu và quyết định điều chỉnh giờ |
| 38 | attachments | Metadata tệp với owner có FK |
| 39 | audit_logs | Audit trước/sau bất biến |
| 40 | notifications | Outbox, retry, người nhận, đọc/xác nhận |

Không có bảng migration, bảng cấu hình phụ hay bảng balance ẩn ngoài danh mục này. Tồn kho là view tổng hợp ledger. Phiên bản schema nằm trong extended property; file triển khai và receipt được quản lý ngoài DB.

## Cách gộp và phạm vi 15 nhóm chức năng

| Nhóm | Lưu trữ chính trong Compact | Điểm cần giữ khi chuyển backend |
|---|---|---|
| FE-01 Auth/RBAC | users, memberships, roles, permissions, grants, sessions | Session/CSRF, last-admin, active membership, scope, thu hồi quyền |
| FE-02 Cấu trúc | branches, departments, locations, room_types, rooms | Mã duy nhất trong chi nhánh, soft deactivation |
| FE-03 Phòng | rooms, room_blocks, room_events | Occupancy khác readiness; checkout làm bằng chứng cũ hết hiệu lực |
| FE-04 Housekeeping | work_items kind CLEANING/REWORK, assignments, checklist, events | Đúng người làm, blocker, từng turnover, cancellation/rework |
| FE-05 Inspection | work_items kind INSPECTION, checklist versions, findings | Phiên bản tiêu chí, kết luận, reinspection, không đổi OCCUPIED thành READY |
| FE-06 Incident/Maintenance | work_items, work_templates, work_links/events, room_blocks | SLA, escalation, reopen, affected rooms, lịch occurrence |
| FE-07 Assets | assets, asset_events, attachments, work_items | Bàn giao nhiều bước, điều kiện tài sản, lịch sử di chuyển |
| FE-08 Inventory | warehouses, items, documents/lines, ledger/policies | Nhập/xuất/hoàn/chuyển/kiểm kê/đảo, số lượng và scope |
| FE-09 Material Request | document kind MATERIAL_REQUEST, lines, document_events | Duyệt theo revision, phần còn lại, partial issue/return, không tự post yêu cầu |
| FE-10 Replenishment | document kind REPLENISHMENT và các RECEIPT con | Nhiều lần nhận, dòng nguồn, đóng phần chưa nhận |
| FE-11 Tasks | work_items kind OPERATIONAL, templates, links, events | Workflow hiện có, dependency, submission/verification, recurring |
| FE-12 Work-time | shifts, workdays, intervals, corrections | Ca qua đêm, ca tự phát, không overlap, sửa giờ có người duyệt |
| FE-13 Internal Request | work_items kind INTERNAL_REQUEST, links, events | Route/accept/reject/handover/withdraw/reopen; liên kết công việc khác |
| FE-14 Notifications/Dashboard | notifications, views/truy vấn có scope | Ghi outbox cùng transaction; eligibility khi gửi/mở; claim lease/retry |
| FE-15 Reports/Audit | audit_logs, domain events, ledger | Báo cáo theo dòng sự kiện, giữ provenance, không dùng JSON thay chỉ mục nghiệp vụ |

[source-to-target.csv](source-to-target.csv) có **149 dòng ánh xạ**, kiểm tự động không bỏ sót bảng nguồn hoặc tham chiếu bảng đích không tồn tại. Đây là mapping **cấp bảng**, không phải ETL cấp cột. `dev3_schema_migrations` được bỏ khỏi mô hình nghiệp vụ mới; các bản vá cũ không có nghĩa trong schema Compact.

JSON chỉ dành cho cấu hình/snapshot lịch sử, trước/sau audit và quy tắc lịch lặp. Các đối tượng lõi, dòng số lượng, khoảng giờ, checklist, findings và quan hệ công việc vẫn là bảng/cột/FK. Snapshot chuyên biệt phải có hợp đồng dữ liệu theo loại sự kiện, không được đẩy toàn bộ entity cũ vào JSON để tránh thiết kế.

## Toàn vẹn đã triển khai

- **159 FK**, gồm FK ghép để chặn dữ liệu khác chi nhánh và ghép đúng workday/user/branch, đúng item của dòng kho, đúng checklist version.
- **78 CHECK**: trạng thái theo loại công việc/chứng từ, ngày giờ, kết quả inspection, số lượng, đơn vị, scope và owner tệp.
- Unique filtered index cho assignment đang ACTIVE, block chưa release, workday/interval đang mở, primary branch, notification recipient/event và reversal.
- Rowversion trên thực thể thay đổi; caller phải gửi version và dùng conditional update. Rowversion tự nó không ngăn lost update nếu caller bỏ điều kiện.
- Published checklist và lịch sử domain/audit/ledger bất biến. Reversal là bút toán mới có FK tới bút toán gốc; không sửa ledger cũ.
- Trigger kiểm đúng phòng/turnover/cleaning parent cho inspection/rework; `current_cleaning_work_id` không thể trỏ inspection.
- Ledger chỉ nhận dòng từ chứng từ đã post, đúng kho/item/qty/sign; không âm tồn; key-range locks giữ đến commit. Deadlock cần retry toàn bộ transaction ở service.
- Hai view: `v_stock_balances`, `v_inventory_document_totals`. Tổng tiền trong view là base_qty × unit_cost; adapter phải thống nhất unit_cost theo **đơn vị gốc**.

## Những điểm chưa hoàn tất ở tầng ứng dụng

Các giới hạn này cần được xử lý trong bước chuyển backend, không được coi schema là ứng dụng đã chạy:

1. **Chưa port state machine và authorization**. CHECK chỉ giới hạn state hợp lệ, không kiểm mọi transition, quyền actor, last-admin, readiness, SLA, timeout hay tách người làm/người duyệt cho mọi workflow.
2. **Chưa có adapter/scaffold/SQL query mới**. Các schema capability/hash của ứng dụng cũ không áp dụng cho Compact. Không tắt guard cũ để ép chạy.
3. **Chưa chuyển dữ liệu** theo lựa chọn người dùng. Không có seed mật khẩu, tài khoản admin, role grants hoặc dữ liệu demo. Phiên đăng nhập cũ phải vô hiệu hóa nếu sau này chuyển hệ thống.
4. **Gộp nhiều cấu trúc làm adapter phức tạp hơn**: phân ca thành workday SCHEDULED; ca qua đêm cần service tính ngày giờ; một checklist dùng nhiều room type được diễn đạt qua nhiều work_templates; các lần nhận chuyển kho là chứng từ con riêng.
5. **Lịch sử đặc thù cần hợp đồng snapshot**: approval cycle, cleaner attribution, rework proof, escalation, handover và feedback. Mapping không chứng minh migration lossless; không tự bỏ dữ liệu cũ nếu sau này ETL.
6. **Kho**: schema chặn sai sign/qty/negative stock, nhưng chưa triển khai service post transaction đầy đủ, partial-fulfillment ceilings, approval snapshot matching, conversion catalog validation, đối soát damaged/missing và retry. Trạng thái nhận chuyển kho cần projection từ chứng từ con vì header có ledger bất biến.
7. **Work-time và dependency**: schema chặn nhiều interval đang mở và interval ngoài workday; kiểm tra overlap giữa các interval đã đóng, đồ thị dependency và thời gian sau correction vẫn cần logic service.
8. **Tài sản chuyển chi nhánh**: phải xử lý các FK scope của công việc/mẫu/attachment đang tham chiếu; chưa có quy trình cutover scope lịch sử đầy đủ cho trường hợp này. Không tự đổi branch_id và bỏ FK.
9. **Outbox**: đã có dữ liệu lease/retry/ack/dedup; chưa viết worker claim/delivery hoặc kiểm chứng crash recovery, nhiều worker và phân quyền khi giao thông báo. Dữ liệu tiêu đề/nội dung lặp theo recipient là đánh đổi có chủ đích.
10. **Hiệu năng/retention**: chưa benchmark tải thực, contention trên work_items/ledger, truy vấn JSON hoặc lưu trữ lịch sử dài hạn. Không có cascade delete; retention cần quy trình riêng. Dữ liệu nhạy cảm/token/hash không được ghi vào audit snapshot.

## Kiểm chứng

- **SQL Server thực tế:** tạo thành công database độc lập với 40 bảng. Metadata/DBCC được kiểm tra bằng `04_Verify.sql`.
- **Automated database tests:** `Test-Compact.ps1`, **38/38 PASS**, giao dịch rollback từng case, **0 dòng fixture còn lại**. Bao gồm positive workflow, branch isolation, duplicate assignment/block, immutable history, checklist version, typed UOM, workday attribution, posted ledger, negative stock, exact reversal và stale rowversion.
- **Phạm vi concurrency:** đã kiểm tra conditional update với stale rowversion; chưa chạy stress nhiều connection hoặc chứng minh không deadlock.
- **Nguồn cũ:** chỉ đọc system catalog; so sánh inventory trước/sau. Không chạy ALTER/INSERT/UPDATE/DELETE trên HotelOpsDB.
- **Build backend / kiểm thử API:** xem [verification.md](verification.md) để phân biệt kết quả build baseline với kiểm chứng database mới. Không có thay đổi C# hay frontend trong công việc này.
- Log/receipt chi tiết nằm ở `artifacts/compact-db/` (được Git ignore); file báo cáo này không chứa thông tin tài khoản hay connection string bí mật.

## Dùng bộ script

Chạy từ thư mục gốc repo, PowerShell/Node.js và SQL Server 2022+; dùng Windows Integrated Authentication có quyền tạo DB. Instance là tham số local, không ghi vào appsettings chung.

```powershell
# Metadata nguồn, không đọc dữ liệu người dùng
powershell -NoProfile -File scripts/database/compact/Inspect-Source.ps1 -Server '.\SQLEXPRESS'

# Tái sinh DDL và mapping (không ghi vào database)
node scripts/database/compact/Generate-Schema.mjs
node scripts/database/compact/Build-Mapping.mjs

# Trên môi trường mới, khi HotelOpsCompactDB chưa tồn tại
powershell -NoProfile -File scripts/database/compact/Install-Compact.ps1 -Server '.\SQLEXPRESS'

# Kiểm tra read-only
sqlcmd -S '.\SQLEXPRESS' -d HotelOpsCompactDB -E -C -b -i scripts/database/compact/04_Verify.sql

# Chỉ dùng trên Compact còn rỗng; mỗi fixture được rollback
powershell -NoProfile -File scripts/database/compact/Test-Compact.ps1 -Server '.\SQLEXPRESS'
```

Installer chạy `01_Schema.sql`, `02_Integrity.sql`, `03_DocumentGuards.sql` trong một transaction. Bình thường từ chối tên DB đã tồn tại. Nếu lần tạo mới lỗi và rollback để lại DB hoàn toàn rỗng, `-ResumeEmpty` chỉ tiếp tục khi không có user object. Không có lệnh DROP/RESTORE hay thay đổi database cũ. Các script SQL được thực thi bởi installer; không chạy từng batch tùy ý lên database đang sử dụng. Các fixture rollback vẫn làm tăng bộ đếm IDENTITY; việc ID bắt đầu lớn hơn 1 là bình thường.

## Lộ trình dùng Compact cho hệ thống

1. Chốt mapping cấp cột và hợp đồng snapshot của từng workflow; giữ các trạng thái/permission đã được duyệt trong code và Change Notes.
2. Scaffold model Compact vào phạm vi riêng trong **project backend hiện tại**, thêm schema gate mới; không ghi đè model cũ khi chưa có adapter.
3. Port Auth/RBAC/cấu trúc trước; tiếp theo room/cleaning/inspection; incident/task/request/work-time; cuối cùng inventory/outbox/reporting. Giữ HTTP DTO/contracts và kiểm thử quyền/scope mỗi bước.
4. Chạy end-to-end các luồng: checkout → cleaning → inspection/rework → ready → check-in; incident → maintenance → material issue → verify; partial transfer/return/reversal; recurring work; request handover; correction; notification retry.
5. Chỉ đổi connection string sau khi các luồng đạt acceptance. Nếu sau này cần dữ liệu cũ: ETL có id-map, đối chiếu tổng kho/giờ làm/provenance, báo cáo bản ghi không chuyển được và phương án quay lại. Không xóa nguồn để đạt giới hạn bảng.
