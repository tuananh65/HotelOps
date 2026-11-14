# Isolated SQL/HTTPS trial — prepared, not executed

No SQL connection, business mutation, migration, seed, API host startup or deployment was performed in this task. Browser regression uses synthetic local responses; C# offline harnesses use doubles/SQL translation and report zero real SQL connections. xUnit live SQL/HTTPS cases were skipped by their existing guards.

Before a runtime trial, record a new evidence receipt containing:

1. Read-only server/instance identity, `DB_NAME()`, database GUID, schema/version/trigger fingerprints and connection encryption details. Do not record credentials or User Secrets values.
2. Independently verified ownership and disposable status of the exact target. A name containing “Trial” alone is insufficient. Reject shared `HotelOpsDB`, production and any unidentified instance.
3. Schema comparison against current HotelOpsDbContext plus candidate cycle/evidence/notification prerequisites. No generated-entity edit or schema upgrade is authorized by this plan.
4. Synthetic-only fixture IDs and exclusive run ownership, cleanup inventory, rollback plan and pre/post unrelated-data checks. Service-owned commits require verified compensating cleanup; do not assume an outer transaction can roll back HTTP commits.
5. HTTPS certificate trust/hostname verification, bound loopback endpoint, synthetic cookie sessions, real CSRF and permission checks. Keep production release fuses closed unless their specific contract and trial conditions are satisfied.
6. Positive, denied, stale ETag, expired session, concurrent source-change and rollback/cleanup cases for the selected workflow. Verify cleanup before reporting runtime acceptance.

Suggested first runtime slice: read-only UC141/UC011/UC013 against synthetic users and two branches. It validates the canonical routes delivered here while avoiding lifecycle mutations. Next mutation slice should be chosen only after cycle/evidence/notification compatibility is verified.

Current result: `SQL_HTTPS_STATUS=NOT_RUN; IDENTITY/DISPOSABLE/SCHEMA/TLS_NOT_VERIFIED`. User permission to prepare an isolated trial is recorded in DEV3-FASTTRACK-141A; no DEV1/DEV2 approval is requested. Missing physical target evidence is a runtime precondition, not an ownership handoff blocker.
