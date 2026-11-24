# P0 — actor, use case, trạng thái và routing

Trạng thái tài liệu: **IMPLEMENTED / runtime NOT_TESTED**, trừ các khoảng trống được đánh dấu PARTIAL trong [traceability](traceability.md). Nguồn source chính: `src/HotelOps.Api/Compact/Services/`; API và database có hồ sơ riêng. Các mã UC-P0 bên dưới là mã truy vết mới, không ghi đè số UC lịch sử.

## Actor và trách nhiệm

| Actor | Trách nhiệm | Giới hạn |
|---|---|---|
| SYS_ADMIN | Tài khoản, role, master data, audit quản trị | Không mặc định có quyền nghiệp vụ |
| OWNER | Xem toàn hệ thống theo các grant ALL | Không ghi nghiệp vụ |
| OPERATION_MANAGER | Nguồn lực, lịch, cấu hình, tài sản, công việc vận hành, duyệt bổ sung kho/điều chuyển phù hợp | Sự cố và IR thông thường chỉ xem; không xuất/nhập kho |
| HOUSEKEEPING_SUPERVISOR | Phân công dọn, kiểm tra giấy, xác nhận refill/sự cố, tái kiểm tra | Không nhận thay quyền thủ kho hay tự nghiệm thu việc đã thực hiện |
| HK_STAFF | Công việc được giao, vật tư cá nhân, yêu cầu refill, báo cáo phòng đúng phạm vi | Không ghi stock ledger trực tiếp, không phân công vượt vật tư |
| MAINTENANCE_SUPERVISOR | Tiếp nhận, giám sát sửa chữa bên ngoài, báo hoàn tất, xử lý lại | Không tạo/điều phối tài khoản kỹ thuật viên nội bộ |
| RECEPTION | Đặt/nhận/trả/chuyển phòng, báo hỏng phòng phù hợp | Không xác nhận sửa xong hay tự thay occupancy qua sự cố |
| STOREKEEPER | Xử lý MR nội bộ, cấp/nhận vật tư, bổ sung kho, điều chuyển theo chứng từ | Không duyệt yêu cầu bổ sung kho thay OM |

Vai trò cũ được thu hồi assignment hiệu lực, giữ lịch sử. DEPT_MANAGER → OPERATION_MANAGER; MAINTENANCE_LEAD → MAINTENANCE_SUPERVISOR; MAINTENANCE_STAFF → HK_STAFF. Danh sách role không thay thế kiểm tra permission, branch, membership, workday, assignment, ownership và handler.

## UC-P0-01 — đầu ca và cấp vật tư

Tiền điều kiện: tài khoản/role/membership hợp lệ, ca HK được phân và nằm trong khung giờ, chưa bắt đầu bằng một operation khác. Bắt đầu ca ghi `WORKING`, chụp cấu hình A của chi nhánh và cấp qua `StaffSupplyService.Start` trong cùng transaction.

Mỗi snapshot giữ configuration revision, item, unit, warehouse, số lượng đầu ca và định mức mỗi sức chứa. Đổi cấu hình sau đó không sửa allocation hiện có. Tám đồ fill mặc định: bàn chải, kem đánh răng, dầu gội, sữa tắm, nước uống, giấy vệ sinh, khăn tắm, khăn mặt; mặc định 10 đầu ca, minimum 50, một đơn vị/khách theo sức chứa. Đơn vị lấy catalog thật; tạo metadata không tạo tồn.

Đủ toàn bộ mặt hàng: chứng từ `SUPPLY_ISSUE` ghi kho giảm và staff balance tăng. Thiếu bất kỳ mặt hàng: không cấp một phần dưới danh nghĩa đã đủ; ca vẫn bắt đầu, allocation `SHORT`, ghi lý do và thông báo. Nhân viên không được nhận phòng vượt số vật tư khả dụng. Retry cùng operation không cấp trùng.

## UC-P0-02 — giao, đổi, hủy và hoàn tất dọn

