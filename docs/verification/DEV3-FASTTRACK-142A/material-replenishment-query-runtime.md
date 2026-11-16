# Material/replenishment query runtime evidence

2026-11-16. Direct read-only SQL verification; no lifecycle acceptance claim.

Extracted the existing linked-work and waiting-request LINQ expressions into internal query methods used by the production read paths and the existing disposable probe. This keeps scope filtering in the caller and exercises actual production expressions, not copied test queries. No endpoint admission, permission, grant or database mutation changed.

The initial probe used ToQueryString as executable SQL. Parameterized SqlQuery output failed with a parameter declaration error. Preserved material-replenishment-queries-20261116.json. Corrected the probe to execute Take(0) through EF ToListAsync with real bound parameters, preserving the exact database identity guard and zero-row assertion.

Direct corrected result in material-replenishment-queries-ef-20261116.json:

- Replenishment notification query: PASS.
- Replenishment waiting Material Request query: PASS.
- Material linked Internal Request query: FAIL, dbo.internal_request_material_links is absent on this disposable recovery database. The production feature's existing InstalledAsync guard returns unavailable before invoking this query. Do not call linked-work runtime ready.

The runtime guard printed matching nonzero expected/actual GUIDs. JSON identity fields still contain zero GUIDs; retain them unmodified and do not use these fields as identity proof. No business rows read, no fixtures, no grants, no writes. Passing zero-row execution proves SQL translation/schema availability only; it does not prove data semantics, authorization, concurrency or UI/API behavior.

Backend solution build PASS, zero warnings/errors. ModelProbe build PASS with three existing EF1002 warnings in SprintCredentialScenario.cs (62/72/83); that scenario was not run. Solution test command executed no tests. No frontend changes in this increment. Next runtime prerequisite is guarded installation of the Internal Request completion/link schema and dependencies on the verified disposable database, after compatibility review; do not create an ad-hoc link table or weaken the capability guard. UC099/101 remain partial; zero newly COMPLETE_VERIFIED UCs.
