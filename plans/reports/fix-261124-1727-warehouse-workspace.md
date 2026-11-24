# Kho chi nhánh — hoàn tất 24/11/2026

## Kết quả

Đã triển khai và cập nhật Production cục bộ https://127.0.0.1:5080/#stock. Mục tiêu là Project 2 compact, database HotelOpsCompactDB; không sửa Project 1, không thay schema/entities/grants, không commit/push.

- Đầu trang hiển thị kho đang xem. Bảng dùng STT, vật tư, tồn cơ sở, đơn vị, tối thiểu và thao tác; bỏ tên kho lặp lại, mã vật tư, biến động cuối.
- Tồn nhỏ hơn tối thiểu tô đỏ kèm chữ Dưới tối thiểu; bằng tối thiểu không báo đỏ. Mobile dùng thẻ, kiểm tra 390/320px không tràn ngang.
- OM có ROOM_SUPPLY_CONFIG sửa tối thiểu qua dialog tại trang kho. API kiểm tra INVENTORY_VIEW, role/permission, branch/warehouse/item, số lượng và hai row versions. Định mức đồng bộ với phiên bản cấu hình vật tư mới, không đổi nhóm/cấp đầu ca/mỗi khách/trạng thái bật tắt hoặc snapshot ca đã mở. Ghi audit và đối soát low-stock trong transaction Serializable. Hai người sửa cùng version chỉ một giao dịch được commit.
- Nút Xem kho chi nhánh khác chỉ liệt kê chi nhánh có quyền INVENTORY_VIEW trong hồ sơ. Tài khoản chỉ có một chi nhánh được giải thích rõ; không thêm quyền đọc chéo chi nhánh.
- OM có trang Lịch sử yêu cầu vật tư riêng tại #documents; giữ phân loại yêu cầu/nhập/xuất/hoàn/chuyển và các thao tác đúng quyền. Bổ sung tìm kiếm/bộ lọc tại trang này, sửa nhãn điều chuyển tiếng Việt. Đây là workspace tra cứu và thao tác theo quyền, không giả định mọi tài khoản chỉ đọc.

## Dữ liệu Production

- DVBV: warehouse_id 413 đổi tên thành Kho Bùi Viện.
- DVCT: warehouse_id 415 đổi tên thành Kho Cao Thắng.
- Mỗi chi nhánh vẫn đúng một kho hoạt động. Giữ ID và liên kết/lịch sử thay vì xóa kho có chứng từ. Kho tầng Sài Gòn 414 vốn inactive vẫn lưu lịch sử, không xuất hiện trong tồn kho hoạt động.
- 11 vật tư inventory đang hoạt động, mỗi vật tư thêm 100 đơn vị cơ sở vào mỗi kho: 22 dòng ledger, hai phiếu RECEIPT POSTED. Không cộng vào tài sản riêng lẻ; không thay đổi số dư nhân viên.
- Phiếu WAREHOUSE-PLUS100-20261124-DVBV và WAREHOUSE-PLUS100-20261124-DVCT, mỗi phiếu 11 dòng × 100. Script đối soát chính xác delta từng warehouse/item trước commit. Chứng từ gắn thủ kho phụ trách; reason/audit nêu rõ đây là script vận hành theo yêu cầu người dùng, không giả là nhân viên đã đăng nhập thực hiện.
- Retry trên clone nhiều lần không nhập trùng. Số phiếu giữ 1/chi nhánh, mỗi phiếu 11 movements. Đóng các episode cảnh báo tồn thấp đã được giải quyết, giữ lịch sử thông báo.
- Backup Production ngay trước áp dụng: HotelOpsCompactDB_before_workday_20261124_174917924.bak trong SQL Server backup directory; COPY_ONLY, CHECKSUM, RESTORE VERIFYONLY đạt. Manifest artifacts/workday/backup-HotelOpsCompactDB.json. Hai clone cũng được backup trước ghi dữ liệu.

## Kiểm chứng

