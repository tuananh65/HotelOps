# Hợp đồng API cần đối chiếu với back-end

Ngoại trừ hai GET sự cố bên dưới, toàn bộ hợp đồng trong tài liệu này là **API đề xuất – cần đối chiếu với back-end**. Không có JWT, refresh endpoint, controller nghiệp vụ hoặc CORS được xác nhận chỉ bằng suy luận từ schema SQL.

## Phần đã được xác nhận bằng source

| Method | Route | Kết quả trong controller | DTO |
|---|---|---|---|
| GET | `/api/incidents` | 200, một JSON array; không có tham số phân trang trong method | `IncidentListDto[]` |
| GET | `/api/incidents/{id:long}` | 200 hoặc 404 rỗng | `IncidentDetailDto` |

Nguồn: `src/HotelOps.Api/Controllers/IncidentsController.cs`, `DTOs/Incidents/IncidentListDto.cs`, `IncidentDetailDto.cs`, `IncidentEventDto.cs`, tại commit `c6fcb6d00289c4d0ccbb20c16031b1d7ed1a5ccd`. Program dùng cấu hình JSON mặc định của ASP.NET Core, không có cấu hình đổi tên thuộc tính riêng được tìm thấy; client dùng camelCase tương ứng. Đây là xác nhận bằng source, chưa xác nhận response thực tế qua HTTP.

`IncidentListDto` có incidentId, incidentNo, branchId/Name, roomId/Number, areaId/Name, assetId/Name, incidentTitle, severityId/Name, priorityId/Name, status, reportedBy/Name, discoveredAt, reportedAt, resolvedAt và closedAt. Detail bổ sung description, closureNote và events. Event có eventId, incidentId, eventType, fromStatus, toStatus, note, actorId, actorName và occurredAt. Client không yêu cầu các DTO này phải có `attributes` hay PageDto.

Các DTO hiện có chưa có assignee, deadline, rowVersion hoặc allowedActions. Các thông tin bổ sung để thao tác phải được cung cấp bởi context đề xuất. Nếu thiếu capability/concurrency, UI không tự bật nút bằng cách suy đoán quyền. API đọc sự cố hiện có chưa giới hạn quyền/phạm vi trong controller/service đã đọc; nhóm back-end cần kiểm tra lại phần này trước sử dụng vận hành.

## URL và transport

`VITE_API_BASE_URL` là origin hoặc prefix ứng dụng **không bao gồm `/api`**; các route trong `endpoints.ts` có `/api`. Để rỗng chỉ khi API được reverse proxy cùng origin. `VITE_AUTH_MODE` nhận `cookie`, `bearer`, hoặc mặc định `unconfigured`. `VITE_CSRF_HEADER` là tên header theo thỏa thuận, mặc định đề xuất `X-CSRF-TOKEN`.

Client hỗ trợ query, JSON, multipart, JSON rỗng/204/205 và tải Blob. Request có timeout 20 giây và có thể hủy các lần đọc cũ khi đổi route/phạm vi. Client không tự retry các lệnh ghi. HTTP 202 chưa phải hoàn thành nghiệp vụ; client yêu cầu tải lại để xác nhận kết quả. Khi server xác nhận rollback bằng MSG06, lỗi không bị đánh dấu nhầm là kết quả ghi chưa biết.

CORS cần cho phép đúng origin chạy front-end, các method/header thực dùng. Với cookie, cần cho phép credentials và cơ chế CSRF phù hợp; không dùng wildcard origin cùng credentials. Với bearer, cho phép Authorization. Cookie flags, domain, SameSite và CSRF bootstrap là nội dung nhóm back-end phải chốt. Không có code back-end/CORS được thêm trong nhiệm vụ này.

## Phiên và quyền

| Method | Endpoint | Request | Response |
|---|---|---|---|
| POST | `/api/auth/login` | LoginRequest `{username,password}` | SessionDto |
| GET | `/api/auth/session` | Không body | SessionDto; 401 nếu không có phiên |
| POST | `/api/auth/logout` | Không body | 204 hoặc body rỗng; server thu hồi phiên |
| POST | `/api/auth/password` | PasswordRequest `{currentPassword,newPassword}` | 204; cookie mode đọc lại session sau thay đổi |

