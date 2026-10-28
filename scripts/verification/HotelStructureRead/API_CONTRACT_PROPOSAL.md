# API contract proposal — chưa triển khai / chưa phê duyệt

Các routes, DTO names, query names và chi tiết validation dưới đây là thiết kế kỹ thuật đề xuất. SRS không quy định tên HTTP fields này. Các gate D1–D7 trong IMPLEMENTATION_PLAN phải được chốt trước task code tương ứng.

## Quy ước chung đề xuất

- GET, authenticated SQL-backed session và permission phù hợp; Cache-Control no-store.
- Paged envelope: { items, page, pageSize, totalCount }. page mặc định1, pageSize20, tối đa100. totalCount sau authorization + tất cả filters, không đếm ngoài scope.
- page/pageSize không hợp lệ:400; page vượt cuối:200/items rỗng. Sorting whitelist; không truyền tên SQL từ client. Unsupported sort/filter value:400, không silently ignore.
- Keyword trim; blank = không lọc. Giới hạn độ dài search, collation/case/accent semantics cần chốt cùng D7, không mặc định search mọi text/navigation.
- 401 khi không có session hợp lệ;403 khi thiếu quyền. Đề xuất cùng403 cho branch không tồn tại/inactive/ngoài scope, không phân biệt existence. Filter ID ngoài authorized location:400 chung, không lộ chi tiết resource; chỉ validate sau auth. Empty authorized result:200.
- Không trả EF entities/navigation graphs. Không trả password/hash/token/session IDs/credentials, user/guest identity hoặc task/inspection evidence.
- Các fields contact của branch không tự coi là secret, nhưng loại khỏi minimal directory vì chưa có nhu cầu. Description/scopeDescription cũng chưa đưa vào list.

## 1. Branch Directory — candidate, gate D1/D4/D7

Route: GET /api/branches

Query đề xuất: search (code/name), isActive (tri-state), page, pageSize, sortBy=code|name, sortDirection=asc|desc.
Default sort: code asc, branchId asc; tên/search/sort này là proposal, UC-013 không quy định branch-list order.

Item: { branchId, code, name, isActive }.

Chỉ branch caller được phép xem theo mapping UC-013 đã duyệt. Không được dùng “có đăng nhập”, bất kỳ UserBranchAccess, hoặc ROOM_VIEW làm quyền branch catalog mặc định. Mapping MASTER_DATA_MANAGE Global cho admin và scoped read cho OM còn pending. Inactive branch không được evaluator Branch hiện tại chấp nhận; isActive=false/all chưa thể cam kết nếu chưa duyệt cách đọc historical branch. Không bỏ active-branch security guard để hỗ trợ filter.

Đây chỉ là entry list UC-013, không trả cây vô hạn. Floor/Area typed tree, lazy expansion và paging rooms/departments là contract phụ thuộc cần review riêng; không tạo fake floor_area_id.

## 2. Department Directory — candidate, gate D1/D7

Route: GET /api/branches/{branchId}/departments

Query đề xuất: search (code/name), isActive tri-state, page, pageSize, sortBy=code|name, sortDirection.
Default sort code asc, departmentId asc; null isActive đề xuất All cho danh sách, phải được duyệt.

Item: { departmentId, branchId, code, name, isActive }.

branchId bắt buộc; authorize branch trước query. Chỉ Department.BranchId trùng approved branch. Không trả managerUserId/manager profile; không dùng ManagerUserId làm scope. Không giới hạn DH vào department của mình. UC-013 chưa cấp read permission cho mọi workflow consumer; chưa mở cho MR/Inventory/Internal Request chỉ vì cần dropdown.

## 3. Room Type Directory — contract bị chặn bởi D1/D2

Route đề xuất: GET /api/room-types

SRS target query: search (code/name), branchId, isActive, page, pageSize; sort mặc định code asc + roomTypeId tie-break.
SRS target item: { roomTypeId, code, name, branchId, activeRoomCount, isActive }.
branchId trong item target là yêu cầu SRS đang thiếu model, KHÔNG phải property có sẵn của RoomType.

