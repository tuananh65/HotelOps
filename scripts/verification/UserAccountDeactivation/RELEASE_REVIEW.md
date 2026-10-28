# Task48 — controlled release review

Scope: internal DEV1 responsibility queries, opt-in verification harness, provisional
business contract and integration documentation. No route, DI registration, existing
business writer, shared infrastructure, generated mapping or schema source changed.

Reviewed source:

- ApprovedDev1ResponsibilityQueries.cs is the approved current-responsibility review.
  It returns separate blockers and unresolved codes; unresolved module/writer gates
  always remain present. Exact target columns prevent actor-FK overblocking.
- Dev1DeactivationPredicates.cs is a narrower diagnostic proof of existing cleaning
  execution ownership. It is retained for comparison with the established resolver,
  not substituted for the broader approved review. A false result cannot clear duties.
- DeactivationResponsibilityQueries.cs (Task46 DEV2 predicates) was reviewed but lacks
  actual positive/negative SQL fixture coverage in this package. It remains an unchanged
  local untracked artifact, EXCLUDED from commit, pending DEV2 integration verification.
  No committed file calls it; historical documentation references do not make it a
  runtime dependency. Do not stage it with this package.
- Verification Program.cs adds only --dev1-predicates; existing activation invocation
  remains intact. The two new suites exercise internal APIs by reflection without
  widening production visibility or introducing another project.

Cleanup: removed the unused InspectedAt projection from the approved review. The two
cleaning queries intentionally answer different questions; neither starts mutations.
No speculative workflow/status expansion or cross-module refactor performed.

Known limits: unbounded target-history scalar materialization is acceptable only for
this unregistered diagnostic helper; load/plan/locking work is required before runtime
wiring. The queries provide neither a consistent multi-query snapshot under ordinary
READ COMMITTED nor safety against uncoordinated writers. Missing/unknown parent states
remain unresolved via LEFT JOIN. No complete eligibility or production-readiness claim.

V1_BUSINESS_CONTRACT.md records Task48 approval as provisional. Broader V1 intent does
not supply missing exact stored-state/workflow semantics. Those gates remain unresolved.

FILE_MANIFEST.txt is the exact proposed source/docs allowlist. All .local.json, bin/obj,
WAV, certificates, Task40 audit artifacts and unrelated files remain excluded. Commit
is conditional on verification and staged review; use the requested message only.

Task48 required package regressions now PASS, including clean retries documented in
VERIFICATION_RESULTS.md. Remaining business/DEV2/full-mutation gates block endpoint
readiness, not this unregistered internal package. NU1900 metadata limitation is disclosed.
The only production cleanup relative to Task47 is removal of an unused projection.
