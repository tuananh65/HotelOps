# HK Staff — triển khai có chọn lọc, giữ UX đã chốt

Yêu cầu hiện tại xác nhận phạm vi HK_STAFF, fullstack và single-agent. Chỉ sửa Project 2, không schema/migration/quyền/dữ liệu chính. Giữ control 31px, navbar title, chi nhánh dưới logo, bộ lọc ẩn, phòng 15 ô/trang và drawer. Kiểm thử ghi chỉ trên database fixture độc lập đã sao lưu.

- [x] Đối chiếu source, SRS/change notes, entity/mapping và database chỉ đọc: role_id=4, đúng 13 quyền; 10 HK Staff mỗi chi nhánh.
- [x] Backend: thống kê cá nhân đúng assignment/lịch, tìm kiếm/lọc trước phân trang, capability IR và ownership khi ghi; không cấp quyền Inspection/Ready.
- [x] Frontend: 7 nhóm theo grants, Cleaning/Rework cá nhân, dashboard thật, checklist/progress, request/material/incident/notification, Workday cá nhân và drawer.
- [x] Kiểm thử fixture API: branch/assignment/concurrency, Cleaning → Inspection → Rework → Reinspection; quyền bị từ chối; ca cá nhân và notifications.
- [x] Typecheck/build FE/BE, unit/integration/UI, review inline (single-agent), cập nhật báo cáo và UX.

| FEATURE | CURRENT_UI | API | PERMISSION | OWNERSHIP | GAP | ACTION |
|---|---|---|---|---|---|---|
| Navigation | menu chung | auth/me | grants thật | profile branch | chưa gom cá nhân | 7 nhóm, route cũ tiếp tục hoạt động |
| Dashboard | đếm 100 việc mine | work list | CLEANING/TASK/ATTENDANCE | created/active assignment | mất lịch sử đã đóng, thiếu KPI/ca | endpoint personal theo actor, local date, không DASHBOARD_VIEW |
| Cleaning/Rework | Work chung | WorkService | CLEANING_VIEW/EXECUTE | active assignee guard có sẵn | tìm trong trang, toggle toàn nhánh, thiếu progress | assignedToMe, server filters/sort, drawer, action modal |
| Checklist | snapshot per work | Checklist | EXECUTE | current assignee | thiếu note/progress UI | dùng snapshot, note, không mẫu/inspection write |
| Room | grid/drawer | rooms/history | ROOM_VIEW | branch | giữ UX | liên kết công việc cá nhân; không occupancy/block mutation |
| Operational | Work | WorkService | TASK_VIEW/EXECUTE | active assignee | lẫn nhiều kind | workspace riêng, không nút quản lý |
| Requests | Work | WorkService | IR_CREATE/VIEW/PROCESS | creator/assignee/authority | cancel/assign UI không biết authority; cancel thiếu kiểm tra | capability từ backend, guard authority |
| Material | Documents | DocumentService | MR_CREATE/VIEW | creator write guard | giữ workflow | materialOnly trong workspace yêu cầu |
| Workday | lịch cá nhân có sẵn | WorkdayService | ATTENDANCE_RECORD OWN | actor, current date/version | giữ workflow | liên kết dashboard và kiểm chứng |
| Incident/Notification | Work/Insights | Work/NotificationService | INCIDENT_CREATE/VIEW, recipient | branch/recipient | menu rời, chưa có document link/pager | gom workspace, giữ object-read check |

Process dùng lại: FE 6424/9324:5186, API 12864:5768. Dừng API trước build C# rồi khởi động lại; fixture 5187 dừng trong finally.

API cũ 12864 đã dừng bằng quyền ngoài sandbox vì giữ DLL; API mới 36268:5768 đã health ok, worker tắt. HK fixture HotelOpsCompactApiTest60d1620fa10b: 26 nhóm Passed. Extended fixture HotelOpsCompactApiTestaae805b66be6: 128 Passed, 0 Failed. Frontend build và .NET build Passed; .NET 28 Passed, 2 Skipped. HK UI 27 views; OM 24; DH 21; 5 suite UX chung Passed. Kiểm tra tầng trong room drawer được bổ sung ở lượt UI/build cuối. Review inline theo single-agent. Không commit/push/deploy.

Hoàn tất: lượt build frontend cuối ghi exit code 0 tại artifacts/housekeeping-staff/frontend-build.log; HK UI cuối có cả tải lên multipart và tải xuống minh chứng bằng trình duyệt. Không còn build/test đang chạy. Báo cáo: plans/reports/implementation-261123-0111-hk-staff-alignment.md.
