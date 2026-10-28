# Current SQL serialization protocol - Task 43

1. The mutation service begins its own local ReadCommitted transaction; the guard borrows that
   transaction and never commits it. No ambient transaction or dirty tracker. Acquire fixed
   database-scoped Exclusive transaction-owned applock HotelOps:GlobalAdminEligibility:v1
   before any User/catalog/resource lock. A clean tracker cannot detect earlier raw SQL locks;
   raw participants must honor this ordering explicitly.
2. ReadSnapshotAsync checks the original EF transaction and APPLOCK_MODE. It uses the same EF
   connection/DbTransaction, first Users TABLOCKX,HOLDLOCK, then UserRoles -> Roles ->
   RolePermissions -> Permissions ROWLOCK,HOLDLOCK. The User X lock stabilizes all identifiers
   and avoids holding shared User locks while waiting on an authentication U-to-X conversion.
   Catalog shared row/range locks are compatible with observed legacy Role UPDLOCK schedules;
   there is no claim against arbitrary external/catalog inverse ordering or escalation.
3. Reset retains existing ordered actor/target User locks, authority ranges, memberships/branches
   and SQL session/proof checks. Profile reuses only the existing reset authority-lock routine,
   while evaluating its unchanged ALL/BRANCH/OWN profile permission rules. Create takes its
   existing Branch -> Department -> initial Role master locks after global/snapshot locks;
   both global permissions are reevaluated at the commit boundary.
4. Mutate and audit inside the same transaction. Reset credential/session revocation is atomic.
   Detach saved entities before authoritative poststate. Conditional/legacy reset checks
   preservation if its target belonged to the baseline durable set. Create/Profile check
   preservation for any nonzero baseline because an identifier change can affect a different
   user. Zero-baseline nonadmin operations are allowed; they cannot remove a durable admin.
5. Before commit, recompute poststate using the original guard; refuse zero durable survivors.
   HTTP maps last-admin refusal to 409; conditional reset returns LastAdministrator; legacy
   Apply throws GlobalAdministrationInvariantException. Existing HTTP reset proof/authority
   is rechecked after a slow poststate read. Create/Profile also recheck their existing authority
   at commit boundary. No permission/role name, credential or session policy is redefined.
6. Commit or rollback releases applock and retained SQL locks. Early denial/exception disposes
   an uncommitted async transaction, including cancellation. Lock timeout is bounded at 5s for
   application callers; HTTP returns503. Deadlock/provider failure rolls back and uses existing
   sanitized error handling. No automatic retry of credential delivery; fresh caller retry
   requires new transaction/preparation and current If-Match. No cached count is reused.

Guard ownership tests reject missing/unsupported/completed transactions and foreign snapshots.
Snapshot does not expose hashes/session secrets. Finite lockout affects immediate login, not
approved durable eligibility; temporary credentials never count. Time can expire grants without
a writer. FUTURE EXPIRY NOT GUARANTEED; no scheduler, recovery or auto-extension is introduced.

Tests observe actual DMV blocking and exercise mixed Create/reset, raw/legacy, authorization
revocation and authentication-style U-to-X schedules. Conservative Users X locking increases
blocking, including login/read traffic. Load/least-privilege deployment, every legacy/external
writer schedule and universal deadlock freedom are NOT VERIFIED. The intentionally inverted
fixture proves failure/rollback/retry, not permission to put applock after a resource lock.
