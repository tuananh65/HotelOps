# Prepare internal deactivation responsibility predicates

Account deactivation cannot safely proceed while a target retains current duties or
the schema cannot establish responsibility. Add internal DEV1 queries that distinguish
approved cleaning/rework/inspection/defect/department-manager blockers from unresolved
data and contracts, plus opt-in unit/SQL verification and a provisional V1 contract.

This package adds no HTTP route, service registration, account mutation or assignment
writer. Deactivation stays blocked. The unverified local DEV2 query helper is excluded;
writer integration and the full atomic transaction belong to a later task.

Validation and known limits: see VERIFICATION_RESULTS.md (Task48) and RELEASE_REVIEW.md.
Solution build passed with zero warnings/errors. Executed parser/predicate SQL,
LastAdmin, Authorization, UC004/005/Activate/007/010 and Startup suites passed;
initial startup timeout and shared-trial UC010 fixture failure passed clean isolated
reruns. Verification builds retain NU1900 (vulnerability metadata unavailable).
Exact bounded commit files: FILE_MANIFEST.txt. Review the base branch carefully:
feature/BE1-database-integration already contains 14 prerequisite commits ahead of
develop before this package; a full branch PR includes those commits too.

This is a prepared PR description only. No PR, push or merge is created by Task48.
