# Task84 — UC142 RESTORED & REVERIFIED

**DONE trên disposable V1 trial, bằng production source hiện tại.** Không phải deployment tới DB chung.
UC142 đã earned1 trong lịch sử; không cộng điểm mới.26/75 UC,60/256=23.4375% giữ nguyên.

## Source tích hợp thật

Ba file DEV1 mới:
- Controllers/RoomTypeDirectoryController.cs: đúng hai GET lịch sử `/api/room-types` và
  `/api/room-types/{id}`, Cookie/HTTPS, query binding, DTO, ETag/no-store/error mapping.
- Services/HotelStructure/RoomTypeDirectoryService.cs: list/detail, SQL scope/filter/count/order/page,
  active-room count, historical revision tag, retained transaction và session/permission gates.
- Services/HotelStructure/RoomTypeDirectorySchema.cs: read-only V1 schema guard từ snapshot.

Program.cs chỉ thêm đúng một AddScoped<RoomTypeDirectoryService>(), sau CleaningMonitorService.
User duyệt riêng; DI_APPROVAL_APPLIED.json chứng minh bỏ đúng dòng mới sẽ khôi phục byte hash ban đầu.
Không sửa registration cũ, middleware, authorization, DbContext/entities, schema source, RBAC hoặc DEV2.
DTO RoomTypeBatchDtos.cs có sẵn được giữ nguyên. Không phục hồi các writer UC017/018/019/UC038.

Read service được tách có chọn lọc từ partial service lớn cf8fb02, thay db.BranchRoomTypes bằng
EF unmapped read projection sau cùng schema guard; không cần đổi global model hoặc suy ownership.
SQL lọc scope trước count/page. Legacy ownership NULL chỉ global ALL thấy; wholly legacy schema
không được relabel V1. CanUpdate và ETag contract giữ lịch sử; không bịa writer endpoints.

## Acceptance thật

Production Program startup, real pinned HTTPS certificate, credentials/cookies/CSRF và SQL sessions.
**125 main +6 account-eligibility =131 checks PASS**. Đây là assertion/check counts, không131UC hay
framework test cases. Accepted receipts: https-sql-resume-receipt.json và account-https-receipt.json.

Verified: global và branch-bound role scope, automatic/explicit branch, legacy global-only ownership,
code/name/status filters, stable tied-code pagination, empty/invalid queries, detail, active count,
strong ETag/action information, anonymous/missing permission/restricted/cross-branch rejection,
role/membership/session revocation, SQL session expiry, inactive account và inactive employment.
DMV xác nhận actual HTTPS detail chờ SQL writer; writer rollback trả dữ liệu gốc. Business/audit
fingerprints không đổi qua GET. Role/permission/catalog/master fingerprints giữ nguyên.

Canonical MASTER_DATA_MANAGE chỉ có ALL grant: test global và branch-bound role assignments dùng
catalog thật, không tạo BRANCH/OWN grants. HK_STAFF với OWN ở module khác không tự có master permission.
Generic core OWN/BRANCH/ALL checks nằm ở security regression; không giả claim live OWN master grant.

## Build, DI và regression

- Production API build PASS,0errors,2NU1900 warnings. Harness builds PASS,4NU1900 entries mỗi build.
-19 fresh-source DI/static/boundary checks PASS; production request200 còn chứng minh actual DI/startup.
- Post-DI security rebuild/run:93+43+14+49=199PASS. Doubles/loopback, không thay live business acceptance.
- Framework dotnet test exit0,0cases: NOT AVAILABLE, không tính PASS.
- Diff/file/Git/postflight chi tiết trong TASK84_VALIDATION.json; Task82 source được giữ nguyên.

## Trial và recovery minh bạch

Đúng một DB mới `HotelOps_Canonical_20261104_044211_7a6d6e19_Fresh` trên instance đã duyệt.
Script kiểm instance/nonexistence trước CREATE, marker/target trước prerequisites/fixtures.
SQL_SCOPE_REVIEW.json ghi nguồn/hash/context/server-operation review. Master chỉ dùng kiểm tra/tạo
đúng target; mọi schema/seed/fixture write trong new target. Giữ trial; không reset/drop DB nào.

Lần fixtures đầu fail SQL334 do EF OUTPUT trên rooms có trigger. Room inserts rollback;6users,
role assignments/floors/types/checklist đã commit được giữ nguyên. Guarded continuation xác minh
checkpoint rồi thêm6identity mới và6rooms bằng parameterized INSERT, không thay shared mappings,
không disable trigger, không xóa/reset fixtures. Probe account thêm1identity riêng. Failed log/code
được giữ; không tính thất bại thành PASS. Production read source không cần sửa để khắc phục lỗi fixture.

Initial verifier CS0117 do controller proposal ở sai assembly được sửa bằng validation-only compile
layout; internal auth API không đổi. Patch mixed line endings và lỗi PowerShell ToHexString trong
receipt đã sửa riêng artifacts; one-line source scope được xác minh bằng before/after SHA.

## Giới hạn và bước tiếp

Chỉ chứng nhận UC142 read runtime trên approved V1 trial. Không deploy production/shared database,
không chứng nhận các FE-02 writer chưa phục hồi; future EF writers với V1 triggers cần mapping review
riêng. No notification/write audit required cho UC142. Không có dependency UC142 read còn mở.
Task85 có thể recovery UC038 riêng (đã counted,0new points); chưa xác minh được một unfinished UC
khác đạt full acceptance độc lập để hứa cộng điểm. Không bắt đầu Task85.
