# Task82 — Chọn UC và đối chiếu baseline

DEV1; branch `feature/BE1-database-integration`, HEAD `4a187320643d0c82f232c6b9dd5a667b8401c280`.
Đọc AGENTS, SRS Change Notes, SRS UC004 §1.4, UC012 §2.1, UC037 §4.10,
GBR007/008/010/015/033/044/048/055; ledger Task80 và reports/receipts Task75–81.
Nguồn implementation: controller/service/DTO UC037 hiện tại; UserAccountCreationService;
Branch maintenance DTO và source/history Task74 tại `cf8fb02`.

## Đúng ba ứng viên

| UC | Baseline và khả năng hoàn tất | Quyết định |
|---|---|---|
| UC037 Monitor Cleaning Tasks, 2 điểm | API list/detail đã chạy thật. Timeline bị giới hạn latest100 và không truy cập được lịch sử cũ. Cancel bắt buộc theo SRS còn cần room lifecycle và notification/retry của DEV2. | Chọn phần implementation độc lập: timeline pagination, SQL-backed read boundary giữ nguyên. Không tuyên bố full completion. |
| UC012 Update Branch Information, 2 điểm | Metadata create/read/update Task74 đã được nghiệm thu trong snapshot, chưa phục hồi toàn bộ runtime. Deactivate phải bảo đảm GBR033 và inactive branch không nhận records mới xuyên writer DEV2; contract chưa chốt. | Không thay thế bằng metadata-only hay khôi phục controller thiếu dependency/DI. |
| UC004 Create User Account, 5 điểm | Source hiện hữu chỉ single-role allowlist. SRS yêu cầu selected roles/scope. Multi-role/global-role/delegation còn cần quyết định được duyệt. | Không tự mở rộng allowlist hoặc tạo grants. |

Không chọn UC138/139; không bỏ notification hoặc giả lập delivery để hoàn tất UC037.
Không chọn UC032/109 vì đã complete lịch sử. Không có ứng viên trong ba UC này đạt full acceptance
trong phạm vi hiện tại; mục tiêu ít nhất một UC complete chưa đạt. Task82 vẫn thay đổi production
controller/service/DTO và kiểm chứng phần độc lập, không chỉ lập readiness package.

## UC032 không phải lỗi cộng điểm

`git show cf8fb02:scripts/verification/DEV1_21_DAY_DELIVERY_PLAN.md` §22 và file local
`obj/task74-validation.local.json` được đọc trực tiếp; receipt local không phải Git blob trong snapshot.
Task73:24UC/56đ → Task74:26UC/60đ. UC032(2đ) + UC109(2đ) đã được cộng đúng một lần.
Ledger Task80 vẫn ghi UC032 `COMPLETE_VERIFIED_V1`, earned2, UC109 earned2.
Đó là historical accepted snapshot, không chứng minh các endpoint Task74 hiện đã được phục hồi.
Không hạ classification vì thiếu source trong checkout này, không cộng lại và không sửa ledger chính.
Xem `LEDGER_RECONCILIATION.json`, `HISTORICAL_RECEIPTS_REVIEW.json`.

## Hợp đồng phần triển khai

GET detail cũ giữ route, HTTPS/cookie/session/SQL permission và branch/source checks.
Query mới: `timelinePage` mặc định1, `timelinePageSize` mặc định100, cho phép1..100.
Cùng page/window áp dụng độc lập cho assignments/progress/audit; mỗi collection có total riêng.
`timelineLimit` là effective page size, thêm `timelinePage`. Các trường/giá trị mặc định cũ giữ nguyên.
Timestamp giảm dần, ID giảm dần để phân biệt ties. Page quá total trả collection rỗng, giữ totals.
Reject âm/zero/overflow400; không dùng paging để vượt identity/permission/scope.
Một request dùng SERIALIZABLE boundary hiện hữu cho count+page. Không cam kết snapshot bất biến
giữa nhiều HTTP requests nếu có history mới được commit; client cần refresh khi dữ liệu thay đổi.
