# Task48 — develop integration plan

Preflight DEV1 HEAD: 6278a8807bb72ff72c16f8173748ff48f5260794.
Branch: feature/BE1-database-integration; upstream origin/feature/BE1-database-integration.
Remote develop verified with read-only git ls-remote:
5299b5e9e8dd917abef327ab9f9bd5494064ccf9, matching local origin/develop.
Before this task's commit, DEV1 is 14 commits ahead, 0 behind develop. No fetch,
pull, merge, rebase, cherry-pick or other worktree mutation was performed.

Inspected develop source does not contain coordinated Users-first DEV2 writers.
The reported DEV2 handoff revision is not present/identified in current develop.
Do not equate older merged maintenance modules with new eligibility coordination.

Suggested PR order:

1. DEV1 prerequisite foundation commits (the existing 14-commit branch delta), reviewed
   by the team, then this internal predicate package. A PR from this branch includes
   the whole delta, not only the Task48 commit: reviewers must assess that scope.
2. DEV2 final writer-coordination PR, with its exact commit/source manifest and SQL
   evidence. Either PR order is technically possible because this package has no
   runtime wiring; choose the order that avoids overlapping feature files.
3. A separate integration task reconciles actual source, reviews the global guard and
   Users/resource order, resolves unsupported DEV1 semantics, implements the atomic
   Deactivate transaction and runs integrated races. HTTP remains disabled in Task48.

Expected overlap: DEV2 may add a shared user-lock helper or touch its writer modules.
Keep existing single-project structure; do not copy a competing locking helper into
this package. Keep exact DEV2 predicates and latest-cycle semantics in its ownership
scope. Resolve any later Program/context/entity conflicts by preserving the reviewed
baseline and obtaining approval for shared changes, not by overwriting one side.

No PR created. No push or merge authorized. Refresh remote comparison immediately
before the developer creates the PR; merge-readiness here applies to the bounded
internal predicate package, not to full branch approval or deactivation readiness.
