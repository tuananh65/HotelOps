# Task72 — Checklist contract cần developer duyệt

Trạng thái: developer đã **duyệt có điều kiện** trong phản hồi Task72 tiếp theo.
Chỉ disposable trial; SRS/Permission Matrix/AGENTS ưu tiên. Nội dung proposal gốc bên
dưới được giữ để đối chiếu, không thay thế điều kiện approval.

Quyết định triển khai: không tự cấp quyền HK_SUPERVISOR hoặc sửa production catalog;
CHECKLIST_MANAGE và BRANCH đã tồn tại. Endpoint luôn kiểm tra SQL grant/assignment/
membership. Harness tạo rồi gỡ một grant tường minh chỉ trong disposable fixture.
Theo SRS form/GBR-005, migration bổ sung nullable criterion_group nvarchar(100) và
real checklist rowversion; không ghi đè Description để giả lập group, không thay
rowversion bằng state hash. Generated entities không sửa, chỉ controlled partial
projections/mappings. Existing legacy ownership/content không backfill. Xem
[deployment contract](../../database/release/checklists/README.md) và báo cáo Task72.

Mục tiêu: UC039 Create Room Inspection Checklist (2 điểm), UC040 Update Room Inspection
Checklist (3 điểm). SRS §5.1/5.2 cần nhiều applicable RoomTypes, version DRAFT/PUBLISHED/
RETIRED, giữ version của inspection cũ. Hiện inspection_checklists chỉ có RoomTypeId
nullable, không branch ownership; CHECKLIST_MANAGE tồn tại nhưng không có grant HK_SUPERVISOR.

Đề xuất concrete V1:

1. Thêm `inspection_checklists.branch_id bigint NULL`, FK non-cascade tới branches;
   NULL chỉ giữ legacy records. API mới bắt buộc branch; không backfill/convert legacy.
2. Thêm bảng `inspection_checklist_room_types(checklist_id bigint NOT NULL,
   room_type_id bigint NOT NULL)`, PK hai cột, hai FK non-cascade tới bảng hiện có và
   reverse supporting index. Một checklist có nhiều RoomTypes cùng branch; không
   gắn RoomType branch khác/global legacy cho business operation mới. Triggers kiểm
   tra branch match/immutable checklist ownership, không rewrite existing records.
3. Giữ `inspection_checklists.room_type_id` hiện hữu cho legacy. New scoped checklist
   để NULL và dùng bridge; không dùng audit JSON hay default_checklist_id làm ownership.
4. Publish tạo version mới/retire version cũ atomically. Không sửa criteria của version
   đã publish/được inspection sử dụng. Template legacy tiếp tục đọc được theo rules cũ;
   writer mới chỉ nhận owned branch checklist. Retire không đổi historical version/FK.
5. UC017 tiếp tục dùng legacy generic template như trước. UC018 có thể chọn scoped
   checklist chỉ khi cùng branch, RoomType thuộc applicability bridge và có effective
   PUBLISHED version. Không tự mở rộng applicability khi tạo RoomType mới; muốn dùng
   scoped checklist, phải tạo type với template legacy trước rồi liên kết/apply qua
   checklist workflow. Không tự đổi default của các RoomTypes khi publish/edit checklist.
6. Cấp **existing** CHECKLIST_MANAGE/BRANCH cho HK_SUPERVISOR qua reviewed catalog script;
   không tạo permission code mới, không role-name bypass. Các role khác không đổi.
7. Chỉ thử trên disposable canonical trials, explicit guarded deployment entry point,
   idempotency/postflight/fault recovery/legacy compatibility/rollback refusal khi có
   dữ liệu mới. Không startup migration, không HotelOpsDB/shared/production deployment.
8. Database First: controlled partial/read projection cho extension, không ghi đè
   generated entities. Cần chỉnh shared partial mapping để EF tương thích triggers;
   UC017/018 eligibility và relevant consumers được regression. Đây là shared schema/
   mapping/catalog impact cho cả DEV1/DEV2, cần approval trước implementation.

Trong khi chờ: tiếp tục UC038/048 read workflows dùng schema hiện hữu. Nếu không được
duyệt, UC039/040 vẫn BLOCKED; không giảm plurality/publish/audit requirement để cộng UC.
