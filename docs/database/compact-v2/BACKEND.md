> Cập nhật 22/11/2026: bản hiện tại là Compact 2.1.0, 40 bảng, có Workday độc lập. Xem [WORKDAY-INTEGRATION.md](WORKDAY-INTEGRATION.md). Nội dung bên dưới lưu mô tả bản 2.0 để đối chiếu.

# Backend Compact v2

Kết quả mới nhất ngày **22/11/2026**: **đã sửa hai lỗi kho, 127 kiểm tra API đạt, 0 lỗi**; xem [INVENTORY-FIX-REPORT.md](INVENTORY-FIX-REPORT.md). [FUNCTIONAL-TEST-REPORT.md](FUNCTIONAL-TEST-REPORT.md) lưu kết quả phát hiện lỗi trước khi sửa; các số liệu phía dưới là kết quả lịch sử của lượt chuyển backend.

Backend chạy qua `Program.cs` → `CompactHost`, dùng `HotelOpsCompactDbContext` được scaffold từ SQL Server schema **2.0.0**: **37 bảng / 6 view**. Không tạo migration, bảng mới, schema tự động hoặc ghi vào database legacy. Các controller/service/worker legacy còn được lưu trong repository để đối chiếu nhưng không được đăng ký vào ứng dụng chạy v2.

## Khởi chạy

Đặt `ConnectionStrings:HotelOpsDb` bằng .NET User Secrets hoặc biến môi trường tới database Compact v2 đã cài. Không đưa thông tin máy local vào appsettings dùng chung. Khi cấu trúc/marker không khớp, ứng dụng từ chối mở HTTP listener. Không dùng connection string của HotelOpsDB cũ.

```powershell
dotnet build HotelOps.slnx
dotnet run --project src/HotelOps.Api --launch-profile https
```

Database độc lập chưa có tài khoản có thể khởi tạo admin bằng lệnh CLI một lần. Cấu hình `Compact:Bootstrap:Username` và `Compact:Bootstrap:Password` qua môi trường hoặc secrets; mật khẩu tối thiểu 12 ký tự. Không truyền mật khẩu qua URL hoặc ghi trong mã nguồn.

```powershell
dotnet run --project src/HotelOps.Api --no-launch-profile -- --bootstrap-admin
```

Bootstrap từ chối khi đã có user/role. Nó tạo danh mục mã permission hiện hữu và admin chỉ có quyền quản trị người dùng/quyền/danh mục, không cấp sẵn các vai trò tác nghiệp. Role/grant/branch access và membership phải được quản trị cấu hình rõ ràng. Department Head dùng `DEPT_MANAGER` cùng các membership `DEPARTMENT_AUTHORITY` cho những phòng ban được quản lý trong chi nhánh. Permission catalog không tự chứng minh một vai trò được phép làm nghiệp vụ.

Project dùng User Secrets ID riêng `hotelops-compact-v2-…`, tách khỏi cấu hình legacy. Trong môi trường local đã cấu hình kết nối tới `HotelOpsCompactDB` và kiểm tra khởi động thực tế. Theo yêu cầu tiếp theo, database chính đã có 11 tài khoản và dữ liệu kiểm thử; xem [TEST-DATA.md](TEST-DATA.md) để đăng nhập và dùng các tình huống mẫu. Không chạy bootstrap lại trên database đã seed.

`Compact:CorsOrigins` là danh sách origin frontend được phép. Bearer session không dùng cookie; frontend gửi `Authorization: Bearer <accessToken>`. Không lưu token vào URL. User mới có mật khẩu tạm chỉ được gọi me/password/logout cho tới khi đổi mật khẩu. Đổi mật khẩu thu hồi mọi session; phải đăng nhập lại. `/health` không trả dữ liệu nghiệp vụ. OpenAPI tại `/openapi/v1.json` trong Development.

## Hợp đồng API

Prefix mới **`/api/v2`**; frontend cũ cần cập nhật. Thao tác trả DTO; resource chung có `{ type, fields }`, tên field camelCase. `rowVersion` là chuỗi base64 từ SQL rowversion. Request ghi truyền phiên bản hiện tại; action còn yêu cầu `operationKey` UUID khác rỗng. Phiên bản cũ hoặc thao tác lặp trả 409; client tải lại dữ liệu trước khi quyết định gửi lại. Các command dùng transaction serializable, kiểm tra scope và ghi audit. Không coi 409 là thành công ngầm.

