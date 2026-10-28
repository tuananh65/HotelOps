# Task 43 - existing writer coverage

Task44 update: technically approved `UserRoleAssignmentService.AssignAsync` now implements
POST /api/users/{id}/roles for five exact operational envelopes, first role only, existing
primary Branch/Department/membership, no self/protected/global/regrant/revoke. It uses the
global-first protocol and nonzero-baseline poststate check. The fresh branch-bound INSERT
and aggregate version/audit have **NO ADMIN-DECREASING EFFECT**; authority is still required.
See ../UserRoleAssignment/VERIFICATION_RESULTS.md for actual HTTP/SQL races and limits.
References below to absent role-management writers concern reducing/rebind/catalog workflows;
they do not exclude this new bounded additive INSERT. Existing external/expiry limits remain.

Current source audit, DEV1 / FE-01, 2026-10-28 Asia/Saigon. Supersedes the historical
Task 41/42 matrix. Source paths below are under src/HotelOps.Api unless noted.
No reachability conclusion is based only on the absence of a public endpoint.

| Writer / exact method | Reachability and decreasing effect | Classification / current protection |
|---|---|---|
| Services/Users/AdminCredentialResetWorkflow.ResetAsync -> TemporaryCredentialService.ResetWithGateAsync | Existing POST /api/users/{id}/reset-credential. Regular -> temporary removes durable eligibility and revokes sessions. | REQUIRES GUARD: protected since Task 42; actor/session/proof, authority and If-Match rechecked under retained locks. |
| Services/Authentication/TemporaryCredentialService.ResetAsync | Public IConditionalCredentialResetService registered by DI; current direct callers are verification harnesses. Internal callable contract is real, not assumed unreachable. Null gate previously bypassed preservation. | REQUIRES GUARD: Task 43 acquires guard for null and nonnull gates. Same result type; LastAdministrator/SerializationUnavailable are explicit outcomes. Trusted caller still owns authorization/reauthentication/identity verification; raw API is not an HTTP substitute. |
| Services/Authentication/TemporaryCredentialService.ApplyAsync (AdminReset) | Public ITemporaryCredentialService in DI; no production HTTP call site found, but callable by internal orchestration and tests. Can reset a regular admin. | REQUIRES GUARD: Task 43 guards its own transaction and throws existing invariant exception subtype on last-admin loss. Signature, one-shot preparation, hash, expiry, sessions/audit semantics preserved. No actor authorization invented for this primitive. |
| Services/Authentication/TemporaryCredentialService.ApplyAsync (InitialProvisioning), StageInitialAsync | Public primitive/transaction participant; Create uses StageInitialAsync. Core refuses any existing hash/temporary marker, so cannot overwrite a durable admin credential. | NO ADMIN-DECREASING EFFECT for credential write alone. Create's identifier insertion is separately guarded. Participant must not acquire a global lock after caller locks; remains caller-owned. |
| Services/Users/UserAccountCreationService.CreateAsync | Existing POST /api/users. Initial roles branch-bound/nonadmin, but insertion can collide across Username/Email columns and remove another user's last usable identifier. | REQUIRES GUARD: Task 43 global-first snapshot before authority/master locks, final poststate plus both global permission checks before commit; 409 on last-admin loss. |
| Services/Users/UserProfileService.UpdateAsync | Existing PATCH /api/users/{id}; email/fullName/phone. Email can remove target or another user's only unambiguous identifier. Username is not patchable. | REQUIRES GUARD: Task 43 global-first snapshot, retained existing authority ranges, If-Match, poststate and existing ALL/BRANCH/OWN permission recheck; 409 on last-admin loss. |
| Services/Authentication/AuthenticationLoginService.LoginAsync | Existing AuthController login, internal interface. Supported credential rehash and LastLoginAt; does not change identifiers/grants/temporary marker to ineligible form. | NO ADMIN-DECREASING EFFECT under existing supported hash contract. No login policy changed. |
| Services/Authentication/CredentialLockoutService.VerifyAsync; AdminReauthenticationService.ReauthenticateAsync | Login/reauth HTTP and internal verification. Five-failure finite LockedUntil/counters. | NO ADMIN-DECREASING EFFECT on approved durable count; finite lockout still affects immediate login. No permanent sentinel invented. |
| Services/Authentication/AuthenticationCredentialWorkflow.ChangePasswordAsync | Existing password-change HTTP and service; verified password rotation uses supported hash, clears temporary marker, creates normal session. | NO ADMIN-DECREASING EFFECT on durable structural eligibility; temporary -> regular is additive. No assumption that a future password change will rescue a reset. |
| Services/Users/UserAccountActivationService.ActivateAsync | PATCH /api/users/{id}/activate; only INACTIVE -> ACTIVE. | NO ADMIN-DECREASING EFFECT. Opposite transition not implemented. |
| Services/Users/UserBranchAssignmentService.AddAsync | Existing add-only membership HTTP/service; no existing role rebind, removal or credential/status edit. Blocks dormant role activation and unsafe OM expansion. | NO ADMIN-DECREASING EFFECT on existing unbound ALL capability. Inverse remove/transfer not inferred safe. |
| SessionRevocationService, SqlSessionService logout/touch/revoke, proof expiry/restart | Existing Auth HTTP and internal stores; removes current sessions or proof only. | NO ADMIN-DECREASING EFFECT on regular credential re-login eligibility. Process-local proof limits unchanged. |
| Account deactivate/delete, employment status mutation | No current DEV1 production service/controller write found beyond initial ACTIVE and activation. EmploymentStatus is not an Auth exclusion today. | NOT REACHABLE in current application mutation source; future decreasing behavior BLOCKED on contract/open-work/last-admin evidence. No endpoint added. |
| UserRole revoke/rebind/IsActive/expiry; Role IsActive, RolePermission/Permission catalog mutations | No current production API/service writers found apart from Create's new branch-bound assignment. Verification fixtures and external SQL/release scripts can change these tables. | NOT REACHABLE as current application management workflow; external/deployment writers BLOCKED on operational serialization/authority contract. Do not treat SQL access as protected. |
| Scheduled grant expiry / external SQL/jobs/imports | Time changes eligibility without executing a mutation; deployed external writer inventory incomplete. | BLOCKED / NOT VERIFIED. FUTURE EXPIRY NOT GUARANTEED. No scheduler or production schema added. |

Method: inspected controllers, DI registrations and all service write/call sites with rg for
ApplyAsync/ResetAsync/StageInitialAsync, ExecuteUpdateAsync, entity additions, PasswordHash,
Username/Email, UserRole and RolePermission. Inspected actual User/UserRole/RolePermission/
UserSession/Audit mappings and generated entities before edits. Direct fixture SQL is evidence,
not production endpoint coverage. See INTEGRATION_VERIFICATION.md for executed methods.
