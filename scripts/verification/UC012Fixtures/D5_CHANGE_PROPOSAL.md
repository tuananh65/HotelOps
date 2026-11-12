# D5 revision2 — exact change proposal only / owner approval pending

No modification to PR51, PR72, production exception mapping or acceptance
expectations is made here. D1–D4 remain design-only, all execution NOT_GRANTED.

| Source | Proposed minimal change after API/source-owner approval |
| --- | --- |
| PR51 be6c7278e1f5749211e97b55c8c21c0727b31fba, BranchAdministrationService.cs:135–136 | Replace blanket SQL/DbUpdateException=>503 classification with an approved provider-cause classifier. Unknown DbUpdateException must propagate to sanitized500. Keep cancellation propagation and caller transaction disposal/rollback. |
| PR72 d051a4a9af3b0abfdc014e9dc48ee3dbe7de2fd2, UC012Acceptance/Host.cs:24–31 | Classify the existing bare DbUpdateException injection as unexpected500. Add a separate accepted transient-provider fault mechanism whose exact exception type/provider number is evidenced; never infer cause from its message or fabricate an arbitrary provider exception. |
| PR72 UC012Acceptance/Cases.cs:92–102 | Verify both accepted fault classes separately: classified storage503 and unexpected500, each with unchanged Branch value/version and no partial audit, exactly one fault hit and no replay. Keep original12 case identities if both subcases belong to AUDIT_ROLLBACK. |
| PR72 Guards.cs TrialReceipt + Program.cs report | After approval, bind fault profiles/version/approval and report actual classification/rollback evidence. Current single AuditFaultStatus does not prove both profiles. Reject old ambiguous fault receipts rather than silently changing expectation. |

Proposed transient profile to approve: SQL Server40501 (temporarily busy) as a
provider-originated storage failure during audit SaveChanges. Exact reproducible
trial mechanism remains pending source-owner approval; plain DbUpdateException
without that cause is not this profile. Do not invent a public exception wrapper
or mock HTTP503 to satisfy acceptance. Provider allowlist must be approved before
code changes; 40501 is a proposal, not an installed policy.

Preserve current operation responses: duplicate409, malformed/validation400,
missing If-Match428, stale412, session401, authority403. Keep deadlock1205 as an
acceptance failure with durable-outcome inspection; do not call it an accepted
race winner or retry it. Existing business conflict handling takes precedence
over transient classification. Do not treat all SQL errors as transient.

API Owner must decide how a caught but unrecognized provider exception maps to
generic500 and confirm sanitized public codes. Schema/constraint/programming
errors are not automatically503. Runtime fault tests must prove actual persisted
rollback, not just returned status or an interceptor's hit counter.

Pending: API/source-owner acceptance of classifier/profile and PR51 scope;
runner-owner acceptance of two-class fault/report receipt revision; separate
schema/fixture/principal/lease/runtime execution approvals. PR51 remains HOLD.

## FASTTRACK-046 owner map and classifier design

- PR51 UC012 business/API source owner: DEV3 FE02. Source pin above is unchanged.
- PR72 acceptance runner source owner: DEV3 verification/FE02; merged at the pin
  above. Runtime/SQL/HTTPS owner is DEV1, not the source owner.
- DEV3 Security reviews classification and sanitized response policy. Final D5
  approval is still pending the Co-Project Leader/API decision requested in
  revision2; source ownership is not that signoff or runtime authorization.

Proposed classification order, not implemented:
1. Preserve cancellation. Preserve explicitly handled duplicate/conflict,
   validation, precondition, session and permission results before classification.
2. Inspect the real provider cause of SqlException or DbUpdateException.InnerException.
   Accept only the exact approved transient provider profile; never inspect text.
3. A recognized transient cause maps to the existing sanitized503 unavailable
   response. An unknown/unclassified cause propagates to the existing sanitized500
   boundary. Roll back the caller transaction in either case; do not retry.
4. Mixed provider errors or ambiguous wrapped causes fail classification. The
   owner must define handling for all provider errors, not only the first number.
   SQL40501 is a proposed profile only, not an approved general allowlist.

## Focused regression matrix (planned, not newly executed)

| Case | Expected HTTP / invariant | Required proof |
| --- | --- | --- |
| Accepted provider-origin transient at audit SaveChanges | 503, no replay | Exact provider profile; unchanged domain/version/audit after rollback |
| Bare DbUpdateException, no recognized provider cause | 500 | Sanitized response, unchanged domain/version/audit |
| Unexpected application exception during audit | 500 | Same rollback and no exception/SQL leakage |
| Unknown SQL error or mixed recognized/unknown provider errors | Not automatically503 | Approved fallback classifier and rollback assertion |
| Duplicate code2601/2627 handled as business conflict | 409 | No duplicate Branch/audit; do not classify transient |
| No-op update | 200 | No RowVersion/audit advance |
| Missing/malformed/stale If-Match | 428/400/412 | No mutation |
| Invalid payload / unauthorized / forbidden | 400/401/403 | No mutation; no protected error detail |
| Deadlock1205 or timeout/cancellation/lost response | Acceptance fails; no replay | Durable-outcome inspection; no claim of confirmed rollback from status alone |
| Concurrent updates on one version | One200 + one412 | Exactly one update/audit; SQL acceptance required |
| Fault-profile receipt missing/old/changed | Fail closed | No SQL/HTTPS dispatch |
| Valid request after fault | Only explicit fresh operation | No queued/replayed original mutation |

Pure classifier and receipt guards can be verified offline after implementation
approval. Provider-fault fidelity, persistence, race and rollback require a separate
approved disposable SQL/HTTPS lease; mocks cannot close those cases.
