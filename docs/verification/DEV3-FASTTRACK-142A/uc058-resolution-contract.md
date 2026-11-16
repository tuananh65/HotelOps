# UC058 result validation increment — 2026-11-16

Status: PARTIAL_IMPLEMENTATION. Newly COMPLETE_VERIFIED UCs: 0.

GBR-077 and UC058 require diagnosis/root cause and action taken. The previous service accepted only an unstructured ResolutionNote. SubmitMaintenanceResultRequest now requires both explicit fields; an optional additional note cannot substitute for either. Whitespace-only values are rejected before execution. Existing resolution_note storage (nvarchar(max) in the release schema and unbounded string in EF mappings) receives labeled normalized content, preserving historical notes without parsing or rewriting them. The transactional maintenance audit additionally records submitted_diagnosis and submitted_action_taken explicitly. Actual end continues to come from the closed work interval's server timestamp, not client input.

Submit now requires the native Maintenance execution ETag after source locks, before interval closure. Its result uses the scoped read query and updated ETag without disclosing material usage. The existing transaction still owns interval, state and audit writes. No generated EF entity, schema, status, grant or notification policy was changed.

The frontend operation catalog matches the new required fields. A004A-030 is NOT added to the production contract registry or canonical host enabled actions. This is not an operational Submit release: material/evidence validation and verifier notification must be completed before admitting the mutation. Maintenance Lead execution and actual HTTPS/SQL acceptance also remain unverified.

Direct evidence:

- Backend solution build PASS, zero warnings/errors.
- Frontend TypeScript/production build PASS. Existing React Router directives and bundle-size warnings remain.
- Focused result-validation and execution-binding tests: 12 PASS.
- Solution test command exits zero without test discovery; not counted as coverage.
- No API/SQL runtime execution or database mutation in this increment.

Changed files: SubmitMaintenanceResultRequest.cs; MaintenanceResolution.cs; MaintenanceWorkService.Workflow.cs; MaintenanceWorkService.Commands.cs; frontend/src/dev2/catalog.generated.ts; MaintenanceResolutionTests.cs; this receipt.

Next: bind result attachments to the Incident under existing evidence permissions; revalidate material consumption against posted issue/return provenance with correct lock order; persist verifier notification intent in the Submit transaction with current branch/role/read authorization. Then expose the complete versioned Submit form and verify the real lifecycle.
