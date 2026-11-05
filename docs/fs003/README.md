# FS-003 — DEV1 Room Operations

Ngày 05/11/2026, Asia/Saigon. **PARTIAL**: FS-002 đã bảo toàn; UC032 frontend integration đã triển khai và kiểm tra bằng fixtures; real HTTPS/API/SQL E2E **BLOCKED_EXTERNAL**. Không cộng UC credit, không gọi UC032/fullstack hoàn thành.

## Kết quả theo phase

| Phase | Trạng thái | Kết quả |
|---|---|---|
| A Preserve FS-002 | Completed | Review đúng 14 files, secret-pattern/JSON/diff checks, build/tests; commit `6426d7a773743c789e2395aa087ea703dda083e3`; push đúng feature branch và remote SHA khớp |
| B Live verification | BLOCKED_EXTERNAL | DB cấu hình hiện tại chưa xác nhận disposable; historical trial existence chưa chứng minh; thiếu trusted HTTPS/frontend PEM/test identity. Không chạy backend/session/login hoặc business SQL |
| C UC032 audit | Completed at source level | Active GET/PUT, DTO, CSRF, HEX ETag, ownership/scope/status/transaction/file policy được đối chiếu source và Task74 approved contract; live schema chưa xác minh |
| D UC032 frontend | Implemented, partial acceptance | Reuse cleaning detail route; read/edit/save/refresh; item/note/attachment validation, conflict/unauthorized/error states; chỉ fixture tests |
| E Dependencies | Completed locally | [DEV3](DEV3_DEPENDENCIES.md), [DEV2](DEV2_DEPENDENCIES.md); không gửi email/chat cho thành viên khác |

## Git và ownership

Branch `feature/dev1-room-ops`; baseline trước preserve `e7f84422a33a8a65c771efdfb248e8231e83bb6a`. FS-002 source/docs giữ nguyên trong commit riêng nêu trên. `docs/fs002/README.md` mô tả thời điểm trước commit; receipt FS-003 này ghi nhận publication mới, không sửa lịch sử FS-002.

Không đổi branch, main/develop/integration/handover; không sửa C#, authentication infrastructure, DB mappings/generated entities/schema, DEV2 hoặc module DEV3. Không create/reset/drop/migrate/seed database. SQL trial scripts/harnesses cũ không được chạy vì có DDL/DML/fixture provisioning.

FS-003 được commit riêng sau review/validation. Exact HEAD/remote SHA sau publication được báo trong completion message; không nhúng SHA tự tham chiếu vào chính commit của nó.

## Cách sử dụng phần UC032

Route hiện có: `/cleaning/tasks/<numeric-task-id>?branchId=<numeric-branch-id>` chuyển screen 26 sang API progress panel. ID không có default/hard-code trong runtime. Backend quyết định quyền; query string không cấp scope. Khi không có `branchId`, screen vẫn là prototype và có nhãn dữ liệu minh họa. Không tự lấy ID/role/branch từ mock để gửi API; không trộn mock overview/actions với live progress.

Client chỉ dùng active progress và CSRF APIs. Không bật recovery CleaningMonitor để cung cấp task list; real task ID cần từ nguồn được phép cho tới khi monitor/source-read contract được duyệt. Chi tiết mapping tại [UC032_FRONTEND_MAPPING.md](UC032_FRONTEND_MAPPING.md).

## Tài liệu và validation

- [ENVIRONMENT_READINESS.md](ENVIRONMENT_READINESS.md): cấu hình/kiểm tra read-only, không lộ host/credentials/token.
- [REAL_API_VERIFICATION.md](REAL_API_VERIFICATION.md): 10 acceptance cases và ranh giới interception/real HTTP/SQL.
- [UC032_API_CONTRACT.md](UC032_API_CONTRACT.md): actual source contract, không tự đặt fields/statuses.
- [BUILD_TEST_RESULTS.md](BUILD_TEST_RESULTS.md): commands, kết quả mới/warnings/limitations.
- [NEXT_ACTIONS.md](NEXT_ACTIONS.md): pending approvals và FS-004.

## Files FS-003

Modified: `frontend/playwright.dev1.config.ts`, `frontend/src/screens/housekeeping/CleaningTaskDetailScreen.tsx`.

Created: `frontend/src/api/dev1/cleaningProgress.ts`, `frontend/src/screens/housekeeping/CleaningProgressPanel.tsx`, `frontend/tests/cleaning-progress.test.mjs`, `frontend/tests/cleaning-progress.spec.ts` và chín Markdown files trong `docs/fs003/` (README, REAL_API_VERIFICATION, ENVIRONMENT_READINESS, UC032_API_CONTRACT, UC032_FRONTEND_MAPPING, DEV3_DEPENDENCIES, DEV2_DEPENDENCIES, BUILD_TEST_RESULTS, NEXT_ACTIONS).

15 files trong changeset FS-003; không xóa file. Outputs/node_modules/browser reports được ignore. Test CSRF strings là fixtures vô hiệu ngoài interception, không phải secrets thật. Không lưu CSRF/session vào localStorage hoặc tài liệu.