Tiền điều kiện phân công: giám sát đúng chi nhánh/quyền; nhân viên đúng role, trong ca và trong khung giờ nhận việc; phòng/công việc hợp lệ. Cần vật tư = sức chứa phòng × định mức trong snapshot ca, không lấy số khách đang ở.

`Reserve` giữ từng mặt hàng; không trừ kho lần nữa. Đổi người giải phóng reservation cũ và tạo reservation mới trong một transaction; nhân viên mới thiếu đồ thì toàn bộ thay đổi thất bại. Hủy giải phóng phần giữ chỗ. `Consume` khi gửi hoàn tất chuyển `RESERVED → CONSUMED`, tăng used và giảm reserved đúng một lần. Vật tư đã rời kho đầu ca nên hoàn tất dọn không xuất kho lần hai. Làm lại không mặc định tiêu hao toàn bộ định mức lần nữa.

Nhân viên bắt đầu trực tiếp và mở báo hoàn tất; không yêu cầu lý do nhận/bắt đầu/hoàn tất, không cập nhật phần trăm, không bàn giao sau bắt đầu, không bắt tải minh chứng. Hạn 10–30 phút tính từ phân công; audit quá hạn của lượt giao giữ riêng với ca làm.

## UC-P0-03 — refill

`HK_STAFF → HOUSEKEEPING_SUPERVISOR → STOREKEEPER → staff balance`.

Nhân viên chọn mặt hàng trong snapshot allocation của mình, số lượng dương và lý do. Chỉ một yêu cầu refill mở/allocation. Tạo `PENDING_SUPERVISOR`. Giám sát xác nhận đầy đủ tập dòng, có thể giảm số lượng dương trong giới hạn đề nghị; chuyển `PENDING_STOREKEEPER`. Thủ kho cấp toàn bộ lượng đã duyệt sau kiểm tồn, quyền, thời gian ca và version. Thành công `FULFILLED`; staff additional issued tăng và kho giảm cùng transaction. Thiếu kho không báo thành công; từ chối bắt buộc lý do. Người tạo có thể hủy yêu cầu refill chưa kết thúc. OM chỉ xem.

Không cấp vào allocation đã đóng hoặc quá khung giờ tiếp nhận refill. Ca quá giờ còn refill mở phải giải quyết/hủy hợp lệ trước khi kết thúc; không bỏ dữ liệu yêu cầu để ép đóng ca.

## UC-P0-04 — kết thúc ca

Quyết định mới thay quy tắc tự hết ca HK: ca HK còn `WORKING` sau giờ dự kiến để hoàn tất công việc đã giao. Không nhận phân công/refill mới ngoài giờ. Nút Kết thúc chỉ hiện theo `canEnd` của backend; `endBlockedReason` giải thích công việc, reservation hoặc refill còn mở.

Nhân viên đã gửi xong việc đang chờ giám sát kiểm tra không bị buộc ở lại chỉ để đợi nghiệm thu. Khi đủ điều kiện, End trả toàn bộ remaining về kho, ghi `SUPPLY_RETURN`, đóng allocation và kết thúc ca với giờ thực tế, cùng transaction. Retry không hoàn trả trùng. Non-HK vẫn theo expiry hiện hành. Không suy ra overtime đã được duyệt chỉ vì ghi nhận giờ làm thực tế.

## UC-P0-05 — cảnh báo thấp và bổ sung kho

`Stock < Minimum` mở một episode theo policy và gửi đúng thủ kho hoạt động trong chi nhánh. Stock đạt lại minimum đóng episode/thông báo; giảm lần sau mở episode mới. Minimum 50 nghĩa 49 cảnh báo, 50 không cảnh báo. Reconcile ở writer và scheduler; tất cả cùng stock transaction khi phát sinh từ writer.

Thủ kho tạo yêu cầu bổ sung: `PENDING_OPERATIONS_MANAGER`. OM độc lập xác nhận lượng → `PENDING_RECEIPT`; **duyệt không tăng tồn**. Thủ kho ghi hàng thực nhận qua receipt; nhận một phần giữ pending, nhận đủ `FULFILLED`. Không tạo supplier management hay giả định hàng đã đến. Lịch sử mỗi receipt được giữ; retry không nhân đôi.

