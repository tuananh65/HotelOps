# Audit baseline Compact V2

Audit bắt đầu 24/11/2026, cập nhật 25/11/2026 (Asia/Saigon). Branch `feature/project2-role-alignment-20261123`, HEAD `d109839d9f1300c66f8d71b7d4957bc39dab84fd`. Project 2 entrypoint `src/HotelOps.Api/Program.cs` gọi CompactHost; không lấy implementation Project 1 làm bằng chứng.

## Target và baseline thực tế
- Workspace `D:/SEP490/Projects/TestFE/HotelOps`.
- SQL audit chỉ đọc `HotelOpsCompactDB` qua local User Secrets, không in connection string.
- Audit runtime: **49 bảng**, 11 role definitions; không FK/CHECK disabled hoặc untrusted; không trigger disabled. Evidence `artifacts/restructure-v2/audit-HotelOpsCompactDB.json` (14 result sets).
- SchemaGate hiện yêu cầu version 3.0.0, 49 tables/205 FK/97 CHECK/38 trigger; compile query cho mọi mapping/view trước mở listener (`Compact/Infrastructure/SchemaGate.cs`). Migration V2 cần contract mới có kiểm chứng, không chỉ sửa số healthcheck.
- Build backend baseline: Passed, 0 warning/0 error. Frontend build: Passed. .NET tests: 101 passed, 3 skipped do cấu hình integration hiện hữu. Frontend compact CI: 9 passed. Logs tại `artifacts/restructure-v2/baseline-*`.
- Kết nối SQL trong sandbox lỗi SSPI; cùng audit ngoài sandbox thành công. Không thay credential hay cấu hình SQL.

## Actor/auth/account
- Source `p0-role-migration.sql` đã gộp DH→OM và chuyển Maintenance Staff→HK, kết thúc ENG membership, chuyển lịch tương lai. Snapshot trước/sau được lưu ở audit `P0_ROLE_MIGRATION/261124-v1`.
- OM chặn mutation sự cố/yêu cầu thường tại `WorkService.Authorization.cs`; room report chặn OM tại `WorkService.RoomIssues.cs`. Giữ guard khi mở lại đội bảo trì.
- Maintenance mutations/candidates/inbox hiện cố định supervisor (`WorkService.Authorization.cs`, `WorkService.MaintenanceInbox.cs`, `WorkService.RoomIssueActions.cs`). Khôi phục grants một mình không đủ.
- First-login restriction đã có: `UserService` đặt TemporaryPasswordExpiresAt; `AuthService`, `SessionAuthentication`, `CompactHost` hạn chế API và frontend `auth.tsx` buộc đổi. Đổi mật khẩu xoay stamp, revoke sessions. Cần runtime case mới, không đánh đồng source với E2E PASS.
- Create account chưa gửi email; reset email qua `PasswordResetEmail` có SMTP nhưng gửi trong transaction. Invitation/retry/outbox/setup token chưa có.

## Inventory và workday
- `StaffSupplyService` cấp đầu ca/hoàn cuối ca, ledger/idempotency; SHORT hiện all-or-nothing. `StaffSupplyService.Reservations` reserve capacity×norm và consume toàn bộ reservation. Chưa room usable assessment.
- StockMovement chỉ có item/warehouse/qty; UNIQUE line+warehouse. Không batch/expiry allocation. Hai writer cần thống nhất: `DocumentService.cs`, `DocumentService.Supplies.cs`.
- A/B/C cũ là fill/durable/asset; không được đổi ý nghĩa máy móc thành A/B/C/D V2. Không có reusable location/condition ledger hoặc laundry/write-off workflow.
- `LowStockService` chống lặp theo episode nhưng khả dụng đang dùng tổng stock, chưa loại expired/quarantined/reserved.
- Transfer đã có request/approve/ship/partial receipt, giữ actual qty và source line. Thiếu discrepancy resolution riêng, nhận 0, batch preservation và projection đủ bốn số rõ ràng.

## Housekeeping/maintenance/UI
- Work, assignments, intervals, checklist templates/results và findings hiện có trong Compact mappings. Quy trình hiện dùng paper inspection và repair supervisor; cần bật checklist execution có snapshot, internal staff và technical review.
- Room issue có link tới cleaning, forward missing→store/broken→maintenance, supplied/repaired quay lại inspection. Không được mất liên kết trong V2.
- UI hiện dùng sidebar theo quyền, drawer chi tiết, dialog hành động, top alerts và center confirmation. Giữ các thành phần chung và màu đã chốt.
- `docs/ui/compact-ux.md` có các quyết định cũ mâu thuẫn V2 (không checklist điện tử, sửa ngoài, trừ maximum fill); V2 mới thay thế đúng các phần này.

## Integration và tài liệu
- Notification lưu DB và lọc quyền theo recipient/domain (`NotificationService`), chưa AddSignalR/MapHub/client.
- Chưa có bằng chứng AI provider runtime trong Compact; external configuration chưa được cấp. Fallback không được gọi là AI đã kiểm chứng.
- SRS nguồn: `docs/requirements/HotelOps_SRS_DRAFT.docx` và bản markdown/chunks. Không tìm thấy Report 4/5 chuẩn trường riêng; tài liệu architecture/testing hiện hữu phải được đối chiếu, không tự thay format Word.

## Giới hạn
Đây là baseline audit, không phải receipt hoàn thành 40 CHG/48 E2E. Không migration dữ liệu chính, không tự khôi phục user/stock khi chưa đủ provenance. Kết quả independent source review tách biệt với runtime DB audit.
