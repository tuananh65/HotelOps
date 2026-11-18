# Bàn giao P1: bộ lọc thu gọn và popup/modal HotelOps

Ngày: 19/11/2026. Nhánh: `integration/dev1-dev2-dev3-unblock-20261114`.
HEAD trước/sau tác vụ: `f9855f1b24f04fa0b107e0841eee6cad47673d98`. Không tạo commit, push hoặc merge.

## Kết quả triển khai

- Bộ lọc nâng cao đóng khi vào trang, refresh hoặc chuyển đường dẫn. Nút **Bộ lọc / Ẩn bộ lọc** có Lucide, số điều kiện và aria-expanded/aria-controls.
- Mở/đóng là trạng thái giao diện riêng; không tự gửi lại truy vấn hoặc xóa điều kiện đang áp dụng. Những màn lọc ngay khi chọn vẫn giữ cơ chế cũ. Những màn có bản nháp được khôi phục từ truy vấn áp dụng; các trường native dùng defaultValue hủy bản nháp khi đóng.
- Tìm kiếm nhanh vẫn ở ngoài phần nâng cao tại danh mục người dùng, yêu cầu nội bộ, thông báo, audit, danh mục tài sản, công việc vận hành, công việc vệ sinh và chọn phòng xem lịch sử.
- Các editor danh mục, vật tư/kho/đơn vị, mẫu ca, hủy phân ca, phân loại/tiếp nhận/phân công sự cố, bảo trì, xử lý yêu cầu, phân công/tiến độ công việc và bước xác nhận quản trị dùng popup chung.
- Form ngắn dùng modal; chi tiết dài và form nhiều trường dùng drawer. Xem nhanh tài khoản, checklist, lịch sử kiểm tra và trạng thái phòng đã chuyển sang drawer.
- Nút lưu/xác nhận được chuyển vào footer cố định, giữ handler, form association, validation và điều kiện disabled. Không lấy nút lưu của modal con ra footer cha.
- Chặn đóng khi đang lưu, cảnh báo thay đổi chưa lưu, giữ lỗi trong popup, focus trap, Escape an toàn, trả focus, backdrop và khóa cuộn có đếm số modal lồng nhau.
- Toast xanh chỉ phát từ các nhánh thành công đã được workflow xác nhận. Không suy đoán thành công từ việc click nút, submit hoặc một request bất kỳ.
- Không đổi API contract, endpoint, nghiệp vụ, status transition, auth, permission, audit, concurrency hoặc database. Hai tệp backend đang có thay đổi từ tác vụ trước được giữ nguyên; P1 không sửa C#.

## Số lượng và quy ước đếm

| Hạng mục | Số lượng |
|---|---:|
| Tệp frontend đã quét | 385 |
| Tệp TSX trong đợt rà soát | 245 |
| Tệp màn hình/điểm vào Page, Screen, Workspace, Route, RouteHost | 91 |
| Tệp có vị trí bộ lọc đã chuẩn hóa | 28 |
| Vị trí bộ lọc nâng cao đóng mặc định | 28 |
| Form HTML được đặt trong lớp thao tác popup hoặc renderer vận hành chung | 45 |
| Vị trí modal: gồm editor, fieldset thao tác và bước xác nhận | 98 |
| Vị trí drawer/chi tiết dài, gồm renderer dùng chung | 8 |
| Tệp source/test trong danh sách P1 | 125 |

Đây là số vị trí khai báo trong source, không phải số URL độc lập hay số ca UAT. Một workspace có thể phục vụ nhiều URL hoặc nhiều bản ghi. Form renderer vận hành dùng bộ lọc khi đọc và modal/drawer khi ghi. Không cộng số modal với số form để suy ra số chức năng.

## Component dùng chung

