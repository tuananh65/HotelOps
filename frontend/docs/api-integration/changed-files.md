# Danh sách chính xác file thay đổi

A: file mới; M: sửa; D: bỏ test prototype đã thay thế. Đường dẫn tính từ gốc repository.

|Trạng thái|File|Mục đích|
|---|---|---|
|A|frontend/.env.example|Cấu hình môi trường API|
|M|frontend/README.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/README.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/api-contract.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/api-dto-fields.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/api-mapping.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/changed-files.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/contract-examples.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/database-schema-audit.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/dto-fields.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/screen-inventory.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/screen-inventory.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/sources-and-conflicts.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/system-messages.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/use-case-specs.json|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/api-integration/verification.md|Tài liệu API, đối chiếu và hướng dẫn|
|M|frontend/docs/screen-manifest.json|Đồng bộ metadata SRS và mô hình UI|
|M|frontend/docs/screen-matrix.md|Tài liệu API, đối chiếu và hướng dẫn|
|M|frontend/docs/screen-routes.md|Tài liệu API, đối chiếu và hướng dẫn|
|A|frontend/docs/verification/api-readiness-desktop.png|Bằng chứng kiểm tra giao diện|
|A|frontend/docs/verification/api-rooms-mobile.png|Bằng chứng kiểm tra giao diện|
|M|frontend/docs/verification/screen-files.json|Bằng chứng kiểm tra giao diện|
|M|frontend/playwright.config.ts|Browser test portable và HTTP fixture mode|
|M|frontend/scripts/verify-screens.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|A|frontend/src/api/adapters.ts|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/config.ts|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/endpoints.ts|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/http.ts|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/modules.ts|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/system-messages.json|Hợp đồng, transport, adapter và API service|
|A|frontend/src/api/types.ts|Hợp đồng, transport, adapter và API service|
|M|frontend/src/catalog.json|Đồng bộ metadata SRS và mô hình UI|
|A|frontend/src/components/ApiGate.tsx|Điều phối tải dữ liệu và phiên/quyền route|
|M|frontend/src/components/ScreenBoundary.tsx|Tích hợp route hoặc nội dung liên quan API|
|A|frontend/src/controllers/useApiScreen.ts|Điều phối tải dữ liệu và phiên/quyền route|
|M|frontend/src/domain.ts|Đồng bộ metadata SRS và mô hình UI|
|M|frontend/src/evidence.ts|Upload file qua API|
|M|frontend/src/locales/screen-descriptions.vi.ts|Tích hợp route hoặc nội dung liên quan API|
|M|frontend/src/main.tsx|Tích hợp route hoặc nội dung liên quan API|
|M|frontend/src/report-engine.ts|Báo cáo/snapshot/export từ server|
|M|frontend/src/report-export.ts|Báo cáo/snapshot/export từ server|
|M|frontend/src/routes/ScreenIndex.tsx|Tích hợp route hoặc nội dung liên quan API|
|M|frontend/src/screens/auth/ChangePasswordScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/auth/ProfileScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/auth/SignInScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/dashboard/DashboardScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/housekeeping/CleaningAssignmentScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/housekeeping/CreateCleaningTaskScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/inspections/ReinspectionScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/inspections/RequestReworkScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/inventory/InventoryDashboardScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/inventory/StockBalanceLedgerScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/maintenance/MaintenanceAssignmentScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/maintenance/MaintenanceTaskDetailScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/maintenance/PreventiveMaintenanceScheduleListScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/maintenance/ReportIncidentScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/reports/OperationalReportsScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/tasks/OverdueWorkScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/tasks/TaskAssignmentScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/tasks/TaskListScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/tasks/WorkloadTimeLogsScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/screens/workTime/WorkdayRecordScreen.tsx|Chờ request, trạng thái và thao tác giao diện|
|M|frontend/src/services.ts|API facade và cache thay nguồn nghiệp vụ giả lập|
|M|frontend/src/services/readModels.ts|API facade và cache thay nguồn nghiệp vụ giả lập|
|M|frontend/src/ui.tsx|Chờ request, trạng thái và thao tác giao diện|
|A|frontend/tests/api-test-helpers.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|M|frontend/tests/browser.spec.ts|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|M|frontend/tests/domain.test.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|A|frontend/tests/fixtures/api-fixture.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|A|frontend/tests/http.test.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|D|frontend/tests/mock-data.test.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
|M|frontend/tests/workflows.test.mjs|Kiểm tra theo hợp đồng API và kiểm kê màn hình|
