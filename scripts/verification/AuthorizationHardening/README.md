# Task08 — Security hardening before commit

Ngày: 22/10/2026 (Asia/Saigon). Branch: feature/BE1-authorization.

**PASS WITH NOTES cho phần hardening đã kiểm chứng; SQL least privilege chưa được áp dụng, live SELECT regression còn BLOCKED.** Không thay SQL Login/User/Permission hoặc dữ liệu HotelOpsDB.

## Kết quả

- Health public giữ GET /api/health/database và HTTP 200/503, chỉ trả status ok/error. Không trả database name, provider message hoặc log raw exception. Chưa thấy consumer trong repository dùng các field cũ; external monitor cần được kiểm tra khi rollout.
- Policy provider giữ contract/policy semantics, truyền RequestAborted và budget 5 giây tới catalog. Tests chứng minh abort và timeout thực tế không cấp quyền.
- 4 live read runners dùng cấu hình riêng, không fallback main connection; preflight quyền hiệu dụng và SELECT-only interceptors/SaveChanges guard. Raw ADO queries hiện có cũng gọi SELECT guard. Credential quyền rộng bị từ chối thật, không có override flag.
- Integration setup độc lập, không import Task03A/03B hoặc đọc deployment-state/main configuration. Full Schema 01 + Seed 03 giữ nguyên; không dùng Upgrade. Server từ config local riêng; receipt/provenance vẫn bắt buộc khi DB có sẵn. Metadata timestamp trong C# guard được parse/format chặt trước khi vào SQL literal.
- HTTP + SQL real session integration đã **chạy lại 102/102** trên database test hiện có. Cleanup cuối có 0 session chưa revoke. Không reset database hoặc tạo principal mới.

## Validation

Build HotelOps.slnx Passed, 0 warning/error. CurrentUserAccessor 14; DataScope model-only 7; PermissionService doubles 43; Authorization loopback 61; ResourceAuthorization loopback 49; hardening 21 checks; HTTPS + real SQL 102. Các mode khác nhau không được gộp thành live SQL coverage. Full DataScope15/Permission50/Authorization65/Resource59 không được tuyên bố Passed trong Task08 vì 4 live runners bị guard chặn thiếu principal read-only.

dotnet test solution exit 0 nhưng chưa discover test project. NU1900 lúc restore project mới trong sandbox đã được retry restore/audit ngoài sandbox thành công, không tắt auditing. Lỗi compile overload AddUserSecrets và warning ASP0000 ban đầu của test infrastructure đã sửa, validation cuối phải xem receipt hiện tại.

## File changes

Application: HealthController.cs và Security/Authorization/PermissionPolicyProvider.cs. Không đổi auth contracts, generated entities, DbContext, Program DI hoặc RBAC matrix trong Task08; các thay đổi từ Tasks01–07 vẫn giữ.

Verification: Shared/ReadOnlyVerification.cs được link vào DataScope, PermissionService, Authorization, ResourceAuthorization (Program.cs + csproj); thêm DataScope --model-only để tách 7 checks offline, không xóa SQL assertions. HttpSqlIntegration: Integration-Environment.ps1, Prepare-Database.ps1, Prepare-Secrets.ps1, Verify-Preservation.ps1, TestDatabase.cs, Fixtures.cs, Program.cs và README. AuthorizationHardening: source/project, Run-Validation.ps1, provisioning draft review-only và 4 tài liệu. Handoff và 4 verification README được cập nhật thông tin mới; receipts/log local được refresh bởi validation.

Recommended commit: source hardening/Shared/project links, portable setup, verification source, tài liệu và provisioning draft có nhãn REVIEW ONLY. Không stage local receipt/fixture IDs/evidence/logs/bin/obj/User Secrets/WAV hoặc deployment artifacts. Không stage cả scripts/verification một lần. Không Commit/Push/Merge/PR.

HttpSqlIntegration/.gitignore được thêm để ngăn stage nhầm các receipt/fixture/evidence JSON và run.lock; file local được giữ nguyên, không xóa hoặc đưa vào Git index.

## Tài liệu

- [SECURITY_FINDINGS.md](SECURITY_FINDINGS.md): trạng thái từng finding và rủi ro còn mở.
- [VERIFICATION_RESULTS.md](VERIFICATION_RESULTS.md): kết quả thực chạy/phần blocked.
- [TEST_ENVIRONMENT_SETUP.md](TEST_ENVIRONMENT_SETUP.md): prerequisites, least privilege và clean checkout.
- [PROVISION_READ_ONLY_REVIEW_ONLY.sql](PROVISION_READ_ONLY_REVIEW_ONLY.sql): draft cho DBA review, **chưa chạy và không được runner tự chạy**.