- `frontend/src/components/workspace-interactions.tsx`: FilterToggleButton, CollapsibleFilterPanel, DeferredFilterPanel, ActionDialog, OperationSurface, restoredFilterDraft, WorkflowFeedbackHost và announceWorkflowSuccess.
- `frontend/src/components/design-system.tsx`: tái sử dụng AppModal/AppDrawer/ConfirmDialog/ActionMenu/AppToast; bỏ FilterDrawer cũ không còn dùng, sửa Escape và khóa cuộn cho modal lồng nhau.
- `frontend/src/styles/design-system.css`: panel thu gọn, toolbar, bố cục responsive, input thống nhất trong popup, footer, backdrop và giới hạn tràn ngang.
- `frontend/src/shared-host/CacaoShell.tsx`: host toast; giữ cây route ổn định qua thay đổi phiên đăng nhập.

## Phạm vi giữ nguyên có chủ đích

Trang đăng nhập/đổi mật khẩu và các route biểu mẫu độc lập như tạo tài khoản, tạo yêu cầu nội bộ, đăng ký/chỉnh sửa tài sản, thực hiện kiểm tra hoặc lập danh sách phân ca vẫn là trang làm việc riêng khi chúng không phải form mở rộng inline. Bước xác nhận bật thêm trên các trang này đã được chuyển sang popup khi có.

Giữ cây cấu trúc khách sạn, submenu, lịch sử dạng accordion, bảng con, bảng kiểm đếm/chỉnh định mức và các vùng thông tin tĩnh. Không tạo chức năng đặt phòng/khách lưu trú hay trường nghiệp vụ chưa được backend hỗ trợ.

## Kiểm chứng

| Kiểm chứng | Kết quả và bằng chứng |
|---|---|
| Frontend build, bao gồm tsc -b và Vite | Passed: exit 0; tsc -b và Vite — `artifacts/ui-p1/build-final.txt` |
| npm run typecheck | Passed: exit 0 — `artifacts/ui-p1/typecheck-final.txt` |
| API/client regression: người dùng, tạo tài khoản, dashboard link, yêu cầu nội bộ, workday, trạng thái phòng | Passed: 387/387 — `api-regression.txt` |
| Dashboard, người dùng, buồng phòng, ca làm việc, phân loại sự cố và P1 | Passed: 59/59 — `browser-regression-final.txt` |
| Bộ P1 sau tinh chỉnh: cancel, API lỗi, duplicate submit, focus, popup lồng nhau, overlay | Passed: 12/12 — `browser-p1-final.txt` |
| Responsive bổ sung: kiểm tra cả tràn ngang bên trong body modal ở 390/800px | Passed: 2/2 — `responsive-final.txt` |
| Vô hiệu hóa tài khoản: disabled mặc định, duplicate submit, unknown outcome, thay chủ thể, response cũ, CSRF | Passed: 27/27 — `user-deactivation-tests-final.txt` |
| Format phạm vi P1 | Passed: 125/125 tệp — `format-check.txt`; danh sách tệp trong `files.json` |
| Whitespace Git | Passed: exit 0 — `diff-check.txt` |
| Production 5080 và Development 5173 | Passed: 13/13 — `production-smoke.txt`, kiểm tra asset mới, login, SQL health, auth/CSRF/cookie, route bảo vệ và lỗi JavaScript |

Các tệp bằng chứng trên đều nằm trong `artifacts/ui-p1/`. Các nhóm test có phần giao nhau, không cộng chúng thành số ca duy nhất.

Bộ kiểm thử vô hiệu hóa tài khoản cũ ban đầu không chạy được 25/27 ca vì thiếu fixture cho deactivation-preview và còn selector tiếng Anh. Đã cập nhật fixture đúng API hiện có, chuẩn hóa phương thức GET mặc định và selector tiếng Việt; giữ nguyên các assertion về quyền, phiên, khóa thao tác và số PATCH. Kết quả sau sửa là 27/27 Passed. Các log trung gian được giữ lại để đối chiếu.

## Giới hạn còn lại

