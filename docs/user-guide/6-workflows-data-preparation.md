# Chuẩn bị dữ liệu cho 6 workflow — khảo sát và kết quả thực hiện

Ngày: 18/11/2026. Database mục tiêu: `localhost / HotelOpsDB`, nhập từ dbdemo.sql.

## Trạng thái thực hiện

**Cập nhật sau khi được người dùng cho phép:** đã backup/phục hồi bản sao, thử schema, áp dụng lên HotelOpsDB và tạo bộ dữ liệu đầu vào `W6_20261118`. Dữ liệu cũ và quyền được đối chiếu giữ nguyên. Xem [bảng ID, kết quả kiểm chứng và giới hạn còn lại](6-workflows-test-data.md). Chưa nghiệm thu E2E đủ 6 workflow.

Phần khảo sát bên dưới ghi nhận **trạng thái trước nâng cấp**, giữ lại để đối chiếu; các mục thiếu schema không còn là kết luận hiện tại. Giới hạn hiện tại gồm quyền `MR_VIEW`, danh mục phân loại sự cố và việc chưa kiểm chứng toàn bộ workflow trên HTTP/UI.

Đã chuẩn bị [preflight chỉ đọc](../../scripts/database/development/Check-SixWorkflowReadiness.sql). Đây là kiểm tra blocker tối thiểu, không phải chứng nhận đầy đủ toàn schema. Có mặt marker không có nghĩa package đã được kiểm chứng; không được thêm marker giả để vượt guard.

## Dữ liệu đang có, không cần tạo lại

| Đối tượng | Số bản ghi thực tế |
|---|---:|
| Phòng | 15 |
| Công việc vệ sinh | 15 |
| Kiểm tra phòng | 13 |
| Sự cố | 12 |
| Bảo trì | 12 |
| Yêu cầu vật tư | 13 |
| Phân ca | 17 |

Chi nhánh `DVBV` (ID hiện tại 1) và `DVCT` (ID hiện tại 2) đang hoạt động. ID chỉ là kết quả quan sát, không được hard-code vào logic nghiệp vụ. Ca có trong dữ liệu nguồn từ 15–17/10/2026, không phải lịch làm việc ngày test hiện tại.

Ví dụ để đọc/đối chiếu lịch sử, **không tự nhận các bản ghi legacy này vào workflow mới**:

| Dữ liệu | ID hiện tại | Chi tiết |
|---|---:|---|
| Phòng 201 / DVBV | 2 | Trạng thái ghi nhận DIRTY |
| Phòng 402 / DVBV | 9 | Trạng thái ghi nhận DIRTY |
| Phòng 303 / DVBV | 7 | Trạng thái ghi nhận OUT_OF_SERVICE |
| Vệ sinh CLN-20261016-001 | 1 | Phòng 2, status legacy PENDING |
| Vệ sinh CLN-20261016-005 | 6 | Phòng 9, status legacy PENDING |
| Inspection | 2 | Phòng 5, inspector ID 8, IN_PROGRESS |
| Inspection | 4, 5, 6 | Phòng 6, chuỗi kết quả legacy FAIL, FAIL, PASS |
| Bảo trì | 1 | Incident 1, assignee 9, ON_HOLD |
| Yêu cầu MR-20261016-001 | 11 | Requester 2, DRAFT |

Trạng thái legacy không tự động tương đương `workflow_status` mới. Không sửa ngày cũ thành ngày hiện tại, không tự đặt kết quả PASS/Ready để tạo cảm giác đã hoàn tất.

## Blocker đã xác định

| Workflow | Điều kiện còn thiếu / giới hạn |
|---|---|
| 1. Phòng/lễ tân | Có phòng để tra cứu; chưa xác nhận luồng check-in/check-out. Các dữ liệu hiệu lực/chu kỳ kiểm tra phụ thuộc foundation chưa đủ. |
| 2. Vệ sinh | `cleaning_tasks` chưa có `row_version`, `workflow_status`; chưa có marker Housekeeping/Progress. Cần kiểm tra cả foundation, assignment, start, resume, closure theo service hiện tại. |
| 3. Kiểm tra/làm lại | `room_inspections` chưa có `row_version`; chưa có marker Checklists. Cần checklist theo branch, snapshot và các điều kiện kiểm tra/làm lại mà source hiện tại yêu cầu. |
| 4. Sự cố/bảo trì | `maintenance_works` chưa có `row_version`; chưa có `maintenance_material_usages`. Foundation sự cố/bảo trì và liên kết workday phải được kiểm chứng trước khi thực hiện. |
| 5. Vật tư | Chưa có `material_request_approval_cycles`, `material_request_approval_line_snapshots`, `material_requests.row_version`. Không thể giả lập lịch sử duyệt bằng cách chỉ đổi status. |
| 6. Ca/thời gian | Chưa có `workdays`, `work_intervals`. Phân ca cũ không thể thay thế hai bảng này. |

Không có database-level extended properties trong bản nhập được kiểm tra. Đây là dấu thiếu receipt của các package, không chỉ là thiếu bản ghi test.

## Bộ dữ liệu đề xuất sau khi prerequisite đạt

Mã nhận diện chung: `W6_20261118`, đặt ở trường mã/ghi chú phù hợp đã tồn tại; không tạo cột mới chỉ để đánh dấu fixture. Lưu manifest ID bên ngoài database và chỉ quản lý bản ghi do bộ chuẩn bị tạo.

