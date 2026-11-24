# Bổ sung đồ fill và đồ dùng bền vào phòng — 24/11/2026

## Kết quả đã xác minh

Đã cập nhật dữ liệu local Production `HotelOpsCompactDB` và Develop `HotelOpsCompactWorkdayTestP0261124`. Không sửa C#, frontend, schema hoặc grants. Cơ chế hiện tại đã dùng ID vật phẩm, ID phòng và sức chứa tối đa; lượt này bổ sung dữ liệu còn thiếu và kiểm chứng thực tế.

| Chi nhánh | Phòng | Fill được cấp | Đồ dùng bền được cấp |
|---|---:|---:|---:|
| Bùi Viện | 48 (45 phòng 2 khách, 3 phòng 4 khách) | 816 đơn vị | 192 đơn vị |
| Cao Thắng | 48 phòng 2 khách | 768 đơn vị | 192 đơn vị |

Mỗi phòng có 8 loại fill đang cấu hình và 4 loại đồ dùng bền: ấm đun, ly uống nước, móc áo, thùng rác, mỗi loại bền 1 đơn vị. Tổng 1.152 cặp phòng–vật phẩm ở mỗi môi trường. Không dùng tên vật phẩm hay tên phòng làm định danh.

Đồ dùng bền thuộc nhóm B được nhận diện từ phiếu xuất đã ghi sổ có `receiving_room_id` và dòng `item_id`, trừ phần hoàn kho/đảo chứng từ. Đồ fill nhóm A được cấp theo `capacity × per_guest_quantity`; cả 8 loại hiện có định mức 1/khách. Tiêu hao lúc hoàn thành vệ sinh dùng reservation đã chụp sức chứa, liên kết balance của đúng nhân viên và đúng item ID; không trừ đồ bền vào vật tư nhân viên.

## Nhập, xuất và nguồn gốc dữ liệu

- Người dùng duyệt rõ dữ liệu kiểm thử: nhập 100 đơn vị mỗi loại fill và mỗi loại bền mới cho mỗi kho. Develop Bùi Viện được duyệt riêng 102 đơn vị mỗi loại fill vì tồn ban đầu bằng 0.
- Mỗi môi trường tạo 194 chứng từ: 2 phiếu nhập, 96 phiếu yêu cầu vật tư và 96 phiếu xuất vào phòng. Phiếu xuất giữ source document/source line và kiểm tra số lượng đã duyệt, người duyệt độc lập với người xuất.
- Tên chứng từ bắt đầu `TEST-ROOM-261124-`; lý do ghi rõ **DỮ LIỆU KIỂM THỬ**, không phải chứng nhận nhận hàng thực tế. Audit ghi executor maintenance-script, synthetic=true, actor hệ thống; các user liên quan trong chứng từ chỉ là chủ thể của fixture đã được duyệt.
- Script chạy trong transaction, khóa vật tư và phạm vi tồn kho, kiểm tra tồn trước xuất; không vô hiệu hóa trigger hoặc kiểm tra concurrency. Chạy lại trên TEST tạo 0 chứng từ và không nhập/cấp trùng.
- Production sau cấp: mỗi loại fill còn 88 ở Bùi Viện và 104 ở Cao Thắng; mỗi loại bền còn 52 ở mỗi kho. Develop: fill còn 0 ở Bùi Viện và 4 ở Cao Thắng; đồ bền còn 52 mỗi loại mỗi kho. Develop cần nhập thêm fill để cấp cho ca mới; không tự tăng ngoài số lượng đã duyệt.

## Sao lưu

Tất cả backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công trước cập nhật:

