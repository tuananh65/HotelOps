# HOTELOPS — THÔNG BÁO ĐỒNG BỘ BACKEND (DEV1 ↔ DEV2)
**Chủ đề:** Cơ cấu vận hành theo cơ sở, RBAC/Authorization, dữ liệu và kế hoạch tích hợp
**Ngày cập nhật:** 20/10/2026
**Trạng thái:** Thống nhất định hướng nghiệp vụ; một số quy tắc chi tiết vẫn đang chờ chốt.
**Nguồn quyết định:** Trao đổi nghiệp vụ DEV1; **không dùng SRS Reception-draft mới và UC Diagram chưa cập nhật làm chuẩn phân quyền tại thời điểm này**.

## 1. Các quyết định ĐÃ CHỐT

1. **Mỗi cơ sở (Branch) có một Operations Manager (OM) và một Department Head (DH)**; hai role **ngang cấp**, không phải quan hệ cấp trên–cấp dưới. Mỗi người chỉ có phạm vi quản lý **một Branch**.
2. **DH quản lý tất cả bộ phận nghiệp vụ thuộc Branch**, nhưng không mặc nhiên có mọi permission. OM điều hành, giám sát và xử lý các nghiệp vụ ngoại lệ; ranh giới theo hành động được RBAC kiểm tra.
3. **Ba bộ phận vận hành nội bộ chính thuộc phạm vi HotelOps:**
   - **Housekeeping:** nhiều Supervisor và Staff; Supervisor điều phối/kiểm tra theo ca; không tạo một role “Trưởng bộ phận Housekeeping” riêng.
   - **Maintenance:** nhiều Lead và Staff; Lead điều phối, phân công và xác minh theo ca; không tạo role “Trưởng bộ phận Maintenance” riêng.
   - **Storekeeping:** một hoặc nhiều Storekeeper theo quy mô và ca; không cần role Trưởng kho riêng. Phương án vận hành gợi ý: một thủ kho chính và người dự phòng được ủy quyền (chưa phải ràng buộc số lượng bắt buộc).
4. **Reception** là role nghiệp vụ độc lập, không nằm trong ba bộ phận nội bộ trên: ghi nhận thủ công **check-in, check-out, đổi phòng** (occupancy event). HotelOps V1 không cần lưu booking/danh tính khách/PMS. Reception **không được nhập trực tiếp** trạng thái Ready/Dirty/Blocked; hệ thống tính trạng thái từ occupancy, cleaning/inspection và block/sự cố.
5. **Quyền là Permission + Data Scope + Business Guard + SoD**, không chỉ dựa vào role hay UC Diagram. Các scope: `OWN`, `DEPARTMENT`, `BRANCH`, `ALL`; OM và DH tác nghiệp trong `BRANCH` được chỉ định. Mặc định từ chối nếu không đủ quyền.
6. **Phân nhiệm (SoD):** Người tạo không tự phê duyệt; người thực hiện vệ sinh không tự kiểm tra công việc mình dọn; người sửa chữa không tự xác minh chính chu kỳ sửa chữa đó; bút toán điều chỉnh/đảo kho không được người đề nghị tự đăng. Nhiều role trên cùng user **không** bỏ qua SoD.
7. **Material Request:** nhân viên → DH duyệt; DH tự tạo → OM *cùng Branch* duyệt dự phòng; OM tự tạo → DH được cấp quyền duyệt và khác requester. Nếu không có người duyệt hợp lệ: để pending; bắt buộc audit các quyết định.
8. **OM giữ các nghiệp vụ:** quản lý tài sản/bàn giao/điều chuyển/thanh lý, đăng điều chỉnh và đảo kho theo quy trình, xử lý escalation sự cố nghiêm trọng và yêu cầu nội bộ quá hạn, dashboard/báo cáo trong Branch.

## 2. Mô hình 10 role nghiệp vụ mục tiêu

