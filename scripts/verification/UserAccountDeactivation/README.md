# Task53 — internal readiness (PARTIAL; actual success BLOCKED)

Task53 adds SQL-tested V1 positive predicates for Operational Task current assignees and
Internal Request in-progress assignees. Existing unresolved gates are preserved. A real
service attempt with valid SQL actor, durable target/admin remaining, role/membership,
two target sessions and zero modeled responsibility references returned ResponsibilitiesUnresolved.
The exact 10 gate codes are in task53-readiness.local.json and REMAINING_BLOCKERS.md.

Focused new readiness/predicate suite (does not rerun the existing fault suite):
`dotnet run --project scripts/verification/UserAccountDeactivation -- <new-patched-trial> <new-receipt.json> --readiness`

Use a canonical disposable trial with Task17 marker and approved Task26 rowversion patch.
The runner verifies identity/schema before fixture writes, uses existing RBAC catalog paths,
rolls back business predicate fixtures and deletes only its generated T53 personnel fixtures.
It never substitutes a clear evaluator or changes account status to simulate success.
No endpoint/DI/feature flag is added. Current results: VERIFICATION_RESULTS.md.

# Task51 — historical integrated deactivation preparation (PARTIAL; HTTP BLOCKED)

Current source baseline: fast-forward to `71a17f98b8c8e1dc0cfa54a12ca595e4e44a800d`.
Task51 adds `UserAccountDeactivationService`, a strict reason DTO and a concrete integrated
responsibility review. No controller, DI registration or enablement flag was added.
Direct service calls also fail closed on unresolved contracts. The mutation/revocation/audit/
LastAdmin poststate code is prepared but cannot currently reach commit for a valid target.
It is NOT certified by the rejection-path SQL checks.

Run independent checks:
`dotnet run --project scripts/verification/UserAccountDeactivation`

Run guarded SQL rejection-path checks (new receipt path required):
`dotnet run --project scripts/verification/UserAccountDeactivation -- <trial-name> <new-receipt.json>`

The SQL runner accepts only the existing Task17-marked disposable canonical trial naming
pattern on the verified local SQL instance. It reuses existing catalog grants, creates only
owned T51 user/session/assignment fixtures and removes those fixtures in finally. Never point
it at HotelOpsDB. Tests do not bypass unresolved gates to demonstrate a success path.

Current source findings: Maintenance ReturnWork recreates responsibility without retained
assignee User eligibility checks; resource-first actor reads also need global TABLOCKX review.
See INTEGRATION_DEPENDENCIES.md, LOCK_ORDER.md, REMAINING_BLOCKERS.md and Task51 results.
Earlier sections below are historical; Task51 takes precedence.

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