- Production: `HotelOpsCompactDB_before_workday_20261124_201319153.bak`.
- Develop: `HotelOpsCompactWorkdayTestP0261124_before_workday_20261124_201426431.bak`.
- Bản sao migration TEST: `HotelOpsCompactWorkdayTestRoomSupplies261124`, được tạo từ Production và sao lưu riêng trước khi chạy script.
- TEST nghiệp vụ: `HotelOpsCompactWorkdayTestWarehouse261124` đã sao lưu trước tạo phòng kiểm thử và gọi API ghi dữ liệu.
- Manifests tại `artifacts/workday/backup-<database>.json`, chứa đường dẫn backup SQL Server thực tế và trạng thái verified.

## Kiểm thử

- Migration TEST: đủ 96 phòng/1.152 cặp, đúng sức chứa, không âm tồn; phiếu nguồn–phiếu xuất khớp ID phòng, ID vật phẩm và số lượng. Rerun tạo 0 chứng từ.
- Đối soát cùng kiểm tra trên Production và Develop: PASS. Bằng chứng `artifacts/room-supplies/audit-<database>.json` chứa danh sách từng phòng/vật phẩm/số lượng.
- `dotnet test tests/HotelOps.Compact.Tests/HotelOps.Compact.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~SupplyMathTests -v minimal`: 8 PASS, 0 FAIL, 0 SKIP. Dùng binary hiện có, không có thay đổi C# trong lượt này.
- API TEST thực: công việc 773, phòng 521 sức chứa 2, mỗi loại fill tiêu hao đúng 2; công việc 778, phòng 526 sức chứa 4, mỗi loại tiêu hao đúng 4. Gửi hoàn thành lại không trừ thêm; ledger mỗi việc đúng 8 item IDs, mỗi ID một bút toán USE.
- Sự cố thiếu đồ 774 giữ đúng roomId/itemId qua báo cáo, giám sát xác nhận, phiếu kho, cấp bù và kiểm tra lại. Báo cùng item ID cho phòng không được gán trả 409 `ROOM_ISSUE_OBJECT_NOT_IN_ROOM`.
- Hai công việc được kiểm tra đạt và phòng trở về READY. Cấp bù cho nhân viên dùng yêu cầu 772 qua supervisor/storekeeper, không sửa balance trực tiếp.
- API health Production5769/Develop5774 và frontend5080 trả 200 sau cập nhật. API TEST tạm PID2060:5775 đã dừng.
- Build backend/frontend: **Not Run trong lượt này**, vì không thay đổi source ứng dụng. Không chạy lại toàn bộ 28 E2E hoặc browser suite; đây là thay đổi dữ liệu, API workflow và đối soát SQL đã chạy thực tế.

## Lỗi đã xử lý khi kiểm thử

Lần migration TEST đầu thiếu phiếu vật tư nguồn, trigger từ chối và transaction rollback toàn bộ. Đã bổ sung chuỗi phiếu yêu cầu đã duyệt và liên kết source lines, giữ nguyên trigger. Script đối soát ban đầu có cột quantity không rõ nguồn; đã sửa alias và chạy lại đạt. Không có lần lỗi đó áp dụng lên Production/Develop.

## Files của lượt này

- `scripts/database/compact-v2/room-supplies.sql`, `room-supplies.ps1`: dữ liệu kiểm thử đã duyệt, backup guard, transaction, chống chạy trùng và đối soát.
- `scripts/verification/room-supplies-audit.ps1`: kiểm tra phòng, danh mục, tồn và liên kết.
- `scripts/verification/room-supplies-runtime-fixture.ps1`, `room-supplies-runtime.mjs`: phòng TEST 4 khách và kiểm thử API thực.
- `plans/261124-1957-room-supplies/plan.md`, báo cáo này, journal và phần bổ sung `docs/ui/compact-ux.md`.

Không commit/push. Không xóa lịch sử hay tài khoản; không sửa mật khẩu. Muốn hoàn tác dữ liệu đã ghi sổ cần chứng từ hoàn/đảo phù hợp; không xóa trực tiếp stock movements. Chỉ restore backup khi có chỉ định riêng và đã đánh giá giao dịch phát sinh sau backup.