- dotnet build HotelOps.slnx --no-restore -m:1: Passed, bản cuối 0 warnings/0 errors.
- dotnet test HotelOps.slnx --no-build --no-restore: Passed, 101 passed/3 skipped. Ba nhóm integration worker/account có điều kiện môi trường bị skip; không tính là đã chạy.
- Frontend TypeScript + Vite build: Passed. Cảnh báo outDir ngoài frontend có chủ đích để giữ artifact cũ.
- Compact frontend unit tests: 9 passed.
- Real API clone HotelOpsCompactWorkdayTestWarehouse261124: cả DVBV/DVCT đạt đồng bộ minimum/policy/config, bảo toàn tham số, giữ inactive, stale/negative/precision/foreign warehouse/cross-branch/HK/store/owner/unauth rejected, concurrency một commit. API fixtures dùng tài khoản TEST riêng đã được duyệt; không thay tài khoản Production.
- Database clone HotelOpsCompactWorkdayTestWarehouseLive261124: bản sao Production, áp dụng và retry đạt, đối soát từng item trước commit. Sau đó áp dụng Production cùng script đã kiểm thử; đọc lại xác nhận hai kho và 22 movements.
- Browser fixture trên develop5174 và bundle Production5080: Passed, warehouse context, STT/ẩn cột, màu đỏ, bằng tối thiểu không đỏ, lỗi lưu không đóng form, lưu thành công cập nhật bảng, chuyển chi nhánh đúng quyền, lịch sử riêng, 1440/390/320px. Đã xem ảnh 320px. Browser intercept API nên không coi đây là kiểm thử ghi API Production.
- Production API /health 200 schema3.0.0; frontend proxy API không token trả401. SQL session xác nhận API Production dùng HotelOpsCompactDB, API thử dùng clone đúng tên.
- Develop API đã đồng bộ endpoint mới, /health200 và proxy5174 trả401 khi không token. Dữ liệu TEST vẫn độc lập, không nhập thêm tồn vào fixture develop.
- git diff --check đạt (chỉ thông báo chuyển LF/CRLF của cấu hình Git).

## Lỗi gặp và xử lý

SQL clone ban đầu lỗi alias authorization là từ khóa; sửa thành [authorization]. Lần kế tiếp ràng buộc posted_at >= created_at phát hiện dùng thời gian đầu transaction trước created_at mặc định; ghi created_at cùng timestamp đã sửa. Các transaction lỗi rollback toàn bộ, không tác động Production. Build lần trung gian lỗi MSB3026/3027/3021 do API tạm giữ DLL; dừng API rồi build lại đạt. Một lần API test gọi trước listener sẵn sàng bị ECONNREFUSED; chạy lại khi API sẵn sàng đạt. Publish có một warning copy OFL.txt bị khóa tạm, retry tự thành công; hash file cuối trùng source. Không còn lỗi runtime trong log khởi động Production.

## Tiến trình và phục hồi

- Production API PID33756, https://localhost:5769, artifacts/production-261124-warehouse-final/api/HotelOps.Api.dll.
- Production frontend PID7680, port5080, static artifacts/production-261124-p0/frontend; bundle từ artifacts/production-261124-warehouse-final/frontend.
- Develop API PID1700, port5774, DB HotelOpsCompactWorkdayTestP0261124; frontend PID48136 port5174.
- API tạm5775 đã dừng. Không để browser kiểm thử chạy nền.
- Phục hồi frontend bằng frontend-before trong release warehouse-final, copy assets trước index.html. API cũ giữ ở artifacts/production-261124-supply-groups/api; dừng đúng PID33756 rồi khởi chạy binary cũ với cấu hình local hiện hữu nếu cần. Không tự restore toàn database đã có phát sinh mới; hoàn tác số lượng phải dùng chứng từ đảo có quyền/duyệt và kiểm tra hàng đã sử dụng. Backup là lựa chọn khôi phục có kiểm soát, không reset tự động.

## Files trong lượt này

- frontend/src/compact/stock.tsx
- frontend/src/compact/stock.css (mới)
- frontend/src/compact/app.tsx (tiếp nối thay đổi cấu hình đã có)
- frontend/src/compact/documents.tsx
- src/HotelOps.Api/Compact/Contracts/SupplyContracts.cs
- src/HotelOps.Api/Compact/Controllers/SuppliesController.cs
- src/HotelOps.Api/Compact/Services/SupplyConfigurationService.cs (giữ thay đổi tự chọn kho của lượt trước)
- scripts/database/compact-v2/warehouse-workspace.sql (mới)
- scripts/database/compact-v2/warehouse-workspace.ps1 (mới)
- scripts/verification/warehouse-minimum.mjs (mới)
- frontend/tests/warehouse-workspace-browser.mjs (mới)
- docs/ui/compact-ux.md
- Báo cáo này.

Bằng chứng: artifacts/warehouse-validation/api-results.json, browser-results.json, ảnh stock-om/restricted-1440/390/320.png, HotelOpsCompactDB-warehouse-data.json và HotelOpsCompactWorkdayTestWarehouseLive261124-warehouse-data.json. Manifest bản chạy: artifacts/production-261124-warehouse-final/runtime.json và artifacts/warehouse-development/process.json. Các thay đổi chưa commit từ hai task cấu hình trước được giữ nguyên.