SessionDto: principal, expiresAt, mustChangePassword, accessToken tùy bearer, csrfToken tùy cookie. Principal chứa id/name/username/email, roleNames, primaryBranch, primaryDepartment, branches, departments và grants. Mỗi grant có useCase, branchIds, departmentIds và ownOnly tùy chọn. `null` nghĩa là toàn bộ phạm vi đã được server cho phép ở chiều đó; `[]` nghĩa là không có phạm vi. Không ghép branch của grant này với department của grant khác để tạo quyền mới.

Client không lưu cookie/token/password/phiên vào localStorage/sessionStorage. Bearer token là opaque string trong bộ nhớ, không giả định là JWT, không tự refresh; reload cần đăng nhập lại. Cookie mode khôi phục qua GET session. `expiresAt` và sliding session phải được server quản lý; hoạt động pointer/keyboard có thể kiểm tra lại session, có giới hạn 60 giây, không tự kéo dài phiên chỉ bằng bộ đếm client.

Quyền ẩn nút/chặn route hỗ trợ giao diện. Server phải kiểm tra lại phiên, UC, branch, department, assignee, phân tách người yêu cầu/người duyệt và trạng thái hiện tại trên mọi request. Quyền sửa thời gian cần `work_interval.correct`, không chỉ UC-111.

## DTO chung và phân trang

`src/api/types.ts` là định nghĩa TypeScript đầy đủ. EntityDto đề xuất là read projection với `id`, `code`, `name`, `statusCode`, `branchId/Name`, `departmentId/Name`, `rowVersion`, `createdAt`, `allowedActions`, `canEdit` và `attributes`. Các trường của attributes theo module nằm trong `api-dto-fields.json` và `contract-examples.json`. CriterionResultDto đề xuất dùng PASS/FAIL/NA; trong tái kiểm tra, UI hiển thị PASS/FAIL thành Fixed/Not Fixed theo ngữ cảnh.

EntityWriteDto chứa `name`, `code`, `branchId`, `departmentId`, `rowVersion` khi cập nhật, cùng attributes chỉ lấy từ whitelist biểu mẫu. Không gửi ID người đang thao tác để giả mạo actor; không gửi status, số dư kho, audit hoặc thời gian thực hiện được tính ở client. Update user chỉ gửi tên/email/phone; đổi scope và role dùng command riêng. User creation/reset trả `UserProvisioningDto {record,temporaryPassword,validForHours}` để hiển thị mật khẩu tạm một lần; mật khẩu không đi vào cache bản ghi.

ID đề xuất là JSON string. Với SQL BIGINT từ API sự cố hiện tại, client chấp nhận số nguyên an toàn và chuyển sang chuỗi; số vượt độ chính xác JavaScript bị từ chối. DTO không dùng tên cột SQL làm tên JSON mặc định. Resource tổng hợp `locations` phải cung cấp ID không trùng giữa floor/area, ví dụ `floor:17`, `area:17`; các reference floorId/areaId phải dùng cùng định danh đó ở biên API. `reference` và nguồn công việc maintenance tổng hợp cũng cần định danh rõ loại nguồn. Server/adapters chịu trách nhiệm chuyển sang khóa SQL, không lấy hai identity của hai bảng làm cùng một ID UI.

PageDto: `{items,page,pageSize,totalCount,snapshot}`. List đề xuất nhận `page` (bắt đầu 1), `pageSize` (client đọc 100), `snapshot` cho các trang tiếp theo, và `branchId` khi chọn chi nhánh. Client tải toàn bộ các trang theo một snapshot rồi dùng bảng/filter/phân trang 20 dòng sẵn có của UI. Snapshot thay đổi hoặc hết trang trước totalCount là lỗi, không được coi là danh sách đầy đủ. Nếu cần pagination/filter hoàn toàn ở server cho dữ liệu lớn, có thể thay controller này; bộ lọc hiện tại được giữ nguyên nghĩa trên toàn bộ tập dữ liệu đã tải. API sự cố hiện có vẫn trả array, không bị ép thành PageDto.

