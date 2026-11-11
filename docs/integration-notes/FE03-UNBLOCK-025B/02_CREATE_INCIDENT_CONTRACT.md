# CreateIncident v1 candidate — PROPOSED

POST /api/incidents, cookie HotelOpsCookie; X-CSRF-TOKEN và antiforgery cookie. Frontend bootstrap GET /api/auth/csrf bằng transport auth/http hiện hữu; không bearer, không client reporter, không retry write. ValidateAntiForgeryToken và ApiCsrfMiddleware được giữ.

Hợp đồng đầy đủ type/required/null/errors/response: CREATE_INCIDENT_CONTRACT.json. Required: branchId>0, incidentTitle (không trắng, <=200), description (không trắng), discoveredAt. Ít nhất một roomId/areaId/assetId. Các ID nguồn/reference optional, omitted/null tương đương không chọn. Reporter optional/null lấy từ session; nếu client cũ gửi reporter khác actor:403. Backend long; frontend chỉ nhận/gửi safe integer.

Actor cookie phải khớp current-user accessor, active/nondeleted/employment ACTIVE và unrestricted; revalidate SQL session dưới khóa user/session trong transaction, quyền branch INCIDENT_CREATE trước và ngay trước commit. Branch active, room/floor/area phải cùng branch và active, parentage nhất quán; asset khóa và không RETIRED, current parents khớp lựa chọn; severity/priority active đúng group. RoomBlock/RoomStatus không được đọc để suy ra quyền hoặc ghi.

201 trả IncidentDetailDto + Location; frontend dùng subset được decode và hiển thị kết quả tại chỗ để hỗ trợ create-only actor.400 model/source/CSRF;401 session;403 permission/reporter/restricted;503 khi initial permission source unavailable. Thứ tự middleware có thể trả CSRF400 trước auth401 cho unsafe request thiếu token. Không có stable domain error code mới cho mỗi placement case; source errors dùng ProblemDetails.detail, model errors dùng ValidationProblemDetails.errors.

Giữ transaction, số incident, CREATED event, audit và trạng thái OPEN. SRS historical gọi Pending Triage và có ảnh/notification; task này không thêm status, upload hoặc notification chưa được duyệt. Ngoài UC049 create/options, baseline list/detail/status/triage và các actor field cũ không được retrofit authorization ở đây; Security/FE06 cần đánh giá trước acceptance rộng hơn.

