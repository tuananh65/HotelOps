# Task55A current - ReturnWork lock order verified in SQL

Official merge 4a187320643d0c82f232c6b9dd5a667b8401c280 is now local HEAD. ReturnWork:
untracked discovery -> owned transaction -> retained assignee User UPDLOCK,HOLDLOCK and
SQL eligibility -> Incident UPDLOCK,HOLDLOCK -> MaintenanceWork UPDLOCK,HOLDLOCK -> authoritative
reload and drift/synchronized-state checks -> writes/log/event/two audits -> commit.
The eligibility helper is the same `UserEligibilityLock` used by DEV1; it does not own,
commit or release its caller's transaction, and SQL reads are AsNoTracking after lock wait.

Actual Deactivate remains: owned transaction -> global-admin applock -> existing Users
TABLOCKX,HOLDLOCK universe snapshot -> sorted actor/target User locks -> retained authority
and SQL-session checks -> target/version SQL reread -> concrete responsibility review.
Responsibility checks execute with the retained User locks. No tracked context is accepted.
The prepared account/session/audit/LastAdmin poststate branch remains behind the unresolved
review; it does not introduce a User-after-feature acquisition in this service.

`task55a-returnwork-accepted.local.json` contains actual DMV evidence and trace assertions:

- ReturnWork-first: real commit barrier after flush; actual Deactivate waits LCK_M_X for the
  writer's retained Users lock; after commit, concrete review contains MAINTENANCE_ASSIGNEE.
- Actual Deactivate-first: pause after acquiring the real Users table lock; Return waits
  LCK_M_IX with only its User acquisition attempted. Deactivate denies business gates and
  rolls back; Return then acquires User -> Incident -> Work and commits. This is denial/release
  acceptance only, not successful deactivation.
- Standalone INACTIVE-first: a separate owned User is changed while its retained lock is
  held. Return waits LCK_M_U, rereads SQL and rejects stale tracked ACTIVE before feature locks.
  This deliberately separate fixture cannot certify full Deactivate-first success.
- Cancellation/timeout1222 leave no pair/history changes. A deliberately injected post-flush
  TEST cycle chooses Return as deadlock1205 victim; all business/audit writes roll back and
  an explicit fresh retry succeeds. No deadlock was observed in the canonical two-order races;
  the injected cycle proves fault cleanup, not compatibility for other production writers.

Global lock-order acceptance remains BLOCKED. Other ExecuteCorrectiveAsync paths without
owner preparation, Asset Reject/Retire and other feature-first actor reads are not certified
by ReturnWork's new Users-first path. Existing broad Users TABLOCKX and their potential cycles
are unchanged; no DEV2/shared serialization edit is made. Full successful Deactivate-first,
all writer races, post-mutation audit rollback and LastAdmin poststate remain separately BLOCKED.

# Task52 historical validated service transaction behavior

New ServiceSqlValidation.cs exercises the real internal service and scoped SQL DbContext:
caller-owned transaction and tracked contexts are refused without taking ownership; an
initially open connection stays open, a service-opened lease closes, and every tested denial,
query failure, cancellation, global-lock timeout and SQL deadlock releases its transaction.
An explicit fresh retry still returns ResponsibilitiesUnresolved. Target state/credentials/
version, sessions, roles, memberships and audit are compared in memory before/after all faults.

Actual DMV wait evidence is persisted in task52-service-final.local.json (3 observations).
The deadlock case intentionally holds a User UPDLOCK first on a test-only connection and
then requests the global guard while Deactivate owns that guard and requests Users TABLOCKX.
SQL selects the service as victim (1205, wrapped by EF InvalidOperationException). The
harness unwraps that provider error; it does not enable automatic retries or alter the service.
This proves failure cleanup for a deliberately inverted TEST writer. It does NOT certify
ReturnWork, feature-first actor reads, production lock-order compatibility or success-path
feature locking. The existing broad Users snapshot lock is unchanged.

# Task51 historical lock-order audit

Prepared Deactivate: caller-owned ReadCommitted transaction -> global administration
applock -> existing Users TABLOCKX,HOLDLOCK snapshot -> sorted actor/target User
UPDLOCK,HOLDLOCK -> retained catalog/authority/membership locks -> SQL session/authority
and target/version reread -> integrated responsibility reads -> conditional account update
-> target session revocation -> atomic audit -> LastAdmin poststate -> final authority/session
recheck -> commit. Early return, query/audit failure and cancellation roll back via disposal.
No automatic retry or hidden transaction owner is introduced.

**Integration is BLOCKED.** Existing LastAdmin uses a broad Users table X lock; it is
not equivalent in concurrency cost to only sorted participant rows. Resource-first actor
reads remain: Maintenance ExecuteCorrectiveAsync calls RequireUserAsync after Incident/Work;
Asset Reject/Retire calls RequireActorAsync after Asset; Inventory Begin calls Actor after
Catalog. A writer holding feature X while waiting on an actor User S lock can form a cycle
with Deactivate holding Users X while reading that feature. Physical behavior depends on
isolation/index plans and needs actual DMV/deadlock evidence. Do not infer safety from
AsNoTracking or from a release-only operation. No such race has been certified here.

More directly, Maintenance ReturnWork transitions COMPLETED to IN_PROGRESS without locking
the existing assignee User first. Even without a deadlock, it can recreate a blocker after
deactivation unless DEV2 fixes discovery, ascending User locks, eligibility and reread.

Historical protocol notes follow.

# Lock order

Required eventual mutation order: caller-owned SQL transaction -> global-admin
application lock -> canonical Users locks -> feature/resource locks -> authoritative
state/authority reread -> write, session revocation and audit -> LastAdmin poststate
check -> commit. Roll back on any rejection/error/cancellation; no automatic reassignment.

GlobalAdminSerialization.ReadSnapshotAsync currently retains TABLOCKX on Users before
catalog reads. Integrators must preserve this existing conservative serialization;
do not acquire target U locks then upgrade a separately acquired user universe lock.
Participant User locks must use ascending IDs and UPDLOCK,HOLDLOCK and be retained
through transaction end. Resource discovery before locking cannot authorize a write.

Current diagnostic helper acquires no retained eligibility locks and offers no race
guarantee. It must not be reused as a standalone check-then-update gate.

DEV1 current business writers remain absent/partial per Task40 matrix. Activation and
branch assignment already lock users in ascending order, but this does not certify
future cleaning/task/shift/handover writers. DEV2 paths listed in integration dependencies
remain resource-first/ordinary eligibility reads. Reverse-order review is not closed.
No SQL lock-manager evidence was collected in this task.
