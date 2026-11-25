# Chuẩn bị database chính thức — 25/11/2026

Đích được Owner xác nhận: HotelOpsCompactDB. Chỉ xóa dữ liệu có bằng chứng test; giữ tài khoản, danh mục và giao dịch thực tế. Chưa thay đổi database chính và chưa chuyển kết nối Production.

## Đã kiểm chứng

- Kiểm kê trực tiếp database chính: 49 bảng, không có FK/CHECK mất trust hoặc trigger bị tắt.
- Backup COPY_ONLY CHECKSUM, RESTORE VERIFYONLY WITH CHECKSUM đạt; restore mới thành HotelOpsCompactWorkdayTestOfficial2611252122. Metadata lưu ở artifacts/workday/backup-HotelOpsCompactDB.json.
- Xác định 194 chứng từ fixture qua audit AUTHORIZED_ROOM_TEST_DATA có synthetic=true và prefix/reason tương ứng; không suy test từ mã danh mục DEMO/P0.
- Runner Invoke-OfficialV2Upgrade.ps1 kiểm hash 11 bước, backup và database đích; bao transaction, kiểm bảo toàn credential/ledger/chứng từ/danh mục/lịch sử. Production cần proof rehearsal commit cùng mã và baseline, cổng5080 dừng.
- Đã chạy đủ 11 bước trên clone, đạt 57 bảng/234 FK/116 CHECK/38 trigger, trusted/enabled và DBCC CHECKCONSTRAINTS. Transaction rollback thành công; proof artifacts/official-v2-upgrade/HotelOpsCompactWorkdayTestOfficial2611252122-dry-run.json.
- Lần chạy đầu bị chặn vì kiểm tuổi backup trộn UTC/local; đã sửa dùng DateTimeOffset.UtcDateTime và chạy lại thành công. Không có thay đổi database ở lần bị chặn.
- Rà artifact phát hiện fingerprint baseline rỗng khi gặp bảng không có dòng. Đã bổ sung xử lý bảng rỗng, bắt buộc hash64 ký tự và chạy lại toàn bộ rollback rehearsal thành công. Các lần chẩn đoán bị chặn/rollback; không áp dụng lên database chính.

## Điểm chưa thể thực hiện an toàn

MR440/ISSUE441 cấp1 ấm phòng103 không có audit chứng minh test. Xóa nguồn fixture194 trong khi giữ hai phiếu này khiến kho413/item222 âm1. Câu hỏi provenance đã gửi Owner; không tự xóa hai phiếu hoặc tạo thêm nguồn nhập để cân kho.

Hai phiếu có liên kết tới request_workflow666, bốn sự kiện liên quan và một thông báo. Cleanup cần archive/manifest và xử lý liên kết có kiểm chứng; không xóa tất cả lịch sử hoặc tắt ràng buộc hàng loạt.

Sau cleanup, migration ấm200 chiếc cũ phải được thay bằng mapping đúng dữ liệu còn lại; không chạy guard200/103/97 trên số dư đã thay đổi, không tự tái tạo fixture.

## Files

- scripts/database/compact-v2/Invoke-OfficialV2Upgrade.ps1
- scripts/local/inspect-official-cleanup.ps1
- plans/261125-2122-official-database/plan.md
- plans/reports/default-261125-2124-official-test-provenance.md

Không sửa C#/frontend trong lượt này, nên build/test ứng dụng không chạy lại. SQL rehearsal đạt; EF SchemaGate/API/UI trên database đã dọn chưa chạy. Hai runtime local trước đó vẫn chạy và vẫn dùng database riêng.