- Chưa chạy toàn bộ `npm test` và UAT bằng tất cả tài khoản thật trên từng màn. Kết quả trình duyệt nghiệp vụ dùng fixture, không phải ghi giao dịch lên database đang demo.
- Smoke Production kiểm tra kết nối SQL và bảo vệ truy cập; không xác nhận toàn bộ workflow nghiệp vụ trên dữ liệu thật.
- Bộ `scheduled-workday-browser.test.mjs` cũ từng còn 48 ca thất bại ở đợt V2, chủ yếu dùng selector tiếng Anh; chưa sửa/chạy lại toàn bộ bộ đó trong P1. Bộ tiếng Việt tương ứng đã Passed 5/5 trong nhóm 59 ca.
- Lint toàn kho chưa được xác nhận Passed; script lint hiện còn tham chiếu playwright.config.ts không tồn tại. Phạm vi tệp P1 được kiểm tra Prettier riêng.
- Vite còn cảnh báo directive use client của react-router và bundle chính lớn hơn 500 kB. Không tắt hay che các cảnh báo này.
- Không chạy dotnet build/test vì P1 không thay đổi C#; không áp dụng schema hay ghi dữ liệu nghiệp vụ SQL.
- Rà soát source và build Passed không đồng nghĩa với UAT 100% của tất cả popup. Không có tuyên bố toàn bộ hệ thống đã pass kiểm thử.

## Cách kiểm tra nhanh

1. Mở trang có bộ lọc: panel phải đóng; ô tìm kiếm nhanh vẫn dùng được.
2. Bấm Bộ lọc, nhập điều kiện, áp dụng; đóng/mở lại để kiểm tra điều kiện đang áp dụng và phân trang.
3. Bấm tạo/sửa/phân công: form mở trong popup, không chèn xuống dưới bảng. Thử Hủy và Escape; nếu đã nhập dữ liệu phải được hỏi trước khi bỏ.
4. Thử lưu hợp lệ và lưu bị API từ chối: chỉ kết quả xác nhận thành công mới có toast xanh. Lỗi giữ trong workflow để sửa hoặc xác minh lại khi trạng thái chưa rõ.
5. Dùng Tab/Shift+Tab trong popup, mở popup con rồi đóng, kiểm tra cuộn trang trở lại bình thường; kiểm tra ở màn hình điện thoại.

## Danh sách vị trí bộ lọc

- `frontend/src/assets/AssetLocationPage.tsx`
- `frontend/src/assets/AssetMaintenanceHistoryPage.tsx`
- `frontend/src/assets/AssetProfilePage.tsx`
- `frontend/src/audit/AuditPage.tsx`
- `frontend/src/dev2/PreventiveOccurrencesPanel.tsx`
- `frontend/src/dev2/Workspace.tsx`
- `frontend/src/features/inspectionChecklists/ChecklistCollection.tsx`
- `frontend/src/features/inspectionHistory/HistoryWorkspace.tsx`
- `frontend/src/hotel/HotelWorkspace.tsx`
- `frontend/src/insights/DashboardFilters.tsx`
- `frontend/src/insights/ReportsPage.tsx`
- `frontend/src/internalRequests/HistoryPage.tsx`
- `frontend/src/inventory/ReturnQueuePage.tsx`
- `frontend/src/inventory/StockBalancePage.tsx`
- `frontend/src/notifications/NotificationCenter.tsx`
- `frontend/src/operationalTasks/TaskReadPage.tsx`
- `frontend/src/operationalTasks/TaskWorkloadPanel.tsx`
- `frontend/src/roles/RoleCatalogRoute.tsx`
- `frontend/src/screens/housekeeping/CleaningTaskListScreen.tsx`
- `frontend/src/screens/housekeeping/RoomTurnoverHistoryScreen.tsx`
- `frontend/src/screens/rooms/RoomStatusBoardScreen.tsx`
- `frontend/src/users/StandaloneUserAccessReviewPage.tsx`
- `frontend/src/users/UserAccessReviewPage.tsx`
- `frontend/src/users/UserDirectoryPage.tsx`
- `frontend/src/workday/ShiftHandoverPage.tsx`
- `frontend/src/workday/ShiftRosterPanel.tsx`
- `frontend/src/workTime/MyTimeLogPage.tsx`
- `frontend/src/workTime/TimeCorrectionsPage.tsx`

