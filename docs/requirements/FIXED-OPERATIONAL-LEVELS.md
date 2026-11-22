# Danh mục mức độ cố định

Quyết định người dùng ngày 22/11/2026: bỏ quản lý Cấu hình nghiệp vụ khỏi UI Admin, lưu cố định đủ giá trị ưu tiên và nghiêm trọng trong database. Không tạo bảng, migration hoặc thay đổi phân quyền.

| Nhóm | Mã nội bộ | Nhãn | Thứ tự |
|---|---|---|---|
| Mức độ ưu tiên | LOW | Thấp | 1 |
| Mức độ ưu tiên | NORMAL | Bình thường | 2 |
| Mức độ ưu tiên | HIGH | Cao | 3 |
| Mức độ ưu tiên | CRITICAL | Khẩn cấp | 4 |
| Mức độ nghiêm trọng | LOW | Nhẹ | 1 |
| Mức độ nghiêm trọng | NORMAL | Trung bình | 2 |
| Mức độ nghiêm trọng | HIGH | Nặng | 3 |
| Mức độ nghiêm trọng | CRITICAL | Nghiêm trọng | 4 |

Bộ mã lấy theo UC-050/UC-064 của SRS. Yêu cầu mới thay thế khả năng chỉnh nhãn/SLA trong trang cấu hình cũ: ứng dụng chỉ đọc các mức này. Backend từ chối tạo, sửa, đổi nhóm hoặc ngừng hoạt động bằng HTTP 405 `FIXED_OPERATIONAL_REFERENCE`; dropdown tác vụ đọc từ DB theo thứ tự trên. Chỉ bảo trì SQL có kiểm soát sau backup mới thay đổi dữ liệu; không thêm trigger/schema hay tuyên bố SQL DBA bị khóa ghi.

Chuẩn hóa `DEMO-HIGH` thành HIGH, `DEMO-MEDIUM` thành NORMAL và mã CFG của Bình thường thành NORMAL, giữ ID 214/215/216 để giữ liên kết hiện hữu. Bổ sung năm mức còn thiếu. Không áp dụng quy tắc trạng thái/SLA mới hoặc tự chuyển lỗi kiểm tra phòng sang MINOR/MAJOR của các module legacy.

`reference_values` là bảng dùng chung nhiều miền nghiệp vụ. UOM, ITEM_CATEGORY, ASSET_TYPE vẫn phục vụ 6 vật tư, 14 dòng chứng từ và 6 tài sản; không xóa các dữ liệu này để ép bảng chỉ còn hai kind. Hai nhóm được chuẩn hóa và khóa ghi trong thay đổi này là PRIORITY và SEVERITY. Các bảng và dữ liệu legacy không đổi.

UI Cấu hình hệ thống chỉ còn vai trò. URL cũ `#settings?resource=references` và `#settings?resource=permissions` chuyển về nội dung vai trò, không tải bảng cấu hình nghiệp vụ. Các màn nghiệp vụ vẫn đọc các danh mục phụ thuộc như trước.

Script triển khai: `scripts/database/compact-v2/Set-FixedOperationalLevels.ps1`, SQL `fixed-operational-levels.sql`. Yêu cầu backup đã VERIFYONLY trong vòng 30 phút; không có `-Apply` thì rollback. Script từ chối mã lạ hoặc trùng ý nghĩa, không xóa/merge ngầm. Chạy lại không tạo giá trị trùng.

Script seed kịch bản chỉ đọc HIGH của PRIORITY và NORMAL của SEVERITY; không tạo DEMO-HIGH/DEMO-MEDIUM qua API nữa. Với database mới, phải khởi tạo dữ liệu cố định bằng SQL trước khi seed kịch bản; script báo rõ nếu thiếu.
