# Task86 — UC012 implementation

Trạng thái UC012 **PARTIAL**; Create/Update production source đã có, Deactivate mutation bị chặn bởi coordination. Không cộng điểm. Receipt/kết quả cuối cùng xem TASK86_VALIDATION.json và acceptance matrix.

## Production files

Mới:
- `src/HotelOps.Api/Controllers/BranchAdministrationController.cs` — HTTP/HTTPS, authenticated Cookie ticket, If-Match, ProblemDetails/ETag; không business logic.
- `src/HotelOps.Api/Services/HotelStructure/BranchAdministrationService.cs` — CreateAsync, ReadAsync, UpdateAsync, DependenciesAsync, DeactivateAsync; transaction/gates/audit.
- `src/HotelOps.Api/Services/HotelStructure/BranchAdministrationSchema.cs` — kiểm actual rowversion, CI collation, enabled unfiltered unique branch-code index; thiếu contract fail closed.
- `src/HotelOps.Api/Services/HotelStructure/BranchDeactivationDependencyReader.cs` — read-only branch facts và unresolved scope; không viết DEV2 records.

Shared duy nhất: Program.cs thêm đúng một AddScoped BranchAdministrationService theo Program.di.patch, được user duyệt A. Không sửa EF model/entities, Authentication/Authorization, RBAC hoặc DEV2 source. Existing DTO dùng lại nguyên file. Rowversion B được user duyệt thiết kế và sau đó cấp quyền SQL riêng, chỉ trên trial mới.

## Routes

| Method | Route | Contract |
|---|---|---|
| POST | /api/branches | Code/name/address/contact; 201 + DTO/ETag; duplicate 409 |
| GET | /api/branches/{id}/edit | DTO/SQL version, 404 nếu thiếu |
| PUT | /api/branches/{id} | Name/address/phone/email + reason; required strong If-Match; stale 412; missing 428; invalid 400 |
| GET | /api/branches/{id}/deactivation-dependencies | Partial dependency facts, luôn CanDeactivate=false trước joint contract |
| POST | /api/branches/{id}/deactivate | Confirmation/reason + If-Match; existing inactive no-op; active có known blockers 409, nếu không vẫn 503 DEACTIVATION_CONCURRENCY_BLOCKED |

Mọi route dùng global MASTER_DATA_MANAGE qua PermissionService/SQL-backed data source, server CurrentUserAccessor + session validation. Không tin actor/role/branch/status client. Middleware CSRF hiện hành bảo vệ mutations; controller không bypass middleware.

## Create/Update và transaction

- Trim/validate, code canonical upper, SQL identity. Name/address bắt buộc; phone/email optional theo DTO/legacy validation; timezone Asia/Ho_Chi_Minh, Active=true từ server.
- Serializable caller-owned transaction; sorted User eligibility lock, SQL session revalidation, effective permission, schema guard, branch update locks; không cache quyền qua lock wait. Check lại session/permission trước commit.
- Feature create applock trong chính transaction, không dùng/chỉnh shared RoomTypes applock. Duplicate normalized query + unique SQL index; không dùng memory lock.
- Update đọc actual SQL rowversion bằng unmapped projection, so bytes rồi conditional SQL UPDATE id+version; stale không ghi; no-op không UpdatedAt/audit/version churn.
- Timestamps từ SYSUTCDATETIME. Audit before/after/changed fields, actor và branch authoritative; audit SaveChanges trong cùng tx. Detach audit trước final session check vì SqlSessionService yêu cầu clean tracker; không mở transaction riêng.
- Non-2xx/exception không commit. SQL deadlock trả lỗi riêng 503, không auto-retry thành PASS.

## SQL riêng được duyệt

Target/instance và exact hashes: SQL_APPROVAL.json. Prepare dùng source scripts hiện hữu, không sửa chúng; kiểm new-name/nonexistence, schema/data compatibility, rollback transactional DDL, forward idempotency. B bổ sung một cột SQL rowversion; generated EF bỏ qua cột tự sinh, service dùng parameterized SQL projection/update. Không tạo EF migration.

Trial-only CHECK constraint trên audit_logs từ chối marker T86_FAIL_CREATE/T86_FAIL_UPDATE để xác minh audit failure rollback; không thêm audit trigger gây thay đổi OUTPUT semantics chung. Constraint do harness tạo được gỡ trong finally; trial database giữ lại. Không migrate shared/protected DB.

## Giới hạn chưa hoàn thành

Không có Active→Inactive update/audit. Chưa joint-approved terminal-state binding/scope PM, chưa coordination toàn bộ business writers. Case successful deactivate, dependency races và inactive-branch consumer behavior không được tính PASS. Chi tiết exact DEV2 handoff ở TASK86_BRANCH_DEACTIVATION_MATRIX.md.

Security/component regression dùng test doubles hoặc SQL translation phải phân biệt với real HTTPS/SQL. Framework solution không có test cases. Build có NU1900 do NuGet vulnerability feed không truy cập được; không bỏ qua cảnh báo này.

## Kết quả cuối

Build production hiện tại PASS (0 errors, 1 NU1900 với --no-restore; initial restore build 2 NU1900). DI/boundary/translation 39 PASS; security 199 PASS; component UC037/038/142 tương ứng 14/26/19 PASS. Framework 0 cases, không ghi PASS.

HTTPS/SQL run đầu: 83 PASS/1 FAIL do sai actor kỳ vọng UC038; run bổ sung: 37 PASS/1 lỗi runner @@SPID cast; run cuối: 51 PASS/0 FAIL, đã đóng positive/negative regression, invalid sessions và actual SQL actor-lock wait revocation. Hai receipt lỗi giữ nguyên. SQL oracle cuối 13 PASS/0 FAIL xác minh persisted rowversion/audit và counts catalog 12/62/140.

Create/Update đã có HTTPS/SQL evidence cho duplicate/concurrency/stale/no-op/validation, SQL audit failure rollback và auth revocation. Deactivation mutation chưa triển khai/accept; stock/task/incident/request independent fixture matrix và deactivation races NOT RUN. Không gọi toàn UC012 COMPLETE.

Postflight xác minh 1094/1095 baseline files giữ cả SHA256/mtime; một Program thay đổi đúng receipt A; HEAD/index/branch và bốn worktree gốc giữ nguyên. Cuối phiên quan sát **worktree thứ năm mới `D:/SEP490/Projects/HotelOps-Codex-B`, `ai/codex-b`, HEAD cùng 4a18732**; Task86 không tạo/sửa worktrees. Full membership không còn identical; POSTFLIGHT_VERIFIED.json báo riêng original preservation và external addition. Receipt POSTFLIGHT_FINAL.json là attempt chẩn đoán với parse UTC sai, đã được thay bằng verified receipt, không dùng danh sách mtime mismatch của attempt đó làm kết luận source thay đổi.
