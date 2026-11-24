# Kiểm tra sau sửa chữa và dọn lại — 24/11/2026

## Thay đổi

- Tiếp nhận sửa chữa đồng thời bắt đầu MAINTENANCE, bỏ modal ghi chú đầu tiên. Drawer hiện Kết thúc sửa chữa và workspace cập nhật số đếm ngay.
- Công việc vệ sinh sau khắc phục vào Chờ kiểm tra, nhãn **Chờ kiểm tra - sc** màu cam; dọn lại đã gửi vào cùng tab, nhãn **Chờ kiểm tra - dl** màu nâu. Không thay enum/trạng thái database.
- PASS sự cố cuối cùng đồng thời ghi inspection PASS cho lần vệ sinh liên quan, kết thúc dọn lại đủ điều kiện và đánh giá READY. Dùng lại transaction, audit, quyền kiểm tra độc lập, optimistic concurrency, turnover và chính sách readiness. Còn sự cố khác hoặc chặn an toàn thì không ép READY.
- Form kiểm tra bỏ checkbox; PASS không cần ghi chú; FAIL cần nội dung không rỗng, tối đa 2.000 ký tự. Bỏ điều kiện tối thiểu 10 ký tự trong kiểm tra vệ sinh và kiểm tra khắc phục. Finding vẫn cần rework thực sự gửi kiểm tra và quyền DEFECT_RESOLVE, lưu bằng chứng PASS.
- Bỏ mục chọn Mức ảnh hưởng trong form báo sự cố, giữ ghi chú mô tả; dùng cấu hình mức đang bật (ưu tiên READINESS). Panel đổi thành Sự cố đã phát hiện. Đây là cách hiểu đã thông báo trong khi chờ phản hồi câu hỏi làm rõ.

## Nguyên nhân phòng 104

Đọc local Production HotelOpsCompactDB: room416/code104 operational_status DIRTY, current_cleaning_work_id652, turnover2. Work652 và rework663 COMPLETED, inspection670 PASSED. Tuy nhiên incident653 Sự cố phòng104 vẫn OPEN từ trước khi dọn lại, khiến v_room_readiness trả BLOCKED. Không có room_block đang mở, không có chuyển trạng thái phòng sang MAINTENANCE.

Frontend đang nhóm BLOCKED dưới nhãn Bảo trì. CleaningDeadline chỉ ghi audit; OperationsWorker chỉ phát thông báo quá hạn. Không có đường tự tạo sự cố hay đổi phòng sang bảo trì khi quá giờ. Không đóng sự cố653 khi chưa có kết quả xử lý thực tế. Muốn phòng104 READY cần xử lý sự cố này theo nghiệp vụ; không phải sửa hạn dọn.

## Kiểm chứng

- `dotnet build HotelOps.slnx --no-restore -v minimal`: Passed, 0 warning, 0 error.
- `dotnet test HotelOps.slnx --no-build --no-restore -v minimal`: 101 Passed, 3 Skipped, 0 Failed. Các test bỏ qua có sẵn: MaintenanceWorkerTests.LeadPlanCreatesOneMaintenanceOccurrenceAcrossRepeatedTicks; WorkerIntegrationTests.RecurrenceAndLowStockAreCommittedOnceAcrossRepeatedTicks; AccountManagementTests.ManagementAndResetPreserveSecurityAndRollbackOnEmailFailure.
- `npm run test:compact:ci`: 9 Passed.
- Frontend build: Passed (chạy lại sau khi thêm thông báo lỗi tiếng Việt).
- Publish Release: Passed, không cảnh báo.
- Database TEST HotelOpsCompactWorkdayTestWarehouse261124 đã backup CHECKSUM/VERIFYONLY trước khi thử; không sửa schema.
- Runtime work808, incidents809/810: nhận sửa chữa không ghi chú -> IN_PROGRESS; không cần start lần hai; gửi kiểm tra; còn sự cố thiếu đồ thì chưa READY; cấp đủ -> SC; PASS cuối cùng không ghi chú -> COMPLETED và READY ngay. Không gọi thêm inspection-result.
- Runtime work817/rework819: FAIL note Bẩn (3 ký tự); dọn lại vào inspect/DL, không còn trong rework; PASS không note/ResolveFindings -> lỗi RESOLVED, vệ sinh COMPLETED, phòng READY. Đồng thời kiểm tra chặn hai lệnh giao việc chưa nhận.
- Edge UI 1440/390: tiếp nhận không modal ghi chú, form kết thúc sửa chữa; màu SC/DL; không checkbox; PASS gửi note=null; form báo sự cố không có Mức ảnh hưởng; quay lại công việc; không lỗi JavaScript/tràn ngang.
- Một lần UI test lỗi do fixture chưa bật khả năng kiểm tra REWORK; đã sửa fixture và chạy lại đạt. Test cấu hình cũ chờ trường Mức ảnh hưởng đã bỏ và kiểm tra request quá sớm; cập nhật kỳ vọng và chờ request, chạy lại đạt.
- `git diff --check`: Passed; Git nhắc LF/CRLF cho các file, không có lỗi whitespace.

