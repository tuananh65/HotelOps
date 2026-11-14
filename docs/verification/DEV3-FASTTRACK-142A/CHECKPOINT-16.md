# FASTTRACK-142A — checkpoint 16: inspection severity catalog

Sprint ACTIVE. Local/remote baseline verified by fetch: `baaa5ffc93e9ad0e0626b632c4a798c631087e53`.

UC043 yêu cầu ghi defect với severity minor/major. Backend đã kiểm tra các giá trị này trong reference_values nhưng execution DTO không trả catalog; frontend chỉ nhận prop tùy chọn từ host nên thao tác defect bị khóa. Đã trả severities trong execution DTO và lấy dữ liệu này trực tiếp trong workspace. Không còn prop severity từ host. Catalog chỉ gồm SEVERITY/MINOR/MAJOR đang active, giữ ID và display name thực, sắp xếp ổn định theo sort_order rồi reference_id. Không tạo reference value trong application code, không thêm permission hay đổi schema.

ExecutionDto vẫn được gọi bên trong BatchAsync sau kiểm tra phiên, quyền INSPECTION_PERFORM, branch và assigned inspector; mutation vẫn kiểm tra lại reference value ở server. Catalog thiếu cho phép đọc response backend cũ nhưng khóa defect; catalog malformed/duplicate bị từ chối. Không mở canonical execution route hoặc approval/reinspection gates trong checkpoint này.

## Bằng chứng trực tiếp

- Backend build và ModelProbe build PASS, 0 warnings/errors.
- Typecheck và frontend production build PASS. Vite giữ cảnh báo use-client/bundle >500 kB; không nới threshold.
- Node contract tests: 15 PASS; gồm invalid/missing/duplicate catalog và các kiểm tra CSRF, ETag, session/uncertain mutation hiện có.
- Edge browser execution suite: 11 PASS. Flow Start → Save FAIL → GET nhận catalog → Record defect/evidence → Finish đến PENDING_CONCLUSION. HTTP được mock; không phải E2E SQL/HTTPS.
- Backend focused regression: 8 PASS. `dotnet test HotelOps.slnx` exit 0 nhưng chỉ restore, không discovery; không tính lệnh solution này là xUnit PASS.
- SQL probe: 11 scenarios PASS, rollbackVerified=true từng scenario. Scenario execution-catalog gọi ExecutionDto thật bằng reflection trong transaction fixture; kiểm tra đúng ID/label/order và loại MINOR inactive. 10 scenario approval domain được chạy lại, không chỉ dẫn kết quả lịch sử. Không kiểm tra HTTP authorization của execution DTO bằng scenario reflection này.
- Lần browser đầu dùng nhầm config testMatch assignment và báo No tests found; giữ log thất bại. Đã thêm config execution riêng và chạy thành công trong checkpoint16-browser-verified.log.txt.

SQL fixture kiểm tra server meita, database HotelOps_DEV3_142A_20261114_cc78c13f, GUID 1D5A9B53-7D19-435A-96AB-9FDFF447B6E0, marker cc78c13ffc124efcad67b3d804baa84c và baseline rỗng trước INSERT. Dữ liệu synthetic, mọi thay đổi rollback; không mutate shared HotelOpsDB. Model/schema đang dùng đã chạy query thành công. Không chạy HTTPS API checkpoint này.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=16_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=baaa5ffc93e9ad0e0626b632c4a798c631087e53
SOURCE_CONSOLIDATION_STATUS=CANONICAL_PRESERVED;NO_DUPLICATE_MERGE
BACKEND_BUILD=PASS_0_WARNINGS_0_ERRORS
FRONTEND_NPM_CI=NOT_RERUN;LOCKFILE_UNCHANGED
FRONTEND_TYPECHECK=PASS
FRONTEND_BUILD=PASS_WITH_EXISTING_VITE_WARNINGS
AUTH_ROUTER_TESTS=11_EXECUTION_BROWSER_PASS_MOCK_HTTP;CANONICAL_AUTH_SUITE_NOT_RERUN
IMPLEMENTED_FUNCTIONALITY=BACKEND_DERIVED_INSPECTION_SEVERITY_CATALOG_AND_DEFECT_UI_BINDING
CHANGED_FILES=frontend/src/features/inspectionExecution/{InspectionRoute.tsx,InspectionWorkspace.tsx,model.ts};frontend/tests/fe05-inspection-execution.{test.mjs,spec.ts};frontend/playwright.inspection-execution.config.ts;src/HotelOps.Api/DTOs/Housekeeping/InspectionWorkflowDtos.cs;src/HotelOps.Api/Services/Housekeeping/InspectionExecutionService.cs;scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveProbe.cs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-16.md,UC_STATUS_MATRIX.md,checkpoint16-*}
UC_REMAINING_GAPS=144_PARTIAL;CANONICAL_EXECUTION_RUNTIME;FE05_APPROVAL_COMPOSITION;UC006_UC010_RESPONSIBILITIES;FE11_FE13_WORKFLOWS
SQL_HTTPS_STATUS=11_ISOLATED_SQL_SCENARIOS_PASS_ROLLBACK_TRUE;HTTPS_NOT_RUN
NEXT_THREE_CODING_TASKS=Canonical inspection execution integration and real HTTPS defect workflow; accepted FE05 notification/projection/receipt composition; confirmed FE11/FE13 lifecycle implementation

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS reported after delivery to avoid self-reference. Matrix remains 144 PARTIAL and 0 COMPLETE_VERIFIED.
