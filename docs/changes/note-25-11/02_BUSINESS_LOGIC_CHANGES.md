# Note 25_11 — Thay đổi nghiệp vụ

## Ca làm việc và quyền

Các lệnh nghiệp vụ được kiểm tra tại MVC filter và service/transaction. Một lịch được phân công chưa phải ca đang làm: cần assignment WORKING, StartedAt hợp lệ, chưa EndedAt, ca/role/bộ phận/membership còn hiệu lực, đúng chi nhánh và khoảng giờ (kể cả qua đêm). Service còn đối chiếu permission với role của ca để không dùng ca của role khác ghi sổ kho.

OM, Owner, SysAdmin được miễn check-in theo quyết định đã chốt; quyền và scope nghiệp vụ vẫn phải hợp lệ. Đọc, đăng nhập, thông báo, bắt đầu/kết thúc ca và cấu hình hợp lệ không bị khóa chung. UI giúp giải thích/khóa nút nhưng API là nơi quyết định cuối cùng. Không thêm role hoặc permission code giả.

## Vật tư và kho

- Kiểm đếm phòng dùng input controlled, không mất draft khi lỗi API/realtime. Tổng hiện có phải bằng còn dùng được + không đạt + hết hạn; số bổ sung không vượt phần thiếu. Chỉ ghi nhận lượng bổ sung thực tế.
- HK đang ca tạo yêu cầu nhiều vật tư tiêu hao với allocation của bản thân. Luồng mới: `PENDING_OPERATIONS_MANAGER` → OM duyệt → `PENDING_STOREKEEPER` → kho cấp → `FULFILLED`. Replay không cấp trùng. OM không được xử lý yêu cầu liên bộ phận thông thường.
- Phiếu nhập tạo draft trực tiếp, metadata chỉ sửa trong draft. Ghi sổ giữ optimistic concurrency, validation và transaction; không bỏ kiểm tra để né lỗi.
- Phiếu kiểm kê chọn nhiều vật tư, snapshot tồn hệ thống readonly, thực đếm inline. Sửa số đếm không tự điều chỉnh tồn.
- Low-stock dựa tồn khả dụng so định mức, không đánh đồng số sở hữu với phần có thể cấp. Thủ kho chỉ kho thuộc chi nhánh; ngoại lệ OM xem kho khác qua transfer catalog hẹp, không mở rộng branch switcher toàn hệ thống.
- Điều chuyển giữ nguyên số thực nhận; chênh lệch còn chờ đối soát, không tự ghi mất hoặc hoàn kho.

## Bảo trì

Trưởng đội phân công, Maintenance Staff thực hiện trong ca. Thành công kết thúc lần sửa với PASS, chuyển yêu cầu kiểm tra sang HK; không tự READY. Không sửa được kết thúc lần sửa với FAIL, giữ tài sản FAULTY và sự cố chưa giải quyết, thông báo trưởng đội.

Trưởng đội đề nghị thay thế, OM cùng chi nhánh duyệt. Với lần sửa thất bại đã kết thúc, phê duyệt tạo một lệnh MAINTENANCE mới có link SOURCE tới lần thất bại, cùng sự cố và checklist snapshot. Lệnh cũ không bị mở lại/ghi đè. Trưởng đội phân công lệnh mới; Staff nhận, bắt đầu, lắp và hoàn thành. Assignment được đóng khi lần sửa kết thúc; nhân viên vẫn đọc được lịch sử đã thực hiện. Sự cố thông thường chấp nhận lần sửa COMPLETED/PASS khi kết luận, không đòi bước Lead verify đã bỏ.

## UI và chức năng bị loại bỏ

Thông báo thụ động tự đóng sau 5 giây, giữ nút X; xác nhận nghiệp vụ không tự đóng. Bell dùng own unread và đồng bộ REST/realtime. Nội dung notification loại GUID khỏi tiêu đề bằng xử lý tập trung, giữ định danh trong dữ liệu liên kết.

OM bỏ OPERATIONAL khỏi trang, lệnh và báo cáo; scheduler ngừng occurrence OPERATIONAL mới, giữ engine/historical records. Checklist chuyển vào cấu hình theo quyền, giữ checklist tại công việc. Dashboard kho bỏ shortcut dư; bỏ trang bổ sung và lịch sử kho độc lập nhưng giữ chứng từ/ledger. Owner chỉ dashboard tổng hợp. Nhân sự OM không phụ thuộc danh sách ứng viên phân ca; tài sản trình bày theo tầng/phòng và giữ mục chưa gán vị trí.

## Database và tài liệu cần cập nhật

Patch SQL: `scripts/database/compact-v2/note-25-11-refill-om.sql`. Không thêm bảng, entity hoặc EF migration; giữ 57 bảng hiện có. Patch mở rộng CHECK constraint của consumable refill và chuyển pending supervisor cũ sang OM, ghi audit/notification; không thay đổi tồn kho. Đã chạy lại trên clone và áp dụng DB development sau backup CHECKSUM/RESTORE VERIFYONLY. Chưa áp dụng HotelOpsCompactDB.

SRS/UC cần phản ánh: FE04/05 kiểm đếm và kiểm tra HK; FE06 bảo trì hai kết quả + replacement follow-up; FE08/09 refill/receipt/stocktake; FE11 loại OPERATIONAL của OM; FE12 ca thực và lịch tuần; FE13 ngoại lệ OM duyệt refill; FE14/15 notifications/dashboard/personnel. Nội dung Note mới và quyết định Owner được ưu tiên hơn SRS cũ.
