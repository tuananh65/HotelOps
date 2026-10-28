# Task24 — User Management Mutation Readiness

Ngày review:2026-10-27 (Asia/Saigon). DEV1; chỉ technical analysis, chưa triển khai mutation APIs.

**Verdict: BLOCKED** cho triển khai toàn bộ nhóm mutations. Readiness audit hoàn tất, có phương án tối thiểu và các gate phê duyệt. UC011 vẫn BLOCKED theo Task23; không mở lại hoặc chọn permission thay developer.

## Baseline / nguồn

Branch feature/BE1-database-integration,HEAD20c1063;4 tracked modifications Task22 và toàn bộ untracked Task20A/21/22/23/local artifacts giữ nguyên. Index trống, không commit/push/merge hoặc xóa file.

Đã đọc AGENTS.md; Task16 coverage/backlog; Task21 contracts/decisions; Task22 implementation/verification; Task23 authorization decision; SRS UC004–007/010 và GBR005/007/008/012/023–031; Authentication/Authorization services, SQL release/schema, EF mappings và canonical seed manifest.

Bằng chứng source chính:
- src/HotelOps.Api/Services/Authentication/TemporaryCredentialService.cs và ITemporaryCredentialService.cs.
- PasswordCredentialService.cs, SessionRevocationService.cs, AuthenticationCredentialWorkflow.cs, SqlSessionService.cs, AuthenticationAudit.cs.
- Security/Authorization/PermissionService.cs và CurrentUserAccessor.cs; Middleware/ApiCsrfMiddleware.cs.
- Data/HotelOpsDbContext.cs; Entities/User.cs, UserRole.cs, UserBranchAccess.cs, RolePermission.cs, AuditLog.cs.
- scripts/database/release/01_HotelOps_Full_Schema.sql;03_HotelOps_RBAC_Catalog_Seed.sql; verification/CanonicalDatabase/CANONICAL_SEED_MANIFEST.md.

## Hồ sơ

- [Transaction/concurrency và proposal](TRANSACTION_AND_CONCURRENCY.md)
- [Identity/credential provisioning](IDENTITY_PROVISIONING.md)
- [Role/scope model](ROLE_SCOPE_ASSIGNMENT.md)
- [Session/security/audit](SESSION_AND_SECURITY.md)
- [Implementation sequence và validation](IMPLEMENTATION_SEQUENCE.md)
- [Decision register](DECISIONS_REQUIRED.md)

Không thêm harness vì source đã đủ chứng minh incompatibility transaction và model gaps; fake harness không chứng minh SQL rollback/races chưa có workflow. Không truy cập hoặc ghi HotelOpsDB; không thay credential/session infrastructure, schema hoặc canonical seed.
