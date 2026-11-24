# Lễ tân: phạm vi yêu cầu và thao tác phòng trong ca

## Thực hiện

- Bỏ tab/chức năng sự cố khỏi workspace lễ tân; URL kind=INCIDENT quay về loại yêu cầu được phép. Giữ grant hiện có, không thu hồi quyền database hoặc ảnh hưởng role khác.
- Nhân viên không còn nút Đang xem việc của tôi; yêu cầu nội bộ cố định cá nhân. Backend giới hạn người tạo, người nhận phân công hiện hành/lịch sử hoàn thành/hủy và lời mời bàn giao chưa xử lý. Thẩm quyền bộ phận và vai trò quản lý chỉ mở phạm vi khi permission/scope hiện có cho phép.
- Chặn mở trực tiếp ID/related ngoài quyền và áp dụng cùng predicate cho báo cáo, dashboard/overview. Không chỉ ẩn nút.
- Lễ tân gộp CLEANING_IN_PROGRESS, chờ dọn, chờ kiểm tra và làm lại thành Cần dọn dẹp; số đếm và nhãn thống nhất, không sửa lifecycle database.
- API đặt/hủy đặt/nhận/trả/chuyển phòng yêu cầu ca WORKING đã bắt đầu, chưa kết thúc, đúng ngày/chi nhánh và trong khung giờ theo múi giờ chi nhánh. Kiểm tra trong transaction Serializable, không lệ thuộc tác vụ kết thúc ca chạy mỗi30giây. UI lấy canOperate, thông báo tiếng Việt và liên kết lịch làm việc; refresh mỗi30giây khi không mở form. Giữ block/unblock theo quyền quản lý riêng.

## Kiểm chứng

- Backend build cuối: PASS,0warning/0error. .NET tests:56PASS,3SKIP(opt-in account/worker integrations).
- Frontend TypeScript+Vite build:PASS. Workflow5PASS. Browser mới và room-board regression(mockAPI)PASS,1440/768/375px, không pageerror.
- SQL/API:29 nhóm PASS trên HotelOpsCompactApiTestd51a06f0e2df; backup fixture trước upgrade. Bao gồm chưa bắt đầu/hết ca, chuyển đặt và chuyển khách đã ở, mine=false, detail/related, báo cáo/overview, lịch sử hủy, quản lý giữ queue.
- Grants thực tế đã đọc: Reception/HK Staff/Maintenance Staff có IR_VIEW BRANCH và ATTENDANCE_RECORD OWN; không thay grants để test pass.
- Hai sub-agent explore đã review độc lập, không ghi source. Fullstack delegate không khởi động được do Windows command line quá dài; primary triển khai.

## Files

Frontend: frontend/src/compact/app.tsx, work.tsx, rooms.tsx, api.ts; frontend/tests/compact-reception-access.mjs mới; frontend/tests/compact-reception-rooms.mjs cập nhật fixture và BASE_URL.

Backend Services: RoomService.cs, mới RoomService.Attendance.cs; WorkService.cs, mới WorkService.RequestVisibility.cs và InternalRequestVisibility.cs; InsightService.cs, InsightService.Owner.cs, InsightService.OwnerReports.cs.

TestSQL: scripts/database/compact-v2/Test-Backend.ps1, mới test-reception-access.ps1. Docs: docs/ui/compact-ux.md và plan/report này. Không sửa generated entities, schema, grants hay Project1. Không commit/push.

## Lỗi trong quá trình kiểm chứng

Fixture đầu gọi test trước bootstrap API; đã sửa thứ tự. Fixture ca thiếu membership/role có hiệu lực đầu ngày; fixture IR gửi sai tên field userId/relatedId; đã sửa dữ liệu/contract test, không giảm validation. Một build bị khóa DLL do test API chạy trước build hoàn tất(MSB3027/3021); sau test dừng, build lại sạch và chạy fixture lại bằng DLL mới. Publish có một retry MSB3026 file font, lần publish cuối sạch. Browser regression ban đầu dùng URL5186 cũ; đã cho cấu hình BASE_URL5082 và chạy lại PASS. Vite cảnh báo artifact outDir ngoài frontend/không tự xóa; Git có thông báo LF/CRLF, diff-check cuối PASS.

## Triển khai và giới hạn

Artifact: artifacts/production-261123-reception. API SHA256 A858E8051E89665551A90F5BE727986612558B575C07049C96A9BEF08B9C92A8; FE index-B-l-Gg0h.js. Không có thay đổi DB primary; chỉ cập nhật source runtime. Test ghi chỉ trên fixture; Production chỉ smoke test đọc. Frontend Production cục bộ là bản build chạy Vite preview, chưa đăng ký Windows service tự khởi động.

Bằng chứng: artifacts/compact-v2/backend/reception-access-verification.json; artifacts/reception-access-ui/verification.json; artifacts/reception-rooms/verification.json.

**VERIFIED Production https://127.0.0.1:5080/**: bundle mới HTTP200, TLS được trình duyệt tin cậy, API health200/auth401, không pageerror. API PID48468; frontend listener31292/launcher40032. Test5187 và preview5082 đã dừng. Đối chiếu grants131/checksum19132 và rooms96/checksum546048433 không đổi trước/sau release. Runtime, smoke receipt, baseline tại artifacts/production-261123-reception/{runtime,verification,database-before,database-after}.json.

Nhật ký quyết định: giữ lifecycle vật lý và grants, thêm điều kiện đọc IR tại service để BRANCH không vượt ownership, dùng attendance thực tế thay vì account ACTIVE/lịch PLANNED. Đánh giá độc lập phát hiện các đường đọc phụ, đã sửa và kiểm thử trước deploy. Không có câu hỏi nghiệp vụ còn chờ.