| Nhóm | Endpoint chính | Hành vi |
|---|---|---|
| Auth | `POST auth/login`, `GET auth/me`, `POST auth/password`, `POST auth/logout` | Session token hash, lockout, expiry, đổi mật khẩu, thu hồi |
| Quản trị | `users`, `users/{id}/memberships`, `users/{id}/roles`, `users/roles/{id}/permissions` | Quyền global riêng; bảo vệ admin cuối và trách nhiệm đang mở |
| Danh mục | `GET/POST catalog/{resource}`, `PUT catalog/{resource}/{id}` | Allowlist field; branchId trên query cho danh mục chi nhánh; rowVersion khi sửa |
| Công việc | `GET/POST work`, `GET work/{id}`, `POST work/{id}/actions/{action}` | Cleaning/rework/inspection/incident/maintenance/operational/internal request |
| Người nhận việc | `GET work/candidates?branchId=…&kind=…` | Danh sách người hoạt động, có branch access và quyền thực hiện tương ứng |
| Checklist/findings | `PUT work/{id}/checklist/{itemId}`, `POST work/{id}/findings`, `POST work/{id}/findings/{findingId}/resolve` | Kết quả kiểm tra, finding và xác nhận khắc phục |
| Liên kết | `POST work/{id}/links` | Dependency cùng chi nhánh; chống vòng DEPENDS_ON |
| Phòng | `GET rooms`, `GET rooms/{id}/history`, `POST rooms/{id}/actions/{action}` | Readiness/occupancy; check-in/check-out/move/block/unblock |
| Kho/chứng từ | `GET/POST documents`, `GET documents/{id}`, `POST documents/{id}/lines`, `PUT documents/{id}/lines/{lineId}`, `POST documents/{id}/actions/{action}` | Request, approval, receipt, issue, transfer, return, stocktake, adjustment, reversal |
| Tài sản | `catalog/assets`, `GET assets/{id}`, `POST assets/{id}/actions/{action}` | Hồ sơ, move, condition, handover, accept/reject, retire |
| Mẫu/lịch | `GET templates`, `POST templates/checklists`, `POST templates/schedules`, `POST templates/schedules/{id}/disable` | Checklist phiên bản bất biến; lịch công việc/maintenance |
| Thông báo | `GET notifications`, `POST notifications/{id}/read`, `POST notifications/{id}/acknowledge` | Chỉ recipient, kiểm quyền nguồn trước khi hiển thị |
| Bằng chứng | `POST evidence`, `GET evidence/{id}`, `POST evidence/{id}/invalidate` | Multipart PNG/JPEG/PDF ≤10 MB; SHA-256, storage key nội bộ |
| Theo dõi | `GET insights/dashboard`, `GET insights/reports/{report}`, `GET insights/audit`, `GET insights/time` | Branch scope, kiểm quyền nguồn; thời gian theo task, không có workday |

Catalog resources: `branches`, `departments`, `locations`, `room-types`, `rooms`, `warehouses`, `items`, `stock-policies`, `references`, `assets`, `roles`, `permissions`. Permissions chỉ đọc qua API; không cho client tạo mã quyền tùy ý. Không có catalog shifts hoặc endpoint workday/time correction.

Work actions: `assign`, `accept`, `start`, `progress`, `pause`, `resume`, `complete`, `verify`, `pass`, `fail`, `cancel`, `reopen`, `submit`, `route`, `reject`, `handover-offer`, `handover-accept`, `handover-reject`, `triage`, `escalate`. Mỗi loại/trạng thái chỉ chấp nhận các action phù hợp. `userId` dùng khi assign/handover; `relatedId` dùng cho phòng ban đích khi route hoặc sự kiện bàn giao khi quyết định; `progress` từ 0–100; triage dùng `priorityId`, `severityId`, `categoryId`, `dueAt`. Pause/reopen/cancel/fail cần lý do 10–2000 ký tự. Không cho client ghi trực tiếp status hoặc thời gian đã chốt.

Document actions: `submit`, `approve`, `reject`, `request-information`, `count`, `cancel`, `close`, `post`. `approve` có thể truyền `approvedLines: [{lineId, quantity}]` cho tất cả các dòng; lượng duyệt không vượt lượng yêu cầu. Người duyệt độc lập với người yêu cầu/cấp phát. MR dùng người đứng đầu được map phòng ban; fallback khi người yêu cầu cũng là head dựa trên phòng ban kho. Adjustment phải khớp chênh lệch count và từ chối nếu tồn đã thay đổi sau kiểm kê. Ledger không sửa/xóa; reversal phải trỏ chính xác giao dịch gốc.

Lịch chỉ nhận `MAINTENANCE`/`OPERATIONAL`, tần suất `DAILY`/`WEEKLY`/`MONTHLY`. Worker mỗi 60 giây tạo occurrence, thông báo quá hạn và tồn thấp; dùng app lock để tránh nhiều instance chạy trùng. Có thể tắt khi test bằng `Compact:WorkerEnabled=false`. Bằng chứng mặc định nằm trong `App_Data/compact-evidence`, không được phục vụ như static files; đặt `Compact:EvidenceDirectory` tới nơi lưu trữ bền vững khi vận hành.

