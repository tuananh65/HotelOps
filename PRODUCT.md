# HotelOps

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

HotelOps là ứng dụng nội bộ cho nhân viên và quản lý khách sạn, phối hợp công việc trong phạm vi chi nhánh được cấp quyền. Người dùng sử dụng cả máy tính và điện thoại. Nhóm vai trò và công việc cần ưu tiên trước chưa được xác định.

## Product Purpose

Hỗ trợ vận hành khách sạn và phối hợp công việc theo chi nhánh. Giao diện phải giúp người dùng tìm đúng công việc, hiểu trạng thái và thực hiện các thao tác được hệ thống cho phép.

## Operating Context

Các nhóm nghiệp vụ được mô tả trong tài liệu dự án gồm cấu trúc khách sạn, vận hành phòng, housekeeping, kiểm tra, sự cố và bảo trì, tài sản, kho, yêu cầu vật tư, bổ sung hàng, phân công, thời gian làm việc, yêu cầu nội bộ, thông báo và báo cáo. Danh sách này mô tả phạm vi sản phẩm, không xác nhận mọi chức năng đã hoàn thiện.

Frontend hiện có dùng React, TypeScript và Vite. Backend dùng một dự án ASP.NET Core Web API, .NET 10, Entity Framework Core và SQL Server theo Database First.

## Capabilities and Constraints

- Người dùng xác nhận phạm vi tài liệu là toàn bộ sản phẩm HotelOps.
- Giao diện cần sử dụng được trên máy tính và điện thoại; đây là web, chưa có yêu cầu ứng dụng native.
- Khi thiết kế lại frontend, đối chiếu nhánh `feature/react-frontend` và giữ đủ màn hình cùng chức năng. Người dùng cho phép thay đổi hoàn toàn diện mạo frontend hiện tại.
- Lập danh mục màn hình và hành vi trước khi sửa để kiểm tra tính đầy đủ; không dùng số lượng route thay cho kiểm chứng chức năng.
- Giữ hợp đồng API, kiểm soát quyền và phạm vi dữ liệu. Không tự tạo quyền, trạng thái hoặc chuyển trạng thái nghiệp vụ.
- Không sửa database schema, generated EF entities hoặc tạo migrations ngoài phạm vi được duyệt.
- Department Head quản lý các phòng ban trong phạm vi chi nhánh, không mặc định là quản lý duy nhất một phòng ban.
- Reception được tách khỏi Operation Manager; quyền và chuyển trạng thái phải dựa trên quyết định đã được duyệt cho từng nghiệp vụ.

## Evidence on Hand

- `docs/requirements/HotelOps_SRS_DRAFT.md`: SRS lịch sử, không phải toàn bộ đặc tả cuối cùng đã duyệt.
- `docs/requirements/SRS_CHANGE_NOTES.md`: các làm rõ, quyết định được duyệt có phạm vi cụ thể và các vấn đề còn chờ quyết định. Không coi mọi nội dung trong file là đã được duyệt.
- `src/HotelOps.Api/Data/HotelOpsDbContext.cs` và `src/HotelOps.Api/Entities/`: bằng chứng về ánh xạ dữ liệu hiện tại, không tự thay thế yêu cầu nghiệp vụ được duyệt.
- `frontend/src/` và `frontend/tests/`: mã giao diện và kiểm thử hiện có; cần kiểm chứng thực tế trước khi công bố kết quả.
- Nhánh tham chiếu frontend do người dùng chỉ định: `feature/react-frontend`.

## Product Principles

1. Giữ đủ màn hình và chức năng khi thay đổi giao diện.
2. Quyền thao tác và dữ liệu phải đúng phạm vi người dùng, chi nhánh và nghiệp vụ.
3. Phân biệt trạng thái nghiệp vụ từ nguồn có thẩm quyền; không suy diễn kết quả thành công hoặc mức sẵn sàng.
4. Hỗ trợ công việc trên cả máy tính và điện thoại.
5. Tách rõ yêu cầu đã xác nhận, quyết định còn mở và hành vi đã kiểm chứng.

## Open Decisions

- Vai trò và luồng công việc ưu tiên cho vòng thiết kế đầu tiên.
- Các quyền và quy tắc chưa được phê duyệt trong tài liệu yêu cầu; rà soát theo từng tác vụ, không tự hoàn thiện ma trận quyền.
- Chưa có tiêu chuẩn accessibility cụ thể được người dùng xác nhận.
- Chưa có cam kết thương hiệu, giọng văn hoặc định hướng thẩm mỹ mới được xác nhận.
