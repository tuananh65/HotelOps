# UC016 dependency preview — IMPLEMENTED_SOURCE_ONLY

Baseline: 6b22608dee01f49e7f37f0dad0ee99c0c893448e. This operation adds a read-only dependency preview; it does not complete deactivation or certify writer coordination.

## Source contract proposed for review

GET /api/branches/{branchId}/floors/{id}/deactivation-dependencies
GET /api/branches/{branchId}/areas/{id}/deactivation-dependencies

Positive Int64 route IDs; cookie session; HTTPS; no-store. Uses the same initial/final SQL session checks and effective global-or-branch MASTER_DATA_MANAGE gate as existing structure reads. The controller introduces no permission policy. Existing serializable transaction and actor/branch/target locking are reused. ETag is the current existing UC015 structure tag, not a preview reservation.

200 body: { record: EditableStructureDto, dependencies: StructureDependencyDto[], writerCoordinationRequired: true }. Each dependency has kind, Int64 count and up to 100 ordered sample IDs from the existing reader. Frontend strips sample IDs and displays only validated known kinds/counts. 400/401/403/404/409/503 preserve existing controller behavior. A denied or unavailable final gate returns no dependency data. SQL failures beyond existing mappings retain framework error handling.

The snapshot may become stale immediately after commit. Empty dependencies never authorize mutation. The POST branch remains byte-identical and fail-closed. No new state transition, audit event or mutation is performed by this read; underlying session middleware can still touch sessions. These GETs must not be tried against shared runtime without approval.

## Frontend

The standalone panel uses the preview GET in place of the ordinary record GET. Abort/session/branch protections and Review -> Confirm remain. Dispatch defaults OFF in panel and existing mutation client. Panel remains unmounted in production router. This contract is proposed in the PR for source review, not declared approved or deployed.

## Executed receipts

- dotnet build HotelOps.slnx: exit 0, 0 errors, 0 warnings.
- dotnet test HotelOps.slnx: exit 0; output reports restore only, no test cases discovered/reported. This is not a test-count PASS receipt.
- dotnet run --project scripts/verification/StructureDeactivationPreview/StructureDeactivationPreview.Verification.csproj: 27 focused checks PASS, exit 0. Adapter/reflection/DTO checks plus explicitly labeled source assertions; no SQL service integration.
- node --test tests/deactivation-preview-api.test.mjs tests/structure-deactivation-api.test.mjs tests/structure-deactivation-browser.test.mjs: 48/48 PASS, exit 0 (14 preview API, 22 existing mutation/read API, 12 offline browser). An added network-vs-malformed test exposed misclassification; catch mapping fixed and final full targeted run passed.
- npm.cmd run typecheck: exit 0.
- npm.cmd run build: exit 0; two inherited React Router use-client warnings.
- Scoped Prettier: exit 0.

## Remaining gates and owners

DEV3 owns deactivation orchestration/final checks/audit/rollback. It remains unimplemented while writer coordination is closed. DEV2 owns Asset/Incident placement guards: published/reviewed candidate and approved integration/SQL race evidence are required, not merely the presence of an old remote branch. OperationalTask COMPLETED responsibility semantics remain unresolved under Task66. No cross-owner writer changes are made here.

Isolated SQL race/rollback acceptance NOT_RUN: no disposable trial lease provided. Live HTTPS NOT_RUN. API runtime activation and deployment are not authorized by this PR. FE01/UC012/UC013/FE12, shared Program.cs/auth policies/schema and all DEV1/DEV2 writers are unchanged.

Status: preview SOURCE_READY for independent review; preview runtime acceptance pending; deactivation READY_FOR_API_INTEGRATION=false; READY_FOR_BUSINESS_ACTIVATION=false; COMPLETE_VERIFIED=false.