Timestamps đề xuất dùng ISO 8601 có Z/offset, lưu UTC và hiển thị Asia/Ho_Chi_Minh. Giá trị từ datetime-local được chuyển từ UTC+7 sang UTC, không phụ thuộc múi giờ máy chạy trình duyệt. DateTime trong DTO C# hiện có cần được xác nhận có Kind/offset rõ ràng khi serialize. Không suy diễn trạng thái SQL cũ sang trạng thái SRS nếu chưa có mapping chính thức.

## Context đọc của màn hình

`GET /api/ui/screens/{screenId}/context?branchId=&recordId=&roomId=&sourceRequestId=` là **đề xuất mới**, không có trong controller hiện tại. Đây là dependency cần thiết của bản triển khai này. Screen 5 dùng `GET /api/dashboards/operations`; Profile/Change Password lấy người dùng từ session; Sign In/Forgot Password không yêu cầu context.

Context trả `ScreenContextDto {snapshot,collections,balances?,metrics?,missingSources?}`. Collections dùng các key của `resourcePaths`, là các EntityDto cần cho màn hình: danh mục có thể chọn, tên tham chiếu, lịch sử, bản ghi liên quan và capabilities. Context của từng màn hình phải đủ tập dữ liệu liên quan mà các thành phần UI đọc; nếu không có dữ liệu thì trả mảng rỗng. Trường không lấy được phải được ghi trong missingSources; không trộn dữ liệu demo vào phần thiếu.

Endpoint chỉ trả dữ liệu người dùng được đọc/chọn cho UC hiện tại. Việc biết screenId không cấp thêm quyền vào User List, Audit Log hoặc kho/chi nhánh khác. Context nên trả projection tối thiểu, không trả toàn bộ EF entities hay toàn bộ database cho tất cả người dùng. Nếu nhóm back-end không chọn API aggregate này, sửa `loadScreen` để ghép các API lookup/related hiện có và giữ nguyên DTO/cache boundary; không phải sửa toàn bộ 80 màn hình.

Với màn maintenance detail, ID work và ID incident được tách riêng. Work projection có incidentId hoặc scheduleId theo nguồn; khi là incident repair, context phải chứa incident tương ứng cùng capability/token. Các nút thực hiện gọi endpoint sự cố bằng incidentId, không giả định workId bằng incidentId.

BalanceDto là số dư do server tính theo warehouseId/itemId và mốc `at` tùy chọn. Nếu ledger hiển thị số dư tại thời điểm giao dịch, context phải trả số dư lịch sử tương ứng; client không cộng một trang ledger để suy ra tồn. Không có balance trả về thì hiển thị N/A. Dashboard metrics đã dùng: readyRooms, activeRooms, openIncidents, overdueTasks, lowStockItems. Mọi số liệu trên dashboard dùng snapshot server.

## Các thao tác nghiệp vụ

103 operation contract được liệt kê đầy đủ ở `endpoints.ts` và `api-mapping.md`. Mỗi operation có ID capability riêng, ví dụ `cleaning.submit-for-inspection`, `inspections.approve`, `materials.partial-issue`, `transactions.confirm-receipt`. Không có endpoint cập nhật status dùng chung.

CommandRequest gửi `rowVersion`, reason/note nếu thao tác có yêu cầu, và input riêng. Không gửi trường trạng thái đích. Response thông thường là EntityDto kết quả. Nếu trả 204, client GET lại bản ghi trước khi báo thành công; create phải trả EntityDto có ID. Các mutation phải trả rowVersion và capability mới sau chuyển trạng thái. Việc khớp version và ghi nhiều bảng phải kiểm tra trong transaction server theo GBR-005/006/007.

