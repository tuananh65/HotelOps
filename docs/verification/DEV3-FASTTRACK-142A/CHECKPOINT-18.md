# FASTTRACK-142A — checkpoint 18: real canonical inspection execution

Sprint ACTIVE. Baseline `134d28da6681ae96b1e4a7450ac165b7e5ced36f`.

Đã chạy production frontend canonical qua HTTPS và SQL thật, không mock/intercept HTTP, không bỏ qua TLS validation. Trial phát hiện lỗi source khi ghi attachment: SQL334 vì EF phát `OUTPUT INSERTED` trực tiếp trên bảng attachments có trigger TR_hk_attachment_revision_v1. Đã thêm `UseSqlOutputClause(false)` cho Attachment trong DbContext partial hiện có, cùng cơ chế các bảng inspection đã sử dụng. Không sửa generated entity, schema hoặc trigger. Thay đổi áp dụng cho mọi writer dùng EF Attachment; mới kiểm chứng runtime inspection, chưa kiểm chứng tất cả module khác.

## Kết quả trực tiếp

- `checkpoint18-browser-fixed.json`: success=true, cleanup=true. Tám bước browser: anonymous deep link/login, admission canonical, Start, Save FAIL, defect với catalog server và PNG synthetic, Finish, reload, logout.
- SQL sau browser xác nhận PENDING_CONCLUSION, một FAIL result, một defect, đúng bốn audit room_inspections. File private chứa đúng bytes synthetic đã gửi. Đây không phải kiểm chứng download hoặc định dạng PNG đầy đủ; payload chỉ đáp ứng signature mà policy hiện tại kiểm tra.
- Backend build và ModelProbe build PASS, 0 warnings/errors. Focused regression 8 PASS. `dotnet test HotelOps.slnx` exit0 chỉ restore, không discovery; không tính là test PASS.
- Bundle production đã build ở checkpoint17 được dùng trực tiếp; frontend source, dependencies, lockfile không đổi. Không chạy lại npm ci/typecheck/build trong checkpoint18.
- Runtime còn các cảnh báo RoomTypeBranchV1 indexes không map và Skip/Take thiếu OrderBy. Không che cảnh báo. API đã dừng sau thử nghiệm.

## Các lần thất bại được giữ lại

1. `browser-first.json`: login/admission thành công, selector state của harness sai. Cleanup=false vì bỏ sót audit authentication actor=NULL, subject là user synthetic. Đã sửa cleanup theo subject ID; recovery transaction chỉ xóa audit_id40213/entity users/360119 trên target đã kiểm tra identity và baseline. `checkpoint18-cleanup-recovery.log.txt` xác nhận còn 0 audit.
2. `browser-second.json`: Start thành công, harness chờ option visible thay vì attached. Cleanup=true.
3. `browser-third.json`: Save FAIL thành công; upload gặp lỗi SQL334 nêu trên. Cleanup=true, không để file mồ côi.
4. `browser-fixed.json`: toàn bộ flow PASS sau sửa EF mapping. Không thay điều kiện thành công để che lỗi source.

## SQL/HTTPS governance và giới hạn

Guard fixture xác minh server meita, database HotelOps_DEV3_142A_20261114_cc78c13f, GUID 1D5A9B53-7D19-435A-96AB-9FDFF447B6E0, marker cc78c13ffc124efcad67b3d804baa84c và baseline synthetic rỗng. Rehearsal fixture + cleanup chạy trong transaction rollback trước setup commit. Browser cần committed fixture nên cleanup sau trial là transaction bù theo ID sở hữu; không tuyên bố HTTP commits được outer rollback. File chỉ xóa theo GUID storage key đã kiểm tra trong thư mục TestResults/sprint18-evidence riêng.

`checkpoint18-cleanup-final.log.txt` xác nhận users/branches/inspections/attachments/audits/sessions đều 0 và attachment trigger enabled. Không tắt trigger hoặc mutate shared HotelOpsDB. Fixture dùng checklist/room type legacy hiện có; chưa chứng minh mọi scoped schema variant. SQL connection dùng TrustServerCertificate cho local SQL, browser HTTPS dùng TLS validation mặc định.

UC042/043 vẫn PARTIAL: happy path execution đã xác minh thật, chưa đủ mọi negative authorization/concurrency case, editing defects, evidence retrieval, approval/rejection/reinspection hay room Ready projection.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=18_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=134d28da6681ae96b1e4a7450ac165b7e5ced36f
SOURCE_CONSOLIDATION_STATUS=CANONICAL_PRESERVED;NO_DUPLICATE_MERGE
BACKEND_BUILD=PASS_0_WARNINGS_0_ERRORS
FRONTEND_NPM_CI=NOT_RERUN
FRONTEND_TYPECHECK=NOT_RERUN;CHECKPOINT17_PASS_HISTORICAL
FRONTEND_BUILD=CHECKPOINT17_PRODUCTION_BUNDLE_USED_IN_REAL_BROWSER
AUTH_ROUTER_TESTS=8_REAL_BROWSER_HTTPS_SQL_STAGES_PASS;8_BACKEND_FOCUSED_PASS
IMPLEMENTED_FUNCTIONALITY=TRIGGER_COMPATIBLE_ATTACHMENT_WRITES;REAL_CANONICAL_INSPECTION_BROWSER_SQL_HARNESS
CHANGED_FILES=src/HotelOps.Api/Data/HotelOpsDbContext.Checklists.cs;scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintInspectionBrowserProbe.cs};frontend/scripts/sprintInspectionBrowser.mjs;docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-18.md,UC_STATUS_MATRIX.md,checkpoint18-*}
SQL_HTTPS_STATUS=REAL_OWNED_SQL_AND_DEFAULT_TLS_BROWSER_PASS;CLEANUP_TRUE;API_STOPPED
UC_REMAINING_GAPS=144_PARTIAL;FE05_APPROVAL_REJECTION_REINSPECTION;UC006_UC010_RESPONSIBILITIES;FE11_FE13_LIFECYCLES;REMAINING_ACCEPTANCE
NEXT_THREE_CODING_TASKS=Inspection negative authority/concurrency HTTPS coverage; accepted FE05 projection/notification/receipt composition; confirmed FE11_FE13 workflows

FINAL_LOCAL_SHA / FINAL_REMOTE_SHA / COMMIT_SHAS / PUSH_STATUS reported after delivery to avoid self-reference.