| Role | Phạm vi/Trách nhiệm chính |
|---|---|
| Administrator | Cấu hình hệ thống, master data, user, role/permission, audit theo quyền được cấp |
| Operations Manager | 1 người/Branch; điều hành & xử lý ngoại lệ trong Branch |
| Department Head | 1 người/Branch; quản lý công việc, lịch/nguồn lực và duyệt vật tư trong Branch |
| Reception | Occupancy events, xem tình trạng phòng trong Branch |
| Housekeeping Supervisor | Có thể nhiều người/Branch, phân công và inspection theo ca |
| Housekeeping Staff | Thực hiện cleaning/rework đã giao |
| Maintenance Lead | Có thể nhiều người/Branch, triage, phân công, xác minh theo ca |
| Maintenance Staff | Thực hiện công việc bảo trì được giao |
| Storekeeper | Có thể nhiều người/Branch, tác nghiệp kho theo permission |
| Owner | Xem dashboard/report, không mặc nhiên có quyền tác nghiệp |

**Không suy ra quan hệ phân cấp giữa role từ danh sách trên.** Supervisor và Lead là chức năng điều phối chuyên môn theo ca, dưới phạm vi vận hành phối hợp giữa OM và DH; không phải DH độc lập.

## 3. Trạng thái dữ liệu đã xác minh

**Database sử dụng:** `HotelOpsDB` cũ, SQL Server + EF Core Database First. **Không thay bằng bộ seed/schema SQL mới 20 bản ghi mỗi bảng.**

- **DEV1 Authentication (FE-01):** đã push ở nhánh `feature/BE1-authentication`; chưa merge vào `develop`.
- **DEV1 Authorization:** đang làm trên `feature/BE1-authorization` tách từ nhánh Authentication; **Authorization Core/API chưa hoàn thành, chưa merge**.
- **Database gốc `HotelOpsDB`:** chưa áp dụng các thay đổi Authorization đang thử nghiệm.
- **Database thử nghiệm `HotelOpsRBAC_Test`:** khôi phục từ backup của DB gốc. Migration 001 và 002 **đã COMMIT trên DB test**:
  - Migration 001: `GENERAL_MANAGER` (role_id 2) → `OPERATIONS_MANAGER`; User 1 được gắn role tại Branch 1; thu hồi Branch 2; 21 grant cũ giới hạn `BRANCH`.
  - Migration 002: chuẩn hóa grant OM thành **20 permission `BRANCH`**, loại bỏ các grant không phù hợp và thêm permission chuyên biệt. **Không áp dụng Migration 001/002 ở DB gốc**.
- **Migration 003 (DH):** chưa chạy. Phương án Dry Run cũ bị tạm dừng vì quyết định mới chỉ cho 1 DH/Branch.

### Gán nhân sự hiện đã chốt

| Cơ sở | Role | User | Tình trạng |
|---|---|---|---|
| Branch 1 | OM | User 1 | Đã chuẩn hóa **chỉ trên DB test** |
| Branch 1 | DH | **User 4 — Phạm Thu Hà** | Đã chốt nghiệp vụ, **chưa thay đổi DB** |
| Branch 2 | DH | **User 19 — Hồ Thị Thu** | Đã chốt nghiệp vụ, **chưa thay đổi DB** |
| Branch 2 | OM | Chưa chỉ định | **Không tự gán** |

**Sáu tài khoản đang giữ DH nhưng không còn là DH chính thức:**
- User **5** (Branch 1): giữ `WAREHOUSE_KEEPER`; chờ thu hồi `DEPT_MANAGER`.
- User **23** (Branch 1) và **25** (Branch 2): giữ `RECEPTION`; chờ thu hồi `DEPT_MANAGER`.
- User **17, 18** (Branch 1) và **20** (Branch 2): hiện chưa có role nghiệp vụ khác; **chưa quyết định gán role thay thế**, tuyệt đối không thu hồi role DH khi chưa có phương án và kiểm tra công việc mở.

**Cột `departments.manager_user_id`:** hiện trỏ vào nhiều user đang giữ role DH cũ. Đề xuất cho ba bộ phận chính: trỏ về DH cấp Branch (User 4/19) và sử dụng dữ liệu shift/assignment để xác định Supervisor/Lead trực ca. **Đây là đề xuất chưa chốt/chưa migration**; không tự cập nhật hàng loạt, đặc biệt với department seed khác như An ninh, Nhà hàng, Lễ tân.

## 4. Những ràng buộc DEV2 cần áp dụng khi viết API

