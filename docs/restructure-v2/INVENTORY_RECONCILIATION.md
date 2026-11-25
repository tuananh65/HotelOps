# Đối soát vật tư và tài sản V2

Phạm vi: database TEST riêng, không thay đổi số dư Production/Develop. Số liệu dưới đây là số trên sổ, không phải xác nhận kiểm kê vật lý.

## Ấm đun — quyết định D11

| Chỉ tiêu | Kết quả |
|---|---:|
| Tổng lượng theo sổ cần đối chiếu | 200 |
| Số dư kho gốc | 103 |
| Đã cấp theo chứng từ phòng | 97 |
| Hồ sơ tài sản chờ kiểm kê đã tạo | 200 |
| Ánh xạ audit tương ứng | 200 |
| Hồ sơ mỗi chi nhánh | 100 |
| Dòng stock movement gốc được giữ | 99 |

Mỗi hồ sơ có mã MIG222, `PENDING_INSPECTION`, inactive. Vị trí thực tế, phòng thực tế và serial chưa được xác minh nên để null. Audit lưu vị trí trên sổ và dòng nguồn. Lần chạy lại trả ALREADY_APPLIED, không tạo thêm hồ sơ.

Không cộng 200 hồ sơ này với số dư kho/phòng thành sở hữu mới. Đây là hồ sơ đối soát cho cùng số lượng cũ, chưa phải giao dịch chuyển đổi tồn hoàn tất. Hai lần cấp phòng 103 Bùi Viện được giữ nguyên bằng chứng, không tự kết luận chiếc mất hay đang sử dụng. Việc chốt vị trí, tình trạng hoặc ghi giảm chờ kiểm kê thực tế.

Bằng chứng: `kettle-pending-assets.json`, `kettle-pending-verification.json`, `kettle-pending-idempotence.json`; SQL `v2-kettle-pending-assets.sql` và `v2-kettle-pending-verify.sql`.

## Tiêu hao và tái sử dụng

- Tiêu hao: tổng phân rã lô của một stock movement phải bằng lượng ghi sổ. FIFO/FEFO chỉ chọn lô đủ điều kiện; metadata nguồn không được sửa sau khi đã xuất.
- Nhân viên giữ vật tư: phân biệt số lượng đang giữ, reservation và lượng có thể dùng. Assessment tính thiếu từ định mức trừ số còn sử dụng được; chỉ tiêu hao phần thực bổ sung.
- Tái sử dụng: di chuyển READY, STAFF_HOLDING, IN_USE, DIRTY, LAUNDRY, DAMAGED không giảm sở hữu. WRITTEN_OFF chỉ tăng sau quyết định độc lập của OM cùng chi nhánh được commit.
- Điều chuyển: phần đã gửi chưa nhận vẫn in transit; mỗi lần thực nhận chỉ chuyển đúng lượng nhận sang sở hữu chi nhánh đích. Không tự ghi mất phần thiếu.
- Điều chỉnh giảm tiêu hao: chọn chính xác lô, OM duyệt, Thủ kho post; không trừ lô tốt thay lô hết hạn.

## Bằng chứng và giới hạn

Audit chỉ đọc lúc 11:58 ngày 25/11/2026: `ledger-reconciliation-current.json`, truy vấn `scripts/verification/v2-ledger-reconciliation.sql`. Chạy trên TEST giữa các workflow probe, trong transaction đọc nhất quán; bao gồm cả lịch sử fixture đã ngừng hoạt động.

| Đối chiếu | Số nhóm/bản ghi | Chênh lệch |
|---|---:|---:|
| Kho tiêu hao: ledger so tổng lô | 117 | 0 |
| Vật tư nhân viên giữ: số dư so lô đang giữ | 24 | 0 |
| Giữ chỗ đang hoạt động so tổng reservation | 26 | 0 |
| Kho tái sử dụng: ledger so vị trí thực ở kho (không gồm đang chuyển/đã ghi giảm/chờ kiểm kê) | 41 | 0 |
| Giao dịch kho đã có phân rã lô | 314 | 0 |
| Người duyệt ghi giảm khác người lập | 13 | 0 |
| FK/CHECK bật và trusted | 234 / 116 | 0 |

