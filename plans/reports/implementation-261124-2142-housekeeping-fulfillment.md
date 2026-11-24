# Giao việc vệ sinh và kiểm tra sau cấp đồ thiếu

## Kết quả

Đã cập nhật local Production (5080/5769) và Develop (5174/5774). Không commit/push, không đổi schema, grant, role hoặc trạng thái lưu trong database.

- API chặn giao dọn mới/dọn lại nếu người nhận còn việc ACTIVE chưa nhận; danh sách ứng viên cùng điều kiện. Dùng transaction serializable và lock cùng thứ tự với vật tư. Kiểm thử hai lệnh đồng thời chỉ một lệnh thành công.
- Hai trạng thái giao mới/dọn lại của HK Staff cùng nhãn Chờ nhận. Bỏ Tạm dừng và hướng dẫn tạm dừng; bỏ Chọn phòng khác; tách nút gửi sự cố khỏi ghi chú.
- Ẩn đúng menu giám sát buồng phòng và thủ kho được yêu cầu. Giữ lịch sử trong Công việc và quyền báo sự cố từ công việc.
- Việc đang chờ bên kho/kỹ thuật không còn trong hàng đợi hiện tại; việc có bước giám sát cần xử lý vẫn hiện. Sau khắc phục quay lại để kiểm tra.
- Đã nối ghi sổ ISSUE đủ số lượng với chuyển sự cố/luồng yêu cầu con sang PENDING_REINSPECTION trong cùng transaction; dùng lại kiểm tra ca, quyền, người xử lý, số lượng cấp, audit và kiểm tra độc lập.
- Phiếu cũ đã cấp đủ có đường Tiếp nhận kiểm tra phòng từ drawer Công việc. Không tự đánh dấu kiểm tra đạt.
- Sửa lưới danh mục vật tư bị ép vào cột 156px; bảng dùng chiều rộng vùng nội dung. Thủ kho thấy Chờ duyệt màu vàng ở phiếu APPROVED; giữ bằng chứng duyệt phía server. Hiển thị chi nhánh/tầng/phòng cấp đồ bền từ liên kết thật trong chi tiết và form cấp phát.

## Nguyên nhân xác minh

- Danh mục vật tư ẩn thanh danh mục phụ nhưng vẫn áp dụng lưới hai cột; nội dung nằm trong cột hẹp.
- Post chứng từ trước đây cập nhật FULLY_ISSUED nhưng không gọi bước report-remedy. Phiếu bị kẹt MATERIAL_REQUESTED, nên không có bước kiểm tra.
- Đọc trực tiếp Production: sự cố 665, phòng 103/room415, MR440 số `5ac4e9973fbf4548a8adabd14a4b938f` đã FULLY_ISSUED nhưng workflow PENDING_STOREKEEPER; công việc664 vẫn PENDING_INSPECTION. Sự cố662 còn chờ kỹ thuật.
- Không sửa trực tiếp trạng thái các phiếu Production. Phiếu665 dùng đường tiếp nhận kiểm tra cho dữ liệu cũ; không cần đổi lượng tồn hay ghi lại chứng từ đã cấp.

## Kiểm chứng

| Hạng mục | Kết quả |
|---|---|
| `dotnet build HotelOps.slnx` | Passed, 0 lỗi/0 cảnh báo |
| `dotnet test HotelOps.slnx --no-build` | Passed: 101; skipped: 3; failed: 0 |
| Frontend `npm run build` | Passed |
| `npm run test:compact:ci` | 9 Passed |
| HK supervisor/staff browser 1440/390 | Passed: menu, trạng thái, không tạm dừng, drawer/history |
| Storekeeper browser 1440/390 | Passed: catalog rộng1132/358px, menu, nhãn vàng, địa điểm cấp |
| Reinspection browser 1440/390 | Passed: mở từ Công việc, tiếp nhận phiếu cũ, form kiểm tra được phép |
| API/SQL TEST | Passed: chặn việc chưa nhận, giao đồng thời, dọn lại, sự cố, tự chuyển sau cấp đủ, kiểm tra độc lập |
| Phiếu cũ qua API cũ + API mới trên cùng TEST | Passed: thiếu số lượng không nhận kiểm tra; xuất đủ thì giám sát nhận/kiểm tra; phòng READY chỉ sau kiểm tra vệ sinh cuối |
| Local sau cập nhật | Passed: assets đúng, không lỗi JS trang login, endpoint riêng tư trả401 qua5080/5769/5774 |

Ba test skipped có sẵn: MaintenanceWorkerTests.LeadPlanCreatesOneMaintenanceOccurrenceAcrossRepeatedTicks; WorkerIntegrationTests.RecurrenceAndLowStockAreCommittedOnceAcrossRepeatedTicks; AccountManagementTests.ManagementAndResetPreserveSecurityAndRollbackOnEmailFailure.