## File thuộc lượt thay đổi này

Backend:
- src/HotelOps.Api/Compact/Services/WorkService.cs
- src/HotelOps.Api/Compact/Services/WorkService.PaperInspection.cs
- src/HotelOps.Api/Compact/Services/WorkService.RoomIssueActions.cs
- src/HotelOps.Api/Compact/Services/WorkService.RoomIssueChildren.cs
- src/HotelOps.Api/Compact/Services/WorkService.RemediationInspection.cs (mới)

Frontend:
- frontend/src/compact/cleaning-queue.tsx
- frontend/src/compact/cleaning-incidents.tsx
- frontend/src/compact/maintenance-workspace.tsx
- frontend/src/compact/paper-inspection.tsx
- frontend/src/compact/room-issues.tsx
- frontend/src/compact/work.tsx
- frontend/src/compact/ui.tsx
- frontend/src/compact/task-ux.css
- frontend/src/compact/p0-error-messages.ts

Kiểm thử và tài liệu:
- scripts/verification/remediation-inspection-runtime.mjs (mới)
- scripts/verification/rework-inspection-runtime.mjs (mới)
- scripts/verification/remediation-inspection-local.mjs (mới)
- frontend/tests/remediation-maintenance-browser.mjs (mới)
- frontend/tests/remediation-inspection-browser.mjs (mới)
- frontend/tests/incident-configuration-browser.mjs
- docs/ui/compact-ux.md
- plans/261124-2144-remediation-inspection/plan.md
- Báo cáo này.

Các file khác đã sửa từ trước được giữ nguyên. Không commit/push. Review trực tiếp trong phiên, không tuyên bố review độc lập.

## Giới hạn

- Không tạo giả kết quả kiểm tra trên dữ liệu Production. Production chỉ đọc/audit ở lượt này.
- Không tự đóng incident653 hoặc giải phóng chặn SAFETY.
- Runtime regression kiểm chứng luồng sự cố từ vệ sinh và dọn lại; không thử mới luồng sự cố độc lập do lễ tân tạo.

## Bản local đã kiểm tra

- Production API PID12604, https://localhost:5769, DB HotelOpsCompactDB.
- Develop API PID26796, http://127.0.0.1:5774, DB HotelOpsCompactWorkdayTestP0261124.
- Release DLL artifacts/production-261124-remediation-inspection/api-verified/HotelOps.Api.dll; SHA256 805DBE2C475C5340BC51C35E05D6C7E7FA403565D41A066C42574B1B4AF6F127.
- Frontend5080 đã phục vụ index-BcWJB7Fm.js/index-QX4Omj8e.css đúng build; API401 khi chưa đăng nhập; không lỗi JavaScript ở trang đăng nhập.
- Hai API cũ và API TEST48960 đã dừng. Giữ frontend5080 và Vite5174 đang dùng. Rollback frontend ở artifacts/production-261124-remediation-inspection/frontend-before, API trước ở artifacts/production-261124-housekeeping-fulfillment/api-verified.
- Evidence: artifacts/remediation-inspection/{cleaning-runtime.json,assignment-runtime-checkpoint.json,production-audit.json,local-release.json}, inspection-ui/browser.json, maintenance-ui/browser.json; artifacts/incident-configuration/browser.json và ảnh desktop/mobile.
