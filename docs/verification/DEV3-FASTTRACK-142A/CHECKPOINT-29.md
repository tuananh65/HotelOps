# FASTTRACK-142A — checkpoint 29

FE15 UC135 đã có component truy vấn snapshot cho bảy nhóm rooms/inspections/incidents/assets/items/tasks/requests, cache theo chủ sở hữu và bộ tạo CSV. Khôi phục có chọn lọc từ candidate `4ce3c470`, không merge/cherry-pick lại branch. Query yêu cầu transaction SERIALIZABLE do caller sở hữu, filters đã được caller authorize; từ chối resolved scope không khớp, liên kết khác branch và kết quả quá 50.000 dòng. Cache giữ bản sao bất biến, hết hạn sau 15 phút, tối đa 20 snapshot/32 MiB; không cho ghi đè ID snapshot. CSV giữ snapshot/filter/section headers, Unicode, số decimal chính xác, chỉ cột đã khai báo và bảo vệ spreadsheet formula injection.

**Đây là component, chưa phải endpoint/UI hoặc export được cấp quyền.** Không đăng ký DI/controller. Bộ tạo CSV không thay thế admission và audit export; chỉ được nối sau khi có authorization hiện thời, field policy và audit commit trước khi gửi file. `ExportFormats=[]` tiếp tục phản ánh chưa có chức năng download được mở. XLSX/PDF, pagination preview và RPT indicators đầy đủ còn thiếu; UC135 vẫn PARTIAL.

Xung đột cần xác nhận: SRS GBR-162 giới hạn Operations Manager/Owner, còn catalog RBAC cấp REPORT_VIEW cho DEPT_MANAGER. Đã gửi câu hỏi về scope/role; không tự bỏ grant hoặc mở API theo một phía. SRS UC135/GBR-163 yêu cầu cùng snapshot, giới hạn dòng và export audit. Đây là business-authority conflict, không phải chờ DEV1/DEV2 hay ownership lịch sử. FE13 read permission và FE05 room mapping vẫn chưa có trả lời.

## Bằng chứng trực tiếp

- `dotnet build HotelOps.slnx`: [PASS, 0 warnings/errors](checkpoint29-backend-build-final.txt).
- `dotnet test HotelOps.slnx`: [chạy thành công nhưng solution chỉ chứa API, không có test project](checkpoint29-solution-test.txt); không tính là test PASS.
- `dotnet test tests/HotelOps.Dev1.RegressionTests --filter FullyQualifiedName~ReportSnapshotTests`: [16 PASS, 0 skipped](checkpoint29-report-tests-final.txt). Kiểm tra owner/expiry/deep-copy/eviction, CSV formula/Unicode/quoting, metadata, nhiều section, header rỗng, row cap, invalid snapshot/filter, UTC/decimal.
- `dotnet build scripts/verification/CanonicalDatabase/ModelProbe`: [PASS, 0 warnings/errors](checkpoint29-probe-build-verified.txt).
- `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe --no-build -- --sprint-reports docs/verification/DEV3-FASTTRACK-142A/checkpoint29-runtime-verified.json`: [36 SQL/component assertions PASS, rollback=true](checkpoint29-runtime-verified.json).
- SQL chạy cả bảy queries; positive rows ở rooms/cleaning, inspection, tasks, requests và inventory; incidents/assets mới xác minh query execution/empty scope, chưa có positive lifecycle fixtures. Kiểm tra UTC boundaries, department linkage, snapshot giữ nguyên sau UPDATE, decimal stock balance/period/low-stock, 50.000 dòng được nhận và 50.001 bị từ chối. Không coi caller-supplied scope là role authorization acceptance.
- [Lượt lỗi assertion decimal](checkpoint29-runtime-final.json) được giữ: kỳ vọng chuỗi `13.1200`, trong khi SQL thực trả `13.120`. Đã sửa so sánh decimal invariant và giữ kiểm tra JSON string; [lượt cuối](checkpoint29-runtime-verified.json) chứa quan sát inventory thực. Không đổi dữ liệu hoặc precision để ép PASS.
- Exact server/database/GUID/run guard trước INSERT; synthetic data trong transaction luôn rollback. [Đọc SQL độc lập](checkpoint29-cleanup.json) xác nhận users/branches/tasks/requests/stock documents/lines/movements/thresholds/warehouses/items/attachments đều 0. Lệnh đọc cleanup đầu dùng nhầm `dbo.items`, lỗi trước khi trả kết quả; đã đối chiếu DbContext và chạy lại đúng `dbo.inventory_items`. Không có shared DB mutation.
- Frontend không đổi: npm ci/typecheck/production build/AuthRouter không chạy lại. HTTPS/browser không chạy trong slice này. Không có full-UC acceptance mới.

```text
TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=29_ACTIVE_PROGRESS
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=efe929f7b006de5443a60f779fa5e4b8a5efe08a
CURRENT_SHA=reported_after_commit
SOURCE_IMPLEMENTED=FE15_SCOPED_SNAPSHOT_QUERY_COMPONENTS_OWNER_CACHE_SAFE_CSV_RENDERER
CHANGED_PATHS=src/HotelOps.Api/Services/Insights/{ReportSnapshotQueries,ReportSnapshotStore,ReportCsvRenderer,InsightUtcJson}.cs;tests/HotelOps.Dev1.RegressionTests/ReportSnapshotTests.cs;scripts/verification/CanonicalDatabase/ModelProbe/{Program,SprintReportProbe}.cs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-29.md,UC_STATUS_MATRIX.md,checkpoint29-*}
TESTS_EXECUTED=16_OFFLINE_PASS;36_REAL_SQL_COMPONENT_ASSERTIONS_PASS;DECIMAL_ASSERTION_FAILURE_RETAINED
BUILD_STATUS=BACKEND_AND_PROBE_PASS_0_WARNINGS_0_ERRORS;FRONTEND_UNCHANGED_NOT_RERUN
FE05_STATUS=PARTIAL;ROOM_MAPPING_AND_PUBLIC_CONCLUSION_REMAIN
UC006_UC010_STATUS=PARTIAL;DEACTIVATION_REVOCATION_COORDINATION_REMAIN
FE11_STATUS=CHECKPOINT27_STARTED_TASK_CRITERIA_AND_PROGRESS_ACCEPTED;CREATE_LIFECYCLE_PENDING
FE13_STATUS=CHECKPOINT28_HISTORY_QUERY_SQL_VERIFIED;READ_PERMISSION_PENDING
FE14_STATUS=PARTIAL;NOTIFICATION_WORKER_REMAINS
FE15_STATUS=QUERY_CACHE_CSV_COMPONENTS_VERIFIED;ROLE_CONFLICT_AND_EXPORT_ADMISSION_AUDIT_UI_PENDING
ISOLATED_SQL_STATUS=EXACT_OWNED_TARGET;ROLLBACK_AND_INDEPENDENT_CLEANUP_VERIFIED
HTTPS_API_STATUS=NOT_RUN_THIS_SLICE
E2E_VERIFIED_WORKFLOWS=NONE_THIS_SLICE;SQL_QUERY_AND_SERIALIZER_COMPONENTS_ONLY
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE15_GBR162_VERSUS_REPORT_VIEW_CATALOG;FE13_READ_PERMISSION;FE05_ROOM_MAPPING;UC006_UC010_WRITER_COORDINATION
COMMIT_SHAS=reported_after_commit
PUSH_STATUS=reported_after_push
NEXT_IMPLEMENTATION=Resolve report role admission then API/UI/export audit; FE11 task lifecycle; UC006/UC010 writer coordination
```
