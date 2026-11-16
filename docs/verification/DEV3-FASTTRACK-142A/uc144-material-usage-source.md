# UC144 independently authorized material usage

Status PARTIAL_IMPLEMENTATION; no completed UC increment.

MaintenanceUsageRead now enriches scoped maintenance detail only when current MR_VIEW and INVENTORY_VIEW authorize the source requests and issuing branch. Existing maintenance session/role/branch/ownership checks remain mandatory. Usage validation binds its work log to the work, original incident to the material request, request line to fulfillment, matching item IDs, base unit, source warehouse, Posted ISSUE document and MATERIAL_FULFILLMENT reference. MaterialReadProvenance checks linked request stock lineage. No quantities or documents are changed.

If any usage is outside scope or has invalid source lineage, the whole usage section returns unavailable; hidden rows are not presented as a complete total. Before disclosure, the contract rereads permission/source state inside its serializable transaction and compares the immutable DTO values. Failure prevents disclosure. Legacy raw MaterialIssueId remains omitted; authorized usage DTOs carry verified document/request references. The frontend renders quantities, units, issue document and a material-request link, with unavailable distinct from an authorized empty result.

Direct verification:
- dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors.
- npm.cmd run build (TypeScript + Vite) PASS; existing React Router directive and >500kB bundle warnings remain.
- MaintenanceReadAccessTests: 5 PASS, 0 failed/skipped. Includes EF SQL translation of the provenance query using a nonconnecting synthetic connection string; this is NOT execution against SQL Server. Existing authority/lineage/read-only admission checks also pass.
- HTTP/SQL/browser acceptance NOT_RUN. No database/schema/grant change.

Human QA: valid grants show usage; missing/revoked MR or inventory grant, OWN requester mismatch, cross-branch fulfillment and mismatched log/item/request/document must hide usage without exposing IDs. Revoke grants before final disclosure; verify failure. Confirm zero usage with valid grants versus unavailable data, and source navigation authorization.

UC144 still requires deadline/overdue contract and implementation, due-date ordering/filtering, actual occurrence lifecycle and runtime acceptance. Pending deadline clarification remains open; no PlannedEndAt-as-SLA assumption.
