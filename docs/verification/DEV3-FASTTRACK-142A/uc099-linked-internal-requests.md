# UC099 linked internal requests

2026-11-16. Source increment, not COMPLETE_VERIFIED.

Material detail now exposes the official internal_request_material_links relation through a minimal DTO (request ID/code/state and mandatory flag). It does not infer relationships from matching items or requester identity. It retains the existing MR_VIEW detail authority and separately evaluates current IR_VIEW using InternalRequestReadAccess. Same-branch scope is applied before projection; no hidden request counts or identifiers are returned. Every disclosed Internal Request is rechecked under current IR_VIEW before final disclosure in the existing Serializable read transaction, covering expiring grants and current responsibility. Navigation uses the existing /internal-requests?requestId route, whose endpoint checks authority again.

The existing completion installer capability check guards access to the link table. Missing capability returns LinkedWorkAvailable=false; a valid capability with no readable links returns a scoped empty list. Neither result proves all mandatory work is complete. No new permission, grant, schema, generated entity or database mutation. This does not grant IR_PROCESS or change UC120 completion authority.

Changed files: MaterialRequestContracts.cs; MaterialReadContract.cs; new MaterialLinkedWorkRead.cs; MaterialHistoryView.tsx; UC_STATUS_MATRIX.md; this receipt.

Direct validation: backend build --no-restore PASS, zero warnings/errors; frontend production build including TypeScript PASS. Existing React Router directives and 944.94 kB bundle warning persist. Existing dev2-ui tests: 4 passed, 1 skipped, module.register deprecation warning. They cover form/auth rendering, not this linked-work query. dotnet test solution exited zero with no tests executed. New query translation, SQL/API/browser behavior and revoked/cross-branch acceptance NOT_RUN. UC099 remains PARTIAL_IMPLEMENTATION pending lifecycle/runtime verification; no newly COMPLETE_VERIFIED UC.