| Thao tác với dòng | DTO input cho từng dòng |
|---|---|
| Duyệt yêu cầu vật tư | lineId, approvedQuantity |
| Xuất một phần | lineId, issuedQuantity |
| Ghi sổ nhập | lineId, receivedQuantity |
| Ghi sổ hoàn kho | lineId, acceptedQuantity |
| Xác nhận nhận chuyển kho | lineId, receivedQuantity, damagedQuantity, missingQuantity, reason nếu có |
| Kiểm kê | lineId, countedQuantity |
| Trả vật tư chưa dùng | lineId, returnedQuantity |

Số lượng requested/approved/issued đã tích lũy và conversion factor từ server không được gửi ngược để sửa trực tiếp. API phải giữ các số lượng riêng và cập nhật theo transaction, không tin số dư/tổng do client tính. Bản nháp có thể có request quantity; ghi sổ, đảo, điều chỉnh và xác nhận nhận có endpoint khác nhau.

Các endpoint bổ sung về assign/reassign, criteria, workdays, corrections, reinspection và liên kết công việc nằm trong phần cuối bảng API. Hai nút generate lịch vốn có trong prototype được chuyển thành POST server; đây là trigger thủ công đề xuất và cần chốt quyền riêng, trong khi job định kỳ vẫn chạy trên server theo SRS. Create Branch/Reactivate Room và các lựa chọn thêm của UI cũ cũng cần nhóm xác nhận; UI chỉ bật action nếu server cấp capability tương ứng.

## Attachment và báo cáo

POST `/api/attachments` dùng multipart key `files`, tối đa 5 JPG/PNG/PDF, mỗi file tối đa 10 MB. Response AttachmentDto[] có id/name/url/contentType. Kiểm tra nội dung/định dạng ở server vẫn bắt buộc. Tạo hoặc thêm evidence gửi attachmentIds. Với bản ghi chưa được tạo, upload API cần một cơ chế staging/token rồi bind trong giao dịch tạo; SQL attachments hiện yêu cầu entity_type/entity_id nên không thể tự giả gắn entity_id=0. Cơ chế staging và dọn file chưa bind cần nhóm back-end xác nhận. Read projection có thể trả attributes.attachments để gallery dùng URL server; không lưu base64. URL protected attachment cần cơ chế download/signed URL phù hợp với chế độ auth; bearer header không tự được gắn cho thẻ img/a.

GET `/api/reports/{type}` nhận from/to và scope/filter, trả ReportSnapshot gồm snapshotId, snapshot, key, from, to, columns, rows, totalCount và missingSources tùy chọn. POST `/api/reports/snapshots/{snapshotId}/export` với `{format:"CSV"|"XLSX"|"PDF"}` trả file Blob. SnapshotId phải gắn đúng người dùng, quyền và bộ lọc trên server. Server ghi audit export và từ chối kết quả trên 50.000 dòng; không xuất bản dữ liệu bị cắt cụt. Client không còn tự tạo file báo cáo từ database trình duyệt.

## Lỗi

Hỗ trợ ASP.NET Core ProblemDetails/ValidationProblemDetails: type/title/status/detail/instance/errors, cùng code/parameters/traceId tùy thỏa thuận. Ví dụ lỗi trường `Attributes.RoomId` được đưa về field `room`; lỗi `Lines[0]`/`Results[0]` được hiển thị ở nhóm tương ứng và gộp các thông báo. Cookie/session lỗi trả 401, thiếu quyền 403, không tồn tại 404, cạnh tranh/xung đột 409 hoặc 412, validation 400/422, lỗi máy chủ 5xx.

Server cần phân biệt rollback đã xác nhận (MSG06), kết quả ghi chưa biết (MSG07), stale version (MSG05), action đã xử lý (MSG28), thiếu tồn (MSG16), lỗi độc lập người duyệt (MSG22) và sai trạng thái (MSG24). Nội dung chi tiết và tham số phải phù hợp SRS; không gửi secret vào lỗi/audit.
