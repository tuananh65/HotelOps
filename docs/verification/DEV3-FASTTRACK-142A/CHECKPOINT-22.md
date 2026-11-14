# FASTTRACK-142A — checkpoint 22

UC044/GBR010: audit của approval domain lưu intent `INSPECTION_PASSED` cùng original-cleaner identity/progress/revision trong transaction domain. Publisher nội bộ đọc event theo cursor, khóa recipient User trước audit/inspection, kiểm tra lại attribution và INSPECTION_VIEW, rồi tạo notification + recipient DELIVERED trong một transaction riêng. Receipt theo inspection ngăn phát trùng; conflicting receipt bị từ chối. Không thêm schema production, permission hoặc endpoint. Caller của publisher cần scope DbContext sạch; lỗi được trả về để retry event, không replay business command. Audit chứa intent chưa giao không được purge. Cursor cho phép caller đi qua những recipient đang bị từ chối; worker scheduling/backoff/activation chưa triển khai.

Publisher chưa đăng ký DI/hosted worker vì approval composition chưa có room projection/authoritative receipt/public action. `AuthoritativeCommitAccepted=false` giữ nguyên. Không coi internal SQL domain commit là full UC044 acceptance. Notification không có ActionUrl cho tới khi source-opening route được tích hợp. Passed là dependency của UC044; không gán nhầm acceptance cho UC126 (SRS UC126 là failed inspection/incident alerts).

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint22-build-delivery.log.txt).
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS, 0 warnings/errors](checkpoint22-probe-build-delivery.log.txt).
- `dotnet test tests/HotelOps.Dev1.RegressionTests --filter FullyQualifiedName~Inspection`: [3 PASS](checkpoint22-regression.log.txt).
- `dotnet run --project tests/HotelOps.Inspection.DomainTests`: [45 PASS, in-memory](checkpoint22-domain.log.txt).
- `dotnet run --project tests/HotelOps.Inspection.LoaderTests`: [64 PASS, doubles](checkpoint22-loader.log.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-approve docs/verification/DEV3-FASTTRACK-142A/checkpoint22-notification-sql-delivery.json`: [24 SQL scenarios PASS](checkpoint22-notification-sql-delivery.json), [execution log](checkpoint22-notification-sql-delivery.log.txt).
- `dotnet test HotelOps.slnx`: [restore-only exit0](checkpoint22-solution-test.log.txt); không tính discovery/PASS.

23 SQL scenarios dùng rollback-only; scenario cuối commit domain/intent thật sau rehearsal cleanup bằng savepoint. Fault CHECK constraint làm recipient INSERT lỗi ở transaction delivery riêng: inspection PASS và audit event vẫn tồn tại, không có inbox rows và event vẫn được discovery. Gỡ riêng fault constraint rồi gọi publisher qua hai SQL connections: kết quả DELIVERED + ALREADY_DELIVERED, đúng một notification/recipient, discovery không còn event đã giao. Retry tiếp trả ALREADY_DELIVERED. Đây là concurrent invocations thật; chưa ép một lịch lock-wait xác định hoặc mô phỏng mất commit acknowledgment.

Các kiểm tra gồm recipient hint giả, thiếu quyền, revoked-after-stage, cấp lại access rồi retry, stale source, conflicting receipt và insert failure. Delivery không thay đổi business source. Exact instance/database/GUID/ownership guard áp dụng cho fixture/cleanup; cleanup diễn tập trước commit. Candidate attribution table/procedure/marker vừa tạo được xóa sau committed trial, không disable immutable trigger. Tất cả synthetic users/branches/audit/notification rows và fault constraint đã được xác nhận sạch; shared HotelOpsDB không bị tác động.

Lịch sử lỗi được giữ: literal raw SQL và tên API savepoint gây build lỗi trước khi sửa; stale-source fixture ban đầu vi phạm CK_inspection_workflow_v1; fault trigger thử nghiệm gây SQL334 vì EF OUTPUT, nên đổi sang fault CHECK constraint; ALTER TABLE không nhận parameter nên fixture dùng literal CHECK(user_id<0) trên bảng recipient synthetic rỗng. Các report lỗi đều có rollback/cleanup evidence. Không sửa production mapping để chiều fault harness.

Không chạy FE/npm ci/typecheck/build/Auth Router/HTTPS ở slice này: không đổi FE/public routes. Bằng chứng browser ở checkpoint20 là lịch sử, không phải E2E approval mới.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=22_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=4e68bfb2f423a9f0a477a103fff014e4f3020ef1
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=ATOMIC_AUDIT_NOTIFICATION_INTENT;EXPLICIT_RECIPIENT_REVALIDATION;INDEPENDENT_IDEMPOTENT_INBOX_DELIVERY;CURSOR_DISCOVERY
CHANGED_PATHS=src/HotelOps.Api/Services/Housekeeping/{InspectionApproveDomainWriter.cs,PassedInspectionNotificationSource.cs};src/HotelOps.Api/Services/Notifications/{InspectionPassedNotificationDelivery.cs,InspectionRecipientReadAccess.cs};scripts/verification/CanonicalDatabase/ModelProbe/{SprintApproveProbe.cs,SprintNotificationCommitProbe.cs};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-22.md,UC_STATUS_MATRIX.md,checkpoint22-*}
TESTS_EXECUTED=24_REAL_SQL_SCENARIOS;3_XUNIT;45_DOMAIN;64_LOADER
BUILD_STATUS=BACKEND_AND_PROBE_PASS_0_WARNINGS_0_ERRORS
FE05_STATUS=PARTIAL;PASSED_NOTIFICATION_COMPONENT_SQL_VERIFIED;PUBLIC_CONCLUSION_PROJECTION_RECEIPT_REJECTION_REINSPECTION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_WRITER_COORDINATION_REMAIN
FE11_STATUS=PARTIAL;LIFECYCLE_INTEGRATION_REMAINS
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_INTEGRATION_REMAIN
FE14_STATUS=PARTIAL;PASSED_DELIVERY_COMPONENT_TESTED;WORKER_AND_OTHER_PRODUCERS_REMAIN
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=PASS;COMMITTED_DELIVERY_FAILURE_RETRY_CONCURRENT_INVOCATIONS_VERIFIED;CLEANUP_CONFIRMED
HTTPS_API_STATUS=NOT_RUN_THIS_SLICE
E2E_VERIFIED_WORKFLOWS=NO_NEW_BROWSER_E2E;REAL_SQL_COMPONENT_DELIVERY_VERIFIED
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=ROOM_PROJECTION_MAPPING;AUTHORITATIVE_APPROVAL_COMPOSITION;WORKER_ACTIVATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=Room projection after Blocked mapping decision; approval receipt/public action; worker scheduling and rejection/reinspection integration

Câu hỏi nghiệp vụ Blocked ↔ OUT_OF_SERVICE đã gửi, chưa có trả lời tại thời điểm viết receipt. Không coi im lặng là đồng ý; không đợi DEV1/DEV2.
