# Tasks46–48 — internal deactivation responsibility preparation

Current contract: V1_BUSINESS_CONTRACT.md (PROVISIONAL). Package scope/exclusions:
RELEASE_REVIEW.md and FILE_MANIFEST.txt. Develop/DEV2 handoff plan:
DEVELOP_INTEGRATION_PLAN.md. Current verification: Task48 section of VERIFICATION_RESULTS.md.
No Deactivate HTTP endpoint, transaction or DI registration is included.

ApprovedDev1ResponsibilityQueries is the packaged approved review; its separate
Blockers/Unresolved output cannot certify full eligibility. Dev1DeactivationPredicates
is a narrower diagnostic ownership comparison. Task46 DeactivationResponsibilityQueries
is deliberately excluded from this commit until its SQL coverage/DEV2 handoff is verified.
The historical Task46/47 notes below describe local development, not the commit manifest.

Task47 post-approval: ApprovedDev1ResponsibilityQueries now implements approved
cleaning, rework, inspection, defect and active department manager predicates with
separate blocker/unresolved output. See current decision table and verification results.
HTTP Deactivate remains closed; unresolved module contracts are never cleared.

Task47 adds a tested positive cleaning-execution subset in Dev1DeactivationPredicates.cs.
It does not resolve the complete DEV1 gate. Current decisions, writer design, handoff
checklist and executed SQL evidence are in DEV1_PREDICATE_DECISIONS.md,
DEV1_WRITER_LOCK_MATRIX.md, DEV2_INTEGRATION_CHECKLIST.md and VERIFICATION_RESULTS.md.
The sections below describe Task46 deliverables. HTTP remains closed in Task47.

Verdict: PARTIAL implementation; HTTP Deactivate BLOCKED.

`Services/Users/DeactivationResponsibilityQueries.cs` implements the six approved
DEV2 blocker predicates as internal asynchronous, cancellation-aware read-only queries.
It is deliberately not registered in DI, exposed over HTTP, or connected to mutation.
An empty result means only that these six predicates found no blockers; it is never
a deactivation eligibility decision. No database access is performed at startup.

No Deactivate transaction was added because DEV1 current-responsibility semantics
remain unresolved. Implementing a successful mutation path would invent business rules.
Existing activation service continues to reject INACTIVE, including direct calls.

See INTEGRATION_DEPENDENCIES.md, OPEN_WORK_PREDICATES.md, LOCK_ORDER.md,
VERIFICATION_RESULTS.md and REMAINING_BLOCKERS.md.

Only the helper and these six documents are new Task46 files. No existing files,
generated mappings, DEV2 modules, shared configuration, catalog or schema are changed.
No commit, push, merge, branch change, deployment or worktree mutation performed.
