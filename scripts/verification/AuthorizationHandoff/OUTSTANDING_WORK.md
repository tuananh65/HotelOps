# Outstanding work và owner

> Task08 đã harden public health, thêm policy cancellation/budget và tách integration setup khỏi deployment-state; xem [trạng thái mới](../AuthorizationHardening/README.md). Least-privilege SQL principal vẫn chờ DBA review, không được coi đã áp dụng. Các mục F02/F03/F04 bên dưới là kế hoạch lịch sử Task07 đã được xử lý trong phạm vi Task08; fresh DB provisioning trên máy DEV2 chưa chạy thực tế.

## DEV1 tiếp tục

| Việc | Điều kiện hoàn tất |
|---|---|
| Production API protection | Có endpoint nghiệp vụ thật, mapping permission/resource và binding trước authorization được duyệt; test action không chạy khi deny |
| Cleaning adapters còn thiếu | Rule view/reassign/start/resume/complete được xác nhận; không dùng IN_PROGRESS adapter cho trạng thái khác |
| Legacy Department Retirement | Giải quyết dependency và phương án dữ liệu riêng đã phê duyệt; không coi RBAC upgrade là retirement hoàn tất |
| Diagnostic exposure F02 | Thống nhất health/readiness public/internal, error contract; loại bỏ public provider messages trước deploy |
| Production authentication deployment | Cấu hình encrypted persistent Data Protection keys/host/cookie deployment; hiện guard chỉ Windows Development |

## DEV2 có thể tích hợp core ngay

ICurrentUserAccessor, IPermissionService, PermissionPolicy/Requirement và handler/provider có thể tái sử dụng theo guide. Chỉ dùng permission code thực tế và đã được duyệt cho module. Branch check có thể dùng nếu rule thực sự branch-level; item/resource endpoints phải có authoritative resolver phù hợp. Không có resolver DEV2 sẵn chỉ vì core đã tồn tại.

DEV2 bổ sung mapping permission cho endpoint của mình, request/resource context, DTO/business service, audit, test deny/allow và concurrency. Muốn thêm resolver phải phối hợp DEV1 trên cơ chế dispatch chung; không ghi đè IPermissionResourceResolver đang dùng cho Cleaning.

## Cần quyết định nghiệp vụ / phối hợp shared components

- Start/Resume Cleaning và các state transition: SRS lịch sử và schema không được tự đồng nhất; cần rule được duyệt.
- Separation of Duties: grant không chứng minh được độc lập người thực hiện/inspection/approval; mỗi workflow xác nhận rule và dữ liệu dùng để kiểm tra.
- Reception, Operation Manager, Department Head và matrix: Change Notes vẫn ghi pending; release catalog đang chạy không tự thay thế approval nghiệp vụ hoặc cho phép tự đổi quyền.
- Concurrency authorization–write: thống nhất transaction/revalidation/locking hoặc row version phù hợp schema hiện có. Không thêm column/migration trong task review.
- Quyền SQL (F01): DBA/developer duyệt credential SELECT-only cho live verification, test-only principal cho SQL integration và provisioning privilege riêng; Task07 không sửa permissions.

## Giai đoạn chuẩn hóa test/handoff sau

1. Tách reusable SQL helper khỏi deployment-state local (F03), giữ fail-closed target guards và provenance; không commit private recovery evidence làm dependency.
2. Cấu hình integration server/database/receipt qua local settings an toàn, validate chặt metadata; không mặc định tái tạo/reset database đã có.
3. Tạo test project được solution discover bằng Microsoft.NET.Test.Sdk + framework được team chọn, phân category unit/doubles/SQL read-only/HTTP SQL. Không chỉ thêm IsTestProject vào console để giả vờ đã discover tests.
4. Tách fixture lifecycle khỏi test assertions, cleanup/recovery khi process bị kill và isolation giữa runner/ứng dụng khác; không phụ thuộc history session IDs cố định.
5. CI unit/doubles chạy mặc định; SQL jobs opt-in với credential riêng, runner isolation và sanitized artifacts. Giữ NuGet vulnerability auditing.
6. Policy catalog timeout/cancellation/telemetry (F04) và performance nhiều SQL SELECT khi traffic tăng; không đổi caching nếu chưa kiểm chứng revocation semantics.

Các cải tiến test có thể để giai đoạn sau đối với commit core; portable SQL handoff chưa được tuyên bố sẵn sàng trước khi F03 được xử lý. Production rollout còn phụ thuộc API protection, deployment hardening, business rules và F01/F02 theo môi trường.

Task07 không thực hiện bất kỳ việc mở rộng nào trong danh sách này, không sửa schema/matrix hoặc reset database.
