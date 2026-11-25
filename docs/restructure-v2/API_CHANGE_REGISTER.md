# API change register V2

Các route dưới prefix `/api/v2`; controllers chỉ chuyển tiếp HTTP sang services. Quyền phải được kiểm ở backend theo role, chi nhánh, chủ thể và trạng thái, không dựa vào nút UI.

| Route / nhóm | Hợp đồng / validation | Bằng chứng hiện có |
|---|---|---|
| GET work/room-issues | page tùy chọn,25 dòng; search tên sự cố/số phòng trước count/paging; bỏ page giữ array. Không cắt ngầm200 hồ sơ, giữ role/branch/owner scope. page<1 hoặc>1000000 trả400 | Build137PASS/3SKIP và review; runtime mới đang chạy |
| POST work/room-issues/{id}/recover | HKS đúng ca/chi nhánh xác minh incident legacy OPEN chưa phân công/workflow/repair. Giữ author/time/parent/turnover; chỉ thêm SOURCE từ ancestry đã kiểm; ghi audit rồi chuyển bảo trì, không tự Ready | Review độc lập sau sửa SOURCE không finding; synthetic runtime đang chạy, không thao tác incident653 |
| POST work/room-issues; POST work/room-issues/{id}/request-material | Số báo thiếu/bổ sung không vượt thiếu đánh giá và lượng đã cấp/cam kết. Reclaim từ kho chỉ khi phiếu đóng hoặc hoàn thực, HKS đúng ca, transaction/version/queue guard vẫn giữ; creator từ báo cáo gốc, approver là HKS |10 regression mới, build137PASS/3SKIP; HTTP quantity/creator/requeue đang chạy |
| GET/POST supplies/room-assessments/{workId} | GET theo quyền xem work; POST chỉ người được giao trong ca, kiểm số lượng/snapshot/version | 11 service/SQL tests và chuỗi Housekeeping HTTP/SQL đạt; còn UI |
| GET/PUT inventory-lifecycle/profiles | Phân loại theo chi nhánh; vật tư mới bắt đầu UNCLASSIFIED/UNKNOWN; OM cấu hình; không đổi nghĩa số dư sống | API tạo danh mục → cấu hình → receipt đã đạt |
| GET inventory-lifecycle/batches; PUT batches/{id}; GET batches/{id}/history | Metadata nguồn, hạn dùng, khả dụng và lịch sử; từ chối đổi identity lô đã xuất. GET hỗ trợ page>=1/pageSize 1–100; có page trả items/total/page/pageSize, bỏ page giữ array cũ không cắt ngầm. Batches hỗ trợ search tên/mã lô/kho trước phân trang, giữ branch/item scope. | transfer-batch-pages-thirteen-pass.json: array compatibility, trang đầu/cuối/rỗng, invalid 400, sai branch 403, browser history/search đạt |
| GET reusable-inventory, destinations, history | Số dư/vị trí/lịch sử theo quyền | HTTP/SQL branch negatives đạt |
| POST reusable-inventory/positions/{id}/move | Chuyển trạng thái/vị trí với version, operationKey, số lượng, lý do | Ownership và retry đã đạt |
| POST positions/{id}/write-off; GET write-offs; POST write-offs/{id}/decision | Thủ kho đề nghị; OM cùng chi nhánh duyệt độc lập; từ chối có lý do | HTTP/SQL approved/rejected/idempotent đạt |
| PUT documents/{id}/adjustment-batches | Snapshot lô và lượng điều chỉnh; đối chiếu lại trước approve/post | HTTP/SQL chọn lô sai, mixed adjustment và partial approval đã đạt |
| documents/{id}/actions/{action}, derive | Chỉ post mới ghi sổ; partial transfer nhận thực tế; không double post/receipt | Reusable transfer và adjustment đạt; direct transfer discrepancy cần thêm regression |
| work/replacements; work/{id}/replacement | Queue/history theo domain; canDecide chặn người đề xuất và việc terminal | 16 service/SQL tests; browser hộp yêu cầu đạt, chưa toàn chuỗi thay tài sản |
| work/{id}/replacement/{command} | propose/confirm/approve/reject/install; độc lập người duyệt, phòng và asset thực tế | 16 service/SQL tests, chưa toàn chuỗi HTTP/UI |
| work/room-issues/{id}/investigation | HKS dùng IR_PROCESS; giám sát bảo trì dùng INCIDENT_MANAGE, đúng đường cấp quyền role/chi nhánh và đang trong ca; lưu lịch sử | maintenance-fifteen-pass.json và browser chọn người liên quan đạt; reporter/discoverer không đổi |
| POST advisory | Kind và horizon hợp lệ, branch scope, thống kê/rules được ghi nhãn; không mutation | Fallback và scope API đạt; external blocked |
| notifications, notifications/{id}/read, SignalR | Chỉ người nhận đủ quyền, lọc lại khi mất role/session; phát snapshot sau commit | SignalR lượt sau 15 đạt; expiry persistence/reopen API/SQL đạt |
| GET/POST/PUT work/internal-request-types | RecipientRoleIds cấu hình vai trò nhận; backend kiểm người được chọn đang giữ vai trò hợp lệ tại chi nhánh. Legacy config đọc được, chỉ đổi sang roles khi lưu; không viết lại yêu cầu cũ | configuration-report-reviewed-fifteen-pass.json: role filter, receiver sai bị403, thiếu receiver400, createdBy giữ người gửi |
| GET/PUT work/room-issue-types | CreateNew tạo loại tùy chỉnh thuộc BROKEN/MISSING; ReferenceId+RowVersion chỉnh sửa; không đổi family/chi nhánh, không đè hai loại mặc định; mã tối đa59 ký tự | configuration-report-reviewed-fifteen-pass.json: tạo qua API/UI, duplicate409, foreign403 |
| GET insights/reports/operations-events | REPORT_VIEW và quyền domain tại chi nhánh; sự kiện ca/phòng/incident/kho/tài sản và audit AUTO_END, tối đa10000. localDates=true đổi ngày theo múi giờ chi nhánh, mặc định giữ timestamp UTC tương thích | configuration-report-reviewed-fifteen-pass.json: shift/incident thực, AUTO_END đúng một lần, range400/branch403, local-day=UTC, mobile |

Account invitation/setup/resend/delivery sử dụng controller và DTO chuyên biệt, token hash một lần, hạn dùng và backend first-login enforcement. Đăng nhập/khôi phục chỉ Username, không EmployeeCode. Bằng chứng mới nhất `login-recovery-username-nineteen-pass.json` gồm19 ca với SMTP sink; không có email thật được gửi. Thời hạn lời mời24giờ, reset30phút; không lấy bằng chứng fixture thời gian thay thời lượng rate-limit/backoff thực.

Lỗi validation dùng 400; mất quyền 403; không tìm thấy 404; xung đột trạng thái/version/operation 409 theo `ApiException`. Không công bố EF entities trực tiếp; phản hồi là DTO/projection/Output.Record theo hợp đồng Compact hiện hành.

Giới hạn: bảng này chưa phải OpenAPI snapshot hoàn chỉnh. Đăng ký mọi route controller không đồng nghĩa chúng đã có E2E PASS.
