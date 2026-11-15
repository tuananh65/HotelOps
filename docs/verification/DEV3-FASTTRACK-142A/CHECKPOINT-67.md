# FASTTRACK-142A checkpoint67 — hoàn tất UC107 Accept/Decline

**UC hoàn thành mới: 1 — UC107 COMPLETE_VERIFIED.** Tổng **14 COMPLETE_VERIFIED / 130 PARTIAL / 144 UC**. [Acceptance ledger](UC107_ACCEPTANCE.md) đối chiếu từng điều kiện SRS với bằng chứng trực tiếp; không nâng các UC Create/Assign/Reopen chỉ vì fixture đã tạo task.

Đã triển khai Decline với reason bắt buộc, chuyển Pending Assignment, kết thúc assignment và ghi progress/audit/notification intent cùng transaction. Giữ CSRF, strong ETag, quyền hiện tại và branch/department scope. Final permission gate kiểm tra lại quyền sau khi assignment bị kết thúc mà không cấp lại quyền đọc lịch sử cho người đã mất trách nhiệm. Accept bảo toàn first actual start khi đã có lịch sử; các guard Start/Resume không bị bỏ.

Danh sách/chi tiết task có workflowStatus từ authority hiện có, bộ lọc trước pagination, priority label và deadline. Decline thành công xóa editor và tải lại danh sách cá nhân. Department Head nhận notification được lưu bền vững, retry không trùng, và được kiểm tra lại quyền khi mở source.

**Workflow hoàn chỉnh đã kiểm chứng:** Assigned → Accept → reload Accepted → Start → progress → Pause/Resume; và Assigned → Decline với reason → Pending Assignment → mất quyền own-task → manager nhận notification → mở task và xem reason/history. [Runtime cuối](checkpoint67-decline-final.json) PASS: 91 records, gồm 33 entry assertions, 41 decline assertions và 21 stages của ba browser. [Hosted worker thật](checkpoint67-decline-worker-final.json) PASS, một delivery, source không bị sửa. Các lần trial thất bại và nguyên nhân được giữ trong acceptance ledger.

## Validation trực tiếp

- [Backend build](checkpoint67-backend-build.log.txt), [probe build](checkpoint67-probe-build.log.txt): PASS, 0 warning/error.
- [Backend regression](checkpoint67-backend-tests-final.log.txt): **588 PASS, 11 SKIP, 0 FAIL**. [Solution test](checkpoint67-solution-test-final.log.txt) exit0 nhưng solution không chứa test project; không tính thành coverage.
- [Focused FE11](checkpoint67-task-tests.log.txt): **44 PASS**; [Auth/Router/notification](checkpoint67-auth-router.log.txt): **841 PASS**, 0 fail/skip.
- [Typecheck](checkpoint67-typecheck.log.txt), [production build](checkpoint67-frontend-build.log.txt): PASS. Vẫn có Vite use-client/chunk>500kB và runtime RoomType index/unordered Take warnings. npm ci/full frontend suite không chạy lại vì lockfile không đổi; không báo PASS mới cho chúng.
- [Disposable install](checkpoint67-TaskDecline-install5.txt) chạy lặp thành công; chỉ thêm PENDING_ASSIGNMENT vào CHECK workflow hiện có, không thêm bảng hoặc sửa generated EF/physical status. Note/audit column lengths được xác minh.
- [SQL cleanup](checkpoint67-task-cleanup-final.txt), [files/host cleanup](checkpoint67-files-host-cleanup.json): PASS. Năm clone và backup đã xóa, nguồn disposable trở về constraint ban đầu, không còn private files/listener. Không mutation shared HotelOpsDB, không production deployment.

## Gaps và việc code tiếp theo

1. **UC105 linked requester:** InternalRequest có requester rõ nhưng mapping các nguồn khác chưa đủ bằng chứng; giữ503 cho verification thiếu provenance. C67 đã hoàn thiện workflow list/filter, không còn gap dùng riêng physical status ở danh sách canonical.
2. **UC102/104 Create/Assign/Reassign:** thay synthetic precondition bằng public workflow tạo task/criteria và phân công; giữ role/branch, lifecycle, deadline và thông báo đúng SRS.
3. **UC106 Reopen/cycle**, rồi UC006/010 và FE13: lưu cycle boundary rõ ràng, bảo toàn performer separation và history. Return không được coi là Reopen.

Không có runtime/security blocker cho UC107 trong disposable trial. Optional schema và worker vẫn cần cấu hình được quản lý ở môi trường triển khai; checkpoint không triển khai vào shared/production.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=67
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=4ef7f660bcd6ac32adbf126b852c47e5ceb5284c
NEWLY_COMPLETED_UCS=UC107
SOURCE_CONSOLIDATION_STATUS=continued canonical source; no handoff/remerge/restart
SQL_HTTPS_STATUS=isolated acceptance PASS; five owned clones/backups removed; shared DB untouched
SPRINT_STATUS=ACTIVE; deadline unchanged 2026-11-16 17:50 UTC

Delivery commit and final remote equality are recorded in the Git delivery receipt after staging review. Changes are limited to FE11/notification integration, regression harnesses, narrow isolated scripts and this evidence.
