# Use case mapping

Các tên Directory dưới đây là nhóm API review; không tự tạo UC hoặc permission mới. Actor từ SRS draft mô tả nhu cầu lịch sử, không phải quyền runtime đã được cấp.

| Nhóm | UC chính thức / actor | Hành vi theo SRS | Quyền và hiện trạng | Entities / dependencies |
|---|---|---|---|---|
| Branch Directory | UC-013 View Branch Structure; Administrator, Operations Manager | Chọn branch trong scope; xem floors/areas dạng cây, rooms và departments; expand/filter active/inactive | Chưa có read mapping chốt. MASTER_DATA_MANAGE là permission manage liên quan, không tự động là quyền đọc UC-013. Chưa có API/service/DTO | Branch, Floor, Area, Room, Department; active branch guard và quyết định tree |
| Department Directory | Lát cắt departments của UC-013; cùng actor UC-013 | Danh sách departments của branch được phép; không tạo UC đọc riêng | Chưa có read mapping chốt. UC-024/025/026 là Create/Update/Deactivate Department, không thay thế read UC | Department.BranchId, Branch; ManagerUserId nullable không phải định nghĩa scope DH |
| Room Type Directory | UC-142 View Room Types List; Administrator | Code/name/branch/số phòng active/status; search code/name; filter branch/status; sort code; page20 max100 | Không có ROOM_TYPE_VIEW trong catalog. MASTER_DATA_MANAGE là candidate cần duyệt. Chưa có API/service/DTO | RoomType, Room; branch ownership trong SRS không được model hiện tại biểu diễn |
| Room Directory | UC-023 View Room Directory; Administrator, OM, HK Supervisor, HK Staff, Maintenance Lead/Staff, Storekeeper, DH, Owner | Search room identifiers; filter branch/floor/area/type/status/active; sort floor rồi room number; chi tiết current status reason/history khi chọn phòng | ROOM_VIEW có thật; catalog hiện chỉ cấp BRANCH cho một số roles, khác actor draft. Chưa có API/service/DTO | Room, Branch, Floor, Area, RoomType; status semantics, branch checks, location consistency |

Reception không có trong actor list draft cũ nhưng đã có ROOM_VIEW/BRANCH trong catalog. Điều này không phê duyệt các lifecycle transitions chưa chốt. DH theo Change Notes là branch-wide, không giới hạn vào một Department.ManagerUserId.

## Phần được SRS xác nhận

- GBR-001/002: đăng nhập và kiểm tra quyền, không có public anonymous directory.
- GBR-015/SYS-PAR-11: chỉ dữ liệu trong scope, mặc định 20/max100, default order theo UC và ID tie-breaker.
- Room List: keyword optional, branch/floor/area/type/status/active; active mặc định All. UI cho phép All Branches với multi-branch user.
- UC-142: search code/name; branch và status filter; code ordering; branch và active-room count là dữ liệu yêu cầu, không được âm thầm bỏ.
- Draft matrix Hotel Master Data / View Directory ghi Full cho A/OM/O/HK Sup/ML/SK/DH, restricted cho HK Staff/MS; footnote restricted branch hoặc department. Matrix có annotation NOT APPROVED, thiếu Reception và chưa reconcile với catalog.

## Các xung đột không được tự giải quyết

1. UC-013 dùng floor_area_id/code/name/parent_floor_id; model tách Floor và Area, Area.FloorId nullable. Không có unified floor_area entity hay floor-parent-floor. DTO tree cần phân biệt node kind + ID, chốt branch-level areas và paging/expand riêng.
2. RoomType global: unique TypeCode; không có BranchId. Room có thể dùng cùng type ở nhiều branches. “Được sử dụng trong branch” không đồng nghĩa “thuộc branch”; type chưa có phòng vẫn là master data.
3. Room không có DepartmentId. Không thêm department filter/own-room rule hoặc suy ra quyền từ phòng ban người dùng.
4. SRS room status UI: Ready, Pending Cleaning, Cleaning In Progress, Pending Inspection, Rework Required, Blocked, Inactive.
   SQL CK_rooms_operational_status: AVAILABLE, OCCUPIED, DIRTY, CLEANING, INSPECTION, READY, OUT_OF_SERVICE.
   IsActive là cờ riêng. Không có mapping một-một đã xác minh, không tự coi OUT_OF_SERVICE = Blocked hoặc DIRTY = mọi trạng thái chờ.
5. Proposed single-branch room list ở API_CONTRACT_PROPOSAL là giai đoạn hẹp hơn UI All Branches. Cần chấp thuận scope này; không tuyên bố hoàn thành UC-023 đầy đủ.
6. ROOM_VIEW không được cấp cho SYS_ADMIN/OWNER/STOREKEEPER hiện tại. Không đổi catalog hoặc bypass để làm khớp draft.

## Required master data

Branch active; Floor thuộc branch; Area optional thuộc branch và floor phù hợp; RoomType tồn tại; Room tham chiếu Branch/Floor/RoomType hợp lệ. Department độc lập với Room, gắn Branch. Canonical reference seed có BR001/BR002 và HK/MT/WH/FO mỗi branch; không hard-code ID hoặc giới hạn số bản ghi trong business logic. Không giả định canonical reference seed đã có rooms, types hoặc personnel fixtures.