Cảnh báo tồn thấp dùng `inventory_policies.low_stock_episode` và `alerted_at`: không lặp trong cùng đợt thiếu hàng; khi tồn phục hồi thì kết thúc đợt, lần thiếu tiếp theo có khóa mới. Không cần bảng outbox riêng ngoài `notifications` hiện có.

## Kiểm chứng và phạm vi

`scripts/database/compact-v2/Test-Backend.ps1` tạo database test có tên sinh ngẫu nhiên, bootstrap và seed **chỉ fixture tổng hợp trong DB test**, chạy HTTP thật rồi dừng process API do script tạo. Database test được giữ để chẩn đoán; database chính và cũ không nhận fixture. Log và kết quả nằm trong `artifacts/compact-v2/backend/`. Không dùng script này để benchmark.

Project `tests/HotelOps.Compact.Tests` nằm trong solution, chạy bằng `dotnet test HotelOps.slnx`; kiểm tra mapping 37 bảng/6 view, nullable provenance, state machine và việc loại credential khỏi DTO. Bộ SQL `Test-Compact.ps1` kiểm chứng các ràng buộc lưu trữ riêng. Bộ test legacy không đồng nghĩa đã kiểm chứng API v2.

Kiểm thử worker dùng `Test-Worker.ps1 -Server <server> -Database <database API test>`; script chỉ nhận tên database fixture sinh bởi Test-Backend, kiểm tra chính xác một test đã thực thi thành công qua TRX. Nó gọi trực tiếp hai lượt `OperationsWorker.Tick`, kiểm chứng occurrence, tiến lịch và dedup cảnh báo. Test này mặc định được skip khi chạy solution không có biến môi trường database test; đã chạy riêng thành công trên SQL Server local.

### Kết quả ngày 21/11/2026

| Kiểm chứng | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx --no-restore` (không dùng shared compiler) | **Passed, 0 lỗi, 0 warning** |
| Bộ test Compact thông thường | **23 passed** |
| HTTP thật qua `Test-Backend.ps1` | **28 passed, 0 failed** |
| Worker trên database fixture | **1 passed**, gồm 3 nhóm kiểm tra sau hai lượt chạy |
| SQL regression trên Compact chính | **58 passed, 0 failed, 0 hàng fixture còn lại** |
| Startup bằng User Secrets thực tế | `/health`: `ok`, schema `2.0.0`, `37` bảng |
| Catalog/DBCC | **37 bảng, 152 FK, 77 CHECK, 26 trigger, 6 view**; không phát hiện lỗi |
| Database legacy | Không có thao tác ghi; SHA-256 inventory metadata trước/sau giống nhau |

HTTP coverage gồm auth/thu hồi session/mật khẩu tạm, scope khác branch, rowversion cũ, task và time intervals, Internal Request, maintenance/incident, handover, inspection/Ready/Reception, partial material issue/ledger, ngăn mượn quyền Reception/Storekeeper từ role khác, thông báo, dashboard/report/audit, asset handover, evidence byte integrity, schedule create/disable và last-admin. Kết quả máy đọc được nằm trong `artifacts/compact-v2/backend/verification.json`, `worker-verification.json`, `worker.trx` và `artifacts/compact-v2/verification.json`.

EF scaffold dùng tool 9 với runtime 10 đã báo cảnh báo phiên bản và nullable alternate key. Mappings đã được chuẩn hóa về scalar, giữ FK tại SQL Server và tắt OUTPUT trên bảng có trigger; model tests xác nhận nullable provenance và 37 bảng/6 view. Không dùng `EnsureCreated` hoặc EF migration.

Các file chính: `Program.cs`, `Compact/CompactHost.cs`, `Compact/Data`, `Compact/Entities`, `Compact/Services`, `Compact/Security`, `Compact/Controllers`, `Compact/Contracts`, project test Compact và các script kiểm chứng. Source legacy còn lưu để đối chiếu, không chạy trong host v2. Đây chưa phải chứng nhận đủ 144 UC hoặc kiểm thử tải, concurrency mọi cặp command, mọi tổ hợp transfer/return/reversal và browser end-to-end. SQL tests bảo vệ các invariant nhưng không thay thế UAT toàn bộ nghiệp vụ.

Các quyết định còn pending trong báo cáo giản lược vẫn chưa được coi là phê duyệt: occupancy reversal, chuyển occupancy liên chi nhánh, bỏ xác nhận bàn giao, chỉnh sửa giờ đã chốt và các thay đổi permission matrix chưa chốt. Không có endpoint tự cấp các quyền đó. Frontend, chuyển dữ liệu cũ, triển khai production và benchmark ngoài phạm vi chuyển backend này.
