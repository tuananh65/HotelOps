# UC029 external write boundary

| Path | Actual support and evidence |
| --- | --- |
| EF SaveChanges sync | Armed command sync rejected CANDIDATE_REQUIRES_ASYNC_COMMAND; not supported producer path |
| EF SaveChangesAsync | Supported enlisted writes; SQL persisted proof at outer EF CommitAsync |
| EF transaction Commit sync | Existing armed transaction rejection |
| EF CommitAsync | Same physical connection/transaction, persisted mutation/cycle/revision/audit/completed operation/publication/intent proof |
| ExecuteSql/ExecuteUpdate/Delete | Constant parameterized supported producer statements; post-write persisted check; recognized explicit COMMIT/ROLLBACK/BEGIN/SAVE text refused before EF execution |
| Raw ADO in producer | Only reviewed VerifyAsync command with exact provider transaction; private BatchAsync owns final commit |
| Direct provider DbTransaction commit | Outside EF callback. Existing negative canary + new other-connection assignment mutation reproduce residual risk |
| Stored procedures | Verify procedure supported; arbitrary dynamic or self-committing procedure unsupported |
| Other SQL connections / external process | Not intercepted; separate SqlConnection changed synthetic assignment reason with no new audit/operation/publication/intent |

CandidateCommandGuard checks only armed exact provider transaction via ConditionalWeakTable. Unarmed requests pass unchanged. It is deliberately a lexical tripwire, NOT a SQL parser, parser-level allowlist, provider wrapper or sandbox. It may reject benign literal/comment keywords; supported producer values are parameters and no arbitrary command text is accepted from HTTP. Dynamic EXEC/obfuscation/procedure self-commit and trusted access to raw connection remain residual risks. No claim of complete C01/global protection.

BOUNDARY_RESULTS.json: four recognized EF control commands rejected; external mutation persisted and four durable counts stayed unchanged. Mutation retained on NEW FS011 scratch, not repaired and not executed on FS010/history/Tester. PHYSICAL_GUARD_RESULTS verifies eight behavior checks plus expected provider bypass. Savepoint API under existing EF owner stays supported; raw SAVE text is not.

PERMISSION_PROBE.json: temporary WITHOUT LOGIN user + DML DENY under one scratch transaction; UPDATE denied229, entire principal/grant rolled back. Current runtime principal NOT changed; no shared/production permissions changed. DENY direct table DML would also block current EF producer. A meaningful restricted deployment needs dedicated least-privilege app principal and reviewed signed/owned stored procedure/facade staging all mandatory rows, no exposed write rights or ALTER/impersonation grant. This needs accepted shared contract; not silently introduced here.

Existing FK/unique/check/immutability constraints preserve link/shape, but SQL Server immediate constraints do not provide a deferred end-of-transaction guarantee that every assignment has audit+publication. Do not invent a trigger that rejects valid intermediate staging or a circular required FK. Global noncooperating-writer guarantees remain a merge/security reconciliation gate.