## UC-P0-06 — thiếu vật dụng lâu bền

HK báo đồ nhóm B đã gán vào phòng thuộc công việc được giao. Phòng cố định, tiêu đề tự tạo, không tự chọn bộ phận xử lý/checklist/hạn ngày. Giám sát kiểm tra và xác nhận hoặc từ chối có lý do. Xác nhận tạo yêu cầu khắc phục vật tư cụ thể cho kho; thủ kho thực xuất vào phòng, không vào staff consumable balance.

Kho chỉ báo đã khắc phục khi chứng từ liên kết đã cấp đủ. Sau đó giám sát kiểm tra giấy. Không đạt: giữ lịch sử, ghi rõ lượng còn thiếu mới, tạo lượt khắc phục mới; không tự xuất lại toàn bộ. Đạt: đóng đúng sự cố và đánh giá readiness chung. Thiếu tồn không được đánh dấu khắc phục/READY; kho có thể tạo replenishment riêng.

## UC-P0-07 — hỏng thiết bị và sửa chữa ngoài

HK hoặc Reception báo thiết bị nhóm C đã gán phòng. HK cần đúng công việc; Reception chỉ loại BROKEN. Giám sát xác minh; sai báo cáo thì reject, không phát sinh sửa chữa/kho. Báo cáo hợp lệ chuyển Maintenance Supervisor.

Maintenance Supervisor tiếp nhận tạo công việc MAINTENANCE con, tự chịu trách nhiệm giám sát; bắt đầu và báo hoàn tất. Không phân công kỹ thuật viên nội bộ. HK Supervisor nhận tái kiểm tra. Không đạt quay lại sửa, lưu snapshot lần hoàn tất trước; đạt đóng công việc sửa và sự cố, đánh giá readiness. Người từng thực hiện sửa không tự nghiệm thu qua quyền giám sát.

## Trạng thái triển khai

Không đồng nhất trạng thái `work_items`, coordination request và sự kiện room issue. Chúng có mục đích riêng, cập nhật cùng transaction.

| Nghiệp vụ | Chuỗi chính | Nhánh kết thúc/ngoại lệ |
|---|---|---|
| Refill coordination | PENDING_SUPERVISOR → PENDING_STOREKEEPER → FULFILLED | REJECTED / CANCELLED |
| Replenishment coordination | PENDING_OPERATIONS_MANAGER → PENDING_RECEIPT → FULFILLED | REJECTED trước duyệt; receipt một phần vẫn PENDING_RECEIPT |
| Room coordination | PENDING_SUPERVISOR → queue chuyên môn → PENDING_REINSPECTION → COMPLETED | REJECTED; REWORK_REQUIRED hoặc quay queue chuyên môn |
| Supply reservation | RESERVED → CONSUMED | RESERVED → RELEASED |
| Allocation | SHORT hoặc OPEN → CLOSED | Xem lý do thiếu; không giả cấp thành công |
| Room issue WorkItem | OPEN → IN_PROGRESS → RESOLVED → CLOSED | CANCELLED khi báo cáo bị từ chối; fail trở lại IN_PROGRESS |
| Maintenance con | PENDING → IN_PROGRESS → COMPLETED → CLOSED | Kiểm tra không đạt quay PENDING kèm event snapshot lần trước |

Room issue event stage hiện tại dùng các mã `ROOM_ISSUE_REPORTED`, `APPROVED`, `REPAIR_REQUESTED`, `REPAIR_CREATED`, `MATERIAL_REQUESTED`, `INSPECTION_REQUESTED`, `INSPECTION_FAILED` (đọc với tiền tố ROOM_ISSUE theo event). Đây không phải giá trị occupancy mới.

## UC-P0-08 — readiness