| Workflow | Dữ liệu chuẩn bị | Người tham gia / nguyên tắc |
|---|---|---|
| 1 | Dùng chi nhánh DVBV; tra cứu phòng gốc. Với luồng ghi, tạo phòng test riêng nếu phòng gốc đang có chu kỳ/block chưa giải quyết. | `le.truong`; không tạo quyền Reception hoặc chuyển trạng thái chưa được duyệt. |
| 2 | Hai kịch bản vệ sinh mới: một chờ phân công, một dùng cho chuỗi thực hiện rồi kiểm tra. Dùng loại vệ sinh/checklist hợp lệ từ server. | `mai.tran` phân công, `lan.dang` thực hiện. Không ghi đè task legacy. |
| 3 | Một lượt kiểm tra đạt và một lượt thất bại có lỗi/làm lại; các trạng thái trung gian phải phát sinh qua service đúng thứ tự. | Người kiểm tra được phép phải khác người thực hiện vệ sinh. Không tự bật role HK_INSPECTOR đã tắt. |
| 4 | Một sự cố test gắn phòng, một công việc bảo trì chờ người được phân công thực hiện. Chỉ thêm chứng từ/vật tư liên quan khi cần. | `minh.le` và `thinh.ngo`; không dùng cùng người thực hiện và xác minh. |
| 5 | Một yêu cầu bản nháp để người dùng tự Submit; một kịch bản đủ tồn; một kịch bản thiếu để cấp một phần nếu quyền/schema hỗ trợ. Tồn phải có chứng từ nguồn hợp lệ. | Người yêu cầu, người duyệt được chỉ định và thủ kho tách biệt; dùng `ha.pham`/người duyệt hợp lệ/`tung.vo` theo quyền thực tế. |
| 6 | Ca hợp lệ theo ngày chạy chuẩn bị, gán cho `lan.dang`, `thinh.ngo`; mặc định chưa bắt đầu workday để người dùng test Start/End. | Không sửa lịch sử tháng 10. Giờ nhập UTC được quy đổi từ Asia/Bangkok; kiểm tra ca chồng lấn. |

Giữ 8 tài khoản đã cấp mật khẩu tạm và sysadmin nguyên trạng. Nếu thiếu membership/quyền nghiệp vụ, lập danh sách chính xác để duyệt riêng; không tự mở rộng role. Mật khẩu hết hạn không được tự reset trong lượt chuẩn bị dữ liệu.

## Phạm vi nâng cấp cần duyệt trước khi ghi

1. Backup `HotelOpsDB` hiện tại, bao gồm những thay đổi người dùng đã test sau lần import.
2. Tạo bản sao phục hồi riêng, xác minh đúng server/database/GUID và khả năng khôi phục trước khi thử thay đổi.
3. Lập và kiểm tra gói nâng cấp tối thiểu từ các script trong repository cho 6 workflow, chỉ áp dụng phần phù hợp; thử rollback và áp dụng trên bản sao trước. Không chạy nguyên installer có guard của máy/database khác.
4. Khi bản sao đạt kiểm chứng, áp dụng cùng gói đã kiểm tra lên `HotelOpsDB`; bảo toàn dữ liệu lịch sử. Không backfill trạng thái workflow legacy bằng suy đoán.
5. Tạo dữ liệu nghiệp vụ qua service/API hiện có theo bảng trên; kiểm tra foreign key, audit, role/branch/SoD, phản hồi API và màn hình cho từng kịch bản.

Các nguồn package đã tìm thấy, **chưa chạy và chưa khẳng định đây là toàn bộ dependency**:

- `scripts/database/development/fe04-foundation-v1.json`: room types/checklists/housekeeping/progress và bộ kiểm chứng đi kèm.
- `scripts/database/development/Install-Fe04Foundation.ps1`: nhiều batch cho execution/resume/closure/cancellation; đang ràng buộc database cũ nên không dùng trực tiếp cho bản import này.
- `scripts/database/development/Install-IncidentFoundation.ps1` và `Install-MaintenanceFoundation.ps1`.
- `database/patches/20261019_inventory_contract.sql`, `20261020_material_request_contract.sql`, `20261022_maintenance_material_usage_contract.sql`.
- Mapping `src/HotelOps.Api/Data/HotelOpsDbContext.WorkTime.cs` và các script prerequisite thời gian làm việc cần đối chiếu thêm khi lập gói nâng cấp.

Không vô hiệu hóa trigger, CHECK/FK hoặc guard; không giả receipt, không dùng script fixture có quy tắc chỉ chạy database rỗng trên database hiện tại.

## Quy định và xác nhận phạm vi

AGENTS.md / Database Verification: “Do not create or modify database tables, columns, foreign keys, or migrations without explicit approval.”

Người dùng đã xác nhận: “cho phép backup, thử nâng cấp schema trên bản sao, rồi áp dụng lên HotelOpsDB và tạo dữ liệu test, giữ nguyên dữ liệu cũ và quyền hiện tại”. Bước nâng cấp/seed đã được thực hiện trong phạm vi đó. Không cần xin lại quyền backup/nâng cấp đã được cấp.

Kết quả mới: backup/schema/seed/kiểm chứng database Passed; build Passed; service Start/End workday trên bản sao Passed và rollback. Solution không có test project; E2E 6 workflow chưa hoàn tất. Báo cáo đầy đủ tại [6-workflows-test-data.md](6-workflows-test-data.md).
