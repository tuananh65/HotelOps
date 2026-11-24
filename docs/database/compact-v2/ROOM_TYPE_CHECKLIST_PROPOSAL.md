# Đề xuất checklist theo hạng phòng — 23/11/2026

**ĐÃ HỦY — không áp dụng.** Lúc 14:40 ngày 23/11/2026, người dùng quyết định checklist là bản giấy do người dùng tự quản lý, không triển khai cấu hình hoặc gán checklist trên hệ thống. Không có schema/grant nào được áp dụng. Nội dung dưới đây chỉ lưu lịch sử đề xuất bị hủy.

Kiểm tra HotelOpsCompactDB thực tế: OPERATION_MANAGER không có CHECKLIST_MANAGE; checklist_templates và room_types chưa có quan hệ áp dụng mẫu. Template hiện có phiên bản, published immutable; work_checklist_items lưu snapshot.

## Thay đổi cần duyệt

1. Cấp đúng permission hiện hữu CHECKLIST_MANAGE với scope BRANCH cho role OPERATION_MANAGER tìm bằng code, không hardcode ID. Không tạo permission hoặc đổi grant của vai trò khác.
2. Thêm bảng room_type_checklists: mỗi hạng phòng tối đa một cấu hình CLEANING và một cấu hình INSPECTION. Bản ghi có branch_id, room_type_id, kind, checklist_template_id nullable (bỏ áp dụng), updated_by, updated_at, row_version. Khóa ngoại composite giữ hạng phòng/mẫu trong cùng chi nhánh; unique(room_type_id,kind). Kiểm tra loại mẫu, published/active ở service. Không sửa bảng mẫu hay snapshot hiện có.
3. Bổ sung entity/mapping trong file riêng của CompactDB, endpoint và UI theo permission này. Không sửa Project1 hoặc shared HotelOpsDbContext.
4. Trước khi áp dụng primary: sao lưu mới, kiểm thử trên database độc lập. DDL/grant trong transaction; ghi audit migration và xác minh sau áp dụng. Không gán mẫu mặc định tự động cho dữ liệu hiện có.

## Hành vi

OM vào Cấu hình vận hành → Checklist & hạng phòng. Tạo mẫu dọn phòng hoặc kiểm tra, cấu hình nội dung/bắt buộc/trọng số, tạo phiên bản mới khi cần thay đổi. Mã mới tự sinh. Chọn hạng phòng và mẫu đã công bố cùng chi nhánh, lưu hoặc bỏ áp dụng với kiểm tra version.

Khi tạo CLEANING hoặc INSPECTION, backend lấy hạng phòng thực của phòng, chọn mẫu đã gán. Client gửi mẫu khác bị từ chối thay vì vượt cấu hình. Nếu chưa gán, giữ lựa chọn mẫu theo contract cũ (INSPECTION vẫn bắt buộc có mẫu). REWORK dùng checklist dọn phòng theo workflow hiện có. Không tự áp lại checklist cho công việc đã tạo; thay đổi mẫu chỉ ảnh hưởng công việc mới.

Kiểm thử gồm thiếu quyền403, chéo chi nhánh, sai loại mẫu, hạng/mẫu inactive, stale version409, hai request đồng thời, version mới không đổi mapping cũ, snapshot công việc cũ không đổi, tạo công việc chọn mẫu theo hạng phòng, UI modal/drawer và responsive.
