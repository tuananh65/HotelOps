# Task85-B — runtime and verified progress

| UC | Historical ledger | Current runtime / this phase |
|---|---|---|
| UC038 View Room Turnover History | accepted historical weight2, earned2 | RESTORED & REVERIFIED: approved DI/current production source;146 main +26 field-oracle real HTTPS/SQL checks PASS |
| UC142 View Room Types List | accepted historical weight1, earned1 | Task84 evidence preserved; Task85B component19 + actual HTTPS/SQL list/detail regression PASS |
| UC037 Monitor Cleaning Tasks | PARTIAL | Task82 pagination preserved,14 boundary + actual HTTPS pagination checks PASS; Cancel not added |
| UC138 | NOT_IMPLEMENTED full workflow | unchanged |
| UC139 | BLOCKED_EXTERNAL | unchanged; no Notification/Pause All implementation |

**26/75,60/256=23.4375%; delta0 UC/0 points.** No delivery-ledger rewrite or duplicate UC038 credit.

Recommended Task86 candidate: **UC012 Update Branch Information** (ledger PARTIAL, weight2,
earned0), subject to a bounded readiness check. SRS §2.1 requires update plus Create and dependency-
checked Deactivate alternatives, rowversion and atomic audit. No notification step identified in
that UC, so more independent of DEV2 notification than UC123 (whose Withdraw alternative requires
notification). Do not narrow UC012 to update-only to claim completion. Existing schema/version/
dependency coverage and exact shared approval scope must be checked before promising acceptance.
If those gates remain unresolved, report blocker; this recommendation is not approval to start
Task86. UC038 acceptance now completed under separate SQL approval; await a new task instruction.
