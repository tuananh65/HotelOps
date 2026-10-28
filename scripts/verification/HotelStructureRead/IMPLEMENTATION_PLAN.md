# Implementation plan — không code trong Task20A

## Decision gates

| ID | Quyết định cần duyệt | Ảnh hưởng |
|---|---|---|
| D1 | Mapping quyền đọc UC-013 và UC-142; admin/OM và workflow consumers có được dùng MASTER_DATA_MANAGE hay quyền hiện có nào không? | Branch/Department/RoomType chưa thể coding; không tự tạo permission hay OR gates |
| D2 | RoomType global catalog, branch usage projection hay branch ownership đúng SRS? Unused types/count scope ra sao? | DTO/filter/SQL của UC-142; schema change nếu cần là task approval riêng |
| D3 | Chấp thuận raw operationalStatusCode cho list-only hay cần mapping display/status reason chuẩn SRS trước? | Không tự đồng nhất SQL states với UI enum; không sửa lifecycle/business modules |
| D4 | UC-013 tree shape: Floor/Area typed IDs, branch-level Area, expansion/paging, inactive branch visibility | Branch list + Department list chưa hoàn thành full structure UC |
| D5 | Chấp thuận Room Directory giai đoạn đầu một required branch thay vì UI All Branches; actor draft/catalog conflict | Không tự mở SYS_ADMIN/OWNER/STOREKEEPER; cần team business/security quyết định nếu muốn thay quyền |
| D6 | Lookup đầy đủ cho workflow consumers chưa có read grant sẽ dùng contract nào? | Phối hợp DEV2/Frontend, không mở rộng role ngầm |
| D7 | Chốt API naming, error/filter validation, search limits/collation, sort nulls/natural order, branch/dept active defaults | Đóng contract trước implementation, không coi technical proposal là SRS requirement |

## Trình tự đề xuất

1. DEV1 + người duyệt requirements/security chốt D3/D5/D7 cho Room Directory list. Đây là task coding đầu tiên được khuyến nghị vì ROOM_VIEW và model đã có; không cần đợi schema RoomType ownership nếu chấp thuận chỉ projection qua Room.
2. Triển khai scoped GET /api/rooms theo API_CONTRACT_PROPOSAL sau approval; một service/query/DTO/controller trong src/HotelOps.Api, async/CancellationToken/AsNoTracking. Không thêm project kiến trúc, migrations hoặc generated entities.
3. Chốt D1/D4, rồi Branch Directory và Department Directory slices. Hoàn thiện floor/area tree + selectors bằng contract review riêng; không gọi bốn lists là full UC-013.
4. Chốt D1/D2 trước Room Type Directory. Không ưu tiên code nhanh bằng cách tự bỏ branch/count yêu cầu.
5. D6 và multi-branch/detail/status-reason là follow-up có scope riêng, phối hợp DEV2 khi liên quan resource/workflows của họ.

Shared DI registration nếu implementation cần sửa Program.cs: giải thích impact và xin approval theo AGENTS trước khi sửa; approval scoped UC-141 cũ không áp dụng service mới. Proposed imperative authorization tận dụng handler hiện có, không yêu cầu tự sửa middleware ordering/catalog/resource resolver.

## First coding task: Room Directory list-only

Scope: GET /api/rooms, một active branch, filters và projection tối thiểu, không Room Detail/status derivation/history hay writes.

Required permission: ROOM_VIEW Branch, current valid session. Entities: Room + Branch/Floor/Area/RoomType; auth đọc User/Session/Role/UserRole/RolePermission/UserBranchAccess qua infrastructure hiện có.

Acceptance criteria sau khi gates được duyệt:

- Đúng route/query/DTO đã chốt; default20/max100, deterministic sorting và post-filter count, empty page200.
- Actor có effective ROOM_VIEW BRANCH chỉ nhận phòng branch được phép; sửa branch/filter IDs không vượt scope.
- Global-only policy không được dùng nhầm; OWN/unknown contexts fail-closed; admin không có ROOM_VIEW không bypass.
- Session/grant/user/branch invalid không trả dữ liệu hoặc count. ALL branchbound/nonbound và multiple assignments được kiểm chứng.
- Tri-state room active/status codes/search/sort theo approved contract; joins không làm mất historical inactive references ngoài ý muốn.
- No sensitive fields/navigation entities trong response; no-store; errors không rò branch existence.
- Source DEV2, Permission Matrix, SQL schema và HotelOpsDB không thay đổi.

## Fixtures tối thiểu — chỉ trial/test DB độc lập được xác minh

Canonical reference seed tách personnel fixtures; không yêu cầu tạo demo account trong canonical seed. Cần xác minh/sắp fixture bằng task được duyệt riêng, Task20A không tạo:

- Hai active branches BR001/BR002 và HK/MT/WH/FO mỗi branch; một inactive branch fixture nếu test active guard.
- Floors có/không FloorNumber; Area thuộc floor, Area.FloorId null, Area null trên room; fixture cross-branch IDs.
- Hai RoomTypes active/inactive, một chưa có room, một dùng ở hai branches để chứng minh global model.
- Rooms đủ stored statuses, active/inactive, room numbers và sort ties trải qua nhiều pages; rooms tham chiếu inactive floor/type nếu model cho phép.
- Users/sessions hợp lệ cho scoped allow/deny, revoked/expired/restricted/inactive cases; assignments và UserBranchAccess đúng branch. Không tái dùng UserId 4/19 hoặc credentials từ HotelOpsDB.
- ALL bound/unbound và OWN/unknown-scope cases nên kiểm thử evaluator bằng isolated fixtures/fakes nếu catalog cố định không có grant tương ứng; không sửa canonical catalog chỉ để tạo test.
- Không yêu cầu setup incident/stock/material transactions cho list-only; không sử dụng live HotelOpsDB làm fixture.

## Test strategy cho task implementation sau này

Build solution, relevant automated query/authorization tests, HTTP validation/pagination/DTO disclosure tests và isolated SQL-backed session/scope tests. Test duplicate room numbers across branches, adversarial filter IDs, sort stability và no permission-count leak. Kiểm tra generated SQL/translation/cancellation khi practical; không tuyên bố in-memory fake chứng minh SQL behavior.

Không chạy SQL integration tests trong review này. Không lấy 144 UC-141/167 regression checks trước đây làm kết quả Hotel Structure API.

## Validation Task20A

- Source inventory, entity/mapping review và remote refs đã kiểm tra; chưa có Hotel Structure Controller/Service/DTO.
- Chỉ documentation thay đổi; build/tests không chạy vì không sửa C# hoặc endpoint. API/database runtime: Not Run; không có endpoint mới để kiểm thử.
- git diff --check và git diff --cached --check: PASS; index và tracked working tree vẫn sạch. Kiểm tra riêng năm file untracked mới: không trailing whitespace hoặc dòng trống thừa cuối file.
- SHA-256: toàn bộ 560 file có trước task giữ nguyên; chỉ thêm đúng năm Markdown trong HotelStructureRead. Untracked tăng từ123 lên128; hai rehearsal worktrees giữ nguyên.
- Final verdict: READY WITH NOTES cho contract review; implementation bị chặn theo gates tương ứng, chưa phê duyệt triển khai toàn bộ nhóm.
