# UC144 scoped maintenance list: source slice

PARTIAL_IMPLEMENTATION; no UC completion increment. This progresses the maintenance read dependency of UC092; it does not implement a preventive occurrence lifecycle or expose missing mutation contracts.

Changes:
- MaintenanceReadAccess joins permission to the same active role assignment, current user and branch access. Lead BRANCH/branch-bound ALL can read branch work; OWN remains assigned only. Staff remains assigned only even with BRANCH. Other roles cannot borrow a Lead/Staff role from another grant.
- Query accepts only coherent source lineage: incident with matching room/area/asset branch and no PM run, or exactly one PM run with exactly one schedule target. Work/schedule department branch must match. No inference of branch from Department alone.
- MaintenanceReadContract admits only A004A-035 GET list. Requires explicit branch, HTTPS, current unrestricted SQL session, permission resource for scoped OWN population, serializable scoped query and final privilege revalidation. Existing detail/mutation gates are unchanged.
- Reused projection/filter implementation via a scoped IQueryable; no EF entities exposed. Added source classification, keyword/source filters, branch input in workspace, and branch context in incident navigation.
- No schema changes, grants, database writes or deployment.

Validation evidence:
- dotnet build HotelOps.slnx --no-restore PASS, 0 warnings/errors after correcting nullable-pattern compilation error.
- npm.cmd run build PASS before the final additive keyword/source fields; React Router use-client and >500kB chunk warnings remain. Final npm.cmd run typecheck PASS.
- dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --no-restore --filter FullyQualifiedName~MaintenanceReadAccessTests --verbosity minimal: 3 PASS, 0 failed/skipped. These are in-memory authority/source-lineage checks, not SQL translation or HTTP acceptance.
- SQL/HTTPS/browser NOT_RUN. No broad regression claim. Existing IncidentReadAccessTests contains an outdated assertion that only two contracts exist; it was not included in this focused run.

Remaining:
- Complete UC144 detail authorization, dedicated source/location/deadline/overdue display and filters/sort using canonical business deadline. PlannedEndAt is not assumed to be an SLA deadline.
- Implement actual UC062 occurrence state/checklist/interval persistence before UC092 links to occurrences. A maintenance-work read record alone is not an accepted occurrence cycle.
- Human QA: Lead and Staff on own/foreign branch, revoked/current grant, role expiry before disclosure, reassignment, malformed/mixed source lineage, keyword/source pagination; verify generated SQL and existing related-incident navigation.