`RoomReadinessService.Reevaluate` đọc policy view trong transaction sau khi lưu sự kiện. READY cần đúng chu kỳ, vệ sinh/kiểm tra hợp lệ, không việc/làm lại/sự cố/bảo trì đang ngăn sẵn sàng và không block hiệu lực. Mỗi sự cố có block độc lập; giải quyết một sự cố không gỡ block khác. SAFETY hold không tự bị gỡ chỉ vì báo sửa xong.

Service không thay đổi OCCUPIED, RESERVED hay OUT_OF_SERVICE; không tự đổi khách/đặt phòng. Kiểm tra giấy đạt là điều kiện cần, không là nút ép READY. Lượt dọn/turnover trước không làm chứng cứ cho lượt mới.

## UC-P0-09 — workspace yêu cầu thống nhất

Inbox là projection từ hồ sơ gốc; không tạo task bản sao để hiển thị. Request có type, branch, người tạo, role queue, current handler, correlation, dòng lượng, timeline và version. Một handler được chọn khi duy nhất đủ điều kiện; nếu không, giữ hàng đợi role. Claim/release phải đúng actor, ca, status, queue và version. Không có API tự do sửa queue/status.

Domain room issue dùng API chuyên biệt, vẫn attach typed metadata và event vào cùng root. Refill/replenishment dùng API requests; generic Work/Document không được làm đường tắt duyệt/cấp phát. Legacy projection giữ domain đọc cũ, không bịa lịch sử thiếu. OM đọc yêu cầu giữa bộ phận, chỉ xử lý replenishment thuộc thẩm quyền.

## UC-P0-10 — điều chuyển liên cơ sở và tài sản

Vật tư: OM nơi nhận tạo yêu cầu → OM nơi cấp duyệt → thủ kho nơi cấp xuất → thủ kho nơi nhận xác nhận thực nhận, cho phép từng phần. Duyệt không đổi tồn; xuất chỉ giảm nguồn, nhận chỉ tăng đích; sai lượng/chi nhánh/version bị chặn. Hủy trước dispatch; không tự đảo transfer xuyên chi nhánh khi chưa có compensation workflow được duyệt.

Tài sản dùng AssetService hiện có: di chuyển phòng/vị trí cùng branch, bàn giao người giữ có người nhận accept/reject, đổi tình trạng theo chứng cứ sửa chữa, ngừng sử dụng nếu không còn việc mở. Đây là luồng khác với chuyển kho. Phạm vi tài sản mở rộng của A cần nghiệm thu riêng, không được suy là đã xong chỉ từ role grants.

## UC-P0-11 — công việc vận hành OM

OM tạo/giao/thực hiện công việc OPERATIONAL; quyền tự xác nhận riêng OPERATIONAL có audit `OM_OPERATIONAL_SELF_REVIEW`. Ngoại lệ này không áp dụng cho sửa chữa, kiểm tra phòng hoặc duyệt MR. Dashboard/action center cần link đến hồ sơ thật; sự cố/IR chỉ là cảnh báo xem, không hiện hành động phê duyệt của OM.

## Bất biến và UX cần nghiệm thu

- Remaining = initial issued + additional issued − used − returned; Available = remaining − reserved. Mọi thành phần không âm; reservation không vượt available.
- Stock closing = opening + posted incoming − posted outgoing. Không đếm return hai lần khi phân loại incoming.
- Rowversion, operation key/payload, transaction và khóa điều phối cùng bảo vệ race/retry; source checks chưa chứng minh concurrency thực tế.
- Giữ grid phòng, drawer, checklist giấy, lịch sử vệ sinh theo ngày, nút nguy hiểm đỏ/chuyển vàng, thẻ KPI trước ca, màu trạng thái đã chấp nhận.
- Tám actor permission navigation; loại menu riêng role cũ; filter ẩn/reset; form mutation trong dialog/drawer; mobile không tràn ngang, chạm dòng mở chi tiết.
- Hành động UI dựa capability backend; lỗi không báo success; không dùng dữ liệu giả cho số liệu thật. Rà soát nguồn cũ trong Compact UX chỉ theo các ngoại lệ P0 nêu trên.
