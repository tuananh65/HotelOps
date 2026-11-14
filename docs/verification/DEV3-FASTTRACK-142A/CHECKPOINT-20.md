# FASTTRACK-142A — checkpoint 20: edit inspection defects

Sprint ACTIVE. Baseline `a7f10e25ddc922b74514e1f404b235d6b0d4e2c6`.

UC043/GBR061 cho phép sửa defect khi Inspecting hoặc Pending Conclusion. Đã thêm PUT `/api/branches/{branchId}/inspections/{id}/defects/{defectId}` và nút Edit/Save/Cancel trong workspace canonical. Dùng request DTO hiện có, kiểm tra session, INSPECTION_PERFORM, branch/assigned inspector, strong If-Match và transaction/audit hiện có. Chỉ sửa description, severity, location; giữ ID, criterion và status. Evidence hiện có giữ nguyên, file mới nếu gửi được append qua storage policy hiện có. Không có delete/rebind evidence hoặc quyền mới.

Server lọc defect theo cả inspection ID; không cho đổi criterion. Cùng guard trạng thái với ghi defect nên code từ chối ngoài INSPECTING/PENDING_CONCLUSION. Đã kiểm chứng hai trạng thái cho phép; chưa chạy runtime edit trên inspection PASSED/FAILED. Không coi guard source là bằng chứng runtime của case chưa chạy.

Source approval candidate đã đối chiếu: IInspectionApproveAcceptedBinding vẫn Closed; Fe05NotificationKind chỉ Failed/Rework, chưa có Passed binding. Không nhập scaffold rồi tuyên bố approval hoàn thành hoặc gỡ dependency gates. Approval/projection/notification/receipt vẫn là implementation gap; editing là phần FE05 độc lập có business rule rõ.

## Bằng chứng trực tiếp

- Backend build và ModelProbe build PASS, 0 warnings/errors. Typecheck và frontend production build PASS; giữ cảnh báo Vite use-client/bundle >500 kB.
- 16 Node contract tests PASS, gồm PUT URL/method/CSRF/If-Match và invalid defect ID trước network.
- 8 backend focused tests PASS; 11 browser execution regression PASS với mock HTTP.
- `dotnet test HotelOps.slnx`: exit0 chỉ restore, không discovery; không tính là test PASS.
- `checkpoint20-runtime-acknowledged.json`: 25 checks/stages thực trên canonical production bundle, HTTPS với TLS validation mặc định, owned SQL; không mock/intercept. Gồm edit ở INSPECTING, edit ở PENDING_CONCLUSION, stale edit412, unknown defect404, các kiểm tra từ chối/rollback và concurrent Finish [200,412] của checkpoint19.
- SQL cuối trial: một defect, description `Synthetic stain final`, đúng sáu inspection audits (bốn thao tác trước + hai edits), PENDING_CONCLUSION; giữ nguyên evidence identity và bytes. Cleanup=true. API trial đã dừng. Không mutate shared HotelOpsDB.

Các report runtime trước acknowledged được giữ lại, tất cả cleanup=true. UI textarea đã có nội dung không có accessible name ổn định cho selector exact; thêm aria-label Description. Harness trước đó dùng text xuất hiện trên form làm tín hiệu hoàn thành edit, có thể đọc ETag trước response PUT. Đã đợi response PUT200 thật trước snapshot checks; giữ nguyên expected statuses/ETag/evidence/audit assertions. Không che các lần timeout/snapshot/status mismatch. Diagnostic screen chỉ từ fixture synthetic ở bước Edit, không chứa credentials.

SQL governance kế thừa harness đã kiểm chứng: exact instance/database/GUID/marker, empty fixture baseline, rehearsal rollback trước setup, cleanup transaction theo IDs sở hữu và storage keys trong private root riêng. Không sửa schema/entities/roles. Runtime vẫn có cảnh báo index RoomTypeBranchV1 và Skip/Take thiếu OrderBy. npm ci không chạy lại vì dependencies/lockfile không đổi.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=20_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=a7f10e25ddc922b74514e1f404b235d6b0d4e2c6
SOURCE_CONSOLIDATION_STATUS=CANONICAL_PRESERVED;APPROVE_CANDIDATE_REVIEWED_NOT_DUPLICATED
BACKEND_BUILD=PASS_0_WARNINGS_0_ERRORS
FRONTEND_NPM_CI=NOT_RERUN
FRONTEND_TYPECHECK=PASS
FRONTEND_BUILD=PASS_WITH_EXISTING_WARNINGS
AUTH_ROUTER_TESTS=16_CONTRACT_PASS;11_MOCK_HTTP_BROWSER_PASS;25_REAL_HTTPS_SQL_CHECKS_STAGES_PASS
IMPLEMENTED_FUNCTIONALITY=UC043_SCOPED_DEFECT_EDIT_API_AND_CANONICAL_UI_WITH_ETAG_AUDIT_EVIDENCE_PRESERVATION
CHANGED_FILES=src/HotelOps.Api/{Controllers/HousekeepingWorkflowController.cs,Services/Housekeeping/InspectionExecutionService.cs};frontend/src/features/inspectionExecution/{api.ts,InspectionWorkspace.tsx};frontend/tests/fe05-inspection-execution.test.mjs;frontend/scripts/sprintInspectionBrowser.mjs;scripts/verification/CanonicalDatabase/ModelProbe/SprintInspectionBrowserProbe.cs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-20.md,UC_STATUS_MATRIX.md,checkpoint20-*}
SQL_HTTPS_STATUS=REAL_CANONICAL_HTTPS_SQL_PASS;CLEANUP_TRUE;API_STOPPED
UC_REMAINING_GAPS=144_PARTIAL;FE05_APPROVAL_REJECTION_REINSPECTION_COMPOSITION;UC006_UC010_RESPONSIBILITIES;FE11_FE13_LIFECYCLES
NEXT_THREE_CODING_TASKS=Passed notification participant and recipient binding; room projection/receipt approval composition; rejection/reinspection lifecycle integration

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS reported after delivery to avoid self-reference.
