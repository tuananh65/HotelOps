# Cấu hình nội bộ và vệ sinh có sự cố — 24/11/2026

## Kết quả

Đã triển khai và cập nhật local Production/Develop. Không commit/push. Không thay schema hay quyền vai trò; giữ Project 1 và các thay đổi có sẵn.

- Loại yêu cầu: tên, mô tả, checkbox vai trò gửi, checkbox người/bộ phận nhận. Người gửi chọn một nơi trong danh sách. API chặn vai trò và nơi nhận không hợp lệ, kể cả gọi trực tiếp. Gửi cho cá nhân tạo assignment và thông báo khi submit; cấu hình không cấp thêm permission.
- Bộ chọn bắt đầu/kết thúc cùng dạng giờ/phút 24 giờ; hỗ trợ kết thúc 24:00. Ca có lịch sử vẫn khóa định nghĩa. Backend chống trùng hiện hữu được kiểm thử cả gửi đồng thời, theo chi nhánh/bộ phận/vai trò/khung giờ.
- Báo cáo phòng chỉ mở nội dung sau chọn loại. Hỏng hiển thị tài sản thiết bị; thiếu hiển thị vật dụng đã cấp cho phòng và số lượng. OM cấu hình yêu cầu ghi chú, tên và bật/tắt hai mức READINESS/SAFETY cho từng loại. Không đổi ý nghĩa an toàn hoặc báo cáo cũ.
- Báo sự cố từ việc vệ sinh xong quay về đúng việc, bao gồm dọn lại. Nhân viên vẫn hoàn thành vệ sinh; API ghi dấu hoàn thành có sự cố và vẫn tiêu hao vật tư đúng một lần.
- Giám sát có nhóm “Kiểm tra phòng gặp sự cố”, tách khỏi kiểm tra thường. Chi tiết có báo cáo gốc và nút xác nhận bằng dialog nhập kết quả kiểm tra.
- Xác nhận hỏng chuyển Giám sát bảo trì; xác nhận thiếu tự tạo yêu cầu từ đúng vật dụng/số lượng đã báo, kho duy nhất trong chi nhánh tiếp nhận. Không xuất kho tại bước xác nhận. Kiểm tra người xử lý, quyền, phiên bản và giao dịch; retry không tạo phiếu trùng.
- Không cho kết luận vệ sinh thông thường trong lúc còn sự cố mở. Sau sửa/bổ sung, giám sát kiểm tra lại và ghi kết quả vệ sinh; readiness chỉ READY khi đủ điều kiện.

## Kiểm chứng

- `dotnet build HotelOps.slnx --no-restore -v minimal`: PASS, 0 lỗi/0 cảnh báo.
- `dotnet test HotelOps.slnx --no-restore -v minimal`: 101 PASS, 3 SKIP theo điều kiện môi trường của test hiện hữu (worker, maintenance worker, account/email). Không coi SKIP là PASS.
- Frontend TypeScript/Vite build PASS; `npm run test:compact:ci`: 9/9 PASS.
- API cấu hình trên TEST DVBV và DVCT: sender role, recipient list/cross-branch, giao người nhận, submit/reject, ca trùng, race hai yêu cầu chỉ một thành công, cấu hình impact, từ chối HK sửa cấu hình. Bằng chứng `artifacts/incident-configuration/runtime-config.json`.
- Workflow thực DVCT: đặt/nhận/trả phòng; bổ sung vật tư qua request–supervisor–storekeeper vì TEST staff đã hết đồ; tạo/start cleaning 764; báo BROKEN 765 và MISSING 766; hoàn thành/retry; hàng đợi riêng; chặn approve sớm, chặn HK xác nhận và PASS thường; xác nhận/retry; repair/stock issue/reinspection; cuối cùng phòng READY. `artifacts/incident-configuration/cleaning-runtime.json`.
- API hồi quy housekeeping cả hai chi nhánh: quyền, hàng đợi, lịch sử riêng: PASS.
- Edge headless 1440/390px: checkbox cấu hình, hai bộ chọn giờ, type-first form, return-to-work, confirmation: PASS; không pageerror/overflow. Chạy lại trên origin local Production 5080. Đây là kiểm tra UI với API fixture; nghiệp vụ API thật được kiểm riêng trên TEST.
- Browser hồi quy housekeeping 2 role × 2 viewport: PASS. Đã xem ảnh mobile và sửa checkbox về hàng ngang, kích thước nhỏ.
- `git -c core.autocrlf=false diff --check`: PASS.

