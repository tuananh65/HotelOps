# FAST012 owner 14-case acceptance driver binding — NOT EXECUTED

This is an exact FE05 owner scenario/binding plan, NOT a completed executable SQL acceptance driver or a DEV2 acceptance receipt. No permissive fixture, local SQL-lane override or second replay framework was introduced. Actual N01 canonical-case equivalence and an approved fixture/SQL lease remain prerequisites. The current IFe05TrialExecutionAuthorization implementation throws N01_SQL_LANE_NOT_ACCEPTED before Reserve. acceptedConcreteBinding remains null for every recipient.

Pinned owner entry points: InspectionN01Composition.cs ComposeRejectInspectionAsync / ComposeInspectionReworkAsync; InspectionOutcomeRecovery.cs; InspectionOwnerSourceCoordination.cs; InspectionTrustedIssuer.cs; original FE04 InspectionRoomProjectionAdapter.cs and snapshot/proof readers. Existing Reserve/Stage/Verify and NotificationCommitGuard own graph validation. All fixture commands require a real actor session and existing grants. No configuration flag may manufacture lane, policy, storage or issuer acceptance.

| ID | Actual invocation to bind on accepted lane | Required persisted assertions, separate from synthetic component tests |
|---|---|---|
| O01 | ComposeRejectInspectionAsync then guarded caller CommitAsync | Exact root FAILED/result FAIL, task REWORK_REQUIRED, audit, durable UC045 result, required notification graph, revision, projection; separate connection observes committed identity |
| O02 | ComposeInspectionReworkAsync then guarded caller CommitAsync | Exact persisted Rework PK/task/Inspection/current cycle/selected defects, original FE04 snapshot digests, real FE05_COMMITTED_V1 proof, UC046 receipt and graph |
| O03 | Repeat O01/O02 same stable operation/canonical request | Completed replay verifies graph/receipt/authority; no second domain mutation, proof, audit or staging |
| O04 | Same operation key with changed canonical request | Conflict, no partial reservation/domain/audit/graph |
| O05 | Revoked actor/session or required grant | Deny before source mutation; real permission evaluator, no mock principal |
| O06 | Valid actor, wrong branch | Deny/no foreign source disclosure; unchanged persisted state |
| O07 | Stale/wrong persisted cleaning cycle | Reject exact source; no latest-task fallback |
| O08 | Changed Inspection revision or D4 evidence association/metadata | Stale rejection before conclusion; D4 invalidation retained |
| O09 | Changed immutable version/content bytes | Storage/proof check rejects; digest reread alone cannot count as immutable storage acceptance |
| O10 | Foreign/removed/duplicate selected defects | Exact selected set rejected; unchanged domain/proof/receipt |
| O11 | Supervisor/cleaner/assignee reassignment after Users prelock | Retained ascending Users roster mismatch rejects; no late Users locks; revoked source-read denies |
| O12 | Mandatory participant failure after domain mutation, including staging/proof/projection/before-commit guard | Explicit full rollback of root/task/Rework/receipt/proof/audit/graph; no independently committed participant |
| O13 | Concurrent Reject/Rework on exact source with same/conflicting keys | Serialized conditional transition; no duplicate mutation; observe conflicts/deadlocks rather than claiming deadlock-free |
| O14 | Unknown commit observation and different-transaction FE04 UC035/036 read | Original request identity, completed graph/receipt/proof/digest/projection revalidation; no automatic mutation retry; positive consumer accepts real committed source |

All O01–O14: NOT_RUN. This plan is not the 14 PASS acceptance matrix. Driver execution is BLOCKED by existing N01 lane/schema/runtime acceptance, recipient policy acceptance, immutable storage and registered SQL issuer/proof/projection prerequisites. FE05 executable binding/fault-injection fixtures still need to be completed against that accepted substrate; this document does not claim otherwise. Do not use the owned FAST010 component database as an authority acceptance substitute.
