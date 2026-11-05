# FS-005 — Cleaning Monitor / Detail

Ngày 06/11/2026 (Asia/Saigon). Baseline `64003ca027e5ec0ae3a1ab3ab890120a1e46fc13`, worktree `D:\SEP490\Projects\HotelOps-DEV1`, branch `feature/dev1-room-ops`. Remote FS-004 được xác minh bằng `git ls-remote`: SHA khớp baseline, không divergence, không cần push lại FS-004.

Phạm vi: hai GET CleaningMonitor đã được người dùng phê duyệt riêng trong phiên này sau khi phát hiện `[NonController]`. Không có phê duyệt kích hoạt recovery controller khác, đổi grant/schema/authentication hay thực hiện cleaning mutations. Các trang list/monitor và detail dùng API thật, không fallback mock; known-ID UC032 giữ nguyên route/transport. Detail thiếu branch yêu cầu thông tin hợp lệ, không lấy bản ghi prototype.

Readiness → danh sách đã lọc theo số phòng → chọn task → detail. Room Directory không cung cấp task ID/current cleaning cycle; không tự dùng room ID làm task ID. Search là keyword, không phải exact room identity. Người dùng chọn ngày khi cần xem task ngoài ngày hôm nay.

Không cộng UC credit. Đây là read integration, không hoàn thành toàn bộ UC037 (cancel còn thiếu), own-task discovery, lifecycle hay notification.

- [Mapping](SCREEN_API_MAPPING.md)
- [Contract/security audit](CONTRACT_AUDIT.md)
- [Live evidence](LIVE_API_RESULTS.md), [receipt](LIVE_RESULTS.json)
- [Regression](REGRESSION_RESULTS.md)
- [Write readiness](WRITE_OPERATION_READINESS.md)
- [Dependencies](DEV3_DEV2_DEPENDENCIES.md)
- [Next actions](NEXT_ACTIONS.md)
