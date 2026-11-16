# Recovery HTTPS runtime preparation

2026-11-16. Direct API boundary evidence, no authenticated UC acceptance.

The sandbox certificate check reported no valid certificate. Rechecking in the actual user context found an existing trusted localhost development certificate, valid 2026-11-01 through 2027-11-01. No certificate installed, trust bypass, insecure fallback or security setting changed.

Started the current API binary on a separate https://localhost:7148 listener using an ephemeral environment connection to the exact previously verified disposable recovery database. SQL encryption and certificate validation remained enabled. StartupSchemaValidator passed before listening. All opt-in notification/task-series workers were explicitly disabled for this boundary probe; no application configuration file or secret was changed.

Default-platform-TLS HttpClient verified 401 for anonymous auth session, Material Request list, replenishment list and Internal Request list; CSRF bootstrap returned 200. No credential, cookie or CSRF value is included in evidence. The anonymous checks do not demonstrate authenticated branch/role scope, session persistence, CSRF mutation rejection or workflow completion.

Runtime emitted three existing EF model warnings about RoomTypeBranchV1 indexes whose properties are not mapped to a table (IX_room_types_default_checklist_v1, UX_room_types_branch_code, UX_room_types_legacy_code). No changes made to generated mappings to suppress them. No business SQL mutation or fixture created. Source changes/build/tests were not needed for this startup probe.

The legacy SprintAuthProbe targets another instance/database and requires empty personnel/session/audit data; it was inspected but not run or pointed at recovery by weakening its identity guard. Next authenticated trial needs a recovery-specific synthetic fixture with current grants and cleanup, bound to this API's exact disposable connection. No newly COMPLETE_VERIFIED UC.