Còn 12 lô legacy với 1.152 đơn vị tại kho và 8 vị trí reusable với 20 đơn vị ở trạng thái chờ đối soát; không tự xác nhận hạn dùng/tình trạng/vị trí thực. Audit không kết luận toàn bộ movement legacy đã có phân rã lô, không thay kiểm kê vật lý hoặc đối soát toàn bộ tài sản. Không sửa bất kỳ số dư nào từ kết quả audit này.

`inventory-expiry-42-pass.json`: 42 kiểm thử HTTP/SQL đạt, gồm phân rã lô, ownership/custody, partial receipt, ghi giảm đúng lô, không ghi trùng và cảnh báo hết hạn. Fixture dùng vật tư tổng hợp riêng, giữ lịch sử và ngừng hoạt động sau test; không phải nhập kho thực tế.

`assessment-runtime-results.json`: 11 kiểm thử service/SQL, giao dịch rollback. Không thay thế toàn bộ Start Shift → End Shift qua UI. Chưa hoàn tất kiểm kê tất cả vật tư thực, đối soát mọi số dư legacy hoặc E2E-48 toàn hệ thống.

## Đối soát sau runtime, 25/11 18:55

[Audit toàn TEST mới](../../artifacts/restructure-v2/ledger-reconciliation-final.json) kiểm 10 nhóm, tất cả mismatch_count=0: tồn tiêu hao173 nhóm, held52, reservation54, reusable57 nhóm,496 movement đã phân rã lô,52 dòng điều chuyển,16 ghi giảm độc lập,234 FK,116 CHECK và200 asset có audit một-một. Không có movement thiếu lifecycle profile.

Báo riêng movement không phân rã lô:606 consumable,672 reusable,99 asset và6 unclassified. Không coi tất cả là legacy hợp lệ chỉ vì không có mapping.12 lô tiêu hao1152 đơn vị và8 vị trí reusable20 đơn vị còn LEGACY_REVIEW; các lượng khác đơn vị không cộng thành một tổng nghiệp vụ. Lượng đang chuyển được nhóm theo item/base unit và giữ chờ đối soát.

[Đối chiếu ấm đun](../../artifacts/restructure-v2/kettle-pending-final.json):200 hồ sơ pending/inactive,103 trên sổ kho+97 lượt cấp phòng,không tạo movement mới. Đây là đối soát sổ, không xác nhận kiểm kê thực tế.


## Audit cuối 25/11 lúc 19:42:35

[Ledger reconciliation](../../artifacts/restructure-v2/ledger-reconciliation-final.json) sau cleanup có14 nhóm, mọi mismatch=0. Bổ sung184 vị trí reusable theo opening migration có provenance + tổng vào−ra;8 vị trí LEGACY_REVIEW giữ riêng;18 opening có position tương ứng;11 nhóm IN_TRANSIT khớp chứng từ gửi−thực nhận. Có185 số dư kho tiêu hao,55 custody tiêu hao,57 reservation,541 movement có batch mapping,200 tài sản pending,18 write-off duyệt độc lập,57 dòng shipment,234 FK và116 CHECK trusted.

12 lô legacy tổng1152 và8 vị trí reusable tổng20 vẫn chờ kiểm kê theo Owner; không tính là tồn sẵn dùng. Kettle200 giữ103 trên sổ kho và97 lượt cấp phòng; không tự xác định vị trí hoặc mất mát. Đây là đối soát sổ TEST, không phải xác nhận vật lý. Regression binary mới155 PASS/3 SKIP; các workflow kho43/HK26/transfer15/replenishment7 checks đã chạy API/SQL/Edge.
