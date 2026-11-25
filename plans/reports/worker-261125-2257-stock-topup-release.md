# Nhập bổ sung hai kho

Đã diễn tập trên HotelOpsCompactWorkdayTestTopup2611252300, sau đó thực thi trên HotelOpsCompactDB. Mỗi lần chạy đều backup COPY_ONLY CHECKSUM và RESTORE VERIFYONLY trước thay đổi.

Đã tạo 2 chứng từ RECEIPT POSTED, 16 dòng: 100 đơn vị cơ sở cho mỗi loại nhóm A tại mỗi kho. Gồm 6 vật tư tiêu hao và 2 loại khăn tái sử dụng. Sổ kho, batch entries và reusable movements được ghi đồng bộ trong transaction; không tắt trigger/constraint, không reset hoặc sửa chứng từ cũ. Audit operation 299d9be0-9324-432b-a407-b536b9b572a5 chống chạy trùng.

Hạn quản lý nội bộ được ghi rõ là ước lượng theo quyền Owner, không phải hạn xác nhận nhà sản xuất: nước uống 25/05/2027; dầu gội, sữa tắm, kem đánh răng 25/11/2027; giấy vệ sinh, bàn chải và khăn 25/11/2028. Consumable profiles chuyển REQUIRED và chỉ lô mới READY; lô legacy chưa xác minh vẫn giữ nguyên.

**Giới hạn:** khăn có mốc kiểm tra tồn kho trong note chứng từ/audit và reusable movement, nhưng schema reusable hiện không có batch expiry nên mốc này chưa tự động khóa cấp phát. Parent cần hoàn tất chức năng hạn tồn kho dụng cụ, không báo đã enforce cho reusable.

Chứng từ ghi rõ thực hiện bằng script theo Owner, không giả là thao tác đăng nhập của thủ kho; created_by/post_by là thủ kho còn hoạt động cùng chi nhánh, audit cấp tổng thể không gán actor đăng nhập giả.

Files: scripts/database/compact-v2/owner-stock-topup-261125.sql; Invoke-OwnerStockTopup.ps1; scripts/verification/owner-stock-topup-manifest.mjs.

Kiểm chứng: SQL transaction kiểm tra đủ 2 kho x 8 loại, stock không âm, consumable batch = ledger; cả clone và official 16 dòng PASS. Node syntax check và manifest assertions PASS. Không thay đổi C# nên không chạy dotnet build trong subtask.

Evidence: artifacts/workday/topup-HotelOpsCompactDB.json; topup-HotelOpsCompactWorkdayTestTopup2611252300.json; backup-HotelOpsCompactDB.json. Parent tạo backup cuối sau toàn bộ thay đổi trước publish.
