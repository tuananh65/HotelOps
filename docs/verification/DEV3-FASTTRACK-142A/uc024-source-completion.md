# UC024 Create Department source completion

2026-11-16. IMPLEMENTED_PENDING_QA, not COMPLETE_VERIFIED.
NEW_SOURCE_COMPLETE=UC024; SOURCE_COMPLETE=46/144; QA_VERIFIED=27/144 historical; SOURCE_INCOMPLETE=98/144.

Canonical Branch Structure now links Add Department and Departments to /structure/departments. Create captures branch, code, name, description and optional Head designation, reviews the form and sends a single CSRF-protected request. Browse/detail remain available; Update is excluded from canonical Department actions pending UC025's routing/authority contract. Successful creation shows MSG08; duplicate/reference errors use safe allowlisted MSG35/MSG03 guidance.

Inspected and reused backend: RoomTypeBatchController and DepartmentBatch2Service, existing departments EF mapping/unique branch-code index. Create normalizes fields, rejects duplicate branch codes, validates optional active DEPT_MANAGER with effective branch role/access, locks actor/selected user, checks current MASTER_DATA_MANAGE and session, creates Active department and audit atomically, and rechecks eligibility before commit. ManagerUserId remains designation only. No role, permission or management authority mapping is automatically granted.

Validation: frontend TypeScript/Vite build PASS with existing directive/bundle warnings; one canonical browser test with mocked transport PASS (confirmation, exact payload, CSRF, MSG08 and no Update option). All 82 hotel transport/DTO checks PASS. Backend source unchanged; no new SQL/HTTPS acceptance, database mutation, schema change or grant installation.

Human QA: actual disposable SQL creation, duplicate/case handling, inactive/foreign-branch Head, revoked/expired role/grant, rollback, audit and authorized listing. Mocked browser success is not persistence evidence.

UC025 remains incomplete: SRS postcondition routes new requests/approvals to the new Head, while approved FE13/material contracts require explicit user-branch-department mapping and prohibit inference from ManagerUserId. A decision request is pending on explicit confirmed mapping replacement versus a separate mapping operation. No change to this authority rule was inferred and no canonical Department update was opened.
