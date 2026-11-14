# FASTTRACK-142A — checkpoint 21

Tiến triển thực: approval domain trả về nguồn người nhận Passed, lấy đúng cleaner attribution bất biến và kiểm tra quyền INSPECTION_VIEW của người nhận theo policy branch của inspection history. Không dùng quyền inspector, không tự cấp quyền cho housekeeper, không dùng current assignee làm attribution. Kết quả phân biệt ELIGIBLE, RECIPIENT_READ_DENIED, ATTRIBUTION_UNAVAILABLE; đây là snapshot trong transaction, chưa phải notification staging/delivery/commit receipt. Delivery phải kiểm tra lại quyền và source.

Khôi phục riêng body DDL R04 từ `d8d9195e13e0a5edb1edc79050ee56f24525209e:scripts/database/release/fe04-cleaner-r04/01_CleanerAttributionCandidate.sql` vào probe. Chỉ đổi guard thành exact instance/database/GUID/DEV3 marker và bắt buộc outer transaction. DDL, immutable capture và dữ liệu synthetic đều rollback; không cài schema thường trực, không đổi generated entities hoặc production permissions. Capture procedure gốc không thay đổi.

## Bằng chứng thực thi

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint21-build.log.txt).
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS, 0 warnings/errors](checkpoint21-probe-build-final.log.txt).
- `dotnet test tests/HotelOps.Dev1.RegressionTests --filter FullyQualifiedName~Inspection`: [3 PASS](checkpoint21-regression.log.txt).
- `dotnet run --project tests/HotelOps.Inspection.DomainTests`: [45 PASS, in-memory](checkpoint21-domain.log.txt).
- `dotnet run --project tests/HotelOps.Inspection.LoaderTests`: [64 PASS, doubles](checkpoint21-loader.log.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-approve docs/verification/DEV3-FASTTRACK-142A/checkpoint21-recipient-sql-final.json`: [18 real SQL scenarios PASS, rollback verified](checkpoint21-recipient-sql-final.json). Bao gồm eligible original cleaner, thiếu read permission, inactive user, expired role, wrong branch, revoked access, missing attribution và 11 cases domain/catalog trước đó. Kiểm tra không phát sinh notification rows, không dùng inspector làm cleaner, không nhận là authoritative commit, dữ liệu và candidate objects không tồn tại sau rollback.
- `dotnet test HotelOps.slnx`: [exit 0, restore-only](checkpoint21-solution-test.log.txt), không tính là test discovery/PASS.
- [Lần SQL đầu thất bại](checkpoint21-recipient-sql.json): fixture dùng nhầm `user_branch_access.is_active`; mapping thực dùng `revoked_at`. Đã sửa fixture, không sửa schema, giữ log và rollbackVerified=true.

Không chạy lại frontend/npm ci/Auth Router/HTTPS: không đổi FE hoặc public route trong slice này. Không có E2E approval mới. Các kết quả checkpoint20 là bằng chứng lịch sử, không tính lại ở đây.

## Receipt

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=21_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=5d713a916f000d4ff6b9959a781247539e5f76ab
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=PASSED_INSPECTION_ORIGINAL_CLEANER_RECIPIENT_SELECTION_AND_EXPLICIT_READ_AUTHORIZATION
CHANGED_PATHS=src/HotelOps.Api/Services/Housekeeping/{InspectionApproveDomainWriter.cs,PassedInspectionNotificationSource.cs};scripts/verification/CanonicalDatabase/ModelProbe/{SprintApproveProbe.cs,SprintCleanerAttribution.sql};docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-21.md,UC_STATUS_MATRIX.md,checkpoint21-*}
TESTS_EXECUTED=18_REAL_SQL;3_XUNIT;45_DOMAIN;64_LOADER
BUILD_STATUS=BACKEND_AND_PROBE_PASS_0_WARNINGS_0_ERRORS
FE05_STATUS=PARTIAL;RECIPIENT_SELECTION_SQL_VERIFIED;STAGING_DELIVERY_PROJECTION_RECEIPT_REJECTION_REINSPECTION_INCOMPLETE
UC006_UC010_STATUS=PARTIAL;ACTIVATION_ASSIGNMENT_PREVIOUSLY_EXERCISED;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=PARTIAL;LIFECYCLE_INTEGRATION_REMAINS
FE13_STATUS=PARTIAL;READ_AUTHORITY_AND_LIFECYCLE_INTEGRATION_REMAIN
FE14_STATUS=PARTIAL;INBOX_PREVIOUSLY_EXERCISED;DURABLE_CROSS_MODULE_DELIVERY_REMAINS
FE15_STATUS=PARTIAL;AUDIT_PREVIOUSLY_EXERCISED;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=18_COMPONENT_SCENARIOS_PASS;ALL_DATA_AND_REHEARSAL_DDL_ROLLED_BACK;SHARED_DB_UNTOUCHED
HTTPS_API_STATUS=NOT_RUN_THIS_SLICE
E2E_VERIFIED_WORKFLOWS=NO_NEW_E2E;PRIOR_INSPECTION_EXECUTION_AND_EDIT_EVIDENCE_IN_CHECKPOINT20
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=PASSED_OUTBOX_RETRY_AND_RECEIPT;ROOM_PROJECTION_MAPPING;FULL_CONCLUSION_COMPOSITION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=Durable Passed staging/delivery with recipient revalidation; room projection/receipt composition; canonical conclusion and reinspection workflows

SRS Blocked và schema OUT_OF_SERVICE chưa được âm thầm coi là đồng nghĩa. Quyền INSPECTION_VIEW hiện không tự cấp cho HK_STAFF; notification delivery không được vượt qua quyền đọc này. Những giới hạn này cần được giải quyết trong composition tiếp theo, không phải lý do chờ owner cũ.