## Danh sách tệp có popup/drawer

- `frontend/src/assets/AssetCrossBranchPage.tsx`: 2 vị trí.
- `frontend/src/assets/AssetCustodyPage.tsx`: 2 vị trí.
- `frontend/src/branch-maintenance/BranchMaintenancePanel.tsx`: 1 vị trí.
- `frontend/src/branch-maintenance/CreateBranchRoute.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentCancellationPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentClassificationPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentEscalationPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentEvidencePanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentReopenPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentRoomBlocksPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/IncidentTriagePanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceAssignmentPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceDeclinePanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceProgressPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceResumePanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceReturnPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceSubmissionPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/MaintenanceVerifyPanel.tsx`: 1 vị trí.
- `frontend/src/dev2/PreventiveOccurrencesPanel.tsx`: 3 vị trí.
- `frontend/src/dev2/PreventiveSchedulesPanel.tsx`: 1 vị trí.
- `frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx`: 1 vị trí.
- `frontend/src/features/inspectionChecklists/ChecklistForm.tsx`: 1 vị trí.
- `frontend/src/features/inspectionChecklists/ChecklistWorkspace.tsx`: 2 vị trí.
- `frontend/src/features/inspectionExecution/InitialInspectionAssignmentPage.tsx`: 1 vị trí.
- `frontend/src/features/inspectionExecution/InspectionReassignmentForm.tsx`: 1 vị trí.
- `frontend/src/features/inspectionExecution/InspectionWorkspace.tsx`: 1 vị trí.
- `frontend/src/features/inspectionExecution/ReinspectionAssignmentPage.tsx`: 1 vị trí.
- `frontend/src/features/inspectionExecution/ReinspectionExecutionPage.tsx`: 1 vị trí.
- `frontend/src/features/inspectionExecution/RequestReworkForm.tsx`: 1 vị trí.
- `frontend/src/features/inspectionHistory/HistoryWorkspace.tsx`: 1 vị trí.
- `frontend/src/hotel/DepartmentDeactivationPanel.tsx`: 1 vị trí.
- `frontend/src/hotel/HotelWorkspace.tsx`: 1 vị trí.
- `frontend/src/hotel/RoomDepartmentsPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/AssignmentPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/CleaningLinksPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/CompletionPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/DecisionPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/EscalationPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/HandoverPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/IncidentLinksPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/MaterialLinksPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/ProgressPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/ReopenPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/RoutePanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/TaskLinksPanel.tsx`: 1 vị trí.
- `frontend/src/internalRequests/WithdrawalPanel.tsx`: 1 vị trí.
- `frontend/src/inventory/AdjustmentPage.tsx`: 1 vị trí.
- `frontend/src/inventory/ItemManagementPage.tsx`: 1 vị trí.
- `frontend/src/inventory/ReceiptPostPanel.tsx`: 1 vị trí.
- `frontend/src/inventory/ReceiptWorkspacePage.tsx`: 1 vị trí.
- `frontend/src/inventory/ReturnPostPage.tsx`: 1 vị trí.
- `frontend/src/inventory/ReturnRequestPage.tsx`: 2 vị trí.
- `frontend/src/inventory/ReversalPage.tsx`: 1 vị trí.
- `frontend/src/inventory/StocktakePage.tsx`: 1 vị trí.
- `frontend/src/inventory/TransferDetailPage.tsx`: 1 vị trí.
- `frontend/src/inventory/TransferWorkspacePage.tsx`: 1 vị trí.
- `frontend/src/inventory/UnitManagementPage.tsx`: 2 vị trí.
- `frontend/src/inventory/WarehouseManagementPage.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/PrioritySlaPage.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/ReferenceCatalogPage.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskAssignmentPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskCreatePanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskCriteriaSetup.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskPlanPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskProgressPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskReopenPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskSeriesPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/TaskVerificationPanel.tsx`: 1 vị trí.
- `frontend/src/operationalTasks/WorkCategoriesPage.tsx`: 1 vị trí.
- `frontend/src/roles/CustomRoleForm.tsx`: 1 vị trí.
- `frontend/src/roles/InitialRolePermissionsPanel.tsx`: 1 vị trí.
- `frontend/src/roles/RolePermissionEditor.tsx`: 1 vị trí.
- `frontend/src/screens/housekeeping/CleaningAssignmentScreen.tsx`: 1 vị trí.
- `frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx`: 4 vị trí.
- `frontend/src/screens/housekeeping/CleaningProgressPanel.tsx`: 1 vị trí.
- `frontend/src/screens/rooms/RoomStatusBoardScreen.tsx`: 1 vị trí.
- `frontend/src/structure-deactivation/StructureDeactivationPanel.tsx`: 1 vị trí.
- `frontend/src/users/ActivateUserPage.tsx`: 1 vị trí.
- `frontend/src/users/AddUserBranchPage.tsx`: 1 vị trí.
- `frontend/src/users/AssignUserRolePage.tsx`: 1 vị trí.
- `frontend/src/users/ChangeDepartmentFlow.tsx`: 1 vị trí.
- `frontend/src/users/CredentialResetRoute.tsx`: 1 vị trí.
- `frontend/src/users/EmployeeBranchRemovalPanel.tsx`: 1 vị trí.
- `frontend/src/users/EmployeeMembershipPage.tsx`: 2 vị trí.
- `frontend/src/users/RevokeUserRoleCandidate.tsx`: 1 vị trí.
- `frontend/src/users/UserDeactivationCandidate.tsx`: 1 vị trí.
- `frontend/src/users/UserDirectoryPage.tsx`: 1 vị trí.
- `frontend/src/workday/ScheduledWorkdayPanel.tsx`: 2 vị trí.
- `frontend/src/workday/ShiftDefinitionsPanel.tsx`: 1 vị trí.
- `frontend/src/workday/ShiftPlanPreviewPanel.tsx`: 1 vị trí.
- `frontend/src/workday/ShiftRosterPanel.tsx`: 1 vị trí.
- `frontend/src/workTime/CorrectionRequestForm.tsx`: 1 vị trí.
- `frontend/src/workTime/TimeCorrectionsPage.tsx`: 1 vị trí.

