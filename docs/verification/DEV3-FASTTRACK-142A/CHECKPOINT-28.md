# FASTTRACK-142A — checkpoint 28

FE13 UC123 query component đã có lịch sử assignment (cả đã kết thúc và hiện tại), feedback DTO, pagination độc lập cho events/assignments với total counts, default 25/max100 và kiểm tra offset overflow. Timestamp sau SQL materialization được trả với UTC kind. List có CurrentAssigneeId chỉ khi đúng một assignment ACTIVE chưa kết thúc và đã đến AssignedAt; không chọn tùy tiện nếu dữ liệu mâu thuẫn. Input control characters bị từ chối. Không trả EF entities hay nội dung linked work chưa có authority riêng.

Scope vẫn là component nhận `IQueryable<InternalRequest>` đã được caller giới hạn và nằm trong transaction authorization/read ổn định. **Không phải endpoint/UI hoặc authorization acceptance.** Catalog hiện không có FE13 read permission; đã gửi câu hỏi về code/role/scope, chưa tự cấp quyền hay mở endpoint. UC123 vẫn PARTIAL; còn read-authority composition, UI, direction filter, linked-work authorization và withdrawal.

## Kiểm chứng

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint28-backend-build-final.txt).
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS](checkpoint28-probe-build-verified.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-ir-history docs/verification/DEV3-FASTTRACK-142A/checkpoint28-runtime-verified.json`: [30 real SQL assertions PASS, rollback=true](checkpoint28-runtime-verified.json).
- SQL kiểm tra caller scope, overdue boundary, literal search, list pagination; events/assignments timestamp ties; ended/current/future handlers; feedback hiện hữu/vắng mặt; UTC; pagination độc lập/out-of-range; default page cap; invalid paging/control input; unique active-assignment index.
- [Lượt fixture lỗi](checkpoint28-runtime-final.json) được giữ: cố chèn thêm ACTIVE assignment đã bị `UX_internal_request_assignments_active` chặn. Fixture được sửa để kiểm tra future timestamp bằng UPDATE và đưa duplicate INSERT thành expected rejection ở cuối transaction. Không disable/drop index, không tính lượt lỗi là PASS.
- Exact server/database/GUID/run guard chạy trước mutation; synthetic fixture trong SERIALIZABLE transaction luôn rollback. [Đọc SQL độc lập sau rollback](checkpoint28-cleanup.json) xác nhận users/branches/requests/events/assignments/feedback/attachments đều trống. Không shared HotelOpsDB mutation; không API hoặc file evidence được tạo trong lượt này.

Frontend/dependencies không đổi nên không chạy lại npm ci/typecheck/build/AuthRouter. Log runtime giữ cảnh báo EF mapping/query infrastructure hiện hữu. Không lấy SQL query tests làm bằng chứng HTTP/role policy.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=28_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=efc51fbf8bb3d17f8cab482bc50f9bcd27482cef
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=FE13_PAGED_EVENTS_ASSIGNMENT_HISTORY_FEEDBACK_CURRENT_HANDLER_AND_UTC_QUERY_CONTRACTS
CHANGED_PATHS=src/HotelOps.Api/Services/InternalRequests/InternalRequestHistoryQueries.cs;scripts/verification/CanonicalDatabase/ModelProbe/SprintInternalRequestHistoryProbe.cs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-28.md,UC_STATUS_MATRIX.md,checkpoint28-*}
TESTS_EXECUTED=30_REAL_SQL_QUERY_ASSERTIONS_PASS;INITIAL_DUPLICATE_FIXTURE_FAILURE_RETAINED
BUILD_STATUS=BACKEND_PASS_0_WARNINGS_0_ERRORS;FRONTEND_UNCHANGED_NOT_RERUN
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=CHECKPOINT27_STARTED_TASK_CRITERIA_AND_PROGRESS_ACCEPTED;CREATE_LIFECYCLE_PENDING
FE13_STATUS=HISTORY_QUERY_EXPANDED_SQL_VERIFIED;READ_PERMISSION_CONTRACT_PENDING;NO_ENDPOINT_UI_ACCEPTANCE
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=PARTIAL;REPORT_EXPORT_ACCEPTANCE_REMAINS
ISOLATED_SQL_STATUS=EXACT_OWNED_TARGET;TRANSACTION_ROLLBACK_AND_INDEPENDENT_CLEANUP_VERIFIED
HTTPS_API_STATUS=NOT_RUN_THIS_SLICE
E2E_VERIFIED_WORKFLOWS=NONE_THIS_SLICE;SQL_QUERY_COMPONENT_ONLY
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE13_READ_PERMISSION_CONTRACT;FE05_ROOM_MAPPING;FE11_CREATE_LIFECYCLE;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=FE13 endpoint/UI after read authority confirmation; independent FE15 report/export scope; FE11 lifecycle contract reconciliation
```
