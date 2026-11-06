# C01 ? PARTIAL, scoped EF physical commit

CandidateCommitGuard singleton is stateless across business commands except a ConditionalWeakTable keyed by exact provider DbTransaction. Identity is actor/key/mutation mode, NEVER a successful-proof flag. UC029 arms its EF-owned transaction before its first write; replay arms historical-proof mode. No transaction owner change. No replacement context/session in producer.

Program registers the interceptor once on the existing context options. Unarmed transactions retain existing behavior. Existing BatchAsync hook remains defense in depth. Immediately before EF provider CommitAsync the interceptor runs parameterized async ADO commands on the exact connection AND transaction, independently of ChangeTracker and previous verification. It rechecks persisted assignment/actor/task/branch/source facts, completed result, mandatory publication, recipient intent and audit through fs009_verify_assignment_guard. Mutation additionally checks ACTIVE/unended assignment, task ASSIGNED/PENDING, exact rowversion, due and current-cycle pointer. Replay checks immutable historical facts without requiring the current task rowversion to equal the original revision.

| Path | Boundary/result |
| --- | --- |
| SaveChanges / SaveChangesAsync inside armed transaction | Save does not commit; outer EF commit checks persisted state. Sync commit rejected. No producer sync SQL operations. |
| EF IDbContextTransaction.Commit | Rejected for armed UC029, all DB operations must be async. |
| EF CommitAsync | Guard checks persisted state at physical commit callback. |
| ExecuteSql / ExecuteUpdate / ExecuteDelete in same transaction | Enlisted writes are checked at EF commit; no caller-provided SQL/delegates are accepted by UC029. |
| ADO command in same transaction | Same persisted commit check if committed through EF owner. |
| Stored procedures | Only constant verify procedure used; no external procedure/transaction owner accepted. |
| Savepoint rollback | Proof reread at commit; restored valid rows pass; stale in-memory verification is insufficient. |
| Rollback / disposal / cancellation before commit | No successful HTTP result inferred; transaction disposed; recovery reads durable evidence only. |
| Explicit provider DbTransaction.Commit/CommitAsync; SQL COMMIT; external connection; stored procedure self-commit | UNSUPPORTED BYPASS outside this interceptor. No such path in UC029 producer. No universal protection claimed. |

Trust boundary: reviewed UC029 producer, private BatchAsync transaction owner and registered EF options. This does not constrain arbitrary SQL clients, administrators or future noncooperating source changes. Public context provider handles remain available to trusted application code. Do not activate shared candidate as if DB enforces deferred commit constraints.

PHYSICAL_GUARD_RESULTS.json contains real SQL negative/positive results and retained raw-provider canary. The canary demonstrates EF bypass, not a claim that all external CleaningAssignment mutations are prevented. No generated EF edits or schema guard trigger installed on assignment table.
