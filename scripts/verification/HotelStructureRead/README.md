# Task 20A — Hotel Structure Read API Contract Review

Ngày review: 2026-10-26. DEV1; phân tích và đề xuất, chưa phê duyệt contract triển khai.

## Baseline và phạm vi

- Branch: feature/BE1-database-integration; HEAD 20c1063e24cf170450d2dffaa81df6c7ef3a896f.
- origin/feature/BE1-database-integration trùng HEAD. origin/develop: 5299b5e9e8dd917abef327ab9f9bd5494064ccf9, không có commit mới so với baseline tích hợp.
- Đã kiểm tra remote bằng git ls-remote; không fetch/pull/merge. Không thay đổi refs.
- Tracked working tree và index sạch trước review; 123 file untracked trước task được bảo toàn. Hai rehearsal worktrees được giữ nguyên.
- Source hiện có Authentication/RBAC và GET /api/users. Chưa có Controller/Service/DTO cho bốn directory được review. Các module DEV2 không được chỉnh sửa.
- Đọc AGENTS.md, Dev1BackendScope/USE_CASE_COVERAGE_MATRIX.md và IMPLEMENTATION_BACKLOG.md. Hồ sơ audit cũ là snapshot trước UC-141, không dùng trạng thái “chưa có” của UC-141 làm hiện trạng mới.

## Hồ sơ

- [Use case và nguồn bằng chứng](USE_CASE_MAPPING.md).
- [Contract đề xuất và các điểm chưa thể chốt](API_CONTRACT_PROPOSAL.md).
- [Quyền và scope](AUTHORIZATION_MATRIX.md).
- [Trình tự triển khai, fixtures và verification](IMPLEMENTATION_PLAN.md).

## Kết luận review

READY WITH NOTES cho hồ sơ chuẩn bị; chưa đủ điều kiện triển khai toàn bộ nhóm APIs.

1. Branch/Department Directory là lát cắt UC-013, không có UC hoặc permission đọc độc lập được xác nhận.
2. UC-142 yêu cầu Room Type theo branch và active-room count; RoomType hiện không có BranchId. Không được tự suy diễn quyền sở hữu từ các Room đang sử dụng type.
3. UC-023 có ROOM_VIEW nhưng actor draft và catalog khác nhau. SYS_ADMIN, OWNER, STOREKEEPER không được tự động vượt qua gate này.
4. Trạng thái phòng trong SRS UI không tương đương trực tiếp CHECK constraint SQL. Contract mã thô/list-only phải được duyệt trước khi code.
5. UC-013 còn yêu cầu floors/areas tree; bốn list proposals không hoàn thành toàn bộ UC-013 hoặc room detail/status reason của UC-023.

## Nguồn và giới hạn xác minh

Nguồn chính trong repository:

- docs/requirements/SRS_CHANGE_NOTES.md: Reception, DH branch scope, Permission Matrix pending.
- docs/requirements/srs_sections/01_overall_requirements.md: UC inventory, §4.4 draft Permission Matrix và footnote restricted branch/department.
- docs/requirements/srs_sections/02_use_case_specifications.md: UC-013, UC-023, UC-142; UC-012/024–026 liên quan nhưng là writes.
- docs/requirements/srs_sections/03_functional_requirements.md: Room List/Detail, Room Type List.
- docs/requirements/srs_sections/05_requirement_appendix.md: GBR-001/002/015, SYS-PAR-11.
- src/HotelOps.Api/Data/HotelOpsDbContext.cs và Entities/: model hiện tại.
- scripts/database/release/01_HotelOps_Full_Schema.sql; 03_HotelOps_RBAC_Catalog_Seed.sql: schema/catalog source, không phải xác nhận dữ liệu live.
- src/HotelOps.Api/Security/Authorization/: evaluator, policy handler và cleaning resource resolver.
- src/HotelOps.Api/Controllers/UsersController.cs: tham chiếu phong cách DTO/no-store, không sao chép Global gate sang ROOM_VIEW.

Task này không kết nối database, không chạy SQL/integration tests; không suy diễn trial receipts cũ là kiểm thử API mới. Chỉ thêm năm tài liệu, không code, stage, commit, push hoặc merge. Kết quả kiểm tra cuối được ghi tại IMPLEMENTATION_PLAN.md.
