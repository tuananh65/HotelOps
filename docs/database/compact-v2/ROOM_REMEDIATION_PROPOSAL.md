# Luồng khắc phục sự cố phòng — phương án quyền cần duyệt

## Bằng chứng và phạm vi

Truy vấn HotelOpsCompactDB ngày 23/11/2026: DEPT_MANAGER chưa có INCIDENT_VIEW, INCIDENT_TRIAGE, MR_CREATE. Giám sát có INCIDENT_CREATE/VIEW; Maintenance Lead có INCIDENT_TRIAGE/MANAGE và MAINTENANCE_ASSIGN/REVIEW; Storekeeper có MR_FULFILL. Có 0 chứng từ đã ghi sổ gắn receiving_room_id.

Không thay role Owner, không cấp mọi quyền, không cấp MASTER_DATA_MANAGE hoặc quyền phân công bảo trì cho Head. Không thêm bảng, cột, enum trạng thái hay EF migration trong phương án này.

## Thay đổi grant đề xuất (chưa áp dụng)

Chỉ thêm cho role lấy theo code DEPT_MANAGER, scope BRANCH:

| Permission hiện có | Mục đích |
|---|---|
| INCIDENT_VIEW | Đọc hồ sơ sự cố phòng trong chi nhánh |
| INCIDENT_TRIAGE | Duyệt, chuyển xử lý, nhận kết quả và yêu cầu kiểm tra lại; cấu hình riêng hai loại báo cáo phòng |
| MR_CREATE | Tạo yêu cầu bổ sung vật tư gửi kho |

Các quyền này cũng mở những endpoint hiện có kiểm tra cùng permission; phải kiểm thử hồi quy các đường gọi này, không chỉ menu mới. Cấu hình loại báo cáo là endpoint giới hạn DEPT_MANAGER + INCIDENT_TRIAGE trong chi nhánh; không mở trang danh mục hệ thống toàn cục. Không sửa grant của vai trò khác.

## Contract nghiệp vụ dự kiến

- Một báo cáo chọn một tài sản/vật tư đã ghi nhận thuộc phòng; có thể tạo nhiều báo cáo. Không sinh đồ mẫu hoặc định mức hạng phòng. Tài sản dựa assets.room_id; vật tư cần truy vết chứng từ giao/hoàn trả đã ghi sổ. Danh sách trống phải hiển thị trống.
- Hai loại Hỏng đồ vật / Thiếu đồ vật được lưu bằng WORK_CATEGORY và SettingsJson hiện có, giới hạn branch trong service. Head bật/tắt hoặc sửa nhãn; không đổi loại của hồ sơ đã dùng.
- Tiêu đề tự tạo từ loại, tên đồ, mã phòng. Phòng cố định. Không chọn bộ phận, checklist, hạn xử lý ở form báo cáo.
- Giữ Work.Kind=INCIDENT; các bước duyệt/chuyển/xác nhận lưu event bất biến, không phát minh trạng thái SQL. Backend chặn các endpoint cũ bỏ qua thứ tự này đối với hồ sơ mới.
- Hỏng: Head duyệt và gửi yêu cầu → Lead tạo bảo trì liên kết bằng quyền hiện có → Lead phân công → Staff hoàn thành → Lead nghiệm thu → Head nhận kết quả và yêu cầu giám sát kiểm tra.
- Thiếu: Head tạo MR đúng bộ phận/phạm vi, kho cấp qua chứng từ hiện có; không tự ghi xuất kho hoặc giả lập đã bổ sung. Kho xác nhận đã bổ sung sau khi có chứng từ cấp đủ → Head yêu cầu kiểm tra.
- Giám sát dùng checklist giấy, ghi Đạt/Không đạt và ghi chú. Không đạt trở lại Head để tiếp tục khắc phục. Đạt mở chu kỳ cần dọn mới, vô hiệu bằng chứng sạch của chu kỳ cũ trong cùng transaction; không đặt READY. Không tự ghi đè phòng OCCUPIED/RESERVED hoặc gỡ block không thuộc sự cố.
- Mọi thao tác kiểm tra branch, role, permission, trạng thái, version, operation key; ghi audit và thông báo. Không giả mạo actor để mượn quyền.

## Trạng thái thực hiện

Đã được người dùng duyệt lúc 16:45 ngày 23/11/2026: ba grant BRANCH và phương án này; phải kiểm thử đầy đủ trước khi sao lưu/áp dụng primary. Script grant đã chạy hai lần trên fixture HotelOpsCompactApiTest689b52b21abc, đúng ba quyền, không thay role khác.

Luồng đã được triển khai và đang kiểm thử lại với quyết định mới ngày 23/11 lúc 18:00: kho tiếp nhận trực tiếp yêu cầu khắc phục đã được Trưởng bộ phận duyệt. Chưa cập nhật Production tại thời điểm ghi chú này.

### Ngoại lệ duyệt vật tư đã được người dùng chấp thuận

- Chỉ phiếu MATERIAL_REQUEST gắn sự cố thiếu đồ trong phòng, có bằng chứng duyệt và sự kiện `ROOM_REMEDIATION_AUTHORIZED` mới cho phép người tạo Head đồng thời ghi nhận phê duyệt. Không giả mạo người tạo/người duyệt.
- `enable-room-remediation.sql` thay điều kiện `CK_inventory_documents_approval` và bổ sung kiểm tra vào trigger chứng từ hiện có `TR_inventory_documents_posted`. Không thêm bảng/cột/trạng thái, không thay số lượng trigger. Bảo vệ chứng từ đã ghi sổ được giữ nguyên.
- Trigger kiểm tra chi nhánh/phòng/sự cố, loại MISSING, trạng thái đã duyệt, sự kiện ủy quyền và vai trò Head còn hiệu lực; khóa các trường liên kết và phê duyệt sau đó.
- Phiếu thông thường vẫn phải có người duyệt độc lập. Kho cấp vẫn phải khác người duyệt, đủ tồn thực tế và không vượt số lượng được duyệt. Quyết định này không cho phép tự ghi xuất kho.
- Script áp dụng luôn sao lưu và VERIFYONLY trước transaction. Phải hoàn tất kiểm thử fixture trước khi áp dụng primary.
