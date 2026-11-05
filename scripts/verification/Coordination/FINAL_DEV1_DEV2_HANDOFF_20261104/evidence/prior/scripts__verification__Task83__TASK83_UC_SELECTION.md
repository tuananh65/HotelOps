# Task83 — Cổng lựa chọn UC

**BLOCKED cho mục tiêu thêm một UC hoàn tất. Không chọn UC mới để triển khai.**

Đã đối chiếu AGENTS, Change Notes, SRS §2.16 UC142 và §4.11 UC038, ledger Task80,
Task82 report/validation/progress và source/receipts thật trong Git snapshot
`cf8fb020362f8f64192e7f2f14a10547cebe5b91` (`integration/dev1-fe-handoff`).

| UC | Historical acceptance/ledger | Working tree hiện tại | Quyết định |
|---|---|---|---|
| UC142 View Room Types List | COMPLETE_VERIFIED_V1, weight1, earned1. Task70 source và accepted receipt có thật trong Git. Report tách49 checks UC142 =40 HTTP/persistence +9 SQL final-boundary; toàn batch345 không phải345 UC142 tests. | DTO đã có; RoomTypeBatchController, RoomTypeBatchService, RoomTypeCreationService, RoomTypeSchema và RoomTypes partial mapping chưa có. Program không đăng ký RoomTypeCreationService. | Không triển khai lại hoặc cộng1 điểm. Current integration chưa có; khôi phục dependency/shared DI/mapping cần phạm vi riêng. |
| UC038 View Room Turnover History | COMPLETE_VERIFIED_V1, weight2, earned2. Task72 history accepted receipt success=true,208 checks dùng chung UC038/048, không phải208 tests riêng UC038. | DTO đã có; OperationalHistoryController/Service/Files chưa có. Snapshot service là partial RoomTypeCreationService dùng BatchAsync/RoomTypeSchema; cùng dependency closure/DI còn thiếu. | Không chọn làm UC mới, không tái tạo writer hoặc copy controller thiếu dependencies. |

Không suy trạng thái COMPLETED chỉ từ báo cáo. Đã đọc controller/service và parsed accepted JSON,
ghi Git blob IDs và current-path presence trong HISTORICAL_SOURCE_EVIDENCE.json;
trích receipt case labels trong HISTORICAL_ACCEPTANCE_REVIEW.json.
Đây là historical accepted snapshot, không phải full runtime acceptance của checkout hiện tại.
Giữ nguyên ledger lịch sử; không tự hạ trạng thái vì chưa tích hợp hoặc nâng thành current deployed.

## Điểm chặn cụ thể

- UC142/038 đã thuộc26 complete/60 điểm: không phải ứng viên unfinished hợp lệ để tăng tiến độ.
- Current RoomType entity/mapping là legacy global, không có BranchId. Snapshot có optional
  RoomTypeSchema/partial model; không được suy RoomType ownership từ các room hiện dùng type.
- Cả hai snapshot controllers dùng shared scoped `RoomTypeCreationService`. Đăng ký này có trong
  Program snapshot nhưng vắng ở Program hiện tại. Task83 cấm sửa Program/mappings khi chưa duyệt.
- Source partial và helper closure thiếu, không phải hai controller có thể copy độc lập.

Không thiếu notification cho bản chất read-only của hai UC này; không tạo dependency DEV2 mới.
Các quan hệ lịch sử CleaningTask/Assignments/Progress/Inspection/ChecklistVersion/Defect/Rework/
Attachments/Audit đã có trong schema; điều đó không thay thế missing API/service integration.

Tuân thủ cổng §4: đánh giá UC142 trước, rồi UC038; không sửa production của cả hai khi không
đủ điều kiện. Không tự chọn UC thứ ba hoặc mở rộng UC037/138/139. Không xin blanket approval.