1. Luôn xác thực session trước khi kiểm tra permission; kiểm tra `role_permissions.data_scope` và branch thực của bản ghi ở **server**, không tin `branch_id` client truyền lên. Việc lọc danh sách, đọc chi tiết và thao tác ghi đều phải kiểm tra phạm vi.
2. Không coi `is_primary` là giới hạn quyền; kiểm tra `user_roles` hiệu lực, `user_branch_access.revoked_at`, trạng thái user và ranh giới Branch.
3. Quyền `BRANCH` cho DH là phạm vi truy cập, **không** cho phép bỏ qua quy tắc người được giao/phê duyệt hay SoD.
4. Tách permission cho `VIEW`, `CREATE`, `ASSIGN`, `EXECUTE`, `APPROVE`, `VERIFY`, `POST`, `REVERSE`, `ESCALATE` khi các hành động có mức nhạy cảm khác nhau; không suy quyền write từ một UC “View” có quan hệ `extend`.
5. Không ghi thẳng `rooms.operational_status` trong API Reception: ghi sự kiện nguồn, xác thực điều kiện chuyển, cập nhật trạng thái tính toán và audit nhất quán.
6. Không hard-code quyền dựa trên tên role cũ (như `GENERAL_MANAGER`, `HK_INSPECTOR`) hoặc mặc định role quản lý có `ALL`.
7. Khi quyền bị thay đổi/thu hồi, yêu cầu tiếp theo phải phản ánh quyền hiệu lực mới; không dùng cache bất biến xuyên session.
8. Những hành động nhạy cảm phải lưu lịch sử actor, trạng thái trước/sau, lý do và thời điểm; chống tạo trùng khi gửi lại request.

## 5. Những điểm CHƯA CHỐT / chưa được DEV2 tự suy diễn

- Danh tính OM Branch 2; cách bàn giao các tài khoản 17/18/20 từ `DEPT_MANAGER`.
- Cơ chế quản lý Supervisor/Lead *theo ca* cụ thể; số nhân viên Storekeeper tối thiểu và quyền người dự phòng.
- Thiết kế vật lý/schema cho occupancy events, check-in/out/move và diễn giải `ROOM_RELEASE` hiện có.
- Thiết kế chính thức của `departments.manager_user_id`, và xử lý các Department seed không thuộc ba bộ phận nội bộ chính.
- Phạm vi `Owner`: toàn bộ hệ thống hay các chi nhánh được chỉ định.
- Cách ủy quyền xác minh khi không có Supervisor/Lead độc lập; không tự cho OM/Reception thay thế hoặc bỏ qua SoD.
- Permission Matrix V4 là nền thiết kế; chưa coi mọi permission code đề xuất đều đã nằm trong `HotelOpsDB` gốc.

## 6. Đề nghị phối hợp với DEV2

- DEV2 **tiếp tục phát triển module được phân công trên branch riêng**, giữ entity/contract ổn định khi chưa thống nhất migration RBAC.
- Trước khi thêm/sửa trường role, branch, department, status, assignment hoặc xử lý quyền, **thông báo DEV1 để đối chiếu Authorization**.
- Gửi DEV1 danh sách API DEV2 cần bảo vệ, gồm: phương thức HTTP, route, action, tài nguyên/branch của record, vai trò tác nghiệp và điều kiện đặc biệt; DEV1 sẽ cùng chuẩn hóa permission/guard.
- **Không chạy Migration 001–003 trên `HotelOpsDB`**, không sử dụng `HotelOpsRBAC_Test` làm cơ sở dữ liệu dùng chung, không merge Authorization vào `develop` khi chưa có sự thống nhất nhóm.
- Những UC Diagram và SRS Reception-draft chưa được đồng bộ sẽ được chỉnh sau; khi có mâu thuẫn, báo lại để chốt nghiệp vụ thay vì tự cấp quyền theo sơ đồ cũ.

**Đề nghị DEV2 phản hồi:** (1) có API/module nào phụ thuộc vào role cũ hoặc Department Manager theo từng department không; (2) có yêu cầu schema/status room nào ảnh hưởng occupancy không; (3) có công việc nào cần phối hợp để DEV1 chuẩn bị Authorization Policies/guards.