Cần chọn và phê duyệt một phương án trước code:

| Phương án | DTO/semantics | Giới hạn |
|---|---|---|
| Global admin catalog slice | { roomTypeId, code, name, isActive }; optional standardCapacity nếu consumer xác nhận | Không branch filter/count; lệch UC-142 hiện tại, cần chấp thuận rõ |
| Usage projection theo authorized branch | Thêm branchId là query context, activeRoomCount chỉ đếm Room.IsActive trong branch đó | Không gọi là ownership; phải chốt có giữ unused types và type active filter độc lập room active hay không |
| Branch-owned types | Contract target SRS | Cần schema/business approval riêng; ngoài Task20A, không tự thêm BranchId/migration |

Không dùng Rooms.Any(branch) để “sửa” model âm thầm, không đếm phòng ngoài scope. Description và StandardCapacity có trong entity nhưng chưa bắt buộc list theo UC-142. Type lookup trong room workflow không tự kế thừa quyền full global catalog.

## 4. Room Directory — đề xuất task code đầu tiên sau D3/D5/D7

Route duy nhất đề xuất: GET /api/rooms?branchId={required}

Query: branchId bắt buộc; search; floorId?; areaId?; roomTypeId?; operationalStatusCode?; isActive?; page=1; pageSize=20.
Slice đầu chưa hỗ trợ All Branches (khác SRS UI, cần approval D5).
Permission: ROOM_VIEW với Branch check; không Global-only, không OWN fallback.

Item đề xuất:
{ roomId, branchId, roomNumber, floor: { floorId, code, name, number }, area: null | { areaId, code, name }, roomType: { roomTypeId, code, name }, operationalStatusCode, isActive }.

- search đề xuất RoomNumber only (SRS “such as room number” chưa chốt thêm identifiers). Blank không lọc.
- floorId phải thuộc branch; areaId phải cùng branch và nếu có floor filter thì phù hợp hierarchy. Cần xử lý Area.FloorId=null nhất quán, không suy ra floor giả.
- roomTypeId không có BranchId để validate ownership; chỉ lọc Room.RoomTypeId trong approved branch. Không kiểm tra type “thuộc branch” bằng field không tồn tại.
- Không tự ẩn Room chỉ vì Floor/Area/RoomType inactive; historical list và Room.IsActive độc lập. Inactive parent fixtures cần kiểm thử và product xác nhận.
- isActive absent = All theo Room List; branch vẫn phải active.
- operationalStatusCode là tên cố ý chỉ raw stored code: AVAILABLE/OCCUPIED/DIRTY/CLEANING/INSPECTION/READY/OUT_OF_SERVICE; invalid code400. Chỉ dùng nếu D3 phê duyệt slice này; chưa đáp ứng display-status SRS.
- Default order theo floor rồi room number. Đề xuất FloorNumber asc nulls-last, FloorCode asc, FloorId asc, RoomNumber asc theo SQL collation, RoomId asc. Chốt D7 cho floor nulls và lexical vs natural room ordering; không tuyên bố SRS đã chọn thuật toán này.
- Chưa mở arbitrary sort: chỉ default floor/room order; query sort khác bị400.
- Không trả statusReason, guest occupancy, assignee, incident detail hoặc history links cho tới khi chốt rules và quyền từng resource. Room Detail/status reason/history là phần UC-023 còn lại; board UC-027 không nằm trong list slice.

## Boundaries của lookup

Có thể dùng projected floor/type fields của những room đã authorized để render chính room list. Điều đó không cung cấp complete lookup (ví dụ unused types/floors) và không cấp quyền directory khác. Mọi full lookup mới cần actor/permission/scope contract rõ; không OR ROOM_VIEW với MASTER_DATA_MANAGE hoặc các permission nghiệp vụ để mở rộng quyền.
