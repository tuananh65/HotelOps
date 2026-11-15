# FASTTRACK-142A checkpoint40 — committed rework Accept/Start

Sprint active; original deadline remains 2026-11-16 17:50 UTC. Newly COMPLETE_VERIFIED: **0**. Totals remain **4 COMPLETE_VERIFIED, 140 PARTIAL**. UC035 remains PARTIAL pending positive canonical HTTPS execution under a properly restricted database principal.

Implemented Pending Acceptance → Accepted → In Progress against committed FE05 authority. Existing session, ownership, CLEANING_EXECUTE, active shift, strong ETag, operation identity, current source proof and workday/busy guards remain enforced. Start opens one real work interval; task, interval and audits commit atomically. Exact retries return the durable original receipt; changed requests cannot reuse its operation key. Receipts describe readiness at the committed event, not permission to use a room: current effective readiness remains authoritative. Accept/Start now calculate BLOCKED or REWORK_REQUIRED in the transaction and reject an unproven readiness. No room physical status is changed.

Canonical assigned-source UI now offers Accept/Start only with the server capability and matching state. It binds CSRF and mutation to the current session generation, removes stale data, refreshes after success and requires a fresh read after an uncertain response. Complete is not exposed. Source read rejects duplicate open rework and unsupported remediation data.

Direct evidence:

- `checkpoint40-readiness-runtime.json`: PASS normal and effective-blocked fixtures. Existing real browser Reject/Request Rework/inbox sequence remains verified. New Accept/Start calls execute the public service with a real session under a disposable SQL principal denied proof DML. No-workday and busy failures preserve the original row version. Injected final audit failure rolls back the new interval and task. Two identical concurrent Starts return success with exactly one interval. Changed-operation body and stale requests are rejected. Both commands preserve effective readiness. Schema marker, executor user and original CHECK definition are restored, synthetic fixtures cleaned.
- `checkpoint40-readiness-regression.txt`: backend regression 507 PASS, 11 SKIP. Backend and ModelProbe builds PASS, zero warnings. `dotnet test HotelOps.slnx --no-restore` succeeds but this solution contains no test project; it is not counted as regression coverage.
- `checkpoint40-readiness-client.txt`: 10 PASS, including canonical route/session/CSRF Accept/Start and blocked/readiness response validation. These are mocked-network browser/client tests, not positive SQL-backed HTTPS commands.
- `checkpoint40-readiness-frontend.txt`: TypeScript compilation and production build PASS. Existing Vite module-directive and bundle-size warnings remain. No dependency change; npm ci not repeated.

Retained failures: initial client TypeScript scope-placement error; initial HTTP browser fixture lacked secure-context randomUUID; SQL audit-fault trigger conflicted with EF OUTPUT and was replaced by a temporary CHECK fault. Corrected reruns passed. No failed log is relabeled PASS.

Complete verified workflow remains Failed inspection → Request Rework → durable authorized assignee notification/inbox. New SQL execution extends it through Accepted/In Progress, but the canonical HTTPS host still fails closed when its database principal can write proof. No proof protection was removed. Complete/remediation and reinspection remain unfinished.

Changed application files: AssignedReworkExecutionService.cs, AssignedReworkReadService.cs, ReworkLifecycleService.cs, CleaningExecutionController.cs; frontend reworkExecution.ts and AssignedReworkRead.tsx. Focused client tests and guarded ModelProbe helpers accompany the implementation. Narrow Accepted/Pending CHECK extension exists only during the disposable trial and is restored exactly. Generated entities, shared database and server settings were not changed.

Initial remote: 80060663e0021484369ae58183963d39b43e1c23. First implementation commit: a7a66d721266b93a355f7325fbf3b31f28fdd4e4. Further delivery identities are recorded by Git. Next: verify a supported least-privilege HTTPS runtime without weakening proof checks, close UC035 browser acceptance, then implement UC036 remediation/UC047 reinspection with transactional evidence and readiness.
