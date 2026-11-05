# FS-007 — Hồ sơ chốt Room Lifecycle / Notification V1

Ngày 06/11/2026, Asia/Saigon. **COMPLETE ở mức deliverables audit, proposal và gói yêu cầu phê duyệt; không phải các contract mới đã được duyệt. FS-008 chưa đủ entry criteria.** Không thay source, không chạy business writes/DDL/login API, không thêm UC credit.

Baseline DEV1: `a5b3f66e9732ff00035f0f5fc43971322e0122e6`, branch `feature/dev1-room-ops`, HEAD/remote khớp và tree sạch trước tác vụ. Handover có thẩm quyền: `28f0a5e4f582bc56ba101b48915d33baadeb7b95` (COORD-FINAL-01). Proposal FS007 không sửa approval cũ; phân biệt design accepted, exact diff approved và deployment/runtime verified.

Đã rõ: ba chiều Occupancy/Housekeeping Readiness/EffectiveBlock độc lập; Ready cần passed current inspection và không effective block; ExpectedReleaseAt không tự release; caller-owned transaction; mandatory intent failure rollback, delivery failure sau commit retry; A01 đã accepted, không yêu cầu chọn interface mới. Exact source A01/A02 vẫn cần DEV2 bàn giao. B01/B02/C01 exact executable packages còn REVISE_REQUIRED.

V1 đề xuất cho UC028: không UPDATE rooms.operational_status để giả lập readiness; thêm current-cycle pointer + readiness-event sidecars sau khi duyệt exact DDL/EF, derived projection, bảo toàn legacy directory/history. UC028 không bị buộc chờ Notification/UC138/Pause All. UC029 giữ GET planning và core eligibility, chỉ mở POST integration khi publication/replay/recipient-read/commit guard đạt acceptance. Không cho prototype Save thay các dependencies này.

## Deliverables

- [Room lifecycle](ROOM_LIFECYCLE_CONTRACT.md) / [state matrix](CLEANING_STATE_MATRIX.md)
- [UC028 Create](UC028_CREATE_PROPOSAL.md) / [UC029 Assign](UC029_ASSIGN_PROPOSAL.md)
- [UC030/031/034](UC030_031_034_DEPENDENCIES.md)
- [Gửi DEV2](DEV2_NOTIFICATION_APPROVAL_REQUEST.md) / [Gửi DEV3](DEV3_PLATFORM_APPROVAL_REQUEST.md)
- [Shared decisions](SHARED_CONTRACT_DECISIONS.md) / [approval matrix](CONTRACT_APPROVAL_MATRIX.md)
- [Implementation plan](IMPLEMENTATION_PLAN.md) / [live write acceptance plan](LIVE_WRITE_ACCEPTANCE_PLAN.md)
- [FS008 entry criteria](FS008_ENTRY_CRITERIA.md)
- [Source/commit evidence](SOURCE_EVIDENCE.json), [SQL metadata](SQL_METADATA.json), [validation](VALIDATION.md)

Hai request viết độc lập bằng tiếng Việt để developer gửi; chưa gửi tới bất kỳ thành viên nào. Không có approval mới từ DEV2/DEV3/chủ dự án trong tác vụ này. SQL write approval FS004 không mở rộng; FS007 chỉ SELECT sys metadata đúng retained trial, không reset/reseed. Build/tests NOT_RUN vì chỉ documentation; kết quả107 xUnit/39 Node/39 browser/2 live từ FS006 là lịch sử, không báo là chạy lại FS007.
