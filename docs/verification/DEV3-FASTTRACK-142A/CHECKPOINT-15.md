# FASTTRACK-142A — checkpoint 15: evidence commit uncertainty

Sprint ACTIVE. Baseline local/remote verified by fetch: `3cea82e992e90e38856346fec822025c3ac1fb80`.

InspectionExecutionService và WorkProgressService trước đây xóa file mới khi lời gọi BatchAsync không trả Success. Nếu SQL đã commit nhưng acknowledgment bị mất, cách cleanup này có thể xóa file mà attachment đã lưu trong database đang tham chiếu.

Đã thêm EvidenceFileLifetime dùng chung cho hai writer. File do invocation sở hữu vẫn được cleanup khi thất bại trước commit. Sau khi bước beforeCommit đã chạy, giữ file khi kết quả commit không xác định. Hai writer bật preserveCommitErrors của BatchAsync hiện có; không đổi transaction helper, authorization, schema hoặc business transitions. File vẫn chỉ được đăng ký sau khi CreateNew thành công.

Đây là sửa lỗi được chứng minh qua control flow; chưa tái hiện mất kết nối SQL thật. Chính sách giữ file có thể để lại orphan nếu commit thực tế rollback. Chưa triển khai reconciliation; không được tự xóa file này dựa riêng vào lỗi HTTP/timeout.

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors (`checkpoint15-build.log.txt`).
- Focused EvidenceFileLifetimeTests: 5 PASS (`checkpoint15-files.log.txt`): cleanup đúng file sở hữu trước commit; giữ bytes sau boundary với success hoặc IOException giả lập; cancellation trước/sau boundary.
- Regression filter Housekeeping/Inspection/Progress/EvidenceFileLifetimeTests: 8 PASS, 0 failed/skipped (`checkpoint15-regression.log.txt`). Bao gồm 5 test trên, không cộng thành 13 test khác nhau.
- `dotnet test HotelOps.slnx`: exit 0 nhưng chỉ restore, không discovery test (`checkpoint15-solution-test.log.txt`); không tính là test PASS.
- Frontend không đổi; npm ci, typecheck, build và Auth/Router không chạy lại checkpoint này. Kết quả checkpoint 14 là bằng chứng lịch sử, không phải kết quả mới.
- Không chạy SQL/HTTPS mutation checkpoint này. Service-level fault injection, SQL rollback/commit ambiguity và end-to-end attachment retrieval còn phải kiểm chứng.

## Phạm vi còn lại

FE05 approval composition vẫn thiếu accepted notification staging, room projection và durable receipt binding; không đưa scaffold đang đóng gate vào production. UC032/043/109 chỉ được gia cố file lifetime, không được đánh dấu COMPLETE. Matrix vẫn 144 PARTIAL / 0 COMPLETE_VERIFIED.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=15_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=3cea82e992e90e38856346fec822025c3ac1fb80
SOURCE_CONSOLIDATION_STATUS=CANONICAL_BASELINE_PRESERVED;NO_DUPLICATE_MERGE
BACKEND_BUILD=PASS_0_WARNINGS_0_ERRORS
FRONTEND_NPM_CI=NOT_RERUN
FRONTEND_TYPECHECK=NOT_RERUN
FRONTEND_BUILD=NOT_RERUN
AUTH_ROUTER_TESTS=NOT_RERUN;CHECKPOINT14_260_PASS_HISTORICAL
IMPLEMENTED_FUNCTIONALITY=INSPECTION_AND_WORK_PROGRESS_EVIDENCE_PRESERVATION_AFTER_COMMIT_ATTEMPT
CHANGED_FILES=Services/Housekeeping/{EvidenceFileLifetime.cs,InspectionExecutionService.cs,WorkProgressService.cs};tests/HotelOps.Dev1.RegressionTests/EvidenceFileLifetimeTests.cs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-15.md,UC_STATUS_MATRIX.md,checkpoint15-*.log.txt}
UC_REMAINING_GAPS=144_PARTIAL;FE05_COMPOSITION;UC006_DEACTIVATION_UC010_REVOKE;FE11_FE13_LIFECYCLES;REMAINING_ACCEPTANCE
SQL_HTTPS_STATUS=NOT_RUN_THIS_CHECKPOINT;NO_SHARED_DATABASE_MUTATION
NEXT_THREE_CODING_TASKS=Service-level evidence commit-fault regression on owned SQL; accepted FE05 projection/notification/receipt composition; independent FE11/FE13 workflow with confirmed authority

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS are reported after delivery to avoid a self-referential commit.