## Toàn bộ tệp source/test P1

- `frontend/src/assets/AssetCrossBranchPage.tsx`
- `frontend/src/assets/AssetCustodyPage.tsx`
- `frontend/src/assets/AssetLocationPage.tsx`
- `frontend/src/assets/AssetMaintenanceHistoryPage.tsx`
- `frontend/src/assets/AssetProfilePage.tsx`
- `frontend/src/audit/AuditPage.tsx`
- `frontend/src/branch-maintenance/BranchMaintenancePanel.tsx`
- `frontend/src/branch-maintenance/CreateBranchRoute.tsx`
- `frontend/src/components/design-system.tsx`
- `frontend/src/dev2/IncidentCancellationPanel.tsx`
- `frontend/src/dev2/IncidentClassificationPanel.tsx`
- `frontend/src/dev2/IncidentEscalationPanel.tsx`
- `frontend/src/dev2/IncidentEvidencePanel.tsx`
- `frontend/src/dev2/IncidentReopenPanel.tsx`
- `frontend/src/dev2/IncidentRoomBlocksPanel.tsx`
- `frontend/src/dev2/IncidentTriagePanel.tsx`
- `frontend/src/dev2/MaintenanceAssignmentPanel.tsx`
- `frontend/src/dev2/MaintenanceDeclinePanel.tsx`
- `frontend/src/dev2/MaintenanceProgressPanel.tsx`
- `frontend/src/dev2/MaintenanceResumePanel.tsx`
- `frontend/src/dev2/MaintenanceReturnPanel.tsx`
- `frontend/src/dev2/MaintenanceSubmissionPanel.tsx`
- `frontend/src/dev2/MaintenanceVerifyPanel.tsx`
- `frontend/src/dev2/PreventiveOccurrencesPanel.tsx`
- `frontend/src/dev2/PreventiveSchedulesPanel.tsx`
- `frontend/src/dev2/Workspace.tsx`
- `frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx`
- `frontend/src/features/inspectionChecklists/ChecklistCollection.tsx`
- `frontend/src/features/inspectionChecklists/ChecklistForm.tsx`
- `frontend/src/features/inspectionChecklists/ChecklistWorkspace.tsx`
- `frontend/src/features/inspectionExecution/InitialInspectionAssignmentPage.tsx`
- `frontend/src/features/inspectionExecution/InspectionReassignmentForm.tsx`
- `frontend/src/features/inspectionExecution/InspectionWorkspace.tsx`
- `frontend/src/features/inspectionExecution/ReinspectionAssignmentPage.tsx`
- `frontend/src/features/inspectionExecution/ReinspectionExecutionPage.tsx`
- `frontend/src/features/inspectionExecution/RequestReworkForm.tsx`
- `frontend/src/features/inspectionHistory/HistoryWorkspace.tsx`
- `frontend/src/hotel/DepartmentDeactivationPanel.tsx`
- `frontend/src/hotel/HotelWorkspace.tsx`
- `frontend/src/hotel/RoomDepartmentsPanel.tsx`
- `frontend/src/insights/ReportsPage.tsx`
- `frontend/src/internalRequests/AssignmentPanel.tsx`
- `frontend/src/internalRequests/CleaningLinksPanel.tsx`
- `frontend/src/internalRequests/CompletionPanel.tsx`
- `frontend/src/internalRequests/DecisionPanel.tsx`
- `frontend/src/internalRequests/EscalationPanel.tsx`
- `frontend/src/internalRequests/HandoverPanel.tsx`
- `frontend/src/internalRequests/HistoryPage.tsx`
- `frontend/src/internalRequests/IncidentLinksPanel.tsx`
- `frontend/src/internalRequests/MaterialLinksPanel.tsx`
- `frontend/src/internalRequests/ProgressPanel.tsx`
- `frontend/src/internalRequests/ReopenPanel.tsx`
- `frontend/src/internalRequests/RoutePanel.tsx`
- `frontend/src/internalRequests/TaskLinksPanel.tsx`
- `frontend/src/internalRequests/WithdrawalPanel.tsx`
- `frontend/src/inventory/AdjustmentPage.tsx`
- `frontend/src/inventory/ItemManagementPage.tsx`
- `frontend/src/inventory/ReceiptPostPanel.tsx`
- `frontend/src/inventory/ReceiptWorkspacePage.tsx`
- `frontend/src/inventory/ReturnPostPage.tsx`
- `frontend/src/inventory/ReturnQueuePage.tsx`
- `frontend/src/inventory/ReturnRequestPage.tsx`
- `frontend/src/inventory/ReversalPage.tsx`
- `frontend/src/inventory/StockBalancePage.tsx`
- `frontend/src/inventory/StocktakePage.tsx`
- `frontend/src/inventory/TransferDetailPage.tsx`
- `frontend/src/inventory/TransferWorkspacePage.tsx`
- `frontend/src/inventory/UnitManagementPage.tsx`
- `frontend/src/inventory/WarehouseManagementPage.tsx`
- `frontend/src/notifications/NotificationCenter.tsx`
- `frontend/src/operationalTasks/PrioritySlaPage.tsx`
- `frontend/src/operationalTasks/ReferenceCatalogPage.tsx`
- `frontend/src/operationalTasks/TaskAssignmentPanel.tsx`
- `frontend/src/operationalTasks/TaskCreatePanel.tsx`
- `frontend/src/operationalTasks/TaskCriteriaSetup.tsx`
- `frontend/src/operationalTasks/TaskPlanPanel.tsx`
- `frontend/src/operationalTasks/TaskProgressPanel.tsx`
- `frontend/src/operationalTasks/TaskReadPage.tsx`
- `frontend/src/operationalTasks/TaskReopenPanel.tsx`
- `frontend/src/operationalTasks/TaskSeriesPanel.tsx`
- `frontend/src/operationalTasks/TaskVerificationPanel.tsx`
- `frontend/src/operationalTasks/TaskWorkloadPanel.tsx`
- `frontend/src/operationalTasks/WorkCategoriesPage.tsx`
- `frontend/src/roles/CustomRoleForm.tsx`
- `frontend/src/roles/InitialRolePermissionsPanel.tsx`
- `frontend/src/roles/RoleCatalogRoute.tsx`
- `frontend/src/roles/RolePermissionEditor.tsx`
- `frontend/src/screens/housekeeping/CleaningAssignmentScreen.tsx`
- `frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx`
- `frontend/src/screens/housekeeping/CleaningProgressPanel.tsx`
- `frontend/src/screens/housekeeping/CleaningTaskListScreen.tsx`
- `frontend/src/screens/housekeeping/RoomTurnoverHistoryScreen.tsx`
- `frontend/src/screens/rooms/RoomStatusBoardScreen.tsx`
- `frontend/src/structure-deactivation/StructureDeactivationPanel.tsx`
- `frontend/src/users/ActivateUserPage.tsx`
- `frontend/src/users/AddUserBranchPage.tsx`
- `frontend/src/users/AssignUserRolePage.tsx`
- `frontend/src/users/ChangeDepartmentFlow.tsx`
- `frontend/src/users/CredentialResetRoute.tsx`
- `frontend/src/users/EmployeeBranchRemovalPanel.tsx`
- `frontend/src/users/EmployeeMembershipPage.tsx`
- `frontend/src/users/RevokeUserRoleCandidate.tsx`
- `frontend/src/users/StandaloneUserAccessReviewPage.tsx`
- `frontend/src/users/UserAccessReviewPage.tsx`
- `frontend/src/users/UserDirectoryPage.tsx`
- `frontend/src/workday/ScheduledWorkdayPanel.tsx`
- `frontend/src/workday/ShiftDefinitionsPanel.tsx`
- `frontend/src/workday/ShiftHandoverPage.tsx`
- `frontend/src/workday/ShiftPlanPreviewPanel.tsx`
- `frontend/src/workday/ShiftRosterPanel.tsx`
- `frontend/src/workTime/CorrectionRequestForm.tsx`
- `frontend/src/workTime/MyTimeLogPage.tsx`
- `frontend/src/workTime/TimeCorrectionsPage.tsx`
- `frontend/src/components/workspace-interactions.tsx`
- `frontend/src/styles/design-system.css`
- `frontend/src/insights/DashboardFilters.tsx`
- `frontend/tests/ui-p1-browser.test.mjs`
- `frontend/tests/design-system-v2-browser.test.mjs`
- `frontend/tests/users-workspace-browser.test.mjs`
- `frontend/tests/room-workspace-browser.test.mjs`
- `frontend/tests/scheduled-workday-vietnamese.test.mjs`
- `frontend/tests/incident-classification-browser.test.mjs`
- `frontend/src/shared-host/CacaoShell.tsx`
- `frontend/tests/user-deactivation-candidate-browser.test.mjs`
- `frontend/src/users/UserDeactivationCandidate.tsx`

Danh sách chi tiết có số dòng nằm trong `artifacts/ui-p1/audit-final.json`. Các script refactor trong artifacts là công cụ chạy một lần, không chạy lại trên source đã chuyển đổi.
