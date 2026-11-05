# FS-006 — Cleaning Lifecycle Phase 1

Ngày 06/11/2026 (Asia/Saigon). Branch `feature/dev1-room-ops`, worktree `D:\SEP490\Projects\HotelOps-DEV1`. Baseline FS005 `680f69026644ee3b6201f695f0768de871e2bb0a` khớp remote, working tree ban đầu sạch.

**PARTIAL — chuẩn bị contract/frontend và GET planning; chưa hoàn thành vertical slice ghi.** UC028 BLOCKED_CONTRACT do thiếu create API + Room Lifecycle/current-cycle/effective-block contract. UC029 có writer hiện hữu nhưng mandatory notification chưa tích hợp (`HousekeepingWriteDto.Notification = NOT_INTEGRATED`); không nối POST từ frontend. Các write tests mới: **LIVE_WRITE_BLOCKED_APPROVAL**. Không có UC credit mới, official ledger không đổi.

IMPLEMENTED: bỏ mock save/create/assign trên hai màn hình trong phạm vi; create hiển thị unavailable; assignment dùng GET planning thật qua transport FS002, hiển thị workload/recorded shifts, search/paging/cancel/error, kiểm tra bản nháp theo DTO hiện có; Save disabled. Monitor Detail có link vào planning. Không thêm/sửa endpoint, production backend, authentication, schema/EF/DI, Workday/Hotel Structure hoặc Notification infrastructure.

Phân loại evidence: unit và HTTP interception là VERIFIED_WITH_MOCK, không chứng minh giao dịch ghi. GET planning/monitor/progress VERIFIED_WITH_REAL_API và VERIFIED_WITH_SQL: live suite PASS2/2, snapshot task giữ nguyên. Chỉ snapshot SQL của task giữ lại được đối chiếu trước/sau; không nhận là whole-database checksum. Login/session-touch vẫn chạy bình thường.

- [Lifecycle matrix](CLEANING_LIFECYCLE_MATRIX.md)
- [UC028](UC028_CONTRACT.md), [UC029](UC029_CONTRACT.md)
- [UC030/031/034](UC030_UC031_UC034_READINESS.md)
- [Frontend mapping](FRONTEND_API_MAPPING.md)
- [Notification](NOTIFICATION_DEPENDENCIES.md), [Room Lifecycle](ROOM_LIFECYCLE_DEPENDENCIES.md)
- [Live acceptance](LIVE_ACCEPTANCE.md), [Build/tests](BUILD_TEST_RESULTS.md)
- [FS007](FS007_RECOMMENDATION.md)

Chỉ commit/push file FS006 sau validation. Không merge/main/develop/integration, không force-push, không đồng bộ worktree khác.
