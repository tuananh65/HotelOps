# Remaining blockers

## Task48 current boundary

V1 intent for all responsibilities is now PROVISIONAL, approved by DEV1 owner.
Missing exact schema/workflow semantics remain UNSUPPORTED/UNRESOLVED; V1 approval
does not invent statuses or designate a next actor. See V1_BUSINESS_CONTRACT.md.
The older phrase "unapproved Shift/Workday/Internal Request contracts" below refers
to missing exact executable contracts, not absence of the new high-level V1 intent.

Full Deactivate remains BLOCKED by lifecycle/actor mappings, missing writer coordination,
DEV2 source handoff and atomic transaction/integrated race verification. Internal package
can be reviewed independently because no runtime endpoint/writer is wired. Task46 DEV2
helper is excluded pending SQL coverage. Remaining source is limited by FILE_MANIFEST.txt.

## Task47 delta — 2026-10-29

Post-approval update: approved cleaning/open rework/designated inspection/assigned
defect/active department predicates are now implemented and SQL-tested. The statement
below about no approval describes the first Task47 pass only. Exact APPROVED /
IMPLEMENTED / UNRESOLVED status is in DEV1_PREDICATE_DECISIONS.md.
Remaining business blockers: ACTIVE cleaning with ambiguous COMPLETED parent,
Operational Task review/reopen mapping, line-manager duties, and explicitly unapproved
Shift/Workday/Internal Request contracts. Writer coordination and full Deactivate
transaction/race integration remain BLOCKED. No endpoint is added.

Implemented only the known executing-cleaning positive subset from the existing
ownership contract. This reduces executable predicate work but does not close the
full cleaning gate. See DEV1_PREDICATE_DECISIONS.md for exact approval questions,
DEV1_WRITER_LOCK_MATRIX.md for present/absent writers, and DEV2_INTEGRATION_CHECKLIST.md
for handoff acceptance. No business approval response was available when implementing
this subset; wider proposed predicates are not active. HTTP remains closed in Task47.

The Task46 blockers below remain applicable; its proposed HTTP409 is still a proposal.

1. Integrate DEV2 coordinated writers through the team's approved Git workflow.
   Task46 does not authorize copying/cherry-picking/merging them here.
2. Approve DEV1 lifecycle mappings and predicates enumerated in OPEN_WORK_PREDICATES,
   including legacy contradictions, shift boundaries, requester/manager obligations
   and acknowledged internal-request handover representation.
3. Implement/audit all relevant DEV1 writers and external/import paths against the
   users-first eligibility protocol, including exact employment eligibility.
4. Implement the atomic Deactivate transaction after these contracts are sufficient:
   SQL session/ALL-BRANCH authority, self/OWN denial, strong If-Match, strict reason,
   global-first serialization, current responsibilities, LastAdmin poststate,
   account/version mutation, session revocation and audit rollback.
5. Execute approved predicate fixtures, required concurrency/fault cases and all
   regressions exclusively on guarded disposable SQL databases. Collect real SQL
   lock-manager evidence in both race orders and resolve reverse lock paths.
6. Only then register HTTP Deactivate. Proposed responsibility response: HTTP409,
   code OPEN_RESPONSIBILITIES, without cross-scope resource details. This is a
   proposed future contract, not a currently exposed response.

Final verdict: PARTIAL independent predicate implementation; integrated Deactivate
BLOCKED. Exact unresolved decisions require developer confirmation before mutation
logic, per AGENTS.md; none are inferred from missing source or schema defaults.
