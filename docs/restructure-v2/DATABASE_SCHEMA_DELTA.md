# Database schema delta — V2

Trạng thái ngày 25/11/2026: chỉ áp dụng trên `HotelOpsCompactWorkdayTestV22611250010`. Database vận hành không thay đổi. Không dùng tài liệu này làm lệnh nâng cấp Production.

## Hợp đồng tương thích

| Thành phần | Compact trước V2 | TEST V2 |
|---|---|---|
| Extended property HotelOpsCompactSchema | 3.0.0 | 3.0.0, chưa đổi số phiên bản phát hành |
| Bảng nghiệp vụ | 49 | 57 |
| Foreign keys | Xem audit baseline | 234, bật và trusted |
| Check constraints | Xem audit baseline | 116, bật và trusted |
| Triggers bảng | Xem audit baseline | 38, bật |
| Views EF | 6 | 6 |

**Chuỗi 3.0.0 riêng lẻ không đủ để xác định tương thích.** Runtime hiện yêu cầu đồng thời số đối tượng trên và biên dịch truy vấn rỗng cho mọi scalar mapping trong `SchemaGate.cs`. Backup 49 bảng trước đây không chạy được với bản V2 này. Chưa tạo backup phát hành V2 hoặc nâng cấp EC2.

## Tám bảng bổ sung

| SQL migration | Bảng | Mục đích |
|---|---|---|
| v2-invitations.sql | account_invitations | Token invitation hash, outbox, trạng thái gửi và retry |
| v2-inventory-batches.sql | item_lifecycle_profiles | Phân loại và chính sách hạn dùng theo chi nhánh |
| v2-inventory-batches.sql | stock_batches | Nguồn lô, hạn dùng, lượng còn và trạng thái |
| v2-inventory-batches.sql | stock_batch_entries | Phân rã giao dịch theo lô |
| v2-inventory-batches.sql | staff_batch_balances | Lô vật tư nhân viên đang giữ |
| v2-reusable-inventory.sql | reusable_positions | Số lượng theo vị trí và trạng thái |
| v2-reusable-inventory.sql | reusable_movements | Lịch sử di chuyển giữ nguyên tổng sở hữu |
| v2-reusable-inventory.sql | reusable_write_offs | Đề nghị và quyết định ghi giảm độc lập |

`v2-room-assessment.sql` bổ sung usable_quantity, assessed_at, assessed_by vào supply_reservations và điều chỉnh constraints. Không thêm bảng assessment riêng. `v2-assessment-guards.sql` bổ sung bảo vệ assessment. Giữ Database First, không tạo EF migrations.

## Dữ liệu và thứ tự phụ thuộc

1. Audit và backup COPY_ONLY/CHECKSUM; RESTORE VERIFYONLY; restore database TEST riêng.
2. `v2-role-provenance.sql`, `v2-recover-maintenance.sql`, `v2-checklist-permissions.sql`: đối chiếu provenance trước khi phục hồi role. Không thay đổi credential.
3. `v2-room-assessment.sql`, `v2-assessment-guards.sql`, `v2-invitations.sql`.
4. `v2-inventory-batches.sql`, sau đó `v2-reusable-inventory.sql`; phân loại khăn/ấm theo D09, không bịa hạn dùng lô lịch sử.
5. `v2-reusable-legacy-settlement.sql`: chỉ đối soát phần có bằng chứng, giữ phần chưa rõ ở LEGACY_REVIEW.
6. `v2-kettle-pending-assets.sql`: tạo hồ sơ chờ kiểm kê theo D11, bảo toàn chứng từ và số dư cũ.
   `v2-reusable-shift-configuration.sql`: cho phép định mức đầu ca nhóm B khi profile là REUSABLE, giữ bất biến revision và kiểm tra classification trong trigger. Không thêm bảng/constraint/trigger; TEST vẫn 57/116/38. API yêu cầu xóa định mức B trước khi chuyển sang loại khác.
7. DBCC constraints, schema gate và runtime tests. Đây là thứ tự phụ thuộc, không phải script tự động áp dụng tất cả trên một database bất kỳ.

Các file nằm trong `scripts/database/compact-v2/`. Script có guard database; không bỏ guard để chạy trực tiếp trên database vận hành. Backup phải mới trước mỗi đợt thay đổi dữ liệu/schema. Khi cần rollback, restore vào database TEST khác và đối chiếu; không reset database hiện có.

Bằng chứng: `artifacts/restructure-v2/*-migration.json`, `inventory-classification.json`, `kettle-pending-verification.json`. Hợp đồng phát hành/schema version cuối cùng còn cần hoàn thiện cùng toàn bộ acceptance suite.
