# Chưa sẵn sàng merge cả feature branch

Không tạo PR hay merge vào develop/main trong FS012. Accepted A01/A02 chưa có; UC029 vẫn provisional; dedicated deployed application login chưa được cấu hình/kiểm chứng; privileged writers vẫn bypass. Local PASS không gỡ các gates này. SQL FS012 có exact trial interlocks, không phải shared deployment migration.

UC028 là ứng viên vertical slice, nhưng baseline FS011 đã chỉ ra closure chưa sạch: RoomTypeCreationService constructor nhận candidate; CleaningTaskLifecycleController chứa candidate inbox/assign; frontend commit 8f6e80a chứa Create cùng Assign. Không có bằng chứng để cherry-pick nguyên các commit đó an toàn. FS012 còn thêm nhánh module vào candidate service và tuple/lifecycle marker; tuyệt đối không kéo chúng vào develop như accepted Notification.

Muốn mở PR riêng UC028 cần extraction có chủ đích: DTO Create/options, Create service/cycle/readiness/audit, đúng controller actions/routes và frontend Create/navigation cùng tests; loại candidate DTO/model/mapping/DI/flag/schema/publisher/dispatcher. Chạy clean build, test và schema compatibility trên baseline develop, review interpretation room history, sau đó Tester chạy trên checkpoint extraction. Chưa có extraction branch/PR được tạo nên chưa coi slice này MERGE_READY.

Ảnh hưởng shared được ghi chính xác ở DEV1_FS012 integration note. Không sửa generated entities/DbContext mapping, User Secrets/appsettings, roles, BE2 modules hoặc worktree khác. Mọi grant/procedure/constraint fault chỉ trên trial FS012; cần review writer inventory và ứng dụng thực của login trước bất kỳ deployment nào.