Lần chạy đầu của script storekeeper cũ dùng cổng5186 không hoạt động, rồi fixture thiếu endpoint workday/personal-summary; đã thay bằng test tập trung có mock đúng contract. Test rework ban đầu thiếu roomId/resolveFindings; đã sửa fixture và chạy thành công. Test phiếu cũ chạm giờ hết ca21:35 và bị403 đúng quy tắc; sao lưu TEST rồi gia hạn riêng6 ca nhánh P0_TEST_DVCT tới23:59 để chạy xong. Fixture reinspection UI bổ sung IR_PROCESS cho đúng giám sát. Không thay ca Production.

Đăng nhập tự động Production bằng bộ seed cũ bị401; không reset mật khẩu hay giả tài khoản. Kiểm thử mutation chạy trên TEST đã sao lưu; Production kiểm chứng bằng đọc SQL và kiểm tra bản local đã phục vụ. Người dùng thực hiện kiểm tra phòng thực tế bằng phiên đăng nhập hiện tại.

## Tệp trong phạm vi lượt này

Backend sửa: `Compact/Services/WorkService.cs`, `WorkService.Cleaning.cs`, `WorkService.CleaningIncidents.cs`, `WorkService.RoomIssues.cs`, `WorkService.RoomIssueActions.cs`, `WorkService.RoomIssueChildren.cs`, `DocumentService.Inventory.cs`; `Compact/Controllers/OperationsControllers.cs`. Thêm `Compact/Services/WorkService.MaterialFulfillment.cs`. Tất cả dưới `src/HotelOps.Api/`.

Frontend sửa dưới `frontend/src/compact/`: `app.tsx`, `catalog.tsx`, `work.tsx`, `cleaning-incidents.tsx`, `room-issues.tsx`, `inventory-documents.tsx`, `inventory-document-form.tsx`, `api.ts`, `ui.tsx`, `task-ux.css`.

Kiểm thử: sửa `frontend/tests/housekeeping-workspace-browser.mjs`; thêm `housekeeping-fulfillment-browser.mjs`, `housekeeping-reinspection-browser.mjs`; thêm scripts dưới `scripts/verification/`: `housekeeping-assignment-runtime.mjs`, `housekeeping-fulfillment-runtime.mjs`, `housekeeping-legacy-delivery-runtime.mjs`, `housekeeping-legacy-delivery-resume.mjs`, `housekeeping-fulfillment-local.mjs`.

Tài liệu: `docs/ui/compact-ux.md`, plan `plans/261124-2058-housekeeping-fulfillment-flow/plan.md`, báo cáo này. Giữ nguyên thay đổi đang có của các lượt trước.

## Bằng chứng và runtime

- `artifacts/housekeeping-fulfillment/`: production-audit.json; cleaning-runtime.json; assignment-runtime-checkpoint.json; legacy/runtime-result.json; storekeeper-browser.json; reinspection-ui/browser.json; local-release.json; screenshots.
- WorkTEST784 + incidents785/786: kiểm thử tự chuyển sau cấp đủ. Work793/rework795: giao đồng thời và chờ nhận dọn lại. Work799/incidents800/801: phiếu cũ.
- API local: `artifacts/production-261124-housekeeping-fulfillment/api-verified`; SHA256 `C2D8C54441D8BA852BD02F5F55ACB30BF96F6A35CA63FC5114AB0C0527431F76` trùng DLL đã build/test.
- Production PID41764/5769/HotelOpsCompactDB; Develop PID38664/5774/HotelOpsCompactWorkdayTestP0261124. Frontend5080 giữ process7680, Vite5174 giữ48136.
- Assets chính: index-x_bS09CF.js, index-15ACC2Qs.css. Bản frontend trước giữ tại `artifacts/production-261124-housekeeping-fulfillment/frontend-before`.
- Đã dừng API TEST14464 và API phiên bản cũ TEST35528 sau kiểm chứng. Không để thêm server tạm.

## Giới hạn

- Chưa tự thực hiện kiểm tra phòng103 trên Production: đây là kết luận nghiệp vụ người giám sát cần xác nhận thực tế. Sau tải lại trang, mở Công việc → phòng103 → Kiểm tra sau khắc phục → Tiếp nhận kiểm tra phòng; cần đang trong ca đúng quyền.
- Không thay trạng thái APPROVED trong DB chỉ để đổi nhãn. Chờ duyệt là nhãn workspace theo yêu cầu; bằng chứng giám sát duyệt thiếu đồ vẫn giữ.
- Không có migration/backfill Production; mọi điều kiện mới dùng schema hiện hữu.