Các lần kiểm thử chưa đạt ban đầu là chuẩn bị kịch bản: check-in thiếu bước reserve; nhân viên TEST hết vật tư. Đã bổ sung bước nghiệp vụ và cấp thêm qua request thật, không bỏ validation hay sửa tồn trực tiếp. Lần thử health nhầm `/api/v2/health` trả 404; route đúng `/health` đã kiểm tra lại.

## Database và local runtime

- TEST `HotelOpsCompactWorkdayTestWarehouse261124`: backup CHECKSUM/VERIFYONLY trước thay đổi, file `HotelOpsCompactWorkdayTestWarehouse261124_before_workday_20261124_192854257.bak`.
- Production `HotelOpsCompactDB`: `HotelOpsCompactDB_before_workday_20261124_194918260.bak`, verified.
- Develop `HotelOpsCompactWorkdayTestP0261124`: `HotelOpsCompactWorkdayTestP0261124_before_workday_20261124_194919770.bak`, verified.
- Backup directory: SQL Server `MSSQL16.SQLEXPRESS/MSSQL/Backup`; manifests `artifacts/workday/backup-<database>.json`.
- Data patch chỉ lấy CleaningWorkId đã có trong snapshot báo cáo để thêm SOURCE link với kiểm tra branch/room/turnover. TEST thêm 3, chạy lại thêm 0; Develop thêm 3; Production thêm 1. Không sửa trạng thái hay xóa lịch sử.
- Production API PID 9944, HTTPS5769; Develop API PID 9220, HTTP5774; FE giữ 5080/5174.
- API DLL `artifacts/production-261124-incident-configuration/api-verified/HotelOps.Api.dll`, SHA256 `1C1C2C426FCF1C8D0D13124D0FD8AF0F3C4E5D98BE99653B5C45D653B56331A2`.
- Frontend active `artifacts/production-261124-p0/frontend`, copy từ `frontend-final` trong release mới. Rollback FE `frontend-before`; API cũ giữ ở release housekeeping-workspace.
- Sau triển khai: health Production/Develop ok; FE5080 HTTP200; API chưa đăng nhập HTTP401; log lỗi startup trống. TEST API5775 đã dừng.
- Publish từng có cảnh báo MSB3026 khóa file font `OFL.txt`, retry thành công. Vite thông báo outDir ngoài frontend root không tự xóa; build thành công.

## Files của lượt này

Frontend tạo `shift-time-picker.tsx`, `request-recipient-fields.tsx`, `cleaning-incidents.tsx`; sửa `internal-settings.tsx/.css`, `workday-settings.tsx`, `workday.css`, `room-issues.tsx`, `work.tsx`, `cleaning-queue.tsx`, `housekeeping-history.tsx`, `housekeeping-workspace.css`, `api.ts` trong `frontend/src/compact/`.

Backend tạo `WorkService.CleaningIncidents.cs`; sửa `WorkService.InternalRequestConfig.cs`, `WorkService.RoomIssueConfig.cs`, `WorkService.RoomIssues.cs`, `WorkService.RoomIssueActions.cs`, `WorkService.cs`, `WorkService.PaperInspection.cs`, `WorkService.Housekeeping.cs`, `WorkService.Maintenance.cs`; contracts `InternalRequestTypeRequest.cs`, `RoomIssueRequests.cs`; controller `OperationsControllers.cs`.

Verification: `frontend/tests/incident-configuration-browser.mjs`, `scripts/verification/incident-configuration-runtime.mjs`, `scripts/verification/cleaning-incidents-runtime.mjs`; data patch `scripts/database/compact-v2/cleaning-incident-links.sql/.ps1`; plan, báo cáo, journal và UX delta.

## Giới hạn và khôi phục

Không chạy giao dịch thử phá dữ liệu trên Production; full workflow chạy trên database TEST. Không tuyên bố kiểm lại toàn bộ 28 E2E P0 cho lượt này. Mức ảnh hưởng dùng hai chính sách nghiệp vụ đã có, không thêm arbitrary status/semantics.

Nếu rollback ứng dụng: khởi động DLL cũ và copy frontend-before (index sau assets). SOURCE link thêm vào tương thích bản cũ và có thể giữ. Chỉ restore backup database khi có chỉ định riêng, vì restore có thể mất giao dịch mới sau thời điểm backup.